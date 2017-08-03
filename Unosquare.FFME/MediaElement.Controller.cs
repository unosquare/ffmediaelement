namespace Unosquare.FFME
{
    using Core;
    using Decoding;
    using Unosquare.FFME.Commands;

    public partial class MediaElement
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
        public void Play()
        {
            Commands.Play();
        }

        /// <summary>
        /// Pauses playback of the currently loaded media.
        /// </summary>
        public void Pause()
        {
            Commands.Pause();
        }

        /// <summary>
        /// Pauses and rewinds the currently loaded media.
        /// </summary>
        public void Stop()
        {
            Commands.Stop();
        }

        /// <summary>
        /// Closes the currently loaded media.
        /// </summary>
        public void Close()
        {
            Commands.Close();
        }

        #endregion

    }
}
