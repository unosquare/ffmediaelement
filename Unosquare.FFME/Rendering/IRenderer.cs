namespace Unosquare.FFME.Rendering
{
    using Decoding;
    using System;

    /// <summary>
    /// Provides a unified API for media rendering classes
    /// </summary>
    internal interface IRenderer
    {
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
        /// Renders the specified media block.
        /// </summary>
        /// <param name="mediaBlock">The media block.</param>
        /// <param name="clockPosition">The clock position.</param>
        /// <param name="renderIndex">Index of the render.</param>
        void Render(MediaBlock mediaBlock, TimeSpan clockPosition, int renderIndex);

        /// <summary>
        /// Gets the parent media element.
        /// </summary>
        MediaElement MediaElement { get; }
    }
}
