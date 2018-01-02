namespace Unosquare.FFME.Platform
{
    using Shared;
    using System;

    /// <summary>
    /// The Windows Platform implementation.
    /// This class is a singleton.
    /// </summary>
    /// <seealso cref="Unosquare.FFME.Shared.IPlatformConnector" />
    internal class WindowsPlatformConnector : IPlatformConnector
    {
        private static readonly object SyncLock = new object();
        private static IPlatformConnector m_Instance = null;

        /// <summary>
        /// Prevents a default instance of the <see cref="WindowsPlatformConnector"/> class from being created.
        /// </summary>
        private WindowsPlatformConnector()
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
                        m_Instance = new WindowsPlatformConnector();

                    return m_Instance;
                }
            }
        }

        /// <summary>
        /// Sets the DLL directory in which external dependencies can be located.
        /// </summary>
        public Func<string, bool> SetDllDirectory => WindowsNativeMethods.SetDllDirectory;

        /// <summary>
        /// Fast pointer memory block copy function
        /// </summary>
        public Action<IntPtr, IntPtr, uint> CopyMemory => WindowsNativeMethods.CopyMemory;

        /// <summary>
        /// Fills the memory.
        /// </summary>
        public Action<IntPtr, uint, byte> FillMemory => WindowsNativeMethods.FillMemory;

        public Action<ActionPriority, Action> UIInvoke => WindowsGui.UIInvoke;

        /// <summary>
        /// Enqueues the given instructions with the given arguments on the main application dispatcher.
        /// This is a way to execute code in a fire-and-forget style
        /// </summary>
        public Action<ActionPriority, Delegate, object[]> UIEnqueueInvoke => WindowsGui.UIEnqueueInvoke;

        /// <summary>
        /// Creates a new instance of the renderer of the given type.
        /// </summary>
        public Func<MediaType, MediaEngine, IMediaRenderer> CreateRenderer => WindowsGui.CreateRenderer;

        /// <summary>
        /// Creates a new UI aware timer with the specified priority.
        /// </summary>
        public Func<ActionPriority, IDispatcherTimer> CreateTimer => WindowsGui.CreateDispatcherTimer;

        /// <summary>
        /// Called when an FFmpeg message is logged.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="T:Unosquare.FFME.MediaLogMessagEventArgs" /> instance containing the event data.</param>
        public void OnFFmpegMessageLogged(object sender, MediaLogMessage e)
        {
            MediaElement.RaiseFFmpegMessageLogged(sender, e);
        }
    }
}
