namespace Unosquare.FFME.Engine
{
    using Decoding;
    using System;

    /// <summary>
    /// Provides a unified API for media rendering classes
    /// </summary>
    internal interface IMediaRenderer
    {
        /// <summary>
        /// Gets the parent media engine.
        /// </summary>
        MediaEngine MediaCore { get; }

        /// <summary>
        /// Waits for the renderer to be ready to render.
        /// This is called only once before all Render calls are made
        /// </summary>
        void OnStarting();

        /// <summary>
        /// Executed when the Play method is called on the parent Media Engine
        /// </summary>
        void OnPlay();

        /// <summary>
        /// Executed when the Pause method is called on the parent Media Engine
        /// </summary>
        void OnPause();

        /// <summary>
        /// Executed when the Stop method is called on the parent Media Engine
        /// </summary>
        void OnStop();

        /// <summary>
        /// Executed when the Close method is called on the parent Media Engine.
        /// Release all resources when this call is received.
        /// </summary>
        void OnClose();

        /// <summary>
        /// Executed after a Seek operation is performed on the parent Media Engine
        /// </summary>
        void OnSeek();

        /// <summary>
        /// Called when a media block is due rendering.
        /// This needs to return immediately so the calling thread is not disturbed.
        /// </summary>
        /// <param name="mediaBlock">The media block.</param>
        /// <param name="clockPosition">The clock position.</param>
        void Render(MediaBlock mediaBlock, TimeSpan clockPosition);

        /// <summary>
        /// Called on every block rendering clock cycle just in case some update operation needs to be performed.
        /// This needs to return immediately so the calling thread is not disturbed.
        /// </summary>
        /// <param name="clockPosition">The clock position.</param>
        void Update(TimeSpan clockPosition);
    }
}
