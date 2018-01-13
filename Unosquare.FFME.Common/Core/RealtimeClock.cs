namespace Unosquare.FFME.Core
{
    using Shared;
    using System;
    using System.Diagnostics;
    using System.Threading;

    /// <summary>
    /// A time measurement artifact.
    /// </summary>
    internal sealed class RealTimeClock
    {
        private readonly Stopwatch Chrono = new Stopwatch();
        private ReaderWriterLock Locker = new ReaderWriterLock();
        private double OffsetMilliseconds = 0;
        private double m_SpeedRatio = Defaults.DefaultSpeedRatio;

        /// <summary>
        /// Initializes a new instance of the <see cref="RealTimeClock"/> class.
        /// The clock starts poaused and at the 0 position.
        /// </summary>
        public RealTimeClock()
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
                try
                {
                    Locker.AcquireReaderLock(Timeout.Infinite);
                    return TimeSpan.FromTicks((long)Math.Round(
                        (OffsetMilliseconds + (Chrono.ElapsedMilliseconds * SpeedRatio)) * TimeSpan.TicksPerMillisecond, 0));
                }
                finally
                {
                    Locker.ReleaseReaderLock();
                }
            }
            set
            {
                try
                {
                    Locker.AcquireWriterLock(Timeout.Infinite);
                    var resume = Chrono.IsRunning;
                    Chrono.Reset();
                    OffsetMilliseconds = value.TotalMilliseconds;
                    if (resume) Chrono.Start();
                }
                finally
                {
                    Locker.ReleaseWriterLock();
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether the clock is running.
        /// </summary>
        public bool IsRunning
        {
            get
            {
                try
                {
                    Locker.AcquireReaderLock(Timeout.Infinite);
                    return Chrono.IsRunning;
                }
                finally
                {
                    Locker.ReleaseReaderLock();
                }
            }
        }

        /// <summary>
        /// Gets or sets the speed ratio at which the clock runs.
        /// </summary>
        public double SpeedRatio
        {
            get
            {
                try
                {
                    Locker.AcquireReaderLock(Timeout.Infinite);
                    return m_SpeedRatio;
                }
                finally
                {
                    Locker.ReleaseReaderLock();
                }
            }
            set
            {
                try
                {
                    Locker.AcquireWriterLock(Timeout.Infinite);
                    if (value < 0d) value = 0d;

                    // Capture the initial position se we set it even after the speedratio has changed
                    // this ensures a smooth position transition
                    var initialPosition = Position;
                    m_SpeedRatio = value;
                    Position = initialPosition;
                }
                finally
                {
                    Locker.ReleaseWriterLock();
                }
            }
        }

        /// <summary>
        /// Starts or resumes the clock.
        /// </summary>
        public void Play()
        {
            try
            {
                Locker.AcquireWriterLock(Timeout.Infinite);
                if (Chrono.IsRunning) return;
                Chrono.Start();
            }
            finally
            {
                Locker.ReleaseWriterLock();
            }
        }

        /// <summary>
        /// Pauses the clock.
        /// </summary>
        public void Pause()
        {
            try
            {
                Locker.AcquireWriterLock(Timeout.Infinite);
                Chrono.Stop();
            }
            finally
            {
                Locker.ReleaseWriterLock();
            }
        }

        /// <summary>
        /// Sets the clock position to 0 and stops it.
        /// The speed ratio is not modified.
        /// </summary>
        public void Reset()
        {
            try
            {
                Locker.AcquireWriterLock(Timeout.Infinite);
                OffsetMilliseconds = 0;
                Chrono.Reset();
            }
            finally
            {
                Locker.ReleaseWriterLock();
            }
        }
    }
}
