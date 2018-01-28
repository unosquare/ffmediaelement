namespace Unosquare.FFME.Platform
{
    using System;
    using System.Threading;
    using System.Windows.Threading;
    using Shared;

    /// <summary>
    /// Encapsulates different types of timers for different GUI context types
    /// into a single API. It provides one-at-a-time synchronized execution of the supplied
    /// Action. Call Dispose on an instance to stop the timer.
    /// </summary>
    /// <seealso cref="IDisposable" />
    internal sealed class GuiTimer : IDisposable
    {
        private Timer ThreadingTimer = null;
        private DispatcherTimer DispatcherTimer = null;
        private System.Windows.Forms.Timer FormsTimer = null;
        private ManualResetEvent IsCycleDone = new ManualResetEvent(true);
        private Action TimerCallback = null;
        private bool IsDisposing = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="GuiTimer"/> class.
        /// </summary>
        /// <param name="contextType">Type of the context.</param>
        /// <param name="interval">The interval.</param>
        /// <param name="callback">The callback.</param>
        public GuiTimer(GuiContextType contextType, TimeSpan interval, Action callback)
        {
            ContextType = contextType;
            Interval = interval;
            TimerCallback = callback;

            switch (contextType)
            {
                case GuiContextType.None:
                    {
                        ThreadingTimer = CreateThreadingTimer();
                        break;
                    }

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
            }
        }

        public GuiTimer(TimeSpan interval, Action callback)
            : this(GuiContext.Current.ContextType, interval, callback)
        {
            // placeholder
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GuiTimer"/> class.
        /// </summary>
        /// <param name="callback">The callback.</param>
        public GuiTimer(Action callback)
            : this(GuiContext.Current.ContextType, Constants.Interval.MediumPriority, callback)
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
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (IsDisposing) return;
            IsDisposing = true;

            IsCycleDone.WaitOne();
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

            IsCycleDone.Dispose();
        }

        /// <summary>
        /// Runs the timer cycle.
        /// </summary>
        /// <param name="state">The state.</param>
        private void RunTimerCycle(object state)
        {
            if (IsDisposing || IsCycleDone.IsSet() == false)
                return;

            try
            {
                IsCycleDone.Reset();
                TimerCallback();
            }
            catch
            {
                throw;
            }
            finally
            {
                IsCycleDone.Set();
            }
        }

        /// <summary>
        /// Creates the threading timer.
        /// </summary>
        /// <returns>The timer</returns>
        private Timer CreateThreadingTimer()
        {
            var timer = new Timer(
                new TimerCallback(RunTimerCycle), this, (int)Interval.TotalMilliseconds, (int)Interval.TotalMilliseconds);
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
                Interval = (int)Interval.TotalMilliseconds,
                Enabled = true
            };

            timer.Tick += (s, e) => { RunTimerCycle(this); };
            timer.Start();
            return timer;
        }
    }
}
