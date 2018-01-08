namespace Unosquare.FFME.MacOS.Platform
{
    using Foundation;
    using Shared;
    using System;
    using System.Diagnostics;
    using Unosquare.FFME.MacOS.Rendering;

    internal class MacPlatform : IPlatform
    {
        private static readonly object SyncLock = new object();
        private static MacPlatform m_Instance = null;

        /// <summary>
        /// Prevents a default instance of the <see cref="MacPlatform"/> class from being created.
        /// </summary>
        private MacPlatform()
        {
            NativeMethods = new MacNativeMethods();
            IsInDebugMode = Debugger.IsAttached;
            IsInDesignTime = false;
        }

        /// <summary>
        /// Gets the default Windows-specific implementation
        /// </summary>
        public static MacPlatform Current
        {
            get
            {
                lock (SyncLock)
                {
                    if (m_Instance == null)
                        m_Instance = new MacPlatform();

                    return m_Instance;
                }
            }
        }

        public INativeMethods NativeMethods { get; }

        public bool IsInDebugMode { get; }

        public bool IsInDesignTime { get; }

        public IDispatcherTimer CreateGuiTimer(ActionPriority priority)
        {
            return new MacDispatcherTimer();
        }

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
            Console.WriteLine($"{message.MessageType,10} - {message.Message}");
        }

        public void GuiEnqueueInvoke(ActionPriority priority, Delegate callback, params object[] arguments)
        {
            NSRunLoop.Main.BeginInvokeOnMainThread(() =>
            {
                callback.DynamicInvoke(arguments);
            });
        }

        public void GuiInvoke(ActionPriority priority, Action action)
        {
            NSRunLoop.Main.BeginInvokeOnMainThread(action.Invoke);
        }
    }
}
