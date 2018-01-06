namespace Unosquare.FFME.Shared
{
    using Decoding;
    using System;

    /// <summary>
    /// Provides a unified API for media rendering classes
    /// </summary>
    public interface IMediaRenderer
    {
        /// <summary>
        /// Gets the parent media engine.
        /// </summary>
        MediaEngine MediaCore { get; }

        /// <summary>
        /// Waits for the renderer to be ready to render.
        /// </summary>
        void WaitForReadyState();

        /// <summary>
        /// Executed when the Play method is called on the parent MediaElement
        /// </summary>
        void Play();

        /// <summary>
        /// Executed when the Pause method is called on the parent MediaElement
        /// </summary>
        void Pause();

        /// <summary>
        /// Executed when the Pause method is called on the parent MediaElement
        /// </summary>
        void Stop();

        /// <summary>
        /// Executed when the Close method is called on the parent MediaElement
        /// </summary>
        void Close();

        /// <summary>
        /// Executed after a Seek operation is performed on the parent MediaElement
        /// </summary>
        void Seek();

        /// <summary>
        /// Called when a media block is due rendering.
        /// This needs to return immediately so the calling thread is not disturbed.
        /// </summary>
        /// <param name="mediaBlock">The media block.</param>
        /// <param name="clockPosition">The clock position.</param>
        void Render(MediaBlock mediaBlock, TimeSpan clockPosition);

        /// <summary>
        /// Called on every block rendering clock cycle just in case some update operation needs to be performed.
        /// This needs to return immediately so the calling thread is not disturbed.
        /// </summary>
        /// <param name="clockPosition">The clock position.</param>
        void Update(TimeSpan clockPosition);
    }
}
