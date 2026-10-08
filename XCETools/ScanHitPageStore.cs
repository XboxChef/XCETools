using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace XCETools
{
    /// <summary>
    /// Paged on-disk storage for scan hit addresses (classic CE / XCE-style scratch
    /// files) so first/next scans can retain millions of candidates without a giant
    /// <see cref="List{UInt32}"/> in RAM.
    /// </summary>
    internal sealed class ScanHitPageStore : IDisposable
    {
        private const int BufferCap = 262144;
        internal const int PartBufferCapacity = BufferCap;

        private readonly string _sessionRoot;
        private readonly Stack<string> _undoDirs = new Stack<string>();
        private string _activeGenDir;
        private int _genSerial;
        private readonly uint[] _buffer = new uint[BufferCap];
        private int _bufferCount;
        private int _partIndex;
        private long _count;
        private bool _disposed;

        /// <param name="scratchKind">Sub-folder under <c>XCEAtlas</c> (e.g. <c>scan-hits</c>, <c>pointer-scans</c>).</param>
        public ScanHitPageStore(string scratchKind = "scan-hits")
        {
            string root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "XCEAtlas", scratchKind);
            _sessionRoot = Path.Combine(root, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_sessionRoot);
            _activeGenDir = Path.Combine(_sessionRoot, "g000");
            Directory.CreateDirectory(_activeGenDir);
        }

        public long Count => _count;

        public bool HasUndo => _undoDirs.Count > 0;

        /// <summary>Wipe all generations and start a new first-scan session.</summary>
        public void StartNewSession()
        {
            Flush();
            try
            {
                if (Directory.Exists(_sessionRoot))
                {
                    foreach (var d in Directory.EnumerateDirectories(_sessionRoot))
                        TryDeleteDir(d);
                }
            }
            catch { /* best effort */ }

            Directory.CreateDirectory(_sessionRoot);
            _undoDirs.Clear();
            _genSerial = 0;
            _activeGenDir = Path.Combine(_sessionRoot, "g000");
            Directory.CreateDirectory(_activeGenDir);
            _partIndex = 0;
            _bufferCount = 0;
            _count = 0;
        }

        public void Append(uint address)
        {
            _buffer[_bufferCount++] = address;
            _count++;
            if (_bufferCount >= BufferCap)
                FlushBufferToNewPart();
        }

        public void Flush()
        {
            if (_bufferCount > 0)
                FlushBufferToNewPart();
        }

        /// <summary>
        /// After a successful next-scan filter: archive the previous generation for undo
        /// and switch <see cref="_activeGenDir"/> to the new folder (already filled by caller).
        /// </summary>
        public void CommitFilteredGeneration(string newGenDir)
        {
            Flush();
            _undoDirs.Push(_activeGenDir);
            _activeGenDir = newGenDir;
            _genSerial++;
            _partIndex = 0;
            _bufferCount = 0;
            RecomputeCountFromActive();
        }

        /// <summary>Restore the previous generation directory after undo.</summary>
        public bool TryUndo(out string error)
        {
            error = null;
            if (_undoDirs.Count == 0)
            {
                error = "Nothing to undo.";
                return false;
            }

            Flush();
            string discard = _activeGenDir;
            _activeGenDir = _undoDirs.Pop();
            _genSerial = Math.Max(0, _genSerial - 1);
            _partIndex = 0;
            _bufferCount = 0;
            TryDeleteDir(discard);
            RecomputeCountFromActive();
            return true;
        }

        /// <summary>Enumerate every hit in the active generation (streaming).</summary>
        public IEnumerable<uint> EnumerateActive()
        {
            Flush();
            foreach (var path in OrderedPartPaths(_activeGenDir))
            {
                using var fs = File.OpenRead(path);
                using var br = new BinaryReader(fs);
                long remain = fs.Length / 4;
                for (long i = 0; i < remain; i++)
                    yield return br.ReadUInt32();
            }
        }

        /// <summary>Read hits from <paramref name="sourceGenDir"/> and write those matching <paramref name="keep"/> into <paramref name="destGenDir"/>.</summary>
        public static bool FilterDirectory(string sourceGenDir, string destGenDir, Func<uint, bool> keep, out long written, out string error)
        {
            written = 0;
            error = null;
            try
            {
                if (Directory.Exists(destGenDir))
                    TryDeleteDir(destGenDir);
                Directory.CreateDirectory(destGenDir);

                var buf = new uint[BufferCap];
                int n = 0;
                int part = 0;
                long total = 0;

                void FlushDest()
                {
                    if (n == 0) return;
                    string p = Path.Combine(destGenDir, PartFileName(part++));
                    WriteUintArray(p, buf, n);
                    total += n;
                    n = 0;
                }

                foreach (var path in OrderedPartPaths(sourceGenDir))
                {
                    using var fs = File.OpenRead(path);
                    using var br = new BinaryReader(fs);
                    long words = fs.Length / 4;
                    for (long i = 0; i < words; i++)
                    {
                        uint a = br.ReadUInt32();
                        if (!keep(a)) continue;
                        buf[n++] = a;
                        if (n >= BufferCap)
                            FlushDest();
                    }
                }

                FlushDest();
                written = total;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                TryDeleteDir(destGenDir);
                return false;
            }
        }

        public string AllocateNextGenerationDir()
        {
            string dir = Path.Combine(_sessionRoot, $"g{_genSerial + 1:D4}_{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            return dir;
        }

        /// <summary>Stream the active generation, keep predicate, commit as the new active generation (pushes undo).</summary>
        public bool TryFilterToNewGeneration(Func<uint, bool> keep, out string error)
        {
            Flush();
            string next = AllocateNextGenerationDir();
            if (!FilterDirectory(_activeGenDir, next, keep, out _, out error))
            {
                TryDeleteDir(next);
                return false;
            }
            CommitFilteredGeneration(next);
            return true;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                if (!string.IsNullOrEmpty(_sessionRoot) && Directory.Exists(_sessionRoot))
                    TryDeleteDir(_sessionRoot);
            }
            catch { /* ignore */ }
        }

        private void RecomputeCountFromActive()
        {
            _count = 0;
            try
            {
                foreach (var p in Directory.EnumerateFiles(_activeGenDir, "S_*.bin"))
                    _count += new FileInfo(p).Length / 4;
            }
            catch { _count = 0; }
        }

        private void FlushBufferToNewPart()
        {
            string path = Path.Combine(_activeGenDir, PartFileName(_partIndex++));
            WriteUintArray(path, _buffer, _bufferCount);
            _bufferCount = 0;
        }

        private static string PartFileName(int index) => $"S_{index:D5}.bin";

        private static void WriteUintArray(string path, uint[] data, int length)
        {
            if (length <= 0) return;
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None,
                       bufferSize: Math.Min(1 << 20, length * 4), FileOptions.SequentialScan))
            {
                var tmp = new byte[length * 4];
                Buffer.BlockCopy(data, 0, tmp, 0, length * 4);
                fs.Write(tmp, 0, tmp.Length);
            }
        }

        private static IEnumerable<string> OrderedPartPaths(string genDir)
        {
            if (!Directory.Exists(genDir))
                yield break;
            var files = Directory.EnumerateFiles(genDir, "S_*.bin").OrderBy(Path.GetFileName, StringComparer.Ordinal).ToList();
            foreach (var f in files)
                yield return f;
        }

        internal static void TryDeleteGenerationDir(string dir) => TryDeleteDir(dir);

        internal static void WritePartFile(string genDir, int partIndex, uint[] data, int length)
            => WriteUintArray(Path.Combine(genDir, PartFileName(partIndex)), data, length);

        private static void TryDeleteDir(string dir)
        {
            try
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, true);
            }
            catch { /* ignore */ }
        }
    }
}
