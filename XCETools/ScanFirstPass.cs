using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace XCETools
{
    /// <summary>CPU-optimized first-scan pass over a contiguous dump (exact value).</summary>
    internal static class ScanFirstPass
    {
        internal const int ParallelThreshold = 256 * 1024;

        public static void RunExact(
            DumpSnapshot snap,
            byte[] pattern,
            int step,
            uint baseAddr,
            bool invert,
            ScanHitPageStore hits)
        {
            if (pattern == null || pattern.Length == 0 || snap == null || snap.Length == 0)
                return;

            int width = pattern.Length;
            int maxOff = snap.Length - width;
            if (maxOff < 0) return;

            if (snap.HasLinearBuffer(out byte[] bytes))
            {
                RunExactOnBuffer(bytes, pattern, step, baseAddr, invert, hits, maxOff, 0);
                return;
            }

            snap.ScanMappedExact(pattern, step, baseAddr, invert, hits);
        }

        /// <summary>Scan a byte buffer slice (used for RAM dumps and mmap chunks).</summary>
        public static void RunExactOnBuffer(
            byte[] data, byte[] pattern, int step, uint baseAddr, bool invert,
            ScanHitPageStore hits, int maxOff, int startOff = 0)
        {
            if (data == null || pattern == null || pattern.Length == 0) return;
            int width = pattern.Length;
            if (maxOff < startOff) return;

            if (maxOff - startOff >= ParallelThreshold && step == 1)
                ScanBytesParallel(data, pattern, width, baseAddr, invert, hits, maxOff, startOff);
            else
                ScanBytes(data, pattern, width, step, baseAddr, invert, hits, maxOff, startOff);
        }

        private static void ScanBytes(
            byte[] data, byte[] pattern, int width, int step, uint baseAddr, bool invert,
            ScanHitPageStore hits, int maxOff, int startOff)
        {
            for (int off = startOff; off <= maxOff; off += step)
            {
                bool match = RegionMatches(data, off, pattern, width);
                if (invert) match = !match;
                if (match) hits.Append(baseAddr + (uint)off);
            }
        }

        private static void ScanBytesParallel(
            byte[] data, byte[] pattern, int width, uint baseAddr, bool invert,
            ScanHitPageStore hits, int maxOff, int startOff)
        {
            int threads = Environment.ProcessorCount;
            var lists = new List<uint>[threads];

            Parallel.For(0, threads, t =>
            {
                int begin = (int)((long)(maxOff + 1L) * t / threads);
                int end = (int)((long)(maxOff + 1L) * (t + 1) / threads) - 1;
                if (begin < startOff) begin = startOff;
                if (begin > maxOff) { lists[t] = null; return; }

                var local = new List<uint>(Math.Max(64, (end - begin + 1) / 64));
                for (int off = begin; off <= end; off++)
                {
                    bool match = RegionMatches(data, off, pattern, width);
                    if (invert) match = !match;
                    if (match) local.Add(baseAddr + (uint)off);
                }
                lists[t] = local;
            });

            for (int t = 0; t < threads; t++)
            {
                var list = lists[t];
                if (list == null) continue;
                for (int i = 0; i < list.Count; i++)
                    hits.Append(list[i]);
            }
        }

        private static bool RegionMatches(byte[] data, int off, byte[] pattern, int width)
        {
            for (int i = 0; i < width; i++)
            {
                if (data[off + i] != pattern[i]) return false;
            }
            return true;
        }
    }
}
