using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace XCETools
{
    /// <summary>
    /// Overlaps CPU scan work with the next xbdm chunk read (single I/O thread, one scan worker).
    /// </summary>
    internal sealed class ScanChunkPipeline : IDisposable
    {
        private readonly BlockingCollection<DumpChunkProgress> _queue;
        private readonly Task _worker;
        private readonly CancellationToken _ct;
        private volatile Exception _error;

        public ScanChunkPipeline(Action<DumpChunkProgress> onChunk, CancellationToken ct)
        {
            if (onChunk == null) throw new ArgumentNullException(nameof(onChunk));
            _ct = ct;
            _queue = new BlockingCollection<DumpChunkProgress>(boundedCapacity: 2);
            _worker = Task.Run(() =>
            {
                try
                {
                    foreach (var item in _queue.GetConsumingEnumerable(ct))
                        onChunk(item);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
                catch (Exception ex) { _error = ex; }
            }, ct);
        }

        public void Enqueue(DumpChunkProgress chunk)
        {
            _queue.Add(chunk.CloneOwnedBuffer(), _ct);
        }

        public void Complete()
        {
            _queue.CompleteAdding();
            try { _worker.Wait(_ct); }
            catch (OperationCanceledException) when (_ct.IsCancellationRequested) { }
            if (_error != null)
                throw new InvalidOperationException("Scan worker failed.", _error);
        }

        public void Dispose() => Complete();
    }
}
