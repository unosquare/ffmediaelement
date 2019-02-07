namespace Unosquare.FFME.Workers
{
    using Primitives;
    using System;
    using System.Threading;

    /// <summary>
    /// Implement frame decoding worker logic
    /// </summary>
    /// <seealso cref="WorkerBase" />
    /// <seealso cref="IMediaWorker" />
    internal sealed class FrameDecodingWorker : WorkerBase, IMediaWorker
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

        /// <inheritdoc />
        protected override void HandleCycleLogicException(Exception ex)
        {
            // TODO: Implement
        }

        /// <inheritdoc />
        protected override void DisposeManagedState()
        {
            // TODO: Dispose the rednerers here
        }
    }
}
