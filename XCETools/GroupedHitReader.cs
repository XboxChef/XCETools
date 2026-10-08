using System;
using System.Collections.Generic;

namespace XCETools
{
    /// <summary>CE-style grouping of hit addresses into minimal <c>getmemex</c> runs.</summary>
    internal static class GroupedHitReader
    {
        internal readonly struct ReadRun
        {
            public readonly uint Start;
            public readonly uint Length;
            public readonly uint[] Addresses;

            public ReadRun(uint start, uint length, uint[] addresses)
            {
                Start = start;
                Length = length;
                Addresses = addresses ?? Array.Empty<uint>();
            }
        }

        /// <summary>
        /// Sort hits and merge into contiguous reads (same 4 KiB page or within <paramref name="maxSpan"/>).
        /// </summary>
        public static List<ReadRun> BuildRuns(IEnumerable<uint> addresses, int valueWidth, uint maxSpan = 0x10000)
        {
            var list = new List<uint>();
            foreach (uint a in addresses)
                list.Add(a);
            if (list.Count == 0) return new List<ReadRun>();

            list.Sort();
            var runs = new List<ReadRun>();
            uint runStart = list[0];
            uint runEnd = unchecked(runStart + (uint)Math.Max(1, valueWidth));
            var runAddrs = new List<uint> { list[0] };

            for (int i = 1; i < list.Count; i++)
            {
                uint addr = list[i];
                uint needEnd = unchecked(addr + (uint)valueWidth);
                bool samePage = (runStart >> 12) == (addr >> 12);
                bool fitsSpan = unchecked(needEnd - runStart) <= maxSpan &&
                                unchecked(addr - runEnd) <= maxSpan;

                if (samePage || fitsSpan)
                {
                    runAddrs.Add(addr);
                    if (needEnd > runEnd) runEnd = needEnd;
                    continue;
                }

                runs.Add(new ReadRun(runStart, runEnd - runStart, runAddrs.ToArray()));
                runStart = addr;
                runEnd = needEnd;
                runAddrs = new List<uint> { addr };
            }

            runs.Add(new ReadRun(runStart, runEnd - runStart, runAddrs.ToArray()));
            return runs;
        }
    }
}
