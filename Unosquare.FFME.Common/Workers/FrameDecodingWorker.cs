namespace Unosquare.FFME.Workers
{
    using Commands;
    using Decoding;
    using Primitives;
    using Shared;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Implement frame decoding worker logic
    /// </summary>
    /// <seealso cref="WorkerBase" />
    /// <seealso cref="IMediaWorker" />
    internal sealed class FrameDecodingWorker : ThreadWorkerBase, IMediaWorker, ILoggingSource
    {
        /// <summary>
        /// Contains the callback that converts decoder frames into blocks
        /// </summary>
        private readonly Action<IEnumerable<MediaType>, CancellationToken> DecodeBlocksAction;

        /// <summary>
        /// The decoded frame count for a cycle
        /// </summary>
        private int DecodedFrameCount;

        /// <summary>
        /// Initializes a new instance of the <see cref="FrameDecodingWorker"/> class.
        /// </summary>
        /// <param name="mediaCore">The media core.</param>
        public FrameDecodingWorker(MediaEngine mediaCore)
            : base(nameof(FrameDecodingWorker), Constants.ThreadWorkerPeriod)
        {
            MediaCore = mediaCore;
            Commands = mediaCore.Commands;
            Container = mediaCore.Container;
            State = mediaCore.State;

            if (UseParallelDecoding)
            {
                DecodeBlocksAction = (all, ct) =>
                {
                    DecodedFrameCount = 0;
                    Parallel.ForEach(all, (t) =>
                        Interlocked.Add(ref DecodedFrameCount,
                        DecodeComponentBlocks(t, ct)));
                };
            }
            else
            {
                DecodeBlocksAction = (all, ct) =>
                {
                    DecodedFrameCount = 0;
                    foreach (var t in Container.Components.MediaTypes)
                        DecodedFrameCount += DecodeComponentBlocks(t, ct);
                };
            }
        }

        /// <inheritdoc />
        public MediaEngine MediaCore { get; }

        /// <inheritdoc />
        ILoggingHandler ILoggingSource.LoggingHandler => MediaCore;

        /// <summary>
        /// Gets the Media Engine's Command Manager.
        /// </summary>
        private CommandManager Commands { get; }

        /// <summary>
        /// Gets the Media Engine's Container.
        /// </summary>
        private MediaContainer Container { get; }

        /// <summary>
        /// Gets the Media Engine's State.
        /// </summary>
        private MediaEngineState State { get; }

        /// <summary>
        /// Whether or not frames should be decoded in parallel.
        /// </summary>
        private bool UseParallelDecoding { get; }

        /// <summary>
        /// Gets a value indicating whether the decoder needs to wait for the reader to receive more packets.
        /// </summary>
        private bool NeedsMorePackets => MediaCore.ShouldReadMorePackets && !MediaCore.Container.Components.HasEnoughPackets;

        /// <inheritdoc />
        protected override void ExecuteCycleLogic(CancellationToken ct)
        {
            try
            {
                // The 2-part logic blocks detect a sync-buffering scenario
                // and then decodes the necessary frames.
                if (MediaCore.HasDecodingEnded || ct.IsCancellationRequested)
                    return;

                // We need to add blocks if the wall clock is over 75%
                // for each of the components so that we have some buffer.
                DecodeBlocksAction.Invoke(Container.Components.MediaTypes, ct);
            }
            finally
            {
                // Provide updates to decoding stats
                State.UpdateDecodingBitRate(MediaCore.Blocks.Values.Sum(b => b.RangeBitRate));

                // Detect End of Decoding Scenarios
                // The Rendering will check for end of media when this condition is set.
                MediaCore.HasDecodingEnded = DetectHasDecodingEnded();
            }
        }

        /// <inheritdoc />
        protected override void OnCycleException(Exception ex) =>
            this.LogError(Aspects.DecodingWorker, "Worker Cycle exception thrown", ex);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int DecodeComponentBlocks(MediaType t, CancellationToken ct)
        {
            // Capture a reference to the blocks and the current Range Percent
            const double rangePercentThreshold = 0.75d;

            var decoderBlocks = MediaCore.Blocks[t];
            var rangePercent = decoderBlocks.GetRangePercent(MediaCore.PlaybackClock(t));
            var addedBlocks = 0;
            var maxAddedBlocks = decoderBlocks.Capacity;

            // Read as much as we can for this cycle but always within range.
            while (addedBlocks < maxAddedBlocks && (decoderBlocks.IsFull == false || rangePercent >= rangePercentThreshold))
            {
                if (ct.IsCancellationRequested || AddNextBlock(t) == false)
                    break;

                addedBlocks++;
                rangePercent = decoderBlocks.GetRangePercent(MediaCore.PlaybackClock(t));
            }

            return addedBlocks;
        }

        /// <summary>
        /// Tries to receive the next frame from the decoder by decoding queued
        /// Packets and converting the decoded frame into a Media Block which gets
        /// queued into the playback block buffer.
        /// </summary>
        /// <param name="t">The MediaType.</param>
        /// <returns>True if a block could be added. False otherwise.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool AddNextBlock(MediaType t)
        {
            // Decode the frames
            var block = MediaCore.Blocks[t].Add(Container.Components[t].ReceiveNextFrame(), Container);
            return block != null;
        }

        /// <summary>
        /// Detects the end of media in the decoding worker.
        /// </summary>
        /// <returns>True if media docding has ended</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool DetectHasDecodingEnded()
        {
            var main = Container.Components.MainMediaType;
            return DecodedFrameCount <= 0
                && CanReadMoreFramesOf(main) == false
                && MediaCore.Blocks[main].IndexOf(MediaCore.PlaybackClock(main)) >= MediaCore.Blocks[main].Count - 1;
        }

        /// <summary>
        /// Gets a value indicating whether more frames can be decoded into blocks of the given type.
        /// </summary>
        /// <param name="t">The media type.</param>
        /// <returns>
        ///   <c>true</c> if more frames can be decoded; otherwise, <c>false</c>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool CanReadMoreFramesOf(MediaType t)
        {
            return
                Container.Components[t].BufferLength > 0 ||
                Container.Components[t].HasPacketsInCodec ||
                MediaCore.ShouldReadMorePackets;
        }
    }
}
