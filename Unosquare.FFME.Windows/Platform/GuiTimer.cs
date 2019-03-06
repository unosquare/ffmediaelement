﻿namespace Unosquare.FFME.Platform
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
        private System.Windows.Forms.Timer FormsTimer;

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
        public bool IsExecutingCycle => (IsCycleDone?.IsCompleted ?? true) == false;

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
            IsCycleDone.Wait();
            IsCycleDone.Dispose();
        }

        /// <summary>
        /// Runs the timer cycle.
        /// </summary>
        /// <param name="state">The state.</param>
        private void RunTimerCycle(object state)
        {
            // Handle the dispose process.
            if (IsDisposing == true)
            {
                if (ThreadingTimer != null)
                {
                    ThreadingTimer.Dispose();
                    ThreadingTimer = null;
                }

                if (FormsTimer != null)
                {
                    FormsTimer.Dispose();
                    FormsTimer = null;
                }

                if (DispatcherTimer != null)
                {
                    DispatcherTimer.Stop();
                    DispatcherTimer = null;
                }

                DisposeCallback?.Invoke();
                return;
            }

            // Skip running this cycle if we are already in the middle of one
            if (IsCycleDone.IsInProgress)
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
        /// <returns>The timer</returns>
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
        /// <returns>The timer</returns>
        private DispatcherTimer CreateDispatcherTimer()
        {
            var timer = new DispatcherTimer(DispatcherPriority.DataBind, GuiContext.Current.GuiDispatcher)
            {
                Interval = Interval,
                IsEnabled = true
            };

            timer.Tick += (s, e) => { RunTimerCycle(this); };
            timer.Start();
            return timer;
        }

        /// <summary>
        /// Creates the forms timer.
        /// </summary>
        /// <returns>The timer</returns>
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
    }
}
