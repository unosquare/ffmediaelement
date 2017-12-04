namespace Unosquare.FFME.macOS.Rendering
{
    using System;
    using Unosquare.FFME.Decoding;
    using Unosquare.FFME.Rendering;

    /// <summary>
    /// Provides Audio Output capabilities.
    /// </summary>
    class AudioRenderer : IRenderer
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:Unosquare.FFME.macOS.Rendering.AudioRenderer"/> class.
        /// </summary>
        /// <param name="mediaElementCore">Media element core.</param>
        public AudioRenderer(MediaElementCore mediaElementCore)
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
