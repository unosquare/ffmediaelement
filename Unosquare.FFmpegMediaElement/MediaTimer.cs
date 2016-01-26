namespace Unosquare.FFmpegMediaElement
{
    using System;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// A high precision timer designed to keep track of
    /// media playback. Control methods mimic media playback
    /// control methods such as Play Pause, Stop and Seek
    /// </summary>
    internal class MediaTimer : INotifyPropertyChanged
    {
        #region Event and Property Backing

        public event PropertyChangedEventHandler PropertyChanged;

        private readonly Stopwatch Stopwatch = new Stopwatch();
        private readonly object SyncLock = new object();
        private long OffsetTicks = 0;
        private bool m_IsPlaying = false;
        private decimal m_SpeedRatio = Constants.DefaultSpeedRatio;

        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaTimer"/> class.
        /// </summary>
        public MediaTimer()
        {
            this.m_SpeedRatio = Constants.DefaultSpeedRatio;
        }

        /// <summary>
        /// Gets a value indicating whether the timer is running. If the timer is stopped or paused,
        /// then this property will return false.
        /// </summary>
        public bool IsPlaying
        {
            get
            {
                lock (SyncLock)
                {
                    return this.m_IsPlaying;
                }
            }
            private set
            {
                lock (SyncLock)
                {
                    var notify = false;
                    if (value != m_IsPlaying)
                        notify = true;

                    this.m_IsPlaying = value;
                    if (notify && PropertyChanged != null)
                        PropertyChanged(this, new PropertyChangedEventArgs("IsPlaying"));
                }
            }
        }

        /// <summary>
        /// Stops the timer and makes the elapsed time effectively 0
        /// </summary>
        public void Stop()
        {
            lock (SyncLock)
            {
                OffsetTicks = 0;
                Stopwatch.Reset();
                IsPlaying = false;
            }
        }

        /// <summary>
        /// Starts or resumes the timer
        /// </summary>
        public void Play()
        {
            lock (SyncLock)
            {
                Stopwatch.Start();
                IsPlaying = true;
            }
        }

        /// <summary>
        /// Pauses the timer
        /// </summary>
        public void Pause()
        {
            lock (SyncLock)
            {
                Stopwatch.Stop();
                IsPlaying = false;
            }
        }

        /// <summary>
        /// Sets the Position to the specified value.
        /// If the timer is running, it will be paused after this method call.
        /// </summary>
        /// <param name="ts">The ts.</param>
        public void Seek(TimeSpan ts)
        {
            lock (SyncLock)
            {
                Stopwatch.Reset();
                OffsetTicks = Helper.RoundTicks(ts.Ticks < 0 ? 0 : ts.Ticks);
                IsPlaying = false;
            }
        }

        /// <summary>
        /// Sets the Position to the specified value.
        /// If the timer is running, it will be paused after this method call.
        /// </summary>
        /// <param name="seconds">The seconds.</param>
        public void Seek(decimal seconds)
        {
            this.Seek(TimeSpan.FromSeconds(Convert.ToDouble(Helper.RoundSeconds(seconds))));
        }

        /// <summary>
        /// Sets the Position to the specified value.
        /// If the timer is running, it will be paused after this method call.
        /// </summary>
        /// <param name="ticks">The ticks.</param>
        public void Seek(long ticks)
        {
            this.Seek(TimeSpan.FromTicks(ticks));
        }

        /// <summary>
        /// Gets or sets the speed ratio at which the timer runs.
        /// </summary>
        public decimal SpeedRatio
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
                    var isPlaying = this.IsPlaying;
                    var differenceTicks = ComputePositionTicks(m_SpeedRatio) - ComputePositionTicks(value);
                    m_SpeedRatio = value;
                    this.Seek(this.PositionTicks + differenceTicks);
                    if (isPlaying) this.Play();
                }
            }
        }

        /// <summary>
        /// Gets or Sets the position in ticks.
        /// </summary>
        public long PositionTicks
        {
            get
            {
                return Position.Ticks;
            }
            set
            {
                this.Seek(value);
            }
        }

        /// <summary>
        /// Gets or Sets the position in the elapsed seconds
        /// </summary>
        public decimal PositionSeconds
        {
            get
            {
                return Helper.RoundSeconds(Convert.ToDecimal(Position.TotalSeconds));
            }
            set
            {
                this.Seek(value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private long ComputePositionTicks(decimal speedRatio)
        {
            var offsetTicks = Convert.ToDecimal(Stopwatch.Elapsed.Ticks) * speedRatio + Convert.ToDecimal(OffsetTicks);
            return Helper.RoundTicks(Convert.ToInt64(offsetTicks));
        }

        /// <summary>
        /// Gets or Sets the TimeSpan representing the total elapsed milliseconds.
        /// </summary>
        public TimeSpan Position
        {
            get
            {
                lock (SyncLock)
                {
                    return TimeSpan.FromTicks(ComputePositionTicks(this.SpeedRatio));
                }
            }
            set
            {
                this.Seek(value);
            }
        }
    }
}
