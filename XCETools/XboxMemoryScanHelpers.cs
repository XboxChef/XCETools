using System;
using System.Collections.Generic;

namespace XCETools
{
    /// <summary>
    /// Shared scan/dump policy: xbdm-friendly chunk sizes and committed-range
    /// clipping from <see cref="XboxConsole.WalkCommittedMemory"/> (CE-style
    /// “active memory only” without duplicating logic in every scanner).
    /// </summary>
    internal static class XboxMemoryScanHelpers
    {
        /// <summary>
        /// Per-read size for scan dumps. Matches <see cref="XboxClient.MemoryDumpChunkSize"/>
        /// (64 KiB by default) — large single <c>getmemex</c> requests often fail on stock xbdm.
        /// </summary>
        public static uint ChunkSize(uint address)
        {
            uint chunk = (uint)Math.Max(0x1000, XboxClient.MemoryDumpChunkSize);
            return chunk;
        }

        /// <summary>
        /// Intersects walkmem regions with <c>[rangeStart, rangeStart + rangeLength)</c>,
        /// merges overlaps, and returns ascending disjoint intervals.
        /// </summary>
        public static List<(uint Start, uint Length)> BuildMergedCommittedIntervals(
            XboxConsole console, uint rangeStart, uint rangeLength)
        {
            var result = new List<(uint Start, uint Length)>();
            if (console == null || rangeLength == 0) return result;

            ulong rangeEnd = (ulong)rangeStart + rangeLength;
            List<XboxMemoryRegion> raw;
            try { raw = console.WalkCommittedMemory(); }
            catch { return result; }

            if (raw == null || raw.Count == 0) return result;

            foreach (var r in raw)
            {
                if (r.Size == 0) continue;
                ulong rb = r.BaseAddress;
                ulong re = rb + r.Size;
                ulong sb = Math.Max(rb, rangeStart);
                ulong se = Math.Min(re, rangeEnd);
                if (se <= sb) continue;
                uint len = (uint)Math.Min((ulong)uint.MaxValue, se - sb);
                result.Add(((uint)sb, len));
            }

            if (result.Count == 0) return result;

            result.Sort((a, b) => a.Start.CompareTo(b.Start));

            var merged = new List<(uint Start, uint Length)>(result.Count);
            foreach (var p in result)
            {
                if (merged.Count == 0)
                {
                    merged.Add(p);
                    continue;
                }

                var last = merged[merged.Count - 1];
                ulong lastEnd = (ulong)last.Start + last.Length;
                ulong pEnd = (ulong)p.Start + p.Length;
                if (p.Start <= lastEnd)
                {
                    ulong newEnd = Math.Max(lastEnd, pEnd);
                    ulong span = newEnd - last.Start;
                    merged[merged.Count - 1] = (last.Start, (uint)Math.Min((ulong)uint.MaxValue, span));
                }
                else
                    merged.Add(p);
            }

            return merged;
        }
    }
}
