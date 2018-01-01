namespace Unosquare.FFME.Core
{
    using System;
    using System.ComponentModel;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Threading;

    /// <summary>
    /// Provides platform-specific implementations of Gui functionality.
    /// </summary>
    internal static class Gui
    {
        #region Constants

        public const FrameworkPropertyMetadataOptions AffectsMeasureAndRender
            = FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender;

        #endregion

        #region Private Declarations

        /// <summary>
        /// Stores whether this instance is in design time
        /// </summary>
        private static bool? m_IsInDesignTime;

        /// <summary>
        /// The application synchronization context
        /// </summary>
        private static SynchronizationContext WinFormsContext = null;

        /// <summary>
        /// The WPF dispatcher
        /// </summary>
        private static Dispatcher WpfDispatcher = null;

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes static members of the <see cref="Gui"/> class.
        /// </summary>
        static Gui()
        {
            // fancy way of ensuring Utils initializes
            // Since WPFUtils will be called in MediaElement's constructor, this means
            // we are also on the main thread!
            Utils.Log(typeof(Gui),
                MediaLogMessageType.Debug,
                $"Platform-specific initialization. Debug Mode: {Utils.IsInDebugMode}");

            // Try to detect the WPF 
            WpfDispatcher = Application.Current?.Dispatcher;
            if (WpfDispatcher != null) return;

            // Store the startup sync context
            WinFormsContext = SynchronizationContext.Current;

            if (WinFormsContext == null
                || WinFormsContext.GetType() != typeof(System.Windows.Forms.WindowsFormsSynchronizationContext))
            {
                Utils.Log(typeof(Gui),
                    MediaLogMessageType.Error,
                    $"Failed to get a valid WinForms {nameof(SynchronizationContext)}.{nameof(SynchronizationContext.Current)}.");

                return;
            }

            Utils.Log(typeof(Gui),
                MediaLogMessageType.Warning,
                "WinForms support is experimental. Please help by reporting any issues!");
        }

        #endregion

        #region Properties

        /// <summary>
        /// Determines if we are currently in Design Time
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is in design time; otherwise, <c>false</c>.
        /// </value>
        public static bool IsInDesignTime
        {
            get
            {
                if (!m_IsInDesignTime.HasValue)
                {
                    m_IsInDesignTime = (bool)DesignerProperties.IsInDesignModeProperty.GetMetadata(
                          typeof(DependencyObject)).DefaultValue;
                }

                return m_IsInDesignTime.Value;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Synchronously invokes the given instructions on the main application dispatcher.
        /// </summary>
        /// <param name="priority">The priority.</param>
        /// <param name="action">The action.</param>
        public static void UIInvoke(DispatcherPriority priority, Action action)
        {
            if (WpfDispatcher != null)
            {
                WpfDispatcher.Invoke(action, priority, null);
                return;
            }

            if (WinFormsContext != null)
            {
                WinFormsContext.Send((s) => { action(); }, priority);
                return;
            }

            throw new Exception($"{nameof(UIInvoke)} failure: unable to get UI context.");
        }

        /// <summary>
        /// Enqueues the given instructions with the given arguments on the main application dispatcher.
        /// This is a way to execute code in a fire-and-forget style
        /// </summary>
        /// <param name="priority">The priority.</param>
        /// <param name="action">The action.</param>
        /// <param name="args">The arguments.</param>
        public static void UIEnqueueInvoke(DispatcherPriority priority, Delegate action, params object[] args)
        {
            try
            {
                if (WpfDispatcher != null)
                {
                    WpfDispatcher.BeginInvoke(action, priority, args);
                    return;
                }

                if (WinFormsContext != null)
                {
                    var postState = new Tuple<Delegate, object[]>(action, args);
                    WinFormsContext.Post((s) =>
                    {
                        var a = s as Tuple<Delegate, object[]>;
                        a.Item1.DynamicInvoke(a.Item2);
                    }, postState);
                    return;
                }

                throw new Exception($"{nameof(UIInvoke)} failure: unable to get UI context.");
            }
            catch (TaskCanceledException)
            {
                // Swallow task cancellation exceptions. This is ok.
                return;
            }
            catch
            {
                // Retrhow the exception
                // TODO: Maybe logging here would be helpful?
                throw;
            }
        }

        #endregion
    }
}
