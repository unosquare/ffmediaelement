namespace Unosquare.FFME
{
    using System;

    /// <summary>
    /// Cross platform abstraction of UI thread aware timer.
    /// </summary>
    public interface IDispatcherTimer
    {
        TimeSpan Interval { get; set; }
        bool IsEnabled { get; set; }

        event EventHandler Tick;

        void Start();
        void Stop();
    }
}
