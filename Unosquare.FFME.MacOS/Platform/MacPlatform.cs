namespace Unosquare.FFME.MacOS.Platform
{
    using Foundation;
    using Shared;
    using System;
    using System.Diagnostics;
    using Unosquare.FFME.MacOS.Rendering;

    internal class MacPlatform : IPlatform
    {
        /// <summary>
        /// Initializes static members of the <see cref="MacPlatform"/> class.
        /// </summary>
        static MacPlatform()
        {
            Instance = new MacPlatform();
        }

        /// <summary>
        /// Prevents a default instance of the <see cref="MacPlatform"/> class from being created.
        /// </summary>
        /// <exception cref="InvalidOperationException">Unable to get a valid GUI context.</exception>
        private MacPlatform()
        {
            NativeMethods = MacNativeMethods.Instance;
            IsInDesignTime = GuiContext.Current.IsInDesignTime;
        }

        /// <summary>
        /// Gets the instance.
        /// </summary>
        /// <value>
        /// The instance.
        /// </value>
        public static MacPlatform Instance { get; }

        public INativeMethods NativeMethods { get; }

        public bool IsInDebugMode { get; } = Debugger.IsAttached;

        public bool IsInDesignTime { get; }

        public IMediaRenderer CreateRenderer(MediaType mediaType, MediaEngine mediaEngine)
        {
            if (mediaType == MediaType.Audio) return new AudioRenderer(mediaEngine);
            else if (mediaType == MediaType.Video) return new VideoRenderer(mediaEngine);
            else if (mediaType == MediaType.Subtitle) return new SubtitleRenderer(mediaEngine);

            throw new ArgumentException($"No suitable renderer for Media Type '{mediaType}'");
        }

        public void HandleFFmpegLogMessage(MediaLogMessage message)
        {
            if (message.MessageType == MediaLogMessageType.Trace) return;
            Debug.WriteLine($"{message.MessageType,10} - {message.Message}");
        }

        public void GuiInvoke(Action action)
        {
            NSRunLoop.Main.BeginInvokeOnMainThread(action.Invoke);
        }
    }
}
