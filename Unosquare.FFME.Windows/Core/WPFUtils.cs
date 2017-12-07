namespace Unosquare.FFME.Core
{
    using System;
    using System.ComponentModel;
    using System.Windows;

    /// <summary>
    /// Provides a set of utilities to perfrom logging, text formatting, 
    /// conversion and other handy calculations.
    /// </summary>
    internal static class WPFUtils
    {
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
    }
}
