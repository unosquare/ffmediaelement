namespace Unosquare.FFME.Rendering
{
    using Engine;
    using Platform;
    using System;
    using System.Windows.Threading;

    /// <summary>
    /// Subtitle Renderer - Does nothing at this point.
    /// </summary>
    /// <seealso cref="IMediaRenderer" />
    internal class SubtitleRenderer : IMediaRenderer, ILoggingSource
    {
        /// <summary>
        /// The synchronize lock
        /// </summary>
        private readonly object SyncLock = new object();
        private TimeSpan? StartTime;
        private TimeSpan? EndTime;

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
        /// <param name="mediaCore">The core media element.</param>
        public SubtitleRenderer(MediaEngine mediaCore)
        {
            MediaCore = mediaCore;
        }

        /// <inheritdoc />
        ILoggingHandler ILoggingSource.LoggingHandler => MediaCore;

        /// <summary>
        /// Gets the parent media element (platform specific).
        /// </summary>
        public MediaElement MediaElement => MediaCore?.Parent as MediaElement;

        /// <inheritdoc />
        public MediaEngine MediaCore { get; }

        /// <inheritdoc />
        public void OnClose()
        {
            SetText(string.Empty);
        }

        /// <inheritdoc />
        public void OnPause()
        {
            // Placeholder
        }

        /// <inheritdoc />
        public void OnPlay()
        {
            // placeholder
        }

        /// <inheritdoc />
        public void OnStop() => OnStarting();

        /// <inheritdoc />
        public void OnSeek()
        {
            // placeholder
        }

        /// <inheritdoc />
        public void OnStarting()
        {
            lock (SyncLock)
            {
                // This initializes the text blocks
                // for subtitle rendering automatically.
                BlockText = string.Empty;
                SetText(string.Empty);
                StartTime = default;
                EndTime = default;
            }
        }

        /// <inheritdoc />
        public void Render(MediaBlock mediaBlock, TimeSpan clockPosition)
        {
            lock (SyncLock)
            {
                if (mediaBlock is SubtitleBlock == false) return;

                // Get a reference to the subtitle block
                var subtitleBlock = (SubtitleBlock)mediaBlock;

                // Raise the subtitles event and keep track of the text.
                var cancelRender = MediaElement.RaiseRenderingSubtitlesEvent(subtitleBlock, clockPosition);

                if (cancelRender)
                {
                    BlockText = string.Empty;
                    StartTime = null;
                    EndTime = null;
                }
                else
                {
                    // Save the block text lines to display
                    BlockText = string.Join("\r\n", subtitleBlock.Text);

                    // Save the start and end times. We will need
                    // them in order to make the subtitles disappear
                    StartTime = subtitleBlock.StartTime;
                    EndTime = subtitleBlock.EndTime;
                }

                // Call the selective update method
                Update(clockPosition);
            }
        }

        /// <inheritdoc />
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
        /// Returns immediately because it queues the action on the UI thread.
        /// </summary>
        /// <param name="text">The text.</param>
        private void SetText(string text)
        {
            lock (SyncLock)
            {
                if (RenderedText == text)
                    return;
            }

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
