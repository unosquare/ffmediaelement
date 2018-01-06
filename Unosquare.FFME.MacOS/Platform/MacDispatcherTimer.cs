﻿namespace Unosquare.FFME.MacOS.Platform
{
    using Shared;
    using System;
    using System.Timers;
    using Core;

    class MacDispatcherTimer : IDispatcherTimer
    {
        Timer timer;

        public bool IsEnabled { get; set; }

        public TimeSpan Interval
        {
            get => TimeSpan.FromMilliseconds(timer.Interval);
            set => timer.Interval = value.TotalMilliseconds;
        }

        public event EventHandler Tick;

        public MacDispatcherTimer()
        {
            timer = new Timer(Constants.UIPropertyUpdateInterval.TotalMilliseconds);
            timer.Elapsed += (sender, e) => Tick?.Invoke(this, EventArgs.Empty);
        }

        public void Start() => timer.Start();

        public void Stop() => timer.Stop();
    }
}
