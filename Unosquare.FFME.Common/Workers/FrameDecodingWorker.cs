﻿namespace Unosquare.FFME.Workers
{
    using Commands;
    using Decoding;
    using Primitives;
    using Shared;
    using System;
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
        /// The decoded frame count for a cycle
        /// </summary>
        private int DecodedFrameCount;

        /// <summary>
        /// The sync-buffering start time to measur ehow long it takes
        /// </summary>
        private DateTime SyncBufferStartTime = DateTime.UtcNow;

        /// <summary>
        /// Initializes a new instance of the <see cref="FrameDecodingWorker"/> class.
        /// </summary>
        /// <param name="mediaCore">The media core.</param>
        public FrameDecodingWorker(MediaEngine mediaCore)
            : base(nameof(FrameDecodingWorker), ThreadPriority.Normal, DefaultPeriod, WorkerDelayProvider.Default)
        {
            MediaCore = mediaCore;
            Commands = mediaCore.Commands;
            Container = mediaCore.Container;
            State = mediaCore.State;
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
        /// Gets a value indicating whether the decoder needs to wait for the reader to receive more packets.
        /// </summary>
        private bool NeedsMorePackets => MediaCore.ShouldReadMorePackets && !MediaCore.Container.Components.HasEnoughPackets;

        /// <inheritdoc />
        protected override void ExecuteCycleLogic(CancellationToken ct)
        {
            #region Setup the Decoding Cycle

            // Update state properties -- this must be done on every cycle
            // because a direct command might have changed the components
            var wallClock = MediaCore.WallClock;
            var main = Container.Components.MainMediaType;
            MediaBlockBuffer blocks;
            DecodedFrameCount = 0;

            #endregion

            try
            {
                // The 2-part logic blocks detect a sync-buffering scenario
                // and then decodes the necessary frames.
                if (MediaCore.HasDecodingEnded || ct.IsCancellationRequested)
                    return;

                #region Sync-Buffering Detection

                // Capture the blocks for easier readability
                blocks = MediaCore.Blocks[main];

                // If we are not in range then we need to enter the sync-buffering state
                if (NeedsMorePackets && !MediaCore.IsSyncBuffering && blocks.IsInRange(wallClock) == false)
                {
                    // TODO: for now we are preventing entering syncbuffering
                    if (true || (State.BufferingProgress >= 0.95 && blocks.Count > 0))
                    {
                        // We don't want to enter a sync-buffering scenario
                        // if we have a full buffer. We just need to decode
                        // more packets and change the position of the clock to what is available
                        // MediaCore.InvalidateRenderers();
                        // wallClock = blocks[wallClock].StartTime;
                        // MediaCore.ChangePosition(wallClock);
                    }
                    else
                    {
                        // Enter sync-buffering scenario
                        MediaCore.Clock.Pause();
                        wallClock = MediaCore.WallClock;
                        MediaCore.IsSyncBuffering = true;
                        SyncBufferStartTime = DateTime.UtcNow;
                        this.LogInfo(Aspects.DecodingWorker,
                            $"SYNC-BUFFER: Started. Buffer: {State.BufferingProgress:p}. Clock: {wallClock.Format()}");
                    }
                }

                #endregion

                #region Component Decoding

                // We need to add blocks if the wall clock is over 75%
                // for each of the components so that we have some buffer.
                foreach (var t in Container.Components.MediaTypes)
                {
                    if (ct.IsCancellationRequested) break;

                    // Capture a reference to the blocks and the current Range Percent
                    blocks = MediaCore.Blocks[t];

                    // Read as much as we can for this cycle
                    while (blocks.IsFull == false)
                    {
                        if (ct.IsCancellationRequested || AddNextBlock(t) == false)
                            break;

                        DecodedFrameCount += 1;
                    }

                    #endregion
                }
            }
            finally
            {
                // Provide updates to decoding stats
                State.UpdateDecodingBitRate(
                    MediaCore.Blocks.Values.Sum(b => b.RangeBitRate));

                // Detect End of Decoding Scenarios
                // The Rendering will check for end of media when this
                // condition is set.
                var hasDecodingEnded = DetectHasDecodingEnded(wallClock, DecodedFrameCount, main);
                MediaCore.HasDecodingEnded = hasDecodingEnded;

                // Detect if an exit from Sync Buffering is required
                var mustExitSyncBuffering = MediaCore.IsSyncBuffering
                    && (ct.IsCancellationRequested || hasDecodingEnded || State.BufferingProgress >= 0.95);

                // Detect if we need an immediate exit from sync buffering
                if (mustExitSyncBuffering || (MediaCore.IsSyncBuffering && !NeedsMorePackets))
                {
                    blocks = MediaCore.Blocks[main];
                    if (blocks.Count > 0 && !blocks.IsInRange(wallClock))
                    {
                        MediaCore.InvalidateRenderers();
                        wallClock = blocks[wallClock].StartTime;
                    }

                    MediaCore.ChangePosition(wallClock);
                    MediaCore.IsSyncBuffering = false;
                    this.LogInfo(Aspects.DecodingWorker,
                        $"SYNC-BUFFER: Completed in {DateTime.UtcNow.Subtract(SyncBufferStartTime).TotalMilliseconds:0.0} ms");

                    if (State.MediaState == PlaybackStatus.Play)
                        MediaCore.ResumePlayback();
                }
            }
        }

        /// <inheritdoc />
        protected override void ExecuteCycleDelay(int wantedDelay, Task delayTask, CancellationToken token)
        {
            // We don't delay if there was at least 1 decoded frame
            // and we are not sync-buffering
            if (token.IsCancellationRequested)
                return;

            if (DecodedFrameCount > 0 && !MediaCore.IsSyncBuffering)
                return;

            // Introduce a delay if the conditions above were not satisfied
            base.ExecuteCycleDelay(wantedDelay, delayTask, token);
        }

        /// <inheritdoc />
        protected override void OnCycleException(Exception ex) =>
            this.LogError(Aspects.DecodingWorker, "Worker Cycle exception thrown", ex);

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
        /// <param name="wallClock">The clock position</param>
        /// <param name="decodedFrameCount">The decoded frame count.</param>
        /// <param name="main">The main.</param>
        /// <returns>True if media ended</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool DetectHasDecodingEnded(TimeSpan wallClock, int decodedFrameCount, MediaType main) =>
                decodedFrameCount <= 0
                && CanReadMoreFramesOf(main) == false
                && MediaCore.Blocks[main].IndexOf(wallClock) >= MediaCore.Blocks[main].Count - 1;

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
