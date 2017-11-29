namespace Unosquare.FFME
{
    using System;

    public enum DispatchPriority
    {
        Invalid = -1,
        Inactive = 0,
        SystemIdle = 1,
        ApplicationIdle = 2,
        ContextIdle = 3,
        Background = 4,
        Input = 5,
        Loaded = 6,
        Render = 7,
        DataBind = 8,
        Normal = 9,
        Send = 10
    }

    public enum CoreMediaState
    {
        Manual = 0,
        Play = 1,
        Close = 2,
        Pause = 3,
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
        /// Creates a new UI aware timer with the specified priority.
        /// </summary>
        public static Func<DispatchPriority, IDispatcherTimer> CreateTimer { get; set; }

        /// <summary>
        /// Sets the DLL directory in which external dependencies can be located.
        /// </summary>
        public static Func<string, bool> SetDllDirectory;

        /// <summary>
        /// Fast pointer memory block copy function
        /// </summary>
        public static Action<IntPtr, IntPtr, uint> CopyMemory;

        /// <summary>
        /// Fills the memory.
        /// </summary>
        public static Action<IntPtr, uint, byte> FillMemory;

        /// <summary>
        /// Synchronously invokes the given instructions on the main application dispatcher.
        /// </summary>
        public static Action<DispatchPriority, Action> UIInvoke;

        /// <summary>
        /// Enqueues the given instructions with the given arguments on the main application dispatcher.
        /// This is a way to execute code in a fire-and-forget style
        /// </summary>
        public static Action<DispatchPriority, Delegate, object[]> UIEnqueueInvoke;
    }
}
