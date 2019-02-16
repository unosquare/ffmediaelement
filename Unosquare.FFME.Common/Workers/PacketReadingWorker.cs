namespace Unosquare.FFME.Workers
{
    using Primitives;
    using Shared;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Implement packet reading worker logic
    /// </summary>
    /// <seealso cref="WorkerBase" />
    /// <seealso cref="IMediaWorker" />
    internal sealed class PacketReadingWorker : ThreadWorkerBase, IMediaWorker
    {
        /// <summary>
        /// Completed whenever a change in the packet buffer is detected.
        /// This needs to be reset manually and prevents high CPU usage in the packet reading worker.
        /// </summary>
        private readonly ManualResetEventSlim BufferChangedEvent = new ManualResetEventSlim(true);

        public PacketReadingWorker(MediaEngine mediaCore)
            : base(nameof(PacketReadingWorker), ThreadPriority.Normal, Constants.Interval.HighPriority, WorkerDelayProvider.Token)
        {
            MediaCore = mediaCore;

            // Packet Buffer Notification Callbacks
            MediaCore.Container.Components.OnPacketQueueChanged = (op, packet, mediaType, state) =>
            {
                MediaCore.State.UpdateBufferingStats(state.Length, state.Count, state.CountThreshold);
                BufferChangedEvent.Set();
            };
        }

        /// <inheritdoc />
        public MediaEngine MediaCore { get; }

        /// <inheritdoc />
        protected override void ExecuteCycleLogic(CancellationToken ct)
        {
            while (MediaCore.ShouldReadMorePackets && ct.IsCancellationRequested == false)
            {
                try { MediaCore.Container.Read(); }
                catch (MediaContainerException) { }

                // No more sync-buffering if we have enough data
                if (MediaCore.CanExitSyncBuffering)
                    MediaCore.IsSyncBuffering = false;

                if (MediaCore.Container.Components.HasEnoughPackets)
                    break;
            }
        }

        protected override void OnCycleException(Exception ex)
        {
            // TODO: Implement
        }

        /// <inheritdoc />
        protected override void OnDisposing()
        {
            BufferChangedEvent.Set();
            BufferChangedEvent.Dispose();
        }

        protected override void ExecuteCycleDelay(int wantedDelay, Task delayTask, CancellationToken token)
        {
            if (wantedDelay == 0 || wantedDelay == Timeout.Infinite)
            {
                base.ExecuteCycleDelay(wantedDelay, delayTask, token);
            }
            else
            {
                BufferChangedEvent.Reset();
                while (!token.IsCancellationRequested)
                {
                    // We now need more packets, we need to stop waiting
                    if (MediaCore.ShouldReadMorePackets)
                        break;

                    // we are sync-buffering but we don't need more packets
                    if (MediaCore.IsSyncBuffering)
                        break;

                    // We detected a change in buffered packets
                    try
                    {
                        if (BufferChangedEvent.Wait(15, token))
                            break;
                    }
                    catch
                    {
                        // ignore and break as the task was most likely cancelled
                        break;
                    }
                }
            }

            // No more sync-buffering if we have enough data
            if (MediaCore.CanExitSyncBuffering)
                MediaCore.IsSyncBuffering = false;
        }
    }
}
