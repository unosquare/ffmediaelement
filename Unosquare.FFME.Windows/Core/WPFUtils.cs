namespace Unosquare.FFME.Core
{
    using System;
    using System.ComponentModel;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Threading;

    /// <summary>
    /// Provides a set of utilities to perfrom logging, text formatting, 
    /// conversion and other handy calculations.
    /// </summary>
    internal static class WPFUtils
    {
        #region Constants

        public const FrameworkPropertyMetadataOptions AffectsMeasureAndRender
            = FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender;

        #endregion

        #region Private Declarations

        private static bool? m_IsInDesignTime;

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes static members of the <see cref="WPFUtils"/> class.
        /// </summary>
        static WPFUtils()
        {
            // fancy way of ensuring Utils initializes
            // Since WPFUtils will be called in MediaElement's constructor, this means
            // we are also on the main thread!
            Console.WriteLine($"Debug mode: {Utils.IsInDebugMode}");
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
        /// Gets the UI dispatcher.
        /// </summary>
        public static Dispatcher UIDispatcher => Application.Current?.Dispatcher;

        /// <summary>
        /// Synchronously invokes the given instructions on the main application dispatcher.
        /// </summary>
        /// <param name="priority">The priority.</param>
        /// <param name="action">The action.</param>
        public static void UIInvoke(DispatcherPriority priority, Action action)
        {
            UIDispatcher?.Invoke(action, priority, null);
        }

        /// <summary>
        /// Enqueues the given instructions with the given arguments on the main application dispatcher.
        /// This is a way to execute code in a fire-and-forget style
        /// </summary>
        /// <param name="priority">The priority.</param>
        /// <param name="action">The action.</param>
        /// <param name="args">The arguments.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static async Task UIEnqueueInvoke(DispatcherPriority priority, Delegate action, params object[] args)
        {
            try
            {
                // Call the code on the UI dispatcher
                await UIDispatcher?.BeginInvoke(action, priority, args);
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
