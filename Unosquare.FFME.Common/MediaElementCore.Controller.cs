namespace Unosquare.FFME
{
    using Core;
    using Commands;
    using Decoding;
    using System;

    public partial class MediaElementCore
    {
        #region Internal Members
#pragma warning disable SA1401 // Fields must be private

        /// <summary>
        /// The command queue to be executed in the order they were sent.
        /// </summary>
        internal readonly MediaCommandManager Commands = null;

        /// <summary>
        /// Represents a real-time time measuring device.
        /// Rendering media should occur as requested by the clock.
        /// </summary>
        internal readonly Clock Clock = new Clock();

        /// <summary>
        /// The underlying media container that provides access to 
        /// individual media component streams
        /// </summary>
        internal MediaContainer Container = null;

#pragma warning restore SA1401 // Fields must be private
        #endregion

        #region Public API

        /// <summary>
        /// Begins or resumes playback of the currently loaded media.
        /// </summary>
        public void Play() => Commands.Play();

        /// <summary>
        /// Pauses playback of the currently loaded media.
        /// </summary>
        public void Pause() => Commands.Pause();

        /// <summary>
        /// Pauses and rewinds the currently loaded media.
        /// </summary>
        public void Stop() => Commands.Stop();

        /// <summary>
        /// Closes the currently loaded media.
        /// </summary>
        public void Close() => Commands.Close();

        /// <summary>
        /// Seeks to the specified position.
        /// </summary>
        /// <param name="position">New position for the player.</param>
        public void Seek(TimeSpan position) => Commands.Seek(position);

        /// <summary>
        /// Sets the specified target speed ration.
        /// </summary>
        /// <param name="targetSpeedRatio">New target speed ratio.</param>
        public void SetSpeedRatio(double targetSpeedRatio) => Commands.SetSpeedRatio(targetSpeedRatio);

        #endregion
    }
}
