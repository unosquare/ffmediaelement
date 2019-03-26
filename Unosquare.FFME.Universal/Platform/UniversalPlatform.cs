namespace Unosquare.FFME.Platform
{
    using Engine;
    using System;
    using Rendering;
    using System.Diagnostics;

    /// <summary>
    /// Represents properties and methods that are specific to UWP
    /// </summary>
    internal sealed class UniversalPlatform : IPlatform
    {
        /// <summary>
        /// Initializes static members of the <see cref="UniversalPlatform"/> class.
        /// </summary>
        static UniversalPlatform()
        {
            Instance = new UniversalPlatform();
        }

        /// <summary>
        /// Prevents a default instance of the <see cref="UniversalPlatform"/> class from being created.
        /// </summary>
        /// <exception cref="InvalidOperationException">Unable to get a valid GUI context.</exception>
        private UniversalPlatform()
        {
            // placeholder
        }

        /// <summary>
        /// Gets the instance.
        /// </summary>
        public static UniversalPlatform Instance { get; }

        /// <inheritdoc />
        public bool IsInDebugMode { get; } = Debugger.IsAttached;

        /// <inheritdoc />
        public bool IsInDesignTime { get; } = GuiContext.Current.IsInDesignTime;

        /// <inheritdoc />
        public IMediaRenderer CreateRenderer(MediaType mediaType, MediaEngine mediaCore)
        {
            switch (mediaType)
            {
                case MediaType.Audio:
                    return new AudioRenderer(mediaCore);
                case MediaType.Video:
                    return new VideoRenderer(mediaCore);
                case MediaType.Subtitle:
                    return new SubtitleRenderer(mediaCore);
                default:
                    throw new NotSupportedException($"No suitable renderer for Media Type '{mediaType}'");
            }
        }

        /// <inheritdoc />
        public void HandleFFmpegLogMessage(MediaLogMessage message) =>
            MediaElement.RaiseFFmpegMessageLogged(typeof(MediaElement), message);
    }
}
