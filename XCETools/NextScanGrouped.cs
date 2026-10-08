using System;
using System.Collections.Generic;
using System.Threading;

namespace XCETools
{
    internal delegate bool GroupedHitPredicate(
        ScanFilterSettings settings,
        DumpSnapshot previous,
        uint dumpBase,
        uint chunkBase,
        byte[] chunk,
        uint address,
        byte[] flipScratch);

    /// <summary>Next-scan filtering via grouped <c>getmemex</c> reads (no full-region re-dump).</summary>
    internal static class NextScanGrouped
    {
        internal delegate void ProgressReport(int percent, long hitsKept);

        internal static bool ShouldUseGroupedRedump(long hitCount, uint regionBytes)
        {
            if (hitCount <= 0) return false;
            if (hitCount > 2_000_000) return false;
            return hitCount * 4096L < regionBytes || regionBytes > 32u * 1024 * 1024;
        }

        internal static string FilterHits(
            XbdmScanSession session,
            ScanHitPageStore hits,
            uint dumpBase,
            DumpSnapshot previous,
            DumpSnapshot currentPatch,
            ScanFilterSettings settings,
            IEnumerable<uint> sourceHits,
            GroupedHitPredicate keepHit,
            CancellationToken ct,
            ProgressReport progress = null)
        {
            if (session == null || hits == null || previous == null || keepHit == null)
                return "Grouped next scan is not ready (missing session, hits, or previous dump).";

            var runs = GroupedHitReader.BuildRuns(sourceHits, settings.ValueWidth);
            if (runs.Count == 0)
            {
                string emptyDir = hits.AllocateNextGenerationDir();
                hits.CommitFilteredGeneration(emptyDir);
                return null;
            }

            string destDir = hits.AllocateNextGenerationDir();
            var writeBuf = new uint[ScanHitPageStore.PartBufferCapacity];
            int writeN = 0;
            int part = 0;
            long kept = 0;
            ulong totalBytes = 0;
            foreach (var r in runs) totalBytes += r.Length;
            ulong doneBytes = 0;

            byte[] ioBuf = null;
            var flip = new byte[8];

            void FlushWrites()
            {
                if (writeN == 0) return;
                ScanHitPageStore.WritePartFile(destDir, part++, writeBuf, writeN);
                writeN = 0;
            }

            try
            {
                foreach (var run in runs)
                {
                    ct.ThrowIfCancellationRequested();
                    if (ioBuf == null || ioBuf.Length < run.Length)
                        ioBuf = new byte[run.Length];

                    session.ReadMemory(run.Start, run.Length, ioBuf, 0, out uint got);
                    if (got < run.Length)
                        Array.Clear(ioBuf, (int)got, (int)(run.Length - got));

                    if (currentPatch != null && got > 0)
                    {
                        int patchOff = (int)(run.Start - dumpBase);
                        currentPatch.PatchBytes(patchOff, ioBuf, 0, (int)got);
                    }

                    foreach (uint addr in run.Addresses)
                    {
                        if (!keepHit(settings, previous, dumpBase, run.Start, ioBuf, addr, flip))
                            continue;
                        writeBuf[writeN++] = addr;
                        kept++;
                        if (writeN >= writeBuf.Length)
                            FlushWrites();
                    }

                    doneBytes += run.Length;
                    if (totalBytes > 0 && progress != null)
                    {
                        int pct = (int)Math.Min(100, doneBytes * 100 / totalBytes);
                        progress(pct, kept);
                    }
                }

                FlushWrites();
                hits.CommitFilteredGeneration(destDir);
                return null;
            }
            catch (OperationCanceledException)
            {
                ScanHitPageStore.TryDeleteGenerationDir(destDir);
                throw;
            }
            catch (Exception ex)
            {
                ScanHitPageStore.TryDeleteGenerationDir(destDir);
                return ex.Message;
            }
        }
    }
}
