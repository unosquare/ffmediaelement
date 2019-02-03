namespace Unosquare.FFME.Workers
{
    using Primitives;
    using System.Threading;

    /// <summary>
    /// Implement packet reading worker logic
    /// </summary>
    /// <seealso cref="ThreadWorkerBase" />
    /// <seealso cref="IMediaWorker" />
    internal sealed class PacketReadingWorker : ThreadWorkerBase, IMediaWorker
    {
        public PacketReadingWorker(MediaEngine mediaCore)
            : base(nameof(PacketReadingWorker), ThreadPriority.Normal)
        {
            MediaCore = mediaCore;
        }

        /// <inheritdoc />
        public MediaEngine MediaCore { get; }

        /// <inheritdoc />
        protected override void ExecuteCycleLogic(CancellationToken ct)
        {
            // TODO: Implement
        }
    }
}
