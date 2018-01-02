namespace Unosquare.FFME.Core
{
    using Rendering;
    using System;

    /// <summary>
    /// The Windows Platform implementation.
    /// This class is a singleton.
    /// </summary>
    /// <seealso cref="Unosquare.FFME.IPlatform" />
    internal class WindowsPlatform : IPlatform
    {
        private static readonly object SyncLock = new object();
        private static IPlatform m_Instance = null;

        /// <summary>
        /// Prevents a default instance of the <see cref="WindowsPlatform"/> class from being created.
        /// </summary>
        private WindowsPlatform()
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
                        m_Instance = new WindowsPlatform();

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

        public Action<CoreDispatcherPriority, Action> UIInvoke => WindowsGui.UIInvoke;

        /// <summary>
        /// Enqueues the given instructions with the given arguments on the main application dispatcher.
        /// This is a way to execute code in a fire-and-forget style
        /// </summary>
        public Action<CoreDispatcherPriority, Delegate, object[]> UIEnqueueInvoke => WindowsGui.UIEnqueueInvoke;

        /// <summary>
        /// Creates a new instance of the renderer of the given type.
        /// </summary>
        public Func<MediaType, MediaElementCore, IRenderer> CreateRenderer => WindowsGui.CreateRenderer;

        /// <summary>
        /// Creates a new UI aware timer with the specified priority.
        /// </summary>
        public Func<CoreDispatcherPriority, IDispatcherTimer> CreateTimer => WindowsGui.CreateDispatcherTimer;

        /// <summary>
        /// Called when an FFmpeg message is logged.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="T:Unosquare.FFME.MediaLogMessagEventArgs" /> instance containing the event data.</param>
        public void OnFFmpegMessageLogged(object sender, MediaLogMessagEventArgs e)
        {
            MediaElement.RaiseFFmpegMessageLogged(sender, e);
        }
    }
}
