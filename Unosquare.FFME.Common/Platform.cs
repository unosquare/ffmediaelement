namespace Unosquare.FFME
{
    using Core;
    using Rendering;
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Dispatcher Priority Enum
    /// </summary>
    public enum CoreDispatcherPriority
    {
        /// <summary>
        /// The invalid
        /// </summary>
        Invalid = -1,

        /// <summary>
        /// The inactive
        /// </summary>
        Inactive = 0,

        /// <summary>
        /// The system idle
        /// </summary>
        SystemIdle = 1,

        /// <summary>
        /// The application idle
        /// </summary>
        ApplicationIdle = 2,

        /// <summary>
        /// The context idle
        /// </summary>
        ContextIdle = 3,

        /// <summary>
        /// The background
        /// </summary>
        Background = 4,

        /// <summary>
        /// The input
        /// </summary>
        Input = 5,

        /// <summary>
        /// The loaded
        /// </summary>
        Loaded = 6,

        /// <summary>
        /// The render
        /// </summary>
        Render = 7,

        /// <summary>
        /// The data bind
        /// </summary>
        DataBind = 8,

        /// <summary>
        /// The normal
        /// </summary>
        Normal = 9,

        /// <summary>
        /// The send
        /// </summary>
        Send = 10
    }

    /// <summary>
    /// Media States
    /// </summary>
    public enum CoreMediaState
    {
        /// <summary>
        /// The manual
        /// </summary>
        Manual = 0,

        /// <summary>
        /// The play
        /// </summary>
        Play = 1,

        /// <summary>
        /// The close
        /// </summary>
        Close = 2,

        /// <summary>
        /// The pause
        /// </summary>
        Pause = 3,

        /// <summary>
        /// The stop
        /// </summary>
        Stop = 4
    }

    /// <summary>
    /// Platform specific stuff:
    ///  - UI aware timer
    ///  - Invocation on UI thread
    ///  - Renderer creation
    /// </summary>
    internal static class Platform
    {
        /// <summary>
        /// Sets the DLL directory in which external dependencies can be located.
        /// </summary>
        public static Func<string, bool> SetDllDirectory { get; internal set; }

        /// <summary>
        /// Fast pointer memory block copy function
        /// </summary>
        public static Action<IntPtr, IntPtr, uint> CopyMemory { get; internal set; }

        /// <summary>
        /// Fills the memory.
        /// </summary>
        public static Action<IntPtr, uint, byte> FillMemory { get; internal set; }

        /// <summary>
        /// Synchronously invokes the given instructions on the main application dispatcher.
        /// </summary>
        public static Action<CoreDispatcherPriority, Action> UIInvoke { get; internal set; }

        /// <summary>
        /// Enqueues the given instructions with the given arguments on the main application dispatcher.
        /// This is a way to execute code in a fire-and-forget style
        /// </summary>
        public static Action<CoreDispatcherPriority, Delegate, object[]> UIEnqueueInvoke { get; internal set; }

        /// <summary>
        /// Creates a new instance of the renderer of the given type.
        /// </summary>
        /// <returns>The renderer that was created</returns>
        /// <exception cref="ArgumentException">mediaType has to be of a vild type</exception>
        public static Func<MediaType, MediaElementCore, IRenderer> CreateRenderer { get; internal set; }

        /// <summary>
        /// Creates a new UI aware timer with the specified priority.
        /// </summary>
        public static Func<CoreDispatcherPriority, IDispatcherTimer> CreateTimer { get; internal set; }
    }
}