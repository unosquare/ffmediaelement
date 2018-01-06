namespace Unosquare.FFME.MacOS.Rendering
{
    using System;
    using Unosquare.FFME.Shared;

    /// <summary>
    /// Provides Audio Output capabilities.
    /// </summary>
    class AudioRenderer : IMediaRenderer
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:Unosquare.FFME.MacOS.Rendering.AudioRenderer"/> class.
        /// </summary>
        /// <param name="mediaEngine">Media element core.</param>
        public AudioRenderer(MediaEngine mediaEngine)
        {
            MediaCore = mediaEngine;
        }

        /// <summary>
        /// Gets the media element core player component.
        /// </summary>
        /// <value>The media element core.</value>
        public MediaEngine MediaCore { get; }

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
