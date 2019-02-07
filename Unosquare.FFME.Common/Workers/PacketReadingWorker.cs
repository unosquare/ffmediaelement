namespace Unosquare.FFME.Workers
{
    using Primitives;
    using Shared;
    using System;
    using System.Threading;

    /// <summary>
    /// Implement packet reading worker logic
    /// </summary>
    /// <seealso cref="WorkerBase" />
    /// <seealso cref="IMediaWorker" />
    internal sealed class PacketReadingWorker : WorkerBase, IMediaWorker
    {
        /// <summary>
        /// Completed whenever a change in the packet buffer is detected.
        /// This needs to be reset manually and prevents high CPU usage in the packet reading worker.
        /// </summary>
        private readonly ManualResetEventSlim BufferChangedEvent = new ManualResetEventSlim(true);

        public PacketReadingWorker(MediaEngine mediaCore)
            : base(nameof(PacketReadingWorker), ThreadPriority.BelowNormal)
        {
            MediaCore = mediaCore;
            Period = TimeSpan.MaxValue;

            // Packet Buffer Notification Callbacks
            MediaCore.Container.Components.OnPacketQueueChanged = (op, packet, mediaType, state) =>
            {
                MediaCore.State.UpdateBufferingStats(state.Length, state.Count, state.CountThreshold);
                BufferChangedEvent.Set();
            };
        }

        /// <inheritdoc />
        public MediaEngine MediaCore { get; }

        /// <summary>
        /// Gets a value indicating whether the reading worker can read packets at the current time.
        /// This is simply a bit-wise AND of negating <see cref="MediaEngine.IsWorkerInterruptRequested"/> == false
        /// and <see cref="MediaEngine.ShouldReadMorePackets"/>
        /// </summary>
        private bool ShouldWorkerReadPackets => MediaCore.IsWorkerInterruptRequested == false && MediaCore.ShouldReadMorePackets;

        /// <inheritdoc />
        protected override void ExecuteCycleLogic(CancellationToken ct)
        {
            while (ShouldWorkerReadPackets && ct.IsCancellationRequested == false)
            {
                try { MediaCore.Container.Read(); }
                catch (MediaContainerException) { }

                if (MediaCore.Container.Components.HasEnoughPackets)
                    break;
            }
        }

        protected override void ExecuteCycleDelay(int wantedDelay, ManualResetEventSlim waitHandle)
        {
            BufferChangedEvent.Reset();
            while (waitHandle.IsSet == false)
            {
                // We now need more packets, we need to stop waiting
                if (ShouldWorkerReadPackets)
                    break;

                // we are sync-buffering but we don't need more packets
                if (MediaCore.IsSyncBuffering)
                    break;

                // We detected a change in buffered packets
                if (BufferChangedEvent.Wait(15))
                    break;
            }

            // No more sync-buffering if we have enough data
            if (MediaCore.CanExitSyncBuffering)
                MediaCore.IsSyncBuffering = false;
        }

        protected override void HandleCycleLogicException(Exception ex)
        {
            // TODO: Implement
        }

        /// <inheritdoc />
        protected override void DisposeManagedState()
        {
            BufferChangedEvent.Set();
            BufferChangedEvent.Dispose();
        }
    }
}
