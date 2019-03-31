namespace Unosquare.FFME.Platform
{
    using Primitives;
    using System;
    using System.Threading;
    using System.Windows.Threading;

    /// <summary>
    /// Encapsulates different types of timers for different GUI context types
    /// into a single API. It provides one-at-a-time synchronized execution of the supplied
    /// Action. Call Dispose on an instance to stop the timer.
    /// </summary>
    internal sealed class GuiTimer : IGuiTimer
    {
        private readonly IWaitEvent IsCycleDone = WaitEventFactory.Create(isCompleted: true, useSlim: true);
        private readonly Action TimerCallback;
        private readonly AtomicBoolean HasRequestedStop = new AtomicBoolean();
        private Timer ThreadingTimer;
        private DispatcherTimer DispatcherTimer;
        private System.Windows.Forms.Timer FormsTimer;

        /// <summary>
        /// Initializes a new instance of the <see cref="GuiTimer" /> class.
        /// </summary>
        /// <param name="contextType">Type of the context.</param>
        /// <param name="interval">The interval.</param>
        /// <param name="callback">The callback.</param>
        public GuiTimer(GuiContextType contextType, TimeSpan interval, Action callback)
        {
            Interval = interval;
            TimerCallback = callback;

            switch (contextType)
            {
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

                default:
                    {
                        ThreadingTimer = CreateThreadingTimer();
                        break;
                    }
            }

            StartTimer();
        }

        /// <inheritdoc />
        public TimeSpan Interval { get; }

        /// <inheritdoc />
        public bool IsExecutingCycle => !IsCycleDone.IsCompleted;

        /// <inheritdoc />
        public void Stop()
        {
            if (HasRequestedStop.Value)
                return;

            HasRequestedStop.Value = true;
        }

        /// <summary>
        /// Starts the timer.
        /// </summary>
        private void StartTimer()
        {
            FormsTimer?.Start();
            ThreadingTimer?.Change(0, Convert.ToInt32(Interval.TotalMilliseconds));
            DispatcherTimer?.Start();
        }

        /// <summary>
        /// Runs the timer cycle.
        /// </summary>
        /// <param name="state">The state.</param>
        private void RunTimerCycle(object state)
        {
            // Skip running this cycle if we are already in the middle of one
            if (IsCycleDone.IsInProgress)
                return;

            try
            {
                // Start a cycle by signaling it
                IsCycleDone.Begin();

                // Call the configured timer callback
                TimerCallback();
            }
            finally
            {
                if (HasRequestedStop == false)
                {
                    // Finalize the cycle
                    IsCycleDone.Complete();
                }
                else
                {
                    // Prevent a new cycle from being queued
                    ThreadingTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                    FormsTimer?.Stop();
                    DispatcherTimer?.Stop();

                    // Handle the dispose process.
                    ThreadingTimer?.Dispose();
                    FormsTimer?.Dispose();

                    // Remove references
                    ThreadingTimer = null;
                    FormsTimer = null;
                    DispatcherTimer = null;

                    // Complete the cycle and dispose of it
                    IsCycleDone.Complete();
                    IsCycleDone.Dispose();
                }
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
                Timeout.Infinite,
                Convert.ToInt32(Interval.TotalMilliseconds));

            return timer;
        }

        /// <summary>
        /// Creates the dispatcher timer.
        /// </summary>
        /// <returns>The timer.</returns>
        private DispatcherTimer CreateDispatcherTimer()
        {
            var timer = new DispatcherTimer(DispatcherPriority.DataBind, GuiContext.Current.GuiDispatcher)
            {
                Interval = Interval,
                IsEnabled = true
            };

            timer.Tick += (s, e) => { RunTimerCycle(this); };
            return timer;
        }

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
            return timer;
        }
    }
}
