namespace Unosquare.FFME.Platform
{
    using System;

    /// <summary>
    /// An interface that provides methods to a Timer that executes its code on
    /// the same thread as the <see cref="MediaElement"/> control.
    /// </summary>
    internal interface IGuiTimer : IDisposable
    {
        /// <summary>
        /// Gets the interval at which the timer ticks.
        /// </summary>
        TimeSpan Interval { get; }

        /// <summary>
        /// Gets a value indicating whether the timer is executing a cycle.
        /// </summary>
        bool IsExecutingCycle { get; }
    }
}
