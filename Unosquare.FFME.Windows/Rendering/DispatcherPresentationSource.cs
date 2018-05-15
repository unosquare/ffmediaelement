namespace Unosquare.FFME.Rendering
{
    using System;
    using System.Windows;
    using System.Windows.Media;

    /// <summary>
    /// Represents a VisualTarget Presentation Source
    /// </summary>
    /// <seealso cref="PresentationSource" />
    /// <seealso cref="IDisposable" />
    internal sealed class DispatcherPresentationSource
        : PresentationSource, IDisposable
    {
        private readonly VisualTarget m_VisualTreeConnector = null;
        private bool m_IsDisposed = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="DispatcherPresentationSource"/> class.
        /// </summary>
        /// <param name="hostVisual">The host visual.</param>
        public DispatcherPresentationSource(HostVisual hostVisual)
        {
            m_VisualTreeConnector = new VisualTarget(hostVisual);
            AddSource();
        }

        /// <inheritdoc/>
        public override Visual RootVisual
        {
            get
            {
                try
                {
                    return m_VisualTreeConnector.RootVisual;
                }
                catch (Exception)
                {
                    return null;
                }
            }

            set
            {
                var oldRoot = m_VisualTreeConnector.RootVisual;
                m_VisualTreeConnector.RootVisual = value;
                RootChanged(oldRoot, value);

                if (value is UIElement rootElement)
                {
                    rootElement.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    rootElement.Arrange(new Rect(rootElement.DesiredSize));
                }
            }
        }

        /// <inheritdoc/>
        public override bool IsDisposed
        {
            get { return m_IsDisposed; }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            RemoveSource();
            m_VisualTreeConnector.Dispose();
            m_IsDisposed = true;
        }

        /// <inheritdoc/>
        protected override CompositionTarget GetCompositionTargetCore() => m_VisualTreeConnector;
    }
}
