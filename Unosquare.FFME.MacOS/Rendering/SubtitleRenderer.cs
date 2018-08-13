namespace Unosquare.FFME.MacOS.Rendering
{
    using System;
    using Unosquare.FFME.Shared;

    /// <summary>
    /// Subtitle Renderer - Does nothing at this point.
    /// </summary>
    class SubtitleRenderer : IMediaRenderer
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:Unosquare.FFME.MacOS.Rendering.SubtitleRenderer"/> class.
        /// </summary>
        /// <param name="mediaEngine">Media element core.</param>
        public SubtitleRenderer(MediaEngine mediaEngine)
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
