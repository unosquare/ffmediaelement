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
        public PacketReadingWorker(MediaEngine mediaCore)
            : base(nameof(PacketReadingWorker))
        {
            MediaCore = mediaCore;
            Container = mediaCore.Container;

            // Packet Buffer Notification Callbacks
            Container.Components.OnPacketQueueChanged = (op, packet, mediaType, state) =>
            {
                MediaCore.State.UpdateBufferingStats(state.Length, state.Count, state.CountThreshold, state.Duration);

                if (op != PacketQueueOp.Queued)
                    return;

                unsafe
                {
                    MediaCore.Connector?.OnPacketRead(packet.Pointer, Container.InputContext);
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
            while (MediaCore.ShouldReadMorePackets)
            {
                if (Container.IsReadAborted || Container.IsAtEndOfStream || ct.IsCancellationRequested ||
                    WorkerState != WantedWorkerState)
                {
                    break;
                }

                try { Container.Read(); }
                catch (MediaContainerException) { /* ignore */ }
            }
        }

        /// <inheritdoc />
        protected override void OnCycleException(Exception ex) =>
            this.LogError(Aspects.ReadingWorker, "Worker Cycle exception thrown", ex);

        protected override void OnDisposing()
        {
            // placeholder
        }
    }
}
