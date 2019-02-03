namespace Unosquare.FFME.Workers
{
    using Primitives;
    using System.Threading;

    /// <summary>
    /// Implement frame decoding worker logic
    /// </summary>
    /// <seealso cref="ThreadWorkerBase" />
    /// <seealso cref="IMediaWorker" />
    internal sealed class FrameDecodingWorker : ThreadWorkerBase, IMediaWorker
    {
        public FrameDecodingWorker(MediaEngine mediaCore)
            : base(nameof(FrameDecodingWorker), ThreadPriority.Normal)
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
