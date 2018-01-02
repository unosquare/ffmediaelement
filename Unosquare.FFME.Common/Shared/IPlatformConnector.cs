namespace Unosquare.FFME.Shared
{
    using System;

    /// <summary>
    /// The Platform-specific implementation interface
    /// Platform specific stuff:
    ///  - UI aware timer
    ///  - Invocation on UI thread
    ///  - Renderer creation
    ///  - TODO: Add the remainder of utility callbacks
    /// </summary>
    public interface IPlatformConnector
    {
        /// <summary>
        /// Sets the DLL directory in which external dependencies can be located.
        /// </summary>
        Func<string, bool> SetDllDirectory { get; }

        /// <summary>
        /// Fast pointer memory block copy function
        /// </summary>
        Action<IntPtr, IntPtr, uint> CopyMemory { get; }

        /// <summary>
        /// Fills the memory.
        /// </summary>
        Action<IntPtr, uint, byte> FillMemory { get;  }

        /// <summary>
        /// Synchronously invokes the given instructions on the main application dispatcher.
        /// </summary>
        Action<ActionPriority, Action> UIInvoke { get; }

        /// <summary>
        /// Enqueues the given instructions with the given arguments on the main application dispatcher.
        /// This is a way to execute code in a fire-and-forget style
        /// </summary>
        Action<ActionPriority, Delegate, object[]> UIEnqueueInvoke { get; }

        /// <summary>
        /// Creates a new instance of the renderer of the given type.
        /// </summary>
        /// <returns>The renderer that was created</returns>
        /// <exception cref="ArgumentException">mediaType has to be of a vild type</exception>
        Func<MediaType, MediaEngine, IMediaRenderer> CreateRenderer { get; }

        /// <summary>
        /// Creates a new UI aware timer with the specified priority.
        /// </summary>
        Func<ActionPriority, IDispatcherTimer> CreateTimer { get; }

        /// <summary>
        /// Called when an FFmpeg message is logged.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="MediaLogMessage"/> instance containing the event data.</param>
        void OnFFmpegMessageLogged(object sender, MediaLogMessage e);
    }
}
