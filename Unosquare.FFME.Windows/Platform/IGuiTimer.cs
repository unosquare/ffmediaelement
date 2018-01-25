namespace Unosquare.FFME.Platform
{
    using System;

    internal interface IGuiTimer : IDisposable
    {
        Action OnTick { get; }

        TimeSpan Interval { get; }

        void Start();

        void Stop();
    }
}
