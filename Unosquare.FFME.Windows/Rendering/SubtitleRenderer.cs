namespace Unosquare.FFME.Rendering
{
    using Platform;
    using Shared;
    using System;
    using System.Windows.Threading;

    /// <summary>
    /// Subtitle Renderer - Does nothing at this point.
    /// </summary>
    /// <seealso cref="IMediaRenderer" />
    internal class SubtitleRenderer : IMediaRenderer
    {
        /// <summary>
        /// The synchronize lock
        /// </summary>
        private readonly object SyncLock = new object();
        private TimeSpan? StartTime = default;
        private TimeSpan? EndTime = default;

        /// <summary>
        /// Holds the text to be rendered when the Update method is called.
        /// </summary>
        private string BlockText = string.Empty;

        /// <summary>
        /// Holds the text that was last rendered when Update was called.
        /// </summary>
        private string RenderedText = string.Empty;

        /// <summary>
        /// Initializes a new instance of the <see cref="SubtitleRenderer"/> class.
        /// </summary>
        /// <param name="mediaEngine">The core media element.</param>
        public SubtitleRenderer(MediaEngine mediaEngine)
        {
            MediaCore = mediaEngine;
        }

        /// <summary>
        /// Gets the parent media element (platform specific).
        /// </summary>
        public MediaElement MediaElement => MediaCore?.Parent as MediaElement;

        /// <summary>
        /// Gets the core platform independent player component.
        /// </summary>
        public MediaEngine MediaCore { get; }

        /// <summary>
        /// Executed when the Close method is called on the parent MediaElement
        /// </summary>
        public void Close()
        {
            SetText(string.Empty);
        }

        /// <summary>
        /// Executed when the Pause method is called on the parent MediaElement
        /// </summary>
        public void Pause()
        {
            // Placeholder
        }

        /// <summary>
        /// Executed when the Play method is called on the parent MediaElement
        /// </summary>
        public void Play()
        {
            // placeholder
        }

        /// <summary>
        /// Executed when the Pause method is called on the parent MediaElement
        /// </summary>
        public void Stop()
        {
            SetText(string.Empty);
        }

        /// <summary>
        /// Executed after a Seek operation is performed on the parent MediaElement
        /// </summary>
        public void Seek()
        {
            // placeholder
        }

        /// <summary>
        /// Waits for the renderer to be ready to render.
        /// </summary>
        public void WaitForReadyState()
        {
            // This initializes the text blocks
            // for subtitle rendering automatically.
            SetText(string.Empty);
        }

        /// <summary>
        /// Renders the specified media block.
        /// </summary>
        /// <param name="mediaBlock">The media block.</param>
        /// <param name="clockPosition">The clock position.</param>
        public void Render(MediaBlock mediaBlock, TimeSpan clockPosition)
        {
            lock (SyncLock)
            {
                var subtitleBlock = mediaBlock as SubtitleBlock;
                if (subtitleBlock == null) return;

                // Save the start and end times. We will need
                // them in order to make the subtitles disappear
                StartTime = subtitleBlock.StartTime;
                EndTime = subtitleBlock.EndTime;

                // Raise the subtitles event and keep track of the text.
                BlockText = MediaElement.RaiseRenderingSubtitlesEvent(subtitleBlock, clockPosition)
                    ? string.Empty
                    : string.Join("\r\n", subtitleBlock.Text);

                // Call the selective update method
                Update(clockPosition);
            }
        }

        /// <summary>
        /// Called when a media block must stop being rendered.
        /// This needs to return immediately so the calling thread is not disturbed.
        /// </summary>
        /// <param name="clockPosition">The clock position.</param>
        public void Update(TimeSpan clockPosition)
        {
            // Check if we have received a start and end time value.
            // if we have not, just clear the text
            if (StartTime.HasValue == false || EndTime.HasValue == false)
            {
                SetText(string.Empty);
                return;
            }

            // Check if the subtitle needs to be cleared based on the start and end times range
            if (clockPosition > EndTime.Value || clockPosition < StartTime.Value)
            {
                SetText(string.Empty);
                return;
            }

            // Update the text with the block text
            SetText(BlockText);
        }

        /// <summary>
        /// Sets the text to be rendered on the text blocks.
        /// Returns immediately because it enqueues the action on the UI thread.
        /// </summary>
        /// <param name="text">The text.</param>
        private void SetText(string text)
        {
            if (RenderedText.Equals(text))
                return;

            // We fire-and-forget the update of the text
            GuiContext.Current.EnqueueInvoke(DispatcherPriority.Render, () =>
            {
                lock (SyncLock)
                {
                    MediaElement.SubtitlesView.Text = text;
                    RenderedText = text;
                }
            });
        }
    }
}
