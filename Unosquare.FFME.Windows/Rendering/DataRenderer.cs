namespace Unosquare.FFME.Rendering
{
    using Container;
    using Engine;
    using Platform;
    using System;

    /// <summary>
    /// DataRenderer Renderer - Does nothing at this point.
    /// </summary>
    /// <seealso cref="IMediaRenderer" />
    internal class DataRenderer : IMediaRenderer
    {
        /// <summary>
        /// The synchronize lock.
        /// </summary>
        private readonly object SyncLock = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="DataRenderer"/> class.
        /// </summary>
        /// <param name="mediaEngine">The core media element.</param>
        public DataRenderer(MediaEngine mediaEngine)
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
        /// Store the last rendered block.
        /// </summary>
        public DataBlock LastRenderedBlock { get; private set; }

        public void OnStarting()
        {
            // Placeholder
        }

        /// <summary>
        /// Executed when the Close method is called on the parent MediaElement.
        /// </summary>
        public void OnClose()
        {
            // Placeholder
        }

        /// <summary>
        /// Executed when the Pause method is called on the parent MediaElement.
        /// </summary>
        public void OnPause()
        {
            // Placeholder
        }

        /// <summary>
        /// Executed when the Play method is called on the parent MediaElement.
        /// </summary>
        public void OnPlay()
        {
            // placeholder
        }

        /// <summary>
        /// Executed when the Pause method is called on the parent MediaElement.
        /// </summary>
        public void OnStop()
        {
            // Placeholder
        }

        /// <summary>
        /// Executed after a Seek operation is performed on the parent MediaElement.
        /// </summary>
        public void OnSeek()
        {
            // placeholder
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
                var dataBlock = mediaBlock as DataBlock;
                if (dataBlock == null) return;

                if (dataBlock != LastRenderedBlock)
                {
                    LastRenderedBlock = dataBlock;
                    MediaElement.RaiseRenderingDataEvent(dataBlock, clockPosition);
                }
            }
        }

        /// <summary>
        /// Called when a media block must stop being rendered.
        /// This needs to return immediately so the calling thread is not disturbed.
        /// </summary>
        /// <param name="clockPosition">The clock position.</param>
        public void Update(TimeSpan clockPosition)
        {
            // Placeholder
        }
    }
}
