namespace Unosquare.FFME.Platform
{
    using Primitives;
    using System;
    using System.Threading;

#if WINDOWS_UWP
    using Windows.UI.Xaml;
#else
    using System.Windows.Threading;
#endif

    /// <summary>
    /// Encapsulates different types of timers for different GUI context types
    /// into a single API. It provides one-at-a-time synchronized execution of the supplied
    /// Action. Call Dispose on an instance to stop the timer.
    /// </summary>
    /// <seealso cref="IDisposable" />
    internal sealed class GuiTimer : IDisposable
    {
        private static readonly TimeSpan DefaultPeriod = TimeSpan.FromMilliseconds(30);
        private readonly AtomicBoolean IsDisposing = new AtomicBoolean();
        private readonly IWaitEvent IsCycleDone = WaitEventFactory.Create(isCompleted: true, useSlim: true);
        private readonly Action TimerCallback;
        private readonly Action DisposeCallback;

        private Timer ThreadingTimer;
        private DispatcherTimer DispatcherTimer;
#if !WINDOWS_UWP
        private System.Windows.Forms.Timer FormsTimer;
#endif

        /// <summary>
        /// Initializes a new instance of the <see cref="GuiTimer" /> class.
        /// </summary>
        /// <param name="contextType">Type of the context.</param>
        /// <param name="interval">The interval.</param>
        /// <param name="callback">The callback.</param>
        /// <param name="disposeCallback">The dispose callback.</param>
        public GuiTimer(GuiContextType contextType, TimeSpan interval, Action callback, Action disposeCallback)
        {
            ContextType = contextType;
            Interval = interval;
            TimerCallback = callback;
            DisposeCallback = disposeCallback;

            switch (contextType)
            {
#if WINDOWS_UWP
                case GuiContextType.UWP:
                    {
                        DispatcherTimer = CreateDispatcherTimer();
                        break;
                    }
#else
                case GuiContextType.WPF:
                    {
                        DispatcherTimer = CreateDispatcherTimer();
                        break;
                    }

                case GuiContextType.WinForms:
                    {
                        FormsTimer = CreateFormsTimer();
                        break;
                    }
#endif

                default:
                    {
                        ThreadingTimer = CreateThreadingTimer();
                        break;
                    }
            }
        }

        public GuiTimer(TimeSpan interval, Action callback)
            : this(GuiContext.Current.Type, interval, callback, null)
        {
            // placeholder
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GuiTimer"/> class.
        /// </summary>
        /// <param name="callback">The callback.</param>
        public GuiTimer(Action callback)
            : this(GuiContext.Current.Type, DefaultPeriod, callback, null)
        {
            // placeholder
        }

        public GuiTimer(Action callback, Action disposeCallback)
            : this(GuiContext.Current.Type, DefaultPeriod, callback, disposeCallback)
        {
            // placeholder
        }

        /// <summary>
        /// Gets the type of the context.
        /// </summary>
        public GuiContextType ContextType { get; }

        /// <summary>
        /// Gets the interval.
        /// </summary>
        public TimeSpan Interval { get; }

        /// <summary>
        /// Gets a value indicating whether this instance is executing a cycle.
        /// </summary>
        public bool IsExecutingCycle => !IsCycleDone.IsCompleted;

        /// <summary>
        /// Waits for one cycle to be completed.
        /// </summary>
        public void WaitOne()
        {
            if (IsDisposing == true) return;
            IsCycleDone.Wait();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (IsDisposing == true) return;
            IsDisposing.Value = true;

            // Prevent a new cycle from being queued
            ThreadingTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            FormsTimer?.Stop();
            DispatcherTimer?.Stop();

            // Wait for the cycle in progress to complete
            IsCycleDone.Wait();

            // Handle the dispose process.
            ThreadingTimer?.Dispose();
            ThreadingTimer = null;

#if !WINDOWS_UWP
            FormsTimer?.Dispose();
            FormsTimer = null;
#endif
            DispatcherTimer = null;

            // Call the dispose callback
            DisposeCallback?.Invoke();
            IsCycleDone.Dispose();
        }

        /// <summary>
        /// Runs the timer cycle.
        /// </summary>
        /// <param name="state">The state.</param>
        private void RunTimerCycle(object state)
        {
            // Skip running this cycle if we are already in the middle of one
            if (IsCycleDone.IsInProgress || IsDisposing == true)
                return;

            // Start a cycle by signaling it
            IsCycleDone.Begin();

            try
            {
                // Call the configured timer callback
                TimerCallback();
            }
            finally
            {
                // Finalize the cycle
                IsCycleDone.Complete();
            }
        }

        /// <summary>
        /// Creates the threading timer.
        /// </summary>
        /// <returns>The timer.</returns>
        private Timer CreateThreadingTimer()
        {
            var timer = new Timer(
                RunTimerCycle,
                this,
                Convert.ToInt32(Interval.TotalMilliseconds),
                Convert.ToInt32(Interval.TotalMilliseconds));

            return timer;
        }

        /// <summary>
        /// Creates the dispatcher timer.
        /// </summary>
        /// <returns>The timer.</returns>
        private DispatcherTimer CreateDispatcherTimer()
        {
#if WINDOWS_UWP
            var timer = new DispatcherTimer { Interval = Interval };
#else
            var timer = new DispatcherTimer(DispatcherPriority.DataBind, GuiContext.Current.GuiDispatcher)
            {
                Interval = Interval,
                IsEnabled = true
            };
#endif

            timer.Tick += (s, e) => { RunTimerCycle(this); };
            timer.Start();
            return timer;
        }

#if !WINDOWS_UWP
        /// <summary>
        /// Creates the forms timer.
        /// </summary>
        /// <returns>The timer.</returns>
        private System.Windows.Forms.Timer CreateFormsTimer()
        {
            var timer = new System.Windows.Forms.Timer
            {
                Interval = Convert.ToInt32(Interval.TotalMilliseconds),
                Enabled = true
            };

            timer.Tick += (s, e) => { RunTimerCycle(this); };
            timer.Start();
            return timer;
        }
#endif
    }
}
