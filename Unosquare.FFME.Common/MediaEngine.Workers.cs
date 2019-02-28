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

        private readonly AtomicBoolean m_HasDecodingEnded = new AtomicBoolean(false);

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

        #endregion

        #region Methods

        /// <summary>
        /// Gets the component start offset. Pass none to get the media start offset.
        /// </summary>
        /// <param name="t">The component media type.</param>
        /// <returns>The component start time</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal TimeSpan GetComponentStartOffset(MediaType t)
        {
            var offset = t == MediaType.None
                ? State.PlaybackStartTime ?? TimeSpan.MinValue
                : Container?.Components[t]?.StartTime ?? TimeSpan.MinValue;

            return offset == TimeSpan.MinValue ? TimeSpan.Zero : offset;
        }

        /// <summary>
        /// Converts from playback time to wall clock time.
        /// </summary>
        /// <param name="playbackTime">The playback time.</param>
        /// <returns>The wall clock time</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal TimeSpan ConvertPlaybackToClockTime(TimeSpan playbackTime) =>
            TimeSpan.FromTicks(playbackTime.Ticks - GetComponentStartOffset(MediaType.None).Ticks);

        /// <summary>
        /// Resumes the playback by resuming the clock and updating the playback state to state.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ResumePlayback()
        {
            // TODO: Only the blockrendering worker can update the clock state
            Clock.Play();
            State.UpdateMediaState(PlaybackStatus.Play);
        }

        /// <summary>
        /// Updates the clock position and notifies the new
        /// position to the <see cref="State" />.
        /// </summary>
        /// <param name="playbackPosition">The position.</param>
        /// <returns>The newly set position</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal TimeSpan ChangePlaybackPosition(TimeSpan playbackPosition)
        {
            // TODO -- we need to fix all occurrences of this. This is the absolute time to compute elapsed time
            Clock.Update(ConvertPlaybackToClockTime(playbackPosition));
            State.ReportPlaybackPosition();
            return playbackPosition;
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

        #endregion
    }
}
