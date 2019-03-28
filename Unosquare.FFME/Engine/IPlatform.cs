namespace Unosquare.FFME.Engine
{
    using Diagnostics;

    /// <summary>
    /// Contains factory methods and properties containing platform-specific implementations
    /// of the functionality that is required by an instance of the Media Engine.
    /// </summary>
    internal interface IPlatform
    {
        /// <summary>
        /// Gets a value indicating whether this instance is in debug mode.
        /// </summary>
        bool IsInDebugMode { get; }

        /// <summary>
        /// Gets a value indicating whether this instance is in design time.
        /// </summary>
        bool IsInDesignTime { get; }

        /// <summary>
        /// Creates a renderer of the specified media type.
        /// </summary>
        /// <param name="mediaType">Type of the media.</param>
        /// <param name="mediaCore">The media engine.</param>
        /// <returns>The renderer.</returns>
        IMediaRenderer CreateRenderer(MediaType mediaType, MediaEngine mediaCore);

        /// <summary>
        /// Handles global FFmpeg library messages.
        /// </summary>
        /// <param name="message">The message.</param>
        void HandleFFmpegLogMessage(MediaLogMessage message);
    }
}
