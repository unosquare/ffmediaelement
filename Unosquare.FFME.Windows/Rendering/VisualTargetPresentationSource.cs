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
    internal class VisualTargetPresentationSource
        : PresentationSource, IDisposable
    {
        private readonly VisualTarget _visualTarget = null;
        private bool _isDisposed = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="VisualTargetPresentationSource"/> class.
        /// </summary>
        /// <param name="hostVisual">The host visual.</param>
        public VisualTargetPresentationSource(HostVisual hostVisual)
        {
            _visualTarget = new VisualTarget(hostVisual);
            AddSource();
        }

        /// <inheritdoc/>
        public override Visual RootVisual
        {
            get
            {
                try
                {
                    return _visualTarget.RootVisual;
                }
                catch (Exception)
                {
                    return null;
                }
            }

            set
            {
                Visual oldRoot = _visualTarget.RootVisual;
                _visualTarget.RootVisual = value;
                RootChanged(oldRoot, value);

                UIElement rootElement = value as UIElement;
                if (rootElement != null)
                {
                    rootElement.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
                    rootElement.Arrange(new Rect(rootElement.DesiredSize));
                }
            }
        }

        /// <inheritdoc/>
        public override bool IsDisposed
        {
            get { return _isDisposed; }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            RemoveSource();
            _isDisposed = true;
        }

        /// <inheritdoc/>
        protected override CompositionTarget GetCompositionTargetCore() => _visualTarget;
    }
}
