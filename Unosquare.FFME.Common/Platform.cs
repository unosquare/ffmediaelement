namespace Unosquare.FFME
{
    using Core;
    using Rendering;
    using System;
    using System.Threading.Tasks;

    public enum CoreDispatcherPriority
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
        public static Func<CoreDispatcherPriority, IDispatcherTimer> CreateTimer { get; set; }

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
        public static Action<CoreDispatcherPriority, Action> UIInvoke;

        /// <summary>
        /// Enqueues the given instructions with the given arguments on the main application dispatcher.
        /// This is a way to execute code in a fire-and-forget style
        /// </summary>
        public static Func<CoreDispatcherPriority, Delegate, object[], Task> UIEnqueueInvoke;

        /// <summary>
        /// Creates a new instance of the renderer of the given type.
        /// </summary>
        /// <param name="mediaType">Type of the media.</param>
        /// <param name="mediaElementCore">Media element core control.</param>
        /// <returns>The renderer that was created</returns>
        /// <exception cref="ArgumentException">mediaType has to be of a vild type</exception>
        public static Func<MediaType, MediaElementCore, IRenderer> CreateRenderer;

        /// <summary>
        /// Creates an empty pump operation with background priority
        /// </summary>
        /// <returns>an empty pump operation</returns>
        public static Task CreatePumpOperation()
        {
            return UIEnqueueInvoke(
                CoreDispatcherPriority.Background, new Action(async () => { await Task.Yield(); }), null);
        }

        /// <summary>
        /// Creates the asynchronous waiter.
        /// </summary>
        /// <param name="dispatcher">The dispatcher.</param>
        /// <param name="backgroundTask">The background task.</param>
        /// <returns>
        /// a dsipatcher operation that can be awaited
        /// </returns>
        public static Task CreateAsynchronousPumpWaiter(Task backgroundTask)
        {
            var operation = UIEnqueueInvoke(CoreDispatcherPriority.Input, new Action(() =>
            {
                var pumpTimes = 0;
                while (backgroundTask.IsCompleted == false)
                {
                    // Pump invoke
                    pumpTimes++;
                    UIInvoke(
                        CoreDispatcherPriority.Background,
                        new Action(async () => { await Task.Yield(); }));
                }

                System.Diagnostics.Debug.WriteLine($"{nameof(CreateAsynchronousPumpWaiter)}: Pump Times: {pumpTimes}");
            }), null);

            return operation;
        }
    }
}
