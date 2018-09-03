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
            // placeholder
        }

        public void Pause()
        {
            // placeholder
        }

        public void Play()
        {
            // placeholder
        }

        public void Render(MediaBlock mediaBlock, TimeSpan clockPosition)
        {
            // placeholder
        }

        public void Seek()
        {
            // placeholder
        }

        public void Stop()
        {
            // placeholder
        }

        public void Update(TimeSpan clockPosition)
        {
            // placeholder
        }

        public void WaitForReadyState()
        {
            // placeholder
        }
    }
}
