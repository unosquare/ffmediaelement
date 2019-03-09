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
                    if (HasIndependentClocks)
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

        public bool HasIndependentClocks { get; private set; }

        public MediaType ContinuousType { get; private set; }

        public MediaType DiscreteType { get; private set; }

        private MediaEngine MediaCore { get; }

        private MediaOptions Options => MediaCore.MediaOptions;

        private MediaComponentSet Components => MediaCore.Container.Components;

        public void Initialize()
        {
            lock (SyncLock)
            {
                Offsets.Clear();
                var needsIndependentClocks = false;

                try
                {
                    if (!Components.HasAudio || !Components.HasVideo)
                        return;

                    // We don't need independent clocks when the video is just album art
                    if (Components.MainMediaType == MediaType.Audio)
                        return;

                    if (Options.IsTimeSyncDisabled)
                    {
                        if (!MediaCore.Container.IsLiveStream)
                        {
                            MediaCore.LogWarning(Aspects.Container,
                                $"Media options had {nameof(MediaOptions.IsTimeSyncDisabled)} set to true but this is not recommended for non-live streams.");
                        }

                        needsIndependentClocks = true;
                        return;
                    }

                    var audioStartTime = GetComponentStartOffset(MediaType.Audio);
                    var videoStartTime = GetComponentStartOffset(MediaType.Video);
                    var startTimeDifference = TimeSpan.FromTicks(Math.Abs(audioStartTime.Ticks - videoStartTime.Ticks));

                    if (startTimeDifference > Constants.TimeSyncMaxOffset)
                    {
                        MediaCore.LogWarning(Aspects.Container,
                            $"{nameof(MediaOptions)}.{nameof(MediaOptions.IsTimeSyncDisabled)} has been ignored because the " +
                            $"streams seem to have unrelated timing information. Time Difference: {startTimeDifference.Format()} s.");

                        needsIndependentClocks = true;
                    }
                }
                finally
                {
                    if (needsIndependentClocks)
                    {
                        Clocks[MediaType.Audio] = Clocks[MediaType.Audio] ?? new RealTimeClock();
                        Clocks[MediaType.Video] = Clocks[MediaType.Video] ?? new RealTimeClock();
                        Clocks[MediaType.Subtitle] = Clocks[MediaType.Video];
                        Clocks[MediaType.None] = Clocks[MediaType.Audio];

                        Offsets[MediaType.Audio] = GetComponentStartOffset(MediaType.Audio);
                        Offsets[MediaType.Video] = GetComponentStartOffset(MediaType.Video);
                        Offsets[MediaType.Subtitle] = Offsets[MediaType.Video];
                        Offsets[MediaType.None] = Offsets[MediaType.Audio];
                    }
                    else
                    {
                        Clocks[MediaType.None] = Clocks[MediaType.None] ?? new RealTimeClock();
                        Clocks[MediaType.Audio] = Clocks[MediaType.None];
                        Clocks[MediaType.Video] = Clocks[MediaType.None];
                        Clocks[MediaType.Subtitle] = Clocks[MediaType.None];

                        Offsets[MediaType.Audio] = GetComponentStartOffset(Components.HasAudio ? MediaType.Audio : MediaType.Video);
                        Offsets[MediaType.Video] = GetComponentStartOffset(Components.HasVideo ? MediaType.Video : MediaType.Audio);
                        Offsets[MediaType.Subtitle] = Offsets[MediaType.Video];
                        Offsets[MediaType.None] = Offsets[MediaType.Video];
                    }

                    HasIndependentClocks = needsIndependentClocks;
                    ContinuousType = Components.HasAudio ? MediaType.Audio : MediaType.Video;

                    // We always set the continuous type to the main media type
                    // if the stream is seekable as we don't want to report positions
                    // or seek over a non-main media type.
                    if (MediaCore.Container.IsStreamSeekable)
                        ContinuousType = Components.MainMediaType;

                    DiscreteType = HasIndependentClocks ? ContinuousType : Components.MainMediaType;
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

        public TimeSpan Position(MediaType t)
        {
            lock (SyncLock)
            {
                if (!HasInitialized) return default;
                var referenceType = HasIndependentClocks ? t : t != ContinuousType ? DiscreteType : ContinuousType;
                return TimeSpan.FromTicks(Clocks[t].Position.Ticks + Offsets[referenceType].Ticks);
            }
        }

        public TimeSpan Position() => Position(HasIndependentClocks ? ContinuousType : DiscreteType);

        public void Update(TimeSpan position, MediaType t)
        {
            lock (SyncLock)
            {
                if (!HasInitialized) return;
                var referenceType = HasIndependentClocks ? t : t != ContinuousType ? DiscreteType : ContinuousType;
                Clocks[t].Update(TimeSpan.FromTicks(position.Ticks - Offsets[referenceType].Ticks));
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
