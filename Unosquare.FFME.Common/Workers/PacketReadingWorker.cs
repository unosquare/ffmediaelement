namespace Unosquare.FFME.Workers
{
    using Primitives;
    using Shared;
    using System;
    using System.Threading;

    /// <summary>
    /// Implement packet reading worker logic
    /// </summary>
    /// <seealso cref="ThreadWorkerBase" />
    /// <seealso cref="IMediaWorker" />
    internal sealed class PacketReadingWorker : ThreadWorkerBase, IMediaWorker
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
            Period = TimeSpan.Zero;

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
            if (ShouldWorkerReadPackets)
            {
                try { MediaCore.Container.Read(); }
                catch (MediaContainerException) { }
            }
        }

        /// <inheritdoc />
        protected override void InterruptDelay()
        {
            BufferChangedEvent.Set();
            base.InterruptDelay();
        }

        /// <inheritdoc />
        protected override void Delay(int wantedDelay, CancellationToken ct)
        {
            if (wantedDelay < 0)
            {
                base.Delay(wantedDelay, ct);
                return;
            }

            BufferChangedEvent.Reset();
            while (ct.IsCancellationRequested == false)
            {
                // We now need more packets, we need to stop waiting
                if (ShouldWorkerReadPackets)
                    break;

                // we are sync-buffering but we don't need more packets
                if (MediaCore.IsSyncBuffering)
                    break;

                // We detected a change in buffered packets
                try
                {
                    if (BufferChangedEvent.Wait(15, ct))
                        break;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            // No more sync-buffering if we have enough data
            if (MediaCore.CanExitSyncBuffering)
                MediaCore.IsSyncBuffering = false;
        }

        /// <inheritdoc />
        protected override void DisposeManagedState()
        {
            base.DisposeManagedState();
            BufferChangedEvent.Set();
            BufferChangedEvent.Dispose();
        }
    }
}
