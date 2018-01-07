namespace Unosquare.FFME.Shared
{
    using System;

    /// <summary>
    /// Contains factory methods and properties containing latfrom-specific implementations
    /// of the functionality that is required by an instance of the Media Engine
    /// </summary>
    public interface IPlatform
    {
        /// <summary>
        /// Retrieves the platform-specific Native methods
        /// </summary>
        INativeMethods NativeMethods { get; }

        /// <summary>
        /// Gets a value indicating whether this instance is in debug mode.
        /// </summary>
        bool IsInDebugMode { get; }

        /// <summary>
        /// Gets a value indicating whether this instance is in design time.
        /// </summary>
        bool IsInDesignTime { get; }

        /// <summary>
        /// Synchronously invokes the given instructions on the main GUI application dispatcher (the UI thread).
        /// </summary>
        /// <param name="priority">The priority.</param>
        /// <param name="action">The action.</param>
        void GuiInvoke(ActionPriority priority, Action action);

        /// <summary>
        /// Enqueues the given instructions with the given arguments on the main GUI application dispatcher.
        /// This is a way to execute code in a fire-and-forget style on the UI thread.
        /// </summary>
        /// <param name="priority">The priority.</param>
        /// <param name="callback">The callback.</param>
        /// <param name="arguments">The arguments.</param>
        void GuiEnqueueInvoke(ActionPriority priority, Delegate callback, params object[] arguments);

        /// <summary>
        /// Creates a renderer of the specified media type.
        /// </summary>
        /// <param name="mediaType">Type of the media.</param>
        /// <param name="mediaEngine">The media engine.</param>
        /// <returns>The renderer</returns>
        IMediaRenderer CreateRenderer(MediaType mediaType, MediaEngine mediaEngine);

        /// <summary>
        /// Creates a UI-aware dispatcher timer that executes actions on a schedule basis.
        /// </summary>
        /// <param name="priority">The priority.</param>
        /// <returns>
        /// An instance of the dispatcher timer
        /// </returns>
        IDispatcherTimer CreateGuiTimer(ActionPriority priority);

        /// <summary>
        /// Handles global FFmpeg library messages
        /// </summary>
        /// <param name="message">The message.</param>
        void HandleFFmpegLogMessage(MediaLogMessage message);
    }
}
