namespace Unosquare.FFME.Engine
{
    using Common;
    using Diagnostics;
    using Primitives;
    using System;
    using System.Runtime.CompilerServices;
    using Unosquare.FFME.Container;

    /// <summary>
    /// Implements a real-time clock controller capable of handling independent
    /// clocks for each of the components.
    /// </summary>
    internal sealed class TimingController
    {
        private readonly object SyncLock = new object();
        private readonly MediaTypeDictionary<RealTimeClock> Clocks = new MediaTypeDictionary<RealTimeClock>();
        private readonly MediaTypeDictionary<TimeSpan> Offsets = new MediaTypeDictionary<TimeSpan>();
        private bool IsReady;
        private MediaType m_ReferenceType;
        private bool m_HasDisconnectedClocks;

        /// <summary>
        /// Initializes a new instance of the <see cref="TimingController"/> class.
        /// </summary>
        /// <param name="mediaCore">The media core.</param>
        public TimingController(MediaEngine mediaCore)
        {
            MediaCore = mediaCore;
        }

        /// <summary>
        /// Gets or sets the speed ratio. All clocks are bound to the same value.
        /// </summary>
        public double SpeedRatio
        {
            get
            {
                lock (SyncLock)
                {
                    if (!IsReady) return Constants.DefaultSpeedRatio;
                    return Clocks[MediaType.None].SpeedRatio;
                }
            }
            set
            {
                lock (SyncLock)
                {
                    if (!IsReady) return;
                    Clocks[MediaType.Audio].SpeedRatio = value;
                    Clocks[MediaType.Video].SpeedRatio = value;
                }
            }
        }

        /// <summary>
        /// Gets the clock type that positions are offset by.
        /// </summary>
        public MediaType ReferenceType
        {
            get { lock (SyncLock) return m_ReferenceType; }
            private set { lock (SyncLock) m_ReferenceType = value; }
        }

        /// <summary>
        /// Gets a value indicating whether the real-time clocks of the components are disconnected clocks.
        /// </summary>
        public bool HasDisconnectedClocks
        {
            get { lock (SyncLock) return m_HasDisconnectedClocks; }
            private set { lock (SyncLock) m_HasDisconnectedClocks = value; }
        }

        /// <summary>
        /// Gets a value indicating whether the real-time clock of the reference type is running.
        /// </summary>
        public bool IsRunning => GetIsRunning(ReferenceType);

        /// <summary>
        /// Gets the playback position of the real-time clock of the timing reference component type.
        /// </summary>
        /// <returns>The clock position.</returns>
        public TimeSpan Position => GetPosition(ReferenceType);

        /// <summary>
        /// Gets the duration of the reference component type.
        /// </summary>
        public TimeSpan? Duration => GetDuration(ReferenceType);

        /// <summary>
        /// Gets the start time of the reference component type.
        /// </summary>
        public TimeSpan StartTime => GetStartTime(ReferenceType);

        /// <summary>
        /// Gets the end time of the reference component type.
        /// </summary>
        public TimeSpan? EndTime => GetEndTime(ReferenceType);

        /// <summary>
        /// Gets the media core.
        /// </summary>
        private MediaEngine MediaCore { get; }

        /// <summary>
        /// Sets up timing and clocks. Call this method when media components change.
        /// </summary>
        public void Setup()
        {
            lock (SyncLock)
            {
                var options = MediaCore?.MediaOptions;
                var components = MediaCore?.Container?.Components;

                if (components == null || options == null)
                {
                    MediaCore?.LogError(Aspects.Timing, "Unable to setup the timing controller. No components or options found.");
                    Reset();
                    return;
                }

                // Save the current clocks so they can be recreated with the
                // same properties (position and speed ratio)
                var lastClocks = new MediaTypeDictionary<RealTimeClock>();
                foreach (var kvp in Clocks)
                    lastClocks[kvp.Key] = kvp.Value;

                try
                {
                    if (options.IsTimeSyncDisabled)
                    {
                        if (!MediaCore.Container.IsLiveStream)
                        {
                            MediaCore.LogWarning(Aspects.Timing,
                                $"Media options had {nameof(MediaOptions.IsTimeSyncDisabled)} set to true. This is not recommended for non-live streams.");
                        }

                        return;
                    }

                    if (!components.HasAudio || !components.HasVideo)
                        return;

                    var audioStartTime = GetComponentStartOffset(MediaType.Audio);
                    var videoStartTime = GetComponentStartOffset(MediaType.Video);
                    var startTimeDifference = TimeSpan.FromTicks(Math.Abs(audioStartTime.Ticks - videoStartTime.Ticks));

                    if (startTimeDifference > Constants.TimeSyncMaxOffset)
                    {
                        MediaCore.LogWarning(Aspects.Timing,
                            $"{nameof(MediaOptions)}.{nameof(MediaOptions.IsTimeSyncDisabled)} has been ignored because the " +
                            $"streams seem to have unrelated timing information. Time Difference: {startTimeDifference.Format()} s.");

                        options.IsTimeSyncDisabled = true;
                    }
                }
                finally
                {
                    if (components.HasAudio && components.HasVideo)
                    {
                        Clocks[MediaType.Audio] = new RealTimeClock();
                        Clocks[MediaType.Video] = new RealTimeClock();

                        Offsets[MediaType.Audio] = GetComponentStartOffset(MediaType.Audio);
                        Offsets[MediaType.Video] = GetComponentStartOffset(MediaType.Video);
                    }
                    else
                    {
                        Clocks[MediaType.Audio] = new RealTimeClock();
                        Clocks[MediaType.Video] = Clocks[MediaType.Audio];

                        Offsets[MediaType.Audio] = GetComponentStartOffset(components.HasAudio ? MediaType.Audio : MediaType.Video);
                        Offsets[MediaType.Video] = Offsets[MediaType.Audio];
                    }

                    // Subtitles will always be whatever the video data is.
                    Clocks[MediaType.Subtitle] = Clocks[MediaType.Video];
                    Offsets[MediaType.Subtitle] = Offsets[MediaType.Video];

                    // Update from previous clocks to keep state
                    foreach (var clock in lastClocks)
                    {
                        Clocks[clock.Key].SpeedRatio = clock.Value.SpeedRatio;
                        Clocks[clock.Key].Update(clock.Value.Position);
                    }

                    // By default the continuous type is the audio component if it's a live stream
                    var continuousType = components.HasAudio && !MediaCore.Container.IsStreamSeekable
                        ? MediaType.Audio
                        : components.SeekableMediaType;

                    var discreteType = components.SeekableMediaType;
                    HasDisconnectedClocks = options.IsTimeSyncDisabled && Clocks[MediaType.Audio] != Clocks[MediaType.Video];
                    ReferenceType = HasDisconnectedClocks ? continuousType : discreteType;

                    // The default data is what the clock reference contains
                    Clocks[MediaType.None] = Clocks[ReferenceType];
                    Offsets[MediaType.None] = Offsets[ReferenceType];
                    IsReady = true;

                    MediaCore.State.ReportTimingStatus();
                }
            }
        }

        /// <summary>
        /// Clears all component clocks and timing offsets.
        /// </summary>
        public void Reset()
        {
            try
            {
                lock (SyncLock)
                {
                    IsReady = false;
                    Clocks.Clear();
                    Offsets.Clear();
                }
            }
            finally
            {
                MediaCore.State.ReportTimingStatus();
            }
        }

        /// <summary>
        /// Gets the playback position of the real-time clock of the given component type.
        /// </summary>
        /// <param name="t">The t.</param>
        /// <returns>The clock position.</returns>
        public TimeSpan GetPosition(MediaType t)
        {
            lock (SyncLock)
            {
                if (!IsReady)
                    return default;

                return TimeSpan.FromTicks(
                    Clocks[t].Position.Ticks +
                    Offsets[HasDisconnectedClocks ? t : ReferenceType].Ticks);
            }
        }

        /// <summary>
        /// Gets the playback duration of the given component type.
        /// </summary>
        /// <param name="t">The t.</param>
        /// <returns>The duration of the component type.</returns>
        public TimeSpan? GetDuration(MediaType t)
        {
            lock (SyncLock)
                return IsReady ? GetComponentDuration(t) : default;
        }

        /// <summary>
        /// Gets the start time of the given component type.
        /// </summary>
        /// <param name="t">The t.</param>
        /// <returns>The duration of the component type.</returns>
        public TimeSpan GetStartTime(MediaType t)
        {
            lock (SyncLock)
                return IsReady ? Offsets[t] : default;
        }

        /// <summary>
        /// Gets the playback end time of the given component type.
        /// </summary>
        /// <param name="t">The t.</param>
        /// <returns>The duration of the component type.</returns>
        public TimeSpan? GetEndTime(MediaType t)
        {
            lock (SyncLock)
            {
                var duration = GetComponentDuration(t);
                return IsReady ? duration.HasValue ? TimeSpan.FromTicks(duration.Value.Ticks + Offsets[t].Ticks) : default : default;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the RTC of the given component type is running.
        /// </summary>
        /// <param name="t">The t.</param>
        /// <returns>Whether the component's RTC is running.</returns>
        public bool GetIsRunning(MediaType t)
        {
            lock (SyncLock)
            {
                if (!IsReady) return default;
                return Clocks[t].IsRunning;
            }
        }

        /// <summary>
        /// Updates the position of the component's clock. Pass none to update all clocks to the same postion.
        /// </summary>
        /// <param name="position">The position to update to.</param>
        /// <param name="t">The clock's media type.</param>
        public void Update(TimeSpan position, MediaType t)
        {
            try
            {
                lock (SyncLock)
                {
                    if (!IsReady)
                        return;

                    if (t == MediaType.None)
                    {
                        Clocks[MediaType.Audio].Update(TimeSpan.FromTicks(
                            position.Ticks -
                            Offsets[HasDisconnectedClocks ? MediaType.Audio : ReferenceType].Ticks));

                        Clocks[MediaType.Video].Update(TimeSpan.FromTicks(
                            position.Ticks -
                            Offsets[HasDisconnectedClocks ? MediaType.Video : ReferenceType].Ticks));

                        return;
                    }

                    Clocks[t].Update(TimeSpan.FromTicks(
                        position.Ticks -
                        Offsets[HasDisconnectedClocks ? t : ReferenceType].Ticks));
                }
            }
            finally
            {
                MediaCore.State.ReportTimingStatus();
            }
        }

        /// <summary>
        /// Pauses the specified clock. Pass none to pause all clocks.
        /// </summary>
        /// <param name="t">The clock type.</param>
        public void Pause(MediaType t)
        {
            try
            {
                lock (SyncLock)
                {
                    if (!IsReady) return;
                    if (t == MediaType.None)
                    {
                        Clocks[MediaType.Audio].Pause();
                        Clocks[MediaType.Video].Pause();
                        return;
                    }

                    Clocks[t].Pause();
                }
            }
            finally
            {
                MediaCore.State.ReportTimingStatus();
            }
        }

        /// <summary>
        /// Resets the position of the specified clock. Pass none to reset all.
        /// </summary>
        /// <param name="t">The media type.</param>
        public void Reset(MediaType t)
        {
            try
            {
                lock (SyncLock)
                {
                    if (!IsReady) return;
                    if (t == MediaType.None)
                    {
                        Clocks[MediaType.Audio].Reset();
                        Clocks[MediaType.Video].Reset();
                        return;
                    }

                    Clocks[t].Reset();
                }
            }
            finally
            {
                MediaCore.State.ReportTimingStatus();
            }
        }

        /// <summary>
        /// Plays or resumes the specified clock. Pass none to play all.
        /// </summary>
        /// <param name="t">The media type.</param>
        public void Play(MediaType t)
        {
            try
            {
                lock (SyncLock)
                {
                    if (!IsReady) return;
                    if (t == MediaType.None)
                    {
                        Clocks[MediaType.Audio].Play();
                        Clocks[MediaType.Video].Play();
                        return;
                    }

                    Clocks[t].Play();
                }
            }
            finally
            {
                MediaCore.State.ReportTimingStatus();
            }
        }

        /// <summary>
        /// Gets the component start offset.
        /// </summary>
        /// <param name="t">The component media type.</param>
        /// <returns>The component start time.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private TimeSpan GetComponentStartOffset(MediaType t)
        {
            if (MediaCore?.Container?.Components is MediaComponentSet components && components[t] is MediaComponent component)
                return component.StartTime == TimeSpan.MinValue ? TimeSpan.Zero : component.StartTime;

            return TimeSpan.Zero;
        }

        /// <summary>
        /// Gets the component duration.
        /// </summary>
        /// <param name="t">The component media type.</param>
        /// <returns>The component duration time.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private TimeSpan? GetComponentDuration(MediaType t)
        {
            if (MediaCore?.Container?.Components is MediaComponentSet components && components[t] is MediaComponent component)
                return component.Duration.Ticks <= 0 ? default(TimeSpan?) : component.Duration;

            return TimeSpan.Zero;
        }
    }
}
