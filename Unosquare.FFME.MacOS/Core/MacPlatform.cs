namespace Unosquare.FFME.MacOS.Core
{
    using Foundation;
    using System;
    using Unosquare.FFME.Core;
    using Unosquare.FFME.MacOS.Rendering;
    using Unosquare.FFME.Rendering;

    internal class MacPlatform : IPlatform
    {
        private static readonly object SyncLock = new object();
        private static IPlatform m_Instance = null;

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
        public static IPlatform Default
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

        public Action<CoreDispatcherPriority, Action> UIInvoke => (priority, action) =>
        {
            NSRunLoop.Main.BeginInvokeOnMainThread(action.Invoke);
        };

        public Action<CoreDispatcherPriority, Delegate, object[]> UIEnqueueInvoke => (priority, action, args) =>
        {
            NSRunLoop.Main.BeginInvokeOnMainThread(() =>
            {
                action.DynamicInvoke(args);
            });
        };

        public Func<MediaType, MediaElementCore, IRenderer> CreateRenderer => (mediaType, m) =>
        {
            if (mediaType == MediaType.Audio) return new AudioRenderer(m);
            else if (mediaType == MediaType.Video) return new VideoRenderer(m);
            else if (mediaType == MediaType.Subtitle) return new SubtitleRenderer(m);

            throw new ArgumentException($"No suitable renderer for Media Type '{mediaType}'");
        };

        public Func<CoreDispatcherPriority, IDispatcherTimer> CreateTimer => (p) => { return new CustomDispatcherTimer(); };

        public void OnFFmpegMessageLogged(object sender, MediaLogMessagEventArgs e)
        {
            if (e.MessageType == MediaLogMessageType.Trace) return;
            Console.WriteLine($"{e.MessageType,10} - {e.Message}");
        }
    }
}
