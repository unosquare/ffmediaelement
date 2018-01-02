namespace Unosquare.FFME.Core
{
    using System;
    using System.Diagnostics;

    /// <summary>
    /// A time measurement artifact.
    /// </summary>
    internal sealed class RealtimeClock
    {
        private readonly object SyncLock = new object();
        private readonly Stopwatch Chrono = new Stopwatch();
        private double OffsetMilliseconds = 0;
        private double m_SpeedRatio = Constants.DefaultSpeedRatio;

        /// <summary>
        /// Initializes a new instance of the <see cref="RealtimeClock"/> class.
        /// The clock starts poaused and at the 0 position.
        /// </summary>
        public RealtimeClock()
        {
            Reset();
        }

        /// <summary>
        /// Gets or sets the clock position.
        /// </summary>
        public TimeSpan Position
        {
            get
            {
                lock (SyncLock)
                    return TimeSpan.FromTicks((long)Math.Round(
                        (OffsetMilliseconds + (Chrono.ElapsedMilliseconds * SpeedRatio)) * TimeSpan.TicksPerMillisecond, 0));
            }
            set
            {
                lock (SyncLock)
                {
                    var resume = Chrono.IsRunning;
                    Chrono.Reset();
                    OffsetMilliseconds = value.TotalMilliseconds;
                    if (resume) Chrono.Start();
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether the clock is running.
        /// </summary>
        public bool IsRunning
        {
            get { lock (SyncLock) return Chrono.IsRunning; }
        }

        /// <summary>
        /// Gets or sets the speed ratio at which the clock runs.
        /// </summary>
        public double SpeedRatio
        {
            get
            {
                lock (SyncLock) return m_SpeedRatio;
            }
            set
            {
                lock (SyncLock)
                {
                    if (value < 0d) value = 0d;

                    // Capture the initial position se we set it even after the speedratio has changed
                    // this ensures a smooth position transition
                    var initialPosition = Position;
                    m_SpeedRatio = value;
                    Position = initialPosition;
                }
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
                OffsetMilliseconds = 0;
                Chrono.Reset();
            }
        }
    }
}
