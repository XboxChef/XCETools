using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;
using System.Threading.Tasks;

namespace XCETools
{
    /// <summary>Progress for one captured chunk (buffer is reused; scan synchronously in the callback).</summary>
    internal readonly struct DumpChunkProgress
    {
        public readonly byte[] Buffer;
        public readonly int BufferDumpOffset;
        public readonly int BufferLength;
        public readonly uint BytesCopied;
        public readonly uint TotalBytes;
        public readonly int Percent;

        public DumpChunkProgress(byte[] buffer, int bufferDumpOffset, int bufferLength, uint bytesCopied, uint totalBytes, int percent)
        {
            Buffer = buffer;
            BufferDumpOffset = bufferDumpOffset;
            BufferLength = bufferLength;
            BytesCopied = bytesCopied;
            TotalBytes = totalBytes;
            Percent = percent;
        }

        /// <summary>Copy chunk bytes for async scan worker (source buffer may be reused).</summary>
        public DumpChunkProgress CloneOwnedBuffer()
        {
            if (Buffer == null || BufferLength <= 0)
                return this;
            var copy = new byte[BufferLength];
            System.Buffer.BlockCopy(Buffer, BufferDumpOffset, copy, 0, BufferLength);
            return new DumpChunkProgress(copy, 0, BufferLength, BytesCopied, TotalBytes, Percent);
        }
    }

    /// <summary>
    /// A contiguous Xbox memory capture: either a small in-RAM buffer or a
    /// temp file mapped read-only so multi-hundred-megabyte scans do not
    /// require a single private <c>byte[length]</c> allocation.
    /// </summary>
    internal sealed class DumpSnapshot : IDisposable
    {
        /// <summary>Below this size we keep a plain <see cref="byte"/>[] for faster random access.</summary>
        public const int MemoryMapThresholdBytes = 512 * 1024;

        private readonly byte[] _bytes;
        private readonly string _mmapPath;
        private MemoryMappedFile _mmf;
        private MemoryMappedViewAccessor _view;
        private readonly int _length;
        private bool _disposed;

        private DumpSnapshot(byte[] bytes)
        {
            _bytes   = bytes ?? throw new ArgumentNullException(nameof(bytes));
            _length  = bytes.Length;
            _mmapPath = null;
        }

        private DumpSnapshot(string path, MemoryMappedFile mmf, MemoryMappedViewAccessor view, int length)
        {
            _mmapPath = path ?? throw new ArgumentNullException(nameof(path));
            _mmf      = mmf ?? throw new ArgumentNullException(nameof(mmf));
            _view     = view ?? throw new ArgumentNullException(nameof(view));
            _length   = length;
            _bytes    = null;
        }

        public static DumpSnapshot Empty { get; } = new DumpSnapshot(Array.Empty<byte>());

        public int Length => _length;

        public bool IsMemoryMapped => _bytes == null && _length > 0;

        /// <summary>True when the dump is a single in-memory buffer (fast CPU scan).</summary>
        internal bool HasLinearBuffer(out byte[] bytes)
        {
            bytes = _bytes;
            return _bytes != null;
        }

        private readonly byte[] _scratch = new byte[8];
        /// <summary>Little-endian scratch for <see cref="BitConverter"/> (float/double); avoids per-read allocations.</summary>
        private readonly byte[] _beFlip = new byte[8];

        /// <summary>Big-endian typed read at <paramref name="off"/> (same rules as classic XCE scan).</summary>
        public double ReadTyped(int off, ScanKind kind, bool isF, bool isD)
        {
            if (_length == 0) return 0;
            if (_bytes != null)
                return ReadTypedFromArray(_bytes, off, kind, isF, isD, _length, _beFlip);
            int w = (int)kind;
            if (off < 0 || off + w > _length) return 0;
            if (kind == ScanKind.Byte)
                return ReadByte(off);
            _view.ReadArray(off, _scratch, 0, w);
            return ReadTypedFromArray(_scratch, 0, kind, isF, isD, w, _beFlip);
        }

        /// <summary>Fast path for “exact value” scans: compare <paramref name="pattern"/> to dump bytes at <paramref name="off"/> (length ≤ 8).</summary>
        public bool RegionMatches(int off, byte[] pattern)
        {
            if (pattern == null || pattern.Length == 0) return false;
            if (off < 0 || off + pattern.Length > _length) return false;
            if (_bytes != null)
            {
                for (int i = 0; i < pattern.Length; i++)
                {
                    if (_bytes[off + i] != pattern[i]) return false;
                }
                return true;
            }
            _view.ReadArray(off, _scratch, 0, pattern.Length);
            for (int i = 0; i < pattern.Length; i++)
            {
                if (_scratch[i] != pattern[i]) return false;
            }
            return true;
        }

        public byte ReadByte(int index)
        {
            if ((uint)index >= (uint)_length) return 0;
            if (_bytes != null) return _bytes[index];
            return _view.ReadByte(index);
        }

        /// <summary>Write live console bytes into this dump (used by grouped next-scan refresh).</summary>
        internal void PatchBytes(int dumpOffset, byte[] src, int srcOffset, int count)
        {
            if (src == null || count <= 0) return;
            if (dumpOffset < 0 || dumpOffset + count > _length) return;
            if (_bytes != null)
                System.Buffer.BlockCopy(src, srcOffset, _bytes, dumpOffset, count);
            else if (_view != null)
                _view.WriteArray(dumpOffset, src, srcOffset, count);
        }

        /// <summary>
        /// Chunked <see cref="XboxConsole.GetMemory"/> into RAM or a sequential temp file, then map for reads.
        /// </summary>
        public static async Task<DumpSnapshot> CaptureAsync(
            XboxConsole console,
            uint start,
            uint length,
            Action<int> reportProgress,
            CancellationToken ct = default,
            bool activeMemoryOnly = false,
            Action<DumpChunkProgress> onChunkScanned = null,
            XbdmScanSession session = null,
            List<(uint Start, uint Length)> committedSegments = null)
        {
            if (console == null) throw new ArgumentNullException(nameof(console));
            if (length == 0) return Empty;

            int oldSendMs = console.Client.SendTimeout;
            int oldRecvMs = console.Client.ReceiveTimeout;
            bool manageTimeouts = session == null;
            try
            {
                if (manageTimeouts)
                {
                    console.Client.ReceiveTimeout = 0;
                    if (oldSendMs < 60000)
                        console.Client.SetConnectionTimeout(60000, 0);
                }

                if (session == null)
                {
                    try { console.InvalidateMemoryCache(false, start, length); }
                    catch { /* best-effort */ }
                }

                List<(uint Start, uint Length)> segments = committedSegments;
                if (activeMemoryOnly)
                {
                    if (segments == null)
                    {
                        if (XbdmScanSession.IsActive)
                        {
                            throw new InvalidOperationException(
                                "Active memory only requires walkmem before the scan session starts.");
                        }
                        segments = XboxMemoryScanHelpers.BuildMergedCommittedIntervals(console, start, length);
                    }
                    if (segments == null || segments.Count == 0)
                    {
                        throw new InvalidOperationException(
                            "Active memory only found no committed pages in this range. " +
                            "Uncheck Active memory only or choose another region.");
                    }
                }

                if (PreferInMemoryCapture(length))
                {
                    var bytes = new byte[length];
                    if (activeMemoryOnly)
                        await FillBufferByCommittedAsync(console, session, start, length, bytes, segments, reportProgress, ct, onChunkScanned).ConfigureAwait(true);
                    else
                        await FillBufferAsync(console, session, start, length, bytes, reportProgress, ct, onChunkScanned).ConfigureAwait(true);
                    if (BufferLooksUnreadable(bytes))
                        throw new InvalidOperationException(
                            "Memory dump appears empty (all zeros). xbdm reads may have failed — reconnect and try a smaller region.");
                    return new DumpSnapshot(bytes);
                }

                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "XCEAtlas", "scan-dumps");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, Guid.NewGuid().ToString("N") + ".bin");

                try
                {
                    var mmf = MemoryMappedFile.CreateFromFile(
                        path, FileMode.Create, null, length, MemoryMappedFileAccess.ReadWrite);
                    var view = mmf.CreateViewAccessor(0, length, MemoryMappedFileAccess.ReadWrite);
                    if (activeMemoryOnly)
                        await FillViewByCommittedAsync(console, session, start, length, view, segments, reportProgress, ct, onChunkScanned).ConfigureAwait(true);
                    else
                        await FillViewAsync(console, session, start, length, view, reportProgress, ct, onChunkScanned).ConfigureAwait(true);
                    view.Flush();
                    if (ViewLooksUnreadable(view, (int)length))
                        throw new InvalidOperationException(
                            "Memory dump appears empty (all zeros). xbdm reads may have failed — reconnect and try a smaller region.");
                    return new DumpSnapshot(path, mmf, view, (int)length);
                }
                catch
                {
                    TryDelete(path);
                    throw;
                }
            }
            finally
            {
                if (manageTimeouts)
                    console.Client.SetConnectionTimeout(oldSendMs, oldRecvMs);
            }
        }

        /// <summary>On 64-bit hosts keep the full scan window in RAM so filtering avoids per-hit mmap I/O.</summary>
        private static bool PreferInMemoryCapture(uint length) =>
            Environment.Is64BitProcess && length <= 0x20000000u;

        public void Dispose()
        {
            if (ReferenceEquals(this, Empty)) return;
            if (_disposed) return;
            _disposed = true;
            try { _view?.Dispose(); } catch { /* ignore */ }
            _view = null;
            try { _mmf?.Dispose(); } catch { /* ignore */ }
            _mmf = null;
            TryDelete(_mmapPath);
        }

        /// <summary>Big-endian typed read from a byte buffer (chunk slice during incremental scan).</summary>
        internal static double ReadTypedInBuffer(byte[] buf, int off, ScanKind kind, bool isF, bool isD, int bufLen, byte[] flip)
            => ReadTypedFromArray(buf, off, kind, isF, isD, bufLen, flip);

        private static double ReadTypedFromArray(byte[] buf, int off, ScanKind kind, bool isF, bool isD, int bufLen, byte[] flip)
        {
            if (off < 0 || off >= bufLen) return 0;
            switch (kind)
            {
                case ScanKind.Byte:
                    return buf[off];
                case ScanKind.Width2:
                    if (off + 2 > bufLen) return 0;
                    return (ushort)((buf[off] << 8) | buf[off + 1]);
                case ScanKind.Width4 when isF:
                    if (off + 4 > bufLen) return 0;
                    flip[0] = buf[off + 3];
                    flip[1] = buf[off + 2];
                    flip[2] = buf[off + 1];
                    flip[3] = buf[off];
                    return BitConverter.ToSingle(flip, 0);
                case ScanKind.Width4:
                    if (off + 4 > bufLen) return 0;
                    return ((uint)buf[off] << 24) | ((uint)buf[off + 1] << 16) | ((uint)buf[off + 2] << 8) | buf[off + 3];
                case ScanKind.Width8 when isD:
                    if (off + 8 > bufLen) return 0;
                    flip[0] = buf[off + 7];
                    flip[1] = buf[off + 6];
                    flip[2] = buf[off + 5];
                    flip[3] = buf[off + 4];
                    flip[4] = buf[off + 3];
                    flip[5] = buf[off + 2];
                    flip[6] = buf[off + 1];
                    flip[7] = buf[off];
                    return BitConverter.ToDouble(flip, 0);
                case ScanKind.Width8:
                    if (off + 8 > bufLen) return 0;
                    {
                        ulong v = 0;
                        for (int i = 0; i < 8; i++) v = (v << 8) | buf[off + i];
                        return v;
                    }
                default:
                    return 0;
            }
        }

        private static void TryDelete(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try { File.Delete(path); } catch { /* ignore */ }
        }

        /// <summary>Exact-value first scan over a memory-mapped dump.</summary>
        internal void ScanMappedExact(byte[] pattern, int step, uint baseAddr, bool invert, ScanHitPageStore hits)
        {
            if (_view == null || pattern == null || pattern.Length == 0) return;
            int width = pattern.Length;
            int maxOff = _length - width;
            if (maxOff < 0) return;

            // Typical 64-bit scans use in-RAM dumps; for mmap fall back, pull one buffer when feasible.
            if (_length <= 64 * 1024 * 1024)
            {
                var tmp = new byte[_length];
                _view.ReadArray(0, tmp, 0, _length);
                ScanFirstPass.RunExactOnBuffer(tmp, pattern, step, baseAddr, invert, hits, maxOff, 0);
                return;
            }

            for (int off = 0; off <= maxOff; off += step)
            {
                bool match = RegionMatches(off, pattern);
                if (invert) match = !match;
                if (match) hits.Append(baseAddr + (uint)off);
            }
        }

        /// <summary>Read one xbdm chunk; on failure retries at half size down to 4 KiB.</summary>
        private static void ReadChunk(
            XboxConsole console, XbdmScanSession session, uint address, uint take, byte[] tmp,
            byte[] dest, int destOff, FileStream fs)
        {
            uint pos = 0;
            while (pos < take)
            {
                uint want = take - pos;
                uint attempt = want;
                bool transferred = false;

                while (attempt >= 0x1000 && !transferred)
                {
                    if (TryReadOnce(console, session, address + pos, attempt, tmp, dest, destOff + (int)pos, fs, out _))
                    {
                        pos += attempt;
                        transferred = true;
                    }
                    else
                        attempt /= 2;
                }

                if (!transferred)
                {
                    ZeroChunk(dest, destOff + (int)pos, tmp, fs, want);
                    pos += want;
                }
            }
        }

        private static bool TryReadOnce(
            XboxConsole console, XbdmScanSession session, uint address, uint take, byte[] tmp,
            byte[] dest, int destOff, FileStream fs, out uint got)
        {
            got = 0;
            try
            {
                if (dest != null)
                {
                    if (session != null)
                        session.ReadMemory(address, take, dest, destOff, out got);
                    else
                        console.GetMemory(address, take, dest, destOff, out got);
                    if (got < take)
                        Array.Clear(dest, destOff + (int)got, (int)(take - got));
                    return got > 0;
                }

                if (session != null)
                    session.ReadMemory(address, take, tmp, 0, out got);
                else
                    console.GetMemory(address, take, tmp, out got);
                if (got < take)
                    Array.Clear(tmp, (int)got, (int)(take - got));
                if (fs != null)
                    fs.Write(tmp, 0, (int)take);
                return got > 0;
            }
            catch
            {
                return false;
            }
        }

        private static void ZeroChunk(byte[] dest, int destOff, byte[] tmp, FileStream fs, uint take)
        {
            if (dest != null)
                Array.Clear(dest, destOff, (int)take);
            else
            {
                Array.Clear(tmp, 0, (int)take);
                fs?.Write(tmp, 0, (int)take);
            }
        }

        /// <summary>True when every sampled byte is zero (likely failed reads, not real RAM).</summary>
        internal static bool BufferLooksUnreadable(byte[] data)
        {
            if (data == null || data.Length == 0) return true;
            const int samples = 512;
            int step = Math.Max(1, data.Length / samples);
            for (int i = 0; i < data.Length; i += step)
            {
                if (data[i] != 0) return false;
            }
            return true;
        }

        internal static bool ViewLooksUnreadable(MemoryMappedViewAccessor view, int length)
        {
            if (view == null || length <= 0) return true;
            const int samples = 512;
            int step = Math.Max(1, length / samples);
            for (int i = 0; i < length; i += step)
            {
                if (view.ReadByte(i) != 0) return false;
            }
            return true;
        }

        private static void ThrowIfDumpCancelled(CancellationToken ct)
        {
            if (!ct.CanBeCanceled) return;
            try { ct.ThrowIfCancellationRequested(); }
            catch (ObjectDisposedException) { throw new OperationCanceledException(ct); }
        }

        private static void NotifyChunkScanned(
            Action<DumpChunkProgress> onChunk, byte[] buffer, int dumpOffset, int chunkLen,
            uint copied, uint total, int pct)
        {
            onChunk?.Invoke(new DumpChunkProgress(buffer, dumpOffset, chunkLen, copied, total, pct));
        }

        private static Task FillBufferAsync(
            XboxConsole console, XbdmScanSession session, uint start, uint length, byte[] dest,
            Action<int> reportProgress, CancellationToken ct,
            Action<DumpChunkProgress> onChunkScanned = null)
        {
            return Task.Run(() =>
            {
                uint chunk = XboxMemoryScanHelpers.ChunkSize(start);
                if (chunk > length) chunk = length;
                uint copied = 0;
                int lastPct = -1;
                while (copied < length)
                {
                    ThrowIfDumpCancelled(ct);
                    uint take = (length - copied) < chunk ? (length - copied) : chunk;
                    uint at = copied;
                    ReadChunk(console, session, start + at, take, null, dest, (int)at, null);
                    int pct = (int)((long)(copied + take) * 100L / length);
                    if (onChunkScanned != null)
                        NotifyChunkScanned(onChunkScanned, dest, (int)at, (int)take, copied + take, length, pct);
                    copied += take;
                    if (pct != lastPct) { lastPct = pct; reportProgress?.Invoke(pct); }
                }
            }, ct);
        }

        /// <summary>
        /// Fills <paramref name="dest"/> (already zeroed) only at offsets that overlap
        /// <paramref name="segments"/> — skips xbdm reads for holes (CE “active memory only”).
        /// </summary>
        private static Task FillBufferByCommittedAsync(
            XboxConsole console,
            XbdmScanSession session,
            uint dumpStart,
            uint _dumpLength,
            byte[] dest,
            List<(uint Start, uint Length)> segments,
            Action<int> reportProgress,
            CancellationToken ct,
            Action<DumpChunkProgress> onChunkScanned = null)
        {
            return Task.Run(() =>
            {
                ulong total = 0;
                if (segments != null)
                {
                    foreach (var s in segments)
                        total += s.Length;
                }

                if (total == 0)
                {
                    reportProgress?.Invoke(100);
                    return;
                }

                ulong done = 0;
                int lastPct = -1;
                var tmp = new byte[65536];
                if (segments != null)
                {
                    foreach (var (segStart, segLen) in segments)
                    {
                        uint segEnd = segStart + segLen;
                        for (uint pos = segStart; pos < segEnd;)
                        {
                            ThrowIfDumpCancelled(ct);
                            uint ch = XboxMemoryScanHelpers.ChunkSize(pos);
                            uint room = segEnd - pos;
                            uint take = room < ch ? room : ch;
                            if (tmp.Length < take)
                                tmp = new byte[take];
                            uint rel = pos - dumpStart;
                            ReadChunk(console, session, pos, take, tmp, dest, (int)rel, null);
                            int pct = (int)Math.Min(100, (done + take) * 100 / total);
                            if (onChunkScanned != null)
                                NotifyChunkScanned(onChunkScanned, dest, (int)rel, (int)take, (uint)Math.Min(total, done + take), (uint)total, pct);
                            pos += take;
                            done += take;
                            if (pct != lastPct) { lastPct = pct; reportProgress?.Invoke(pct); }
                        }
                    }
                }
            }, ct);
        }

        private static Task FillStreamByCommittedAsync(
            XboxConsole console,
            uint dumpStart,
            uint _dumpLength,
            FileStream fs,
            List<(uint Start, uint Length)> segments,
            Action<int> reportProgress,
            CancellationToken ct)
        {
            return Task.Run(() =>
            {
                ulong total = 0;
                if (segments != null)
                {
                    foreach (var s in segments)
                        total += s.Length;
                }

                if (total == 0)
                {
                    reportProgress?.Invoke(100);
                    return;
                }

                ulong done = 0;
                int lastPct = -1;
                var tmp = new byte[65536];
                int flushBudget = 0;
                if (segments != null)
                {
                    foreach (var (segStart, segLen) in segments)
                    {
                        uint segEnd = segStart + segLen;
                        fs.Position = segStart - dumpStart;
                        for (uint pos = segStart; pos < segEnd;)
                        {
                            ThrowIfDumpCancelled(ct);
                            uint ch = XboxMemoryScanHelpers.ChunkSize(pos);
                            uint room = segEnd - pos;
                            uint take = room < ch ? room : ch;
                            if (tmp.Length < take)
                                tmp = new byte[take];
                            ReadChunk(console, null, pos, take, tmp, null, 0, fs);
                            pos += take;
                            if (++flushBudget >= 32)
                            {
                                flushBudget = 0;
                                fs.Flush(false);
                            }
                        }
                        done += segLen;
                        int pct = (int)Math.Min(100, done * 100 / total);
                        if (pct != lastPct) { lastPct = pct; reportProgress?.Invoke(pct); }
                    }
                }
            }, ct);
        }

        /// <summary>Chunked read/write to an on-disk file (no giant <c>byte[]</c>).</summary>
        public static async Task WriteDumpToFileAsync(
            XboxConsole console,
            uint start,
            uint length,
            string path,
            Action<int> reportProgress,
            CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("path required", nameof(path));
            try { console.InvalidateMemoryCache(false, start, length); }
            catch { /* ignore */ }

            using (var fs = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None,
                       bufferSize: 1 << 20, FileOptions.SequentialScan))
            {
                if (length > 0) fs.SetLength(length);
                await FillStreamAsync(console, start, length, fs, reportProgress, ct).ConfigureAwait(true);
                fs.Flush(true);
            }
        }

        private static Task FillStreamAsync(
            XboxConsole console, uint start, uint length, FileStream fs,
            Action<int> reportProgress, CancellationToken ct)
        {
            return Task.Run(() =>
            {
                uint chunk = XboxMemoryScanHelpers.ChunkSize(start);
                if (chunk > length) chunk = length;
                var tmp = new byte[chunk];
                uint copied = 0;
                int lastPct = -1;
                int flushBudget = 0;
                fs.Position = 0;
                while (copied < length)
                {
                    ThrowIfDumpCancelled(ct);
                    uint take = (length - copied) < chunk ? (length - copied) : chunk;
                    ReadChunk(console, null, start + copied, take, tmp, null, 0, fs);
                    copied += take;
                    int pct = (int)((long)copied * 100L / length);
                    if (pct != lastPct) { lastPct = pct; reportProgress?.Invoke(pct); }
                    if (++flushBudget >= 32)
                    {
                        flushBudget = 0;
                        fs.Flush(false);
                    }
                }
            }, ct);
        }

        private static void ReadChunkToView(
            XboxConsole console, XbdmScanSession session, uint address, uint take, byte[] tmp,
            MemoryMappedViewAccessor view, int destOff)
        {
            try
            {
                uint read;
                if (session != null)
                    session.ReadMemory(address, take, tmp, 0, out read);
                else
                    console.GetMemory(address, take, tmp, out read);
                if (read < take)
                    Array.Clear(tmp, (int)read, (int)(take - read));
                view.WriteArray(destOff, tmp, 0, (int)take);
            }
            catch
            {
                Array.Clear(tmp, 0, (int)take);
                view.WriteArray(destOff, tmp, 0, (int)take);
            }
        }

        private static Task FillViewAsync(
            XboxConsole console, XbdmScanSession session, uint start, uint length, MemoryMappedViewAccessor view,
            Action<int> reportProgress, CancellationToken ct,
            Action<DumpChunkProgress> onChunkScanned = null)
        {
            return Task.Run(() =>
            {
                uint chunk = XboxMemoryScanHelpers.ChunkSize(start);
                if (chunk > length) chunk = length;
                var tmp = new byte[chunk];
                uint copied = 0;
                int lastPct = -1;
                while (copied < length)
                {
                    ThrowIfDumpCancelled(ct);
                    uint take = (length - copied) < chunk ? (length - copied) : chunk;
                    int at = (int)copied;
                    ReadChunkToView(console, session, start + copied, take, tmp, view, at);
                    int pct = (int)((long)(copied + take) * 100L / length);
                    if (onChunkScanned != null)
                        NotifyChunkScanned(onChunkScanned, tmp, at, (int)take, copied + take, length, pct);
                    copied += take;
                    if (pct != lastPct) { lastPct = pct; reportProgress?.Invoke(pct); }
                }
            }, ct);
        }

        private static Task FillViewByCommittedAsync(
            XboxConsole console,
            XbdmScanSession session,
            uint dumpStart,
            uint _dumpLength,
            MemoryMappedViewAccessor view,
            List<(uint Start, uint Length)> segments,
            Action<int> reportProgress,
            CancellationToken ct,
            Action<DumpChunkProgress> onChunkScanned = null)
        {
            return Task.Run(() =>
            {
                ulong total = 0;
                if (segments != null)
                {
                    foreach (var s in segments)
                        total += s.Length;
                }

                if (total == 0)
                {
                    reportProgress?.Invoke(100);
                    return;
                }

                ulong done = 0;
                int lastPct = -1;
                var tmp = new byte[65536];
                if (segments != null)
                {
                    foreach (var (segStart, segLen) in segments)
                    {
                        uint segEnd = segStart + segLen;
                        for (uint pos = segStart; pos < segEnd;)
                        {
                            ThrowIfDumpCancelled(ct);
                            uint ch = XboxMemoryScanHelpers.ChunkSize(pos);
                            uint room = segEnd - pos;
                            uint take = room < ch ? room : ch;
                            if (tmp.Length < take)
                                tmp = new byte[take];
                            uint rel = pos - dumpStart;
                            ReadChunkToView(console, session, pos, take, tmp, view, (int)rel);
                            int pct = (int)Math.Min(100, (done + take) * 100 / total);
                            if (onChunkScanned != null)
                                NotifyChunkScanned(onChunkScanned, tmp, (int)rel, (int)take, (uint)Math.Min(total, done + take), (uint)total, pct);
                            pos += take;
                            done += take;
                            if (pct != lastPct) { lastPct = pct; reportProgress?.Invoke(pct); }
                        }
                    }
                }
            }, ct);
        }
    }
}
