namespace Unosquare.FFME.MacOS.Core
{
    using Foundation;
    using Shared;
    using System;
    using Unosquare.FFME.MacOS.Rendering;

    internal class MacPlatform : IPlatformConnector
    {
        private static readonly object SyncLock = new object();
        private static IPlatformConnector m_Instance = null;

        /// <summary>
        /// Prevents a default instance of the <see cref="MacPlatform"/> class from being created.
        /// </summary>
        private MacPlatform()
        {
            // placeholder
        }

        /// <summary>
        /// Gets the default Windows-specific implementation
        /// </summary>
        public static IPlatformConnector Default
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

        public Func<string, bool> SetDllDirectory => NativeMethods.SetDllDirectory;

        public Action<IntPtr, IntPtr, uint> CopyMemory => NativeMethods.CopyMemory;

        public Action<IntPtr, uint, byte> FillMemory => NativeMethods.FillMemory;

        public Action<ActionPriority, Action> UIInvoke => (priority, action) =>
        {
            NSRunLoop.Main.BeginInvokeOnMainThread(action.Invoke);
        };

        public Action<ActionPriority, Delegate, object[]> UIEnqueueInvoke => (priority, action, args) =>
        {
            NSRunLoop.Main.BeginInvokeOnMainThread(() =>
            {
                action.DynamicInvoke(args);
            });
        };

        public Func<MediaType, MediaEngine, IMediaRenderer> CreateRenderer => (mediaType, m) =>
        {
            if (mediaType == MediaType.Audio) return new AudioRenderer(m);
            else if (mediaType == MediaType.Video) return new VideoRenderer(m);
            else if (mediaType == MediaType.Subtitle) return new SubtitleRenderer(m);

            throw new ArgumentException($"No suitable renderer for Media Type '{mediaType}'");
        };

        public Func<ActionPriority, IDispatcherTimer> CreateTimer => (p) => { return new CustomDispatcherTimer(); };

        public void OnFFmpegMessageLogged(object sender, MediaLogMessage e)
        {
            if (e.MessageType == MediaLogMessageType.Trace) return;
            Console.WriteLine($"{e.MessageType,10} - {e.Message}");
        }
    }
}
