namespace Unosquare.FFME.Engine
{
    using Common;
    using Container;
    using Diagnostics;
    using Primitives;
    using System;
    using System.Threading;

    /// <summary>
    /// Implement packet reading worker logic.
    /// </summary>
    /// <seealso cref="IMediaWorker" />
    internal sealed class PacketReadingWorker : IntervalWorkerBase, IMediaWorker, ILoggingSource
    {
        /// <summary>
        /// Completed whenever a change in the packet buffer is detected.
        /// This needs to be reset manually and prevents high CPU usage in the packet reading worker.
        /// </summary>
        private readonly ManualResetEventSlim BufferChangedEvent = new ManualResetEventSlim(true);

        public PacketReadingWorker(MediaEngine mediaCore)
            : base(nameof(PacketReadingWorker), Constants.DefaultTimingPeriod, ThreadPriority.Normal, IntervalWorkerMode.DefaultSleepLoop)
        {
            MediaCore = mediaCore;
            Container = mediaCore.Container;

            // Packet Buffer Notification Callbacks
            Container.Components.OnPacketQueueChanged = (op, packet, mediaType, state) =>
            {
                MediaCore.State.UpdateBufferingStats(state.Length, state.Count, state.CountThreshold);
                BufferChangedEvent.Set();

                if (op == PacketQueueOp.Queued)
                {
                    unsafe
                    {
                        MediaCore.Connector?.OnPacketRead(packet.Pointer, Container.InputContext);
                    }
                }
            };
        }

        /// <inheritdoc />
        public MediaEngine MediaCore { get; }

        /// <inheritdoc />
        ILoggingHandler ILoggingSource.LoggingHandler => MediaCore;

        /// <summary>
        /// Gets the Media Engine's container.
        /// </summary>
        private MediaContainer Container { get; }

        /// <inheritdoc />
        protected override void ExecuteCycleLogic(CancellationToken ct)
        {
            while (MediaCore.ShouldReadMorePackets && ct.IsCancellationRequested == false)
            {
                try { Container.Read(); }
                catch (MediaContainerException) { /* ignore */ }
            }

            BufferChangedEvent.Reset();
            while (!ct.IsCancellationRequested)
            {
                // We now need more packets, we need to stop waiting
                if (MediaCore.ShouldReadMorePackets)
                    break;

                // We don't want to keep waiting if reads have been aborted
                if (Container.IsReadAborted)
                    break;

                // We don't want to wait if we are at the end of the stream
                if (Container.IsAtEndOfStream)
                    break;

                // We detected a change in buffered packets
                try
                {
                    if (BufferChangedEvent.Wait(5, ct))
                        break;
                }
                catch
                {
                    // ignore and break as the task was most likely cancelled
                    break;
                }
            }
        }

        /// <inheritdoc />
        protected override void OnCycleException(Exception ex) =>
            this.LogError(Aspects.ReadingWorker, "Worker Cycle exception thrown", ex);

        /// <inheritdoc />
        protected override void Dispose(bool alsoManaged)
        {
            BufferChangedEvent.Set();
            base.Dispose(alsoManaged);
            BufferChangedEvent.Dispose();
        }

        protected override void OnDisposing()
        {
            // placeholder
        }
    }
}
