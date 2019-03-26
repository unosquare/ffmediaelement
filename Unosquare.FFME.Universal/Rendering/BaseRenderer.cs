namespace Unosquare.FFME.Rendering
{
    using Engine;
    using System;

    internal abstract class BaseRenderer : IMediaRenderer, ILoggingSource
    {
        protected BaseRenderer(MediaEngine mediaCore)
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
        public virtual void OnStarting()
        {
            // placeholder
        }

        /// <inheritdoc />
        public virtual void OnPlay()
        {
            // placeholder
        }

        /// <inheritdoc />
        public virtual void OnPause()
        {
            // placeholder
        }

        /// <inheritdoc />
        public virtual void OnStop()
        {
            // placeholder
        }

        /// <inheritdoc />
        public virtual void OnClose()
        {
            // placeholder
        }

        /// <inheritdoc />
        public virtual void OnSeek()
        {
            // placeholder
        }

        /// <inheritdoc />
        public virtual void Render(MediaBlock mediaBlock, TimeSpan clockPosition)
        {
            // placeholder
        }

        /// <inheritdoc />
        public virtual void Update(TimeSpan clockPosition)
        {
            throw new NotImplementedException();
        }
    }
}
