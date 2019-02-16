namespace Unosquare.FFME.Workers
{
    using Decoding;
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
            Container = mediaCore.Container;

            // Packet Buffer Notification Callbacks
            Container.Components.OnPacketQueueChanged = (op, packet, mediaType, state) =>
            {
                MediaCore.State.UpdateBufferingStats(state.Length, state.Count, state.CountThreshold);
                BufferChangedEvent.Set();
            };
        }

        /// <inheritdoc />
        public MediaEngine MediaCore { get; }

        private MediaContainer Container { get; }

        /// <inheritdoc />
        protected override void ExecuteCycleLogic(CancellationToken ct)
        {
            while (MediaCore.ShouldReadMorePackets && ct.IsCancellationRequested == false)
            {
                try { Container.Read(); }
                catch (MediaContainerException) { }
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

            base.OnDisposing();
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
        }
    }
}
