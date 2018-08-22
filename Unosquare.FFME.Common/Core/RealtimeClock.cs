﻿namespace Unosquare.FFME.Core
{
    using Shared;
    using System;
    using System.Diagnostics;

    /// <summary>
    /// A time measurement artifact.
    /// </summary>
    internal sealed class RealTimeClock
    {
        private readonly Stopwatch Chrono = new Stopwatch();
        private readonly object SyncLock = new object();
        private long OffsetTicks;
        private double m_SpeedRatio = Constants.Controller.DefaultSpeedRatio;

        /// <summary>
        /// Initializes a new instance of the <see cref="RealTimeClock"/> class.
        /// The clock starts paused and at the 0 position.
        /// </summary>
        public RealTimeClock() => Reset();

        /// <summary>
        /// Gets or sets the clock position.
        /// </summary>
        public TimeSpan Position
        {
            get
            {
                lock (SyncLock)
                {
                    return TimeSpan.FromTicks(
                        OffsetTicks + Convert.ToInt64(Chrono.Elapsed.Ticks * SpeedRatio));
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether the clock is running.
        /// </summary>
        public bool IsRunning => Chrono.IsRunning;

        /// <summary>
        /// Gets or sets the speed ratio at which the clock runs.
        /// </summary>
        public double SpeedRatio
        {
            get
            {
                lock (SyncLock)
                {
                    return m_SpeedRatio;
                }
            }
            set
            {
                lock (SyncLock)
                {
                    // Capture the initial position se we set it even after the speedratio has changed
                    // this ensures a smooth position transition
                    var initialPosition = Position;
                    m_SpeedRatio = value < 0d ? 0d : value;
                    Update(initialPosition);
                }
            }
        }

        /// <summary>
        /// Sets a new position value atomically
        /// </summary>
        /// <param name="value">The new value that the position porperty will hold.</param>
        public void Update(TimeSpan value)
        {
            lock (SyncLock)
            {
                var resume = Chrono.IsRunning;
                Chrono.Reset();
                OffsetTicks = value.Ticks;
                if (resume) Chrono.Start();
            }
        }

        /// <summary>
        /// Starts or resumes the clock.
        /// </summary>
        public void Play()
        {
            lock (SyncLock)
            {
                if (Chrono.IsRunning) return;
                Chrono.Start();
            }
        }

        /// <summary>
        /// Pauses the clock.
        /// </summary>
        public void Pause()
        {
            lock (SyncLock)
                Chrono.Stop();
        }

        /// <summary>
        /// Sets the clock position to 0 and stops it.
        /// The speed ratio is not modified.
        /// </summary>
        public void Reset()
        {
            lock (SyncLock)
            {
                OffsetTicks = 0;
                Chrono.Reset();
            }
        }
    }
}
