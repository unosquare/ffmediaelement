namespace Unosquare.FFME.Core
{
    using Shared;
    using System;
    using System.Diagnostics;

    /// <summary>
    /// A time measurement artifact.
    /// </summary>
    internal sealed class RealTimeClock
    {
        private readonly Stopwatch Chronometer = new Stopwatch();
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
                        OffsetTicks + Convert.ToInt64(Chronometer.Elapsed.Ticks * SpeedRatio));
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether the clock is running.
        /// </summary>
        public bool IsRunning => Chronometer.IsRunning;

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
                    // Capture the initial position se we set it even after the speed ratio has changed
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
        /// <param name="value">The new value that the position property will hold.</param>
        public void Update(TimeSpan value)
        {
            lock (SyncLock)
            {
                var resume = Chronometer.IsRunning;
                Chronometer.Reset();
                OffsetTicks = value.Ticks;
                if (resume) Chronometer.Start();
            }
        }

        /// <summary>
        /// Starts or resumes the clock.
        /// </summary>
        public void Play()
        {
            lock (SyncLock)
            {
                if (Chronometer.IsRunning) return;
                Chronometer.Start();
            }
        }

        /// <summary>
        /// Pauses the clock.
        /// </summary>
        public void Pause()
        {
            lock (SyncLock)
                Chronometer.Stop();
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
                Chronometer.Reset();
            }
        }
    }
}
