namespace Unosquare.FFME
{
    using Shared;
    using System;

    public partial class MediaEngine
    {
        /// <summary>
        /// Gets the internal real time clock position.
        /// This is different from the regular property as this is the immediate value
        /// (i.e. might not yet be applied)
        /// </summary>
        public TimeSpan RealTimeClockPosition => Clock.Position;

        /// <summary>
        /// Resets the controller properies.
        /// </summary>
        internal void ResetControllerProperties()
        {
            Controller.Volume = Constants.Controller.DefaultVolume;
            Controller.Balance = Constants.Controller.DefaultBalance;
            Controller.SpeedRatio = Constants.Controller.DefaultSpeedRatio;
            Controller.IsMuted = false;
            Controller.Position = TimeSpan.Zero;
            Media.VideoSmtpeTimecode = string.Empty;
            Media.VideoHardwareDecoder = string.Empty;
            Media.HasMediaEnded = false;
        }
    }
}
