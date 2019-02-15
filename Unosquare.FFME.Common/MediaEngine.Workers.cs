﻿namespace Unosquare.FFME
{
    using Primitives;
    using Shared;
    using System;
    using System.Runtime.CompilerServices;
    using Workers;

    public partial class MediaEngine
    {
        /// <summary>
        /// This partial class implements:
        /// 1. Packet reading from the Container
        /// 2. Frame Decoding from packet buffer and Block buffering
        /// 3. Block Rendering from block buffer
        /// </summary>

        #region State Management

        private readonly AtomicBoolean m_IsSyncBuffering = new AtomicBoolean(false);
        private readonly AtomicBoolean m_HasDecodingEnded = new AtomicBoolean(false);

        /// <summary>
        /// Holds the materialized block cache for each media type.
        /// </summary>
        public MediaTypeDictionary<MediaBlockBuffer> Blocks { get; } = new MediaTypeDictionary<MediaBlockBuffer>();

        /// <summary>
        /// Gets the preloaded subtitle blocks.
        /// </summary>
        public MediaBlockBuffer PreloadedSubtitles { get; private set; }

        /// <summary>
        /// Gets the worker collection
        /// </summary>
        internal MediaWorkerSet Workers { get; private set; }

        /// <summary>
        /// Holds the block renderers
        /// </summary>
        internal MediaTypeDictionary<IMediaRenderer> Renderers { get; } = new MediaTypeDictionary<IMediaRenderer>();

        /// <summary>
        /// Holds the last rendered StartTime for each of the media block types
        /// </summary>
        internal MediaTypeDictionary<TimeSpan> LastRenderTime { get; } = new MediaTypeDictionary<TimeSpan>();

        /// <summary>
        /// Gets or sets a value indicating whether the decoder worker is sync-buffering
        /// </summary>
        internal bool IsSyncBuffering
        {
            get => m_IsSyncBuffering.Value;
            set => m_IsSyncBuffering.Value = value;
        }

        /// <summary>
        /// Gets or sets a value indicating whether the decoder worker has decoded all frames.
        /// This is an indication that the rendering worker should probe for end of media scenarios
        /// </summary>
        internal bool HasDecodingEnded
        {
            get => m_HasDecodingEnded.Value;
            set => m_HasDecodingEnded.Value = value;
        }

        /// <summary>
        /// Gets a value indicating whether the packet reader has finished sync-buffering.
        /// </summary>
        internal bool CanExitSyncBuffering
        {
            get
            {
                if (IsSyncBuffering == false)
                    return false;

                if (Container.Components.BufferLength > BufferLengthMax)
                    return true;

                if (Container.Components.HasEnoughPackets)
                    return true;

                return Container.IsLiveStream && Blocks.Main(Container).IsFull;
            }
        }

        /// <summary>
        /// Gets the buffer length maximum.
        /// port of MAX_QUEUE_SIZE (ffplay.c)
        /// </summary>
        internal long BufferLengthMax => 16 * 1024 * 1024;

        /// <summary>
        /// Gets a value indicating whether packets can be read and
        /// room is available in the download cache.
        /// </summary>
        internal bool ShouldReadMorePackets
        {
            get
            {
                if (Container?.Components == null)
                    return false;

                if (Container.IsReadAborted || Container.IsAtEndOfStream)
                    return false;

                // If it's a live stream always continue reading, regardless
                if (Container.IsLiveStream)
                    return true;

                // For network streams always expect a minimum buffer length
                if (Container.IsNetworkStream && Container.Components.BufferLength < BufferLengthMax)
                    return true;

                // if we don't have enough packets queued we should read
                return Container.Components.HasEnoughPackets == false;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Initializes the media block buffers and
        /// starts packet reader, frame decoder, and block rendering workers.
        /// </summary>
        internal void StartWorkers()
        {
            // Initialize the block buffers
            foreach (var t in Container.Components.MediaTypes)
            {
                Blocks[t] = new MediaBlockBuffer(Constants.MaxBlocks[t], t);
                Renderers[t] = Platform.CreateRenderer(t, this);
                InvalidateRenderer(t);
            }

            // Create the renderer for the preloaded subs
            if (PreloadedSubtitles != null)
            {
                var t = PreloadedSubtitles.MediaType;
                if (Renderers[t] == null)
                    Renderers[t] = Platform.CreateRenderer(t, this);

                InvalidateRenderer(t);
            }

            Clock.SpeedRatio = Constants.Controller.DefaultSpeedRatio;
            IsSyncBuffering = true;

            // Instantiate the workers and fire them up.
            Workers = new MediaWorkerSet(this);
            Workers.Start();
        }

        /// <summary>
        /// Stops the packet reader, frame decoder, and block renderers
        /// </summary>
        internal void StopWorkers()
        {
            // Pause the clock so no further updates are propagated
            Clock.Pause();

            // Cause an immediate Packet read abort
            Container?.SignalAbortReads(false);

            // Workers = null;
            Workers.Dispose();

            // Call close on all renderers
            foreach (var renderer in Renderers.Values)
                renderer.Close();

            // Remove the renderers disposing of them
            Renderers.Clear();

            // Reset the clock
            ResetPosition();
        }

        /// <summary>
        /// Pre-loads the subtitles from the MediaOptions.SubtitlesUrl.
        /// </summary>
        internal void PreLoadSubtitles()
        {
            DisposePreloadedSubtitles();
            var subtitlesUrl = Container.MediaOptions.SubtitlesUrl;

            // Don't load a thing if we don't have to
            if (string.IsNullOrWhiteSpace(subtitlesUrl))
                return;

            try
            {
                PreloadedSubtitles = LoadBlocks(subtitlesUrl, MediaType.Subtitle, this);

                // Process and adjust subtitle delays if necessary
                if (Container.MediaOptions.SubtitlesDelay != TimeSpan.Zero)
                {
                    var delay = Container.MediaOptions.SubtitlesDelay;
                    for (var i = 0; i < PreloadedSubtitles.Count; i++)
                    {
                        var target = PreloadedSubtitles[i];
                        target.StartTime = TimeSpan.FromTicks(target.StartTime.Ticks + delay.Ticks);
                        target.EndTime = TimeSpan.FromTicks(target.EndTime.Ticks + delay.Ticks);
                        target.Duration = TimeSpan.FromTicks(target.EndTime.Ticks - target.StartTime.Ticks);
                    }
                }

                Container.MediaOptions.IsSubtitleDisabled = true;
            }
            catch (MediaContainerException mex)
            {
                DisposePreloadedSubtitles();
                this.LogWarning(Aspects.Component,
                    $"No subtitles to side-load found in media '{subtitlesUrl}'. {mex.Message}");
            }
        }

        /// <summary>
        /// Returns the value of a discrete frame position of the main media component if possible.
        /// Otherwise, it simply rounds the position to the nearest millisecond.
        /// </summary>
        /// <param name="position">The position.</param>
        /// <returns>The snapped, discrete, normalized position</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal TimeSpan SnapPositionToBlockPosition(TimeSpan position)
        {
            if (Container == null)
                return position.Normalize();

            var blocks = Blocks.Main(Container);
            if (blocks == null) return position.Normalize();

            return blocks.GetSnapPosition(position) ?? position.Normalize();
        }

        /// <summary>
        /// Resumes the playback by resuming the clock and updating the playback state to state.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ResumePlayback()
        {
            Clock.Play();
            State.UpdateMediaState(PlaybackStatus.Play);
        }

        /// <summary>
        /// Updates the clock position and notifies the new
        /// position to the <see cref="State" />.
        /// </summary>
        /// <param name="position">The position.</param>
        /// <returns>The newly set position</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal TimeSpan ChangePosition(TimeSpan position)
        {
            Clock.Update(position);
            State.UpdatePosition();
            return position;
        }

        /// <summary>
        /// Resets the clock to the zero position and notifies the new
        /// position to rhe <see cref="State"/>.
        /// </summary>
        /// <returns>The newly set position</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal TimeSpan ResetPosition()
        {
            Clock.Reset();
            State.UpdatePosition();
            return TimeSpan.Zero;
        }

        /// <summary>
        /// Sends the given block to its corresponding media renderer.
        /// </summary>
        /// <param name="block">The block.</param>
        /// <param name="clockPosition">The clock position.</param>
        /// <returns>
        /// The number of blocks sent to the renderer
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int SendBlockToRenderer(MediaBlock block, TimeSpan clockPosition)
        {
            // No blocks were rendered
            if (block == null) return 0;

            // Process property changes coming from video blocks
            State.UpdateDynamicBlockProperties(block, Blocks[block.MediaType]);

            // Send the block to its corresponding renderer
            Renderers[block.MediaType]?.Render(block, clockPosition);
            LastRenderTime[block.MediaType] = block.StartTime;

            // Log the block statistics for debugging
            LogRenderBlock(block, clockPosition, block.Index);

            // At this point, we are certain that a blocl has been
            // sent to its corresponding renderer.
            return 1;
        }

        /// <summary>
        /// Gets a value indicating whether more frames can be decoded into blocks of the given type.
        /// </summary>
        /// <param name="t">The media type.</param>
        /// <returns>
        ///   <c>true</c> if more frames can be decoded; otherwise, <c>false</c>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool CanReadMoreFramesOf(MediaType t)
        {
            return
                Container.Components[t].BufferLength > 0 ||
                Container.Components[t].HasPacketsInCodec ||
                ShouldReadMorePackets;
        }

        /// <summary>
        /// Tries to receive the next frame from the decoder by decoding queued
        /// Packets and converting the decoded frame into a Media Block which gets
        /// queued into the playback block buffer.
        /// </summary>
        /// <param name="t">The MediaType.</param>
        /// <returns>True if a block could be added. False otherwise.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool AddNextBlock(MediaType t)
        {
            // Decode the frames
            var block = Blocks[t].Add(Container.Components[t].ReceiveNextFrame(), Container);
            return block != null;
        }

        /// <summary>
        /// Invalidates the last render time for the given component.
        /// Additionally, it calls Seek on the renderer to remove any caches
        /// </summary>
        /// <param name="t">The t.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void InvalidateRenderer(MediaType t)
        {
            // This forces the rendering worker to send the
            // corresponding block to its renderer
            LastRenderTime[t] = TimeSpan.MinValue;
            Renderers[t]?.Seek();
        }

        /// <summary>
        /// Detects the end of media in the decoding worker.
        /// </summary>
        /// <param name="decodedFrameCount">The decoded frame count.</param>
        /// <param name="main">The main.</param>
        /// <returns>True if media ended</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool DetectHasDecodingEnded(int decodedFrameCount, MediaType main) =>
                decodedFrameCount <= 0
                && CanReadMoreFramesOf(main) == false
                && Blocks[main].IndexOf(WallClock) >= Blocks[main].Count - 1;

        /// <summary>
        /// Logs a block rendering operation as a Trace Message
        /// if the debugger is attached.
        /// </summary>
        /// <param name="block">The block.</param>
        /// <param name="clockPosition">The clock position.</param>
        /// <param name="renderIndex">Index of the render.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void LogRenderBlock(MediaBlock block, TimeSpan clockPosition, int renderIndex)
        {
            // Prevent logging for production use
            if (Platform.IsInDebugMode == false) return;

            try
            {
                var drift = TimeSpan.FromTicks(clockPosition.Ticks - block.StartTime.Ticks);
                this.LogTrace(Aspects.RenderingWorker,
                    $"{block.MediaType.ToString().Substring(0, 1)} "
                    + $"BLK: {block.StartTime.Format()} | "
                    + $"CLK: {clockPosition.Format()} | "
                    + $"DFT: {drift.TotalMilliseconds,4:0} | "
                    + $"IX: {renderIndex,3} | "
                    + $"PQ: {Container?.Components[block.MediaType]?.BufferLength / 1024d,7:0.0}k | "
                    + $"TQ: {Container?.Components.BufferLength / 1024d,7:0.0}k");
            }
            catch
            {
                // swallow
            }
        }

        #endregion
    }
}
