namespace Unosquare.FFME.Workers
{
    using Core;
    using Decoding;
    using Primitives;
    using Shared;
    using System;

    internal sealed class ClockController
    {
        private readonly object SyncLock = new object();
        private readonly MediaTypeDictionary<RealTimeClock> Clocks = new MediaTypeDictionary<RealTimeClock>();

        public ClockController(MediaEngine mediaCore)
        {
            MediaCore = mediaCore;
        }

        private MediaEngine MediaCore { get; }

        private MediaOptions Options => MediaCore.MediaOptions;

        private MediaContainer Container => MediaCore.Container;

        private MediaComponentSet Components => Container.Components;

        private bool HasMasterClock
        {
            get
            {
                lock (SyncLock)
                    return Clocks.Count > 1;
            }
        }

        public void Initialize()
        {
            lock (SyncLock)
            {
                Clocks.Clear();
                var needsIndependentClocks = false;

                try
                {
                    if (Components.HasAudio && Components.HasVideo && MediaCore.State.IsLiveStream)
                    {
                        if (Options.IsTimeSyncDisabled)
                            needsIndependentClocks = true;

                        if (!needsIndependentClocks)
                        {
                            var audioStartTime = Components[MediaType.Audio].StartTime;
                            var videoStartTime = Components[MediaType.Video].StartTime;
                            var startTimeDifference = TimeSpan.FromTicks(Math.Abs(audioStartTime.Ticks - videoStartTime.Ticks));

                            if (startTimeDifference > Constants.TimeSyncMaxOffset)
                            {
                                MediaCore.LogWarning(Aspects.RenderingWorker,
                                    $"{nameof(MediaOptions)}.{nameof(MediaOptions.IsTimeSyncDisabled)} has been set to true because the " +
                                    $"streams seem to have unrelated timing information. Time Difference: {startTimeDifference.Format()} s.");

                                needsIndependentClocks = true;
                            }
                        }
                    }
                }
                finally
                {
                    if (needsIndependentClocks)
                    {
                        Clocks[MediaType.Audio] = new RealTimeClock();
                        Clocks[MediaType.Audio].Update(Components.Audio.StartTime);

                        Clocks[MediaType.Video] = new RealTimeClock();
                        Clocks[MediaType.Video].Update(Components.Video.StartTime);
                    }
                    else
                    {
                        Clocks[MediaType.None] = new RealTimeClock();
                        Clocks[MediaType.None].Update(Components.Main.StartTime);
                    }
                }
            }
        }

        public TimeSpan Position(MediaType t)
        {
            lock (SyncLock)
            {
                if (HasMasterClock)
                    return Clocks[MediaType.None].Position;

                //if (t == MediaType.None)
                    return
                // TODO return main for none. etc.
            }
        }
    }
}
