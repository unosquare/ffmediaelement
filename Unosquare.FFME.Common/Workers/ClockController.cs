namespace Unosquare.FFME.Workers
{
    using Core;
    using Decoding;
    using Primitives;
    using Shared;
    using System;
    using System.Runtime.CompilerServices;

    internal sealed class ClockController
    {
        private readonly object SyncLock = new object();
        private readonly MediaTypeDictionary<RealTimeClock> Clocks = new MediaTypeDictionary<RealTimeClock>();
        private readonly MediaTypeDictionary<TimeSpan> Offsets = new MediaTypeDictionary<TimeSpan>();
        private bool HasInitialized = false;

        public ClockController(MediaEngine mediaCore)
        {
            MediaCore = mediaCore;
        }

        public double SpeedRatio
        {
            get
            {
                lock (SyncLock)
                {
                    if (!HasInitialized) return Constants.Controller.DefaultSpeedRatio;
                    return Clocks[MediaType.None].SpeedRatio;
                }
            }
            set
            {
                lock (SyncLock)
                {
                    if (!HasInitialized) return;
                    if (HasMultipleClocks)
                    {
                        Clocks[MediaType.Audio].SpeedRatio = value;
                        Clocks[MediaType.Video].SpeedRatio = value;
                    }
                    else
                    {
                        Clocks[MediaType.None].SpeedRatio = value;
                    }
                }
            }
        }

        public MediaType ReferenceType { get; private set; }

        public bool HasMultipleClocks { get; private set; }

        public bool HasDisconnectedClocks { get; private set; }

        private MediaEngine MediaCore { get; }

        private MediaOptions Options => MediaCore.MediaOptions;

        private MediaComponentSet Components => MediaCore.Container.Components;

        public void Initialize()
        {
            lock (SyncLock)
            {
                // Save the current clocks so they can be recreated with the
                // same properties (position and speed ratio)
                var lastClocks = new MediaTypeDictionary<RealTimeClock>();
                foreach (var kvp in Clocks)
                    lastClocks[kvp.Key] = kvp.Value;

                try
                {
                    if (Options.IsTimeSyncDisabled)
                    {
                        if (!MediaCore.Container.IsLiveStream)
                        {
                            MediaCore.LogWarning(Aspects.Container,
                                $"Media options had {nameof(MediaOptions.IsTimeSyncDisabled)} set to true but this is not recommended for non-live streams.");
                        }

                        return;
                    }

                    if (!Components.HasAudio || !Components.HasVideo)
                        return;

                    var audioStartTime = GetComponentStartOffset(MediaType.Audio);
                    var videoStartTime = GetComponentStartOffset(MediaType.Video);
                    var startTimeDifference = TimeSpan.FromTicks(Math.Abs(audioStartTime.Ticks - videoStartTime.Ticks));

                    if (startTimeDifference > Constants.TimeSyncMaxOffset)
                    {
                        MediaCore.LogWarning(Aspects.Container,
                            $"{nameof(MediaOptions)}.{nameof(MediaOptions.IsTimeSyncDisabled)} has been ignored because the " +
                            $"streams seem to have unrelated timing information. Time Difference: {startTimeDifference.Format()} s.");

                        Options.IsTimeSyncDisabled = true;
                    }
                }
                finally
                {
                    if (Components.HasAudio && Components.HasVideo)
                    {
                        Clocks[MediaType.Audio] = new RealTimeClock();
                        Clocks[MediaType.Video] = new RealTimeClock();

                        Offsets[MediaType.Audio] = GetComponentStartOffset(MediaType.Audio);
                        Offsets[MediaType.Video] = GetComponentStartOffset(MediaType.Video);

                        HasMultipleClocks = true;
                    }
                    else
                    {
                        Clocks[MediaType.Audio] = new RealTimeClock();
                        Clocks[MediaType.Video] = Clocks[MediaType.Audio];

                        Offsets[MediaType.Audio] = GetComponentStartOffset(Components.HasAudio ? MediaType.Audio : MediaType.Video);
                        Offsets[MediaType.Video] = Offsets[MediaType.Audio];

                        HasMultipleClocks = false;
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
                    var continuousType = Components.HasAudio && !MediaCore.Container.IsStreamSeekable
                        ? MediaType.Audio
                        : Components.MainMediaType;

                    var discreteType = Components.MainMediaType;
                    HasDisconnectedClocks = Options.IsTimeSyncDisabled && HasMultipleClocks;
                    ReferenceType = HasDisconnectedClocks ? continuousType : discreteType;

                    // The default data is what the clock reference contains
                    Clocks[MediaType.None] = Clocks[ReferenceType];
                    Offsets[MediaType.None] = Offsets[ReferenceType];

                    HasInitialized = true;
                }
            }
        }

        public bool IsRunning(MediaType t)
        {
            lock (SyncLock)
            {
                if (!HasInitialized) return default;
                return Clocks[t].IsRunning;
            }
        }

        public bool IsRunning() => IsRunning(ReferenceType);

        public TimeSpan Position(MediaType t)
        {
            lock (SyncLock)
            {
                if (!HasInitialized)
                    return default;

                return TimeSpan.FromTicks(
                    Clocks[t].Position.Ticks +
                    Offsets[HasDisconnectedClocks ? t : ReferenceType].Ticks);
            }
        }

        public TimeSpan Position() => Position(ReferenceType);

        public void Update(TimeSpan position, MediaType t)
        {
            lock (SyncLock)
            {
                if (!HasInitialized)
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

        public void Pause(MediaType t)
        {
            lock (SyncLock)
            {
                if (!HasInitialized) return;
                if (t == MediaType.None)
                {
                    Clocks[MediaType.Audio].Pause();
                    Clocks[MediaType.Video].Pause();
                    return;
                }

                Clocks[t].Pause();
            }
        }

        public void Reset(MediaType t)
        {
            lock (SyncLock)
            {
                if (!HasInitialized) return;
                if (t == MediaType.None)
                {
                    Clocks[MediaType.Audio].Reset();
                    Clocks[MediaType.Video].Reset();
                    return;
                }

                Clocks[t].Reset();
            }
        }

        public void Play(MediaType t)
        {
            lock (SyncLock)
            {
                if (!HasInitialized) return;
                if (t == MediaType.None)
                {
                    Clocks[MediaType.Audio].Play();
                    Clocks[MediaType.Video].Play();
                    return;
                }

                Clocks[t].Play();
            }
        }

        /// <summary>
        /// Gets the component start offset.
        /// </summary>
        /// <param name="t">The component media type.</param>
        /// <returns>The component start time</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private TimeSpan GetComponentStartOffset(MediaType t) =>
            Components[t].StartTime == TimeSpan.MinValue ? TimeSpan.Zero : Components[t].StartTime;
    }
}
