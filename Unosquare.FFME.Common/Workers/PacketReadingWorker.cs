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
        public PacketReadingWorker(MediaEngine mediaCore)
            : base(nameof(PacketReadingWorker), ThreadPriority.BelowNormal)
        {
            MediaCore = mediaCore;
            Period = TimeSpan.FromMilliseconds(5);
        }

        /// <inheritdoc />
        public MediaEngine MediaCore { get; }

        /// <inheritdoc />
        protected override void ExecuteCycleLogic(CancellationToken ct)
        {
            while (ct.IsCancellationRequested == false && MediaCore.ShouldReadMorePackets)
            {
                try { MediaCore.Container.Read(); }
                catch (MediaContainerException) { }
                finally
                {
                    // No more sync-buffering if we have enough data
                    if (MediaCore.CanExitSyncBuffering)
                        MediaCore.IsSyncBuffering = false;
                }
            }

            // No more sync-buffering if we have enough data
            if (MediaCore.CanExitSyncBuffering)
                MediaCore.IsSyncBuffering = false;
        }
    }
}
