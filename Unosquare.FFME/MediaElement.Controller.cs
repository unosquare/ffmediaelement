namespace Unosquare.FFME
{
    using Core;
    using Decoding;
    using System;
    using Unosquare.FFME.Commands;

    partial class MediaElement
    {

        #region Internal Members

        /// <summary>
        /// The underlying media container that provides access to 
        /// individual media component streams
        /// </summary>
        internal MediaContainer Container = null;

        /// <summary>
        /// Represents a real-time time measuring device.
        /// Rendering media should occur as requested by the clock.
        /// </summary>
        internal readonly Clock Clock = new Clock();

        /// <summary>
        /// The command queue to be executed in the order they were sent.
        /// </summary>
        internal readonly MediaCommandManager Commands = null;

        /// <summary>
        /// When position is being set from within this control, this field will
        /// be set to true. This is useful to detect if the user is setting the position
        /// or if the Position property is being driven from within
        /// </summary>
        internal volatile bool IsPositionUpdating = false;

        #endregion

        #region Public API

        public async void Play()
        {
            await Commands.Play();
        }

        public async void Pause()
        {
            await Commands.Pause();
        }

        public async void Stop()
        {
            await Commands.Stop();
        }

        public async void Close()
        {
            await Commands.Close();
        }

        #endregion

    }
}
