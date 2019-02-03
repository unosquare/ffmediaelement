namespace Unosquare.FFME.Workers
{
    using Primitives;
    using Shared;
    using System.Threading;

    /// <summary>
    /// Implements the block rendering worker.
    /// </summary>
    /// <seealso cref="TimerWorkerBase" />
    /// <seealso cref="IMediaWorker" />
    internal sealed class BlockRenderingWorker : TimerWorkerBase, IMediaWorker
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BlockRenderingWorker"/> class.
        /// </summary>
        /// <param name="mediaCore">The media core.</param>
        public BlockRenderingWorker(MediaEngine mediaCore)
            : base(nameof(BlockRenderingWorker))
        {
            MediaCore = mediaCore;
            Period = Constants.Interval.HighPriority;
        }

        /// <inheritdoc />
        public MediaEngine MediaCore { get; }

        /// <summary>
        /// TODO: Renderers should be a property of this worker.
        /// This worker should own the renderers and the methods to create or replace them
        /// </summary>
        private MediaTypeDictionary<IMediaRenderer> Renderers => MediaCore.Renderers;

        /// <inheritdoc />
        protected override void ExecuteCycleLogic(CancellationToken ct)
        {
            // TODO: Implement
        }

        /// <inheritdoc />
        protected override void DisposeManagedState()
        {
            base.DisposeManagedState();

            // TODO: Dispose the rednerers here
        }
    }
}
