namespace Unosquare.FFME
{
    using Primitives;
    using Shared;
    using System;
    using System.Linq;
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

        private DateTime SyncBufferStartTime = DateTime.UtcNow;

        /// <summary>
        /// Holds the materialized block cache for each media type.
        /// </summary>
        public MediaTypeDictionary<MediaBlockBuffer> Blocks { get; } = new MediaTypeDictionary<MediaBlockBuffer>();

        /// <summary>
        /// Gets the preloaded subtitle blocks.
        /// </summary>
        public MediaBlockBuffer PreloadedSubtitles { get; internal set; }

        /// <summary>
        /// Gets the worker collection
        /// </summary>
        internal MediaWorkerSet Workers { get; set; }

        /// <summary>
        /// Holds the block renderers
        /// </summary>
        internal MediaTypeDictionary<IMediaRenderer> Renderers { get; } = new MediaTypeDictionary<IMediaRenderer>();

        /// <summary>
        /// Holds the last rendered StartTime for each of the media block types
        /// </summary>
        internal MediaTypeDictionary<TimeSpan> LastRenderTime { get; } = new MediaTypeDictionary<TimeSpan>();

        /// <summary>
        /// Gets a value indicating whether the decoder worker is sync-buffering.
        /// Sync-buffering is entered when there are no main blocks for the current clock.
        /// This in turn pauses the clock (without changing the media state).
        /// The decoder exits this condition when buffering is no longer needed and
        /// updates the clock position to what is available in the main block buffer.
        /// </summary>
        internal bool IsSyncBuffering
        {
            get => m_IsSyncBuffering.Value;
            private set => m_IsSyncBuffering.Value = value;
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

        /// <summary>
        /// Gets a value indicating whether the decoder needs to wait for the reader to receive more packets.
        /// </summary>
        internal bool NeedsMorePackets => ShouldReadMorePackets && !Container.Components.HasEnoughPackets;

        #endregion

        #region Methods

        /// <summary>
        /// Signals that the engine has entered the syn-buffering state.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SignalSyncBufferingEntered()
        {
            if (IsSyncBuffering)
                return;

            PausePlayback();
            SyncBufferStartTime = DateTime.UtcNow;
            IsSyncBuffering = true;

            this.LogInfo(Aspects.RenderingWorker,
                $"SYNC-BUFFER: Entered at {PlaybackPosition.TotalSeconds:0.000} s." +
                $" | Drop Late Frames: {MediaOptions.DropLateFrames}" +
                $" | Disable Time Sync: {MediaOptions.IsTimeSyncDisabled}" +
                $" | Buffer Progress: {State.BufferingProgress:p2}" +
                $" | Buffer Audio: {Container?.Components[MediaType.Audio]?.BufferCount}" +
                $" | Buffer Video: {Container?.Components[MediaType.Video]?.BufferCount}");
        }

        /// <summary>
        /// Signals that the engine has exited the syn-buffering state.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SignalSyncBufferingExited()
        {
            if (!IsSyncBuffering)
                return;

            IsSyncBuffering = false;
            this.LogInfo(Aspects.RenderingWorker,
                $"SYNC-BUFFER: Exited in {DateTime.UtcNow.Subtract(SyncBufferStartTime).TotalSeconds:0.000} s." +
                $" | Commands Pending: {Commands.HasPendingCommands}" +
                $" | Decoding Ended: {HasDecodingEnded}" +
                $" | Buffer Progress: {State.BufferingProgress:p2}" +
                $" | Buffer Audio: {Container?.Components[MediaType.Audio]?.BufferCount}" +
                $" | Buffer Video: {Container?.Components[MediaType.Video]?.BufferCount}");
        }

        /// <summary>
        /// Updates the clock position and notifies the new
        /// position to the <see cref="State" />.
        /// </summary>
        /// <param name="playbackPosition">The position.</param>
        /// <param name="t">The corresponding media type clock to update</param>
        /// <returns>The newly set position</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal TimeSpan ChangePlaybackPosition(TimeSpan playbackPosition, MediaType t)
        {
            Clock.Update(playbackPosition, t);
            State.ReportPlaybackPosition();
            return playbackPosition;
        }

        /// <summary>
        /// Updates the clock position and notifies the new
        /// position to the <see cref="State" />.
        /// </summary>
        /// <param name="playbackPosition">The position.</param>
        /// <returns>The newly set position</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal TimeSpan ChangePlaybackPosition(TimeSpan playbackPosition) =>
            ChangePlaybackPosition(playbackPosition, Clock.DiscreteType);

        /// <summary>
        /// Pauses the playback by pausing the RTC.
        /// This does not change the any state.
        /// </summary>
        /// <param name="t">The clock to pause</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void PausePlayback(MediaType t)
        {
            Clock.Pause(t);
            State.ReportPlaybackPosition();
        }

        /// <summary>
        /// Pauses the playback by pausing the RTC.
        /// This does not change the any state.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void PausePlayback() => PausePlayback(MediaType.None);

        /// <summary>
        /// Resets the clock to the zero position and notifies the new
        /// position to rhe <see cref="State"/>.
        /// </summary>
        /// <returns>The newly set position</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal TimeSpan ResetPlaybackPosition()
        {
            Clock.Pause(MediaType.None);
            Clock.Reset(MediaType.None);
            State.ReportPlaybackPosition();
            return TimeSpan.Zero;
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
        /// Invalidates the last render time for all renderers given component.
        /// Additionally, it calls Seek on the renderers to remove any caches
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void InvalidateRenderers()
        {
            var mediaTypes = Renderers.Keys.ToArray();
            foreach (var t in mediaTypes)
                InvalidateRenderer(t);
        }

        /// <summary>
        /// Gets the component start offset. Pass none to get the media start offset.
        /// </summary>
        /// <param name="t">The component media type.</param>
        /// <returns>The component start time</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private TimeSpan GetComponentStartOffset(MediaType t)
        {
            t = (t == MediaType.None && Container?.Components[MediaType.Audio] != null)
                ? MediaType.Audio : MediaType.None;

            var offset = t == MediaType.None
                ? State.PlaybackStartTime ?? TimeSpan.MinValue
                : Container?.Components[t]?.StartTime ?? TimeSpan.MinValue;

            return offset == TimeSpan.MinValue ? TimeSpan.Zero : offset;
        }

        #endregion
    }
}
