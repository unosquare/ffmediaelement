namespace Unosquare.FFME
{
    using System;

    /// <summary>
    /// Cross platform abstraction of UI thread aware timer.
    /// </summary>
    public interface IDispatcherTimer
    {
        /// <summary>
        /// Occurs when [tick].
        /// </summary>
        event EventHandler Tick;

        /// <summary>
        /// Gets or sets the interval.
        /// </summary>
        /// <value>
        /// The interval.
        /// </value>
        TimeSpan Interval { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is enabled.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is enabled; otherwise, <c>false</c>.
        /// </value>
        bool IsEnabled { get; set; }

        /// <summary>
        /// Starts this instance.
        /// </summary>
        void Start();

        /// <summary>
        /// Stops this instance.
        /// </summary>
        void Stop();
    }
}
