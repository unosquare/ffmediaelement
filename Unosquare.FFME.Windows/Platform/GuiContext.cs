namespace Unosquare.FFME.Platform
{
    using Shared;
    using System;
    using System.Collections.Concurrent;
    using System.ComponentModel;
    using System.Threading;
    using System.Windows;
    using System.Windows.Forms;
    using System.Windows.Media;
    using System.Windows.Threading;

    /// <summary>
    /// The WPF or WinForms graphical context
    /// </summary>
    internal class GuiContext
    {
        private SynchronizationContext Context = null;
        private System.Windows.Forms.Timer WinFormsTimer = null;
        private DispatcherTimer WpfTimer = null;
        private ConcurrentQueue<GuiAction> ActionQueue = new ConcurrentQueue<GuiAction>();

        /// <summary>
        /// Initializes static members of the <see cref="GuiContext"/> class.
        /// </summary>
        static GuiContext()
        {
            Current = new GuiContext();
        }

        /// <summary>
        /// Prevents a default instance of the <see cref="GuiContext"/> class from being created.
        /// </summary>
        private GuiContext()
        {
            Context = SynchronizationContext.Current;
            ContextType = GuiContextType.None;

            if (Context is DispatcherSynchronizationContext) ContextType = GuiContextType.WPF;
            else if (Context is WindowsFormsSynchronizationContext) ContextType = GuiContextType.WinForms;

            IsValid = Context != null && ContextType != GuiContextType.None;

            if (ContextType == GuiContextType.WinForms)
            {
                WinFormsTimer = new System.Windows.Forms.Timer
                {
                    Interval = (int)Constants.Interval.HighPriority.TotalMilliseconds,
                };

                WinFormsTimer.Tick += (s, e) => { ProcessActionQueue(); };
                WinFormsTimer.Start();
            }
            else if (ContextType == GuiContextType.WPF)
            {
                WpfTimer = new DispatcherTimer(DispatcherPriority.DataBind)
                {
                    Interval = Constants.Interval.HighPriority
                };

                WpfTimer.Tick += (s, e) => { ProcessActionQueue(); };
                WpfTimer.Start();
            }

            // Design-time detection
            try
            {
                IsInDesignTime = (bool)DesignerProperties.IsInDesignModeProperty.GetMetadata(
                    typeof(DependencyObject)).DefaultValue;
            }
            catch
            {
                IsInDesignTime = false;
            }

            // RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
        }

        /// <summary>
        /// Gets the current instance.
        /// </summary>
        public static GuiContext Current { get; }

        /// <summary>
        /// Gets a value indicating whetherthe context is in design time
        /// </summary>
        public bool IsInDesignTime { get; }

        /// <summary>
        /// Returns true if this context is valid.
        /// </summary>
        public bool IsValid { get; }

        /// <summary>
        /// Gets the type of the context.
        /// </summary>
        public GuiContextType ContextType { get; }

        /// <summary>
        /// Enqueues a UI call
        /// </summary>
        /// <param name="callback">The callback.</param>
        /// <param name="arguments">The arguments.</param>
        /// <returns>The delegate arguments</returns>
        public WaitHandle EnqueueInvoke(Delegate callback, params object[] arguments)
        {
            var action = new GuiAction(callback, arguments);
            ActionQueue.Enqueue(action);
            return action.IsDone.WaitHandle;
        }

        public WaitHandle EnqueueInvoke(Action callback)
        {
            return EnqueueInvoke(callback, null);
        }

        public void Invoke(Action callback)
        {
            EnqueueInvoke(callback).WaitOne(1000);
        }

        private void ProcessActionQueue()
        {
            while (ActionQueue.TryDequeue(out var current))
            {
                try { current.Execute(); }
                catch { throw; }
                finally { current.Dispose(); }
            }
        }

        private sealed class GuiAction : IDisposable
        {
            private bool IsDisposed = false; // To detect redundant calls

            public GuiAction(Delegate action, params object[] p)
            {
                Action = action;
                Params = p;
                IsDone = new ManualResetEventSlim(false);
            }

            public Delegate Action { get; }

            public object[] Params { get; }

            public ManualResetEventSlim IsDone { get; }

            public void Execute()
            {
                Action.DynamicInvoke(Params);
            }

            #region IDisposable Support

            public void Dispose()
            {
                Dispose(true);
            }

            private void Dispose(bool alsoManaged)
            {
                if (!IsDisposed)
                {
                    if (alsoManaged)
                    {
                        IsDone.Set();
                        IsDone.Dispose();
                    }

                    IsDisposed = true;
                }
            }

            #endregion
        }
    }
}
