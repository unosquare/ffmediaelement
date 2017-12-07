namespace Unosquare.FFME.MacOS.Rendering
{
    using System;
    using Unosquare.FFME.Decoding;
    using Unosquare.FFME.Rendering;

    /// <summary>
    /// Subtitle Renderer - Does nothing at this point.
    /// </summary>
    class SubtitleRenderer : IRenderer
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:Unosquare.FFME.MacOS.Rendering.SubtitleRenderer"/> class.
        /// </summary>
        /// <param name="mediaElementCore">Media element core.</param>
        public SubtitleRenderer(MediaElementCore mediaElementCore)
        {
            MediaElementCore = mediaElementCore;
        }

        /// <summary>
        /// Gets the media element core player component.
        /// </summary>
        /// <value>The media element core.</value>
        public MediaElementCore MediaElementCore { get; }

        public void Close()
        {
        }

        public void Pause()
        {
        }

        public void Play()
        {
        }

        public void Render(MediaBlock mediaBlock, TimeSpan clockPosition)
        {
        }

        public void Seek()
        {
        }

        public void Stop()
        {
        }

        public void Update(TimeSpan clockPosition)
        {
        }

        public void WaitForReadyState()
        {
        }
    }
}
