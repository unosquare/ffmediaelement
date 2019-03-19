namespace Unosquare.FFME
{
    using Engine;
    using Primitives;
    using System;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using Workers;

    public partial class MediaEngine
    {
        #region State Management

        /// <summary>
        /// Gets the buffer length maximum.
        /// port of MAX_QUEUE_SIZE (ffplay.c)
        /// </summary>
        internal const long BufferLengthMax = 16 * 1024 * 1024;

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
                $" | Disconnected Clocks: {Timing.HasDisconnectedClocks}" +
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
        /// Updates the specified clock type to a new playback position.
        /// </summary>
        /// <param name="playbackPosition">The new playback position</param>
        /// <param name="t">The clock type. Pass none for all clocks</param>
        /// <param name="reportPosition">If the new playback position should be reported.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ChangePlaybackPosition(TimeSpan playbackPosition, MediaType t, bool reportPosition)
        {
            if (Timing.HasDisconnectedClocks && t == MediaType.None)
            {
                this.LogWarning(Aspects.Container,
                    $"Changing the playback position on disconnected clocks is not supported." +
                    $"Plase set the {nameof(MediaOptions.IsTimeSyncDisabled)} to false.");
            }

            Timing.Update(playbackPosition, t);
            InvalidateRenderers();

            if (reportPosition)
                State.ReportPlaybackPosition();
        }

        /// <summary>
        /// Updates the clock position and notifies the new
        /// position to the <see cref="State" />.
        /// </summary>
        /// <param name="playbackPosition">The position.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ChangePlaybackPosition(TimeSpan playbackPosition) =>
            ChangePlaybackPosition(playbackPosition, MediaType.None, true);

        /// <summary>
        /// Pauses the playback by pausing the RTC.
        /// This does not change the state.
        /// </summary>
        /// <param name="t">The clock to pause</param>
        /// <param name="reportPosition">If the new playback position should be reported.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void PausePlayback(MediaType t, bool reportPosition)
        {
            Timing.Pause(t);

            if (reportPosition)
                State.ReportPlaybackPosition();
        }

        /// <summary>
        /// Pauses the playback by pausing the RTC.
        /// This does not change the state.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void PausePlayback() => PausePlayback(MediaType.None, true);

        /// <summary>
        /// Resets the clock to the zero position and notifies the new
        /// position to rhe <see cref="State"/>.
        /// </summary>
        /// <returns>The newly set position</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal TimeSpan ResetPlaybackPosition()
        {
            Timing.Pause(MediaType.None);
            Timing.Reset(MediaType.None);
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
            Renderers[t]?.OnSeek();
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

        #endregion
    }
}
