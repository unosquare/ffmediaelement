namespace Unosquare.FFME.Rendering
{
    using System;
    using System.Threading;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Threading;

    internal abstract class HostedVisualElement<T> : FrameworkElement
        where T : FrameworkElement
    {
        public HostedVisualElement()
        {
            Host = new HostVisual();
            AddVisualChild(Host);
            Loaded += HandleLoadedEvent;
        }

        public Dispatcher HostDispatcher { get; private set; }

        public T Element { get; private set; }

        protected HostVisual Host { get; }

        protected override int VisualChildrenCount => 1;

        private HostedPresentationSource PresentationSource { get; set; }

        protected override Visual GetVisualChild(int index) => index == 0 ? Host : null;

        protected abstract T CreateHostedElement();

        protected override Size MeasureOverride(Size constraint)
        {
            var targetSize = default(Size);

            HostDispatcher?.InvokeAsync(new Action(() =>
            {
                Element?.Measure(constraint);
                targetSize = Element?.DesiredSize ?? default;
            })).Wait(TimeSpan.FromMilliseconds(50));

            return targetSize;
        }

        /// <inheritdoc/>
        protected override Size ArrangeOverride(Size finalSize)
        {
            HostDispatcher?.InvokeAsync(new Action(() => { Element?.Arrange(new Rect(finalSize)); }));
            return finalSize;
        }

        private void HandleLoadedEvent(object sender, RoutedEventArgs e)
        {
            Loaded -= HandleLoadedEvent;

            var doneCreating = new ManualResetEvent(false);
            var thread = new Thread(() =>
            {
                PresentationSource = new HostedPresentationSource(Host);
                doneCreating.Set();
                Element = CreateHostedElement();
                PresentationSource.RootVisual = Element;
                Dispatcher.Run();
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Priority = ThreadPriority.Highest;
            thread.Start();
            doneCreating.WaitOne();
            doneCreating.Dispose();

            while (Dispatcher.FromThread(thread) == null)
            {
                Thread.Sleep(50);
            }

            HostDispatcher = Dispatcher.FromThread(thread);
            Dispatcher.BeginInvoke(new Action(() => { InvalidateMeasure(); }));
        }

        private sealed class HostedPresentationSource : PresentationSource, IDisposable
        {
            private readonly VisualTarget HostConnector;
            private bool m_IsDisposed = false;

            public HostedPresentationSource(HostVisual host)
            {
                HostConnector = new VisualTarget(host);
            }

            public override Visual RootVisual
            {
                get
                {
                    return HostConnector.RootVisual;
                }
                set
                {
                    var oldRoot = HostConnector.RootVisual;

                    // Set the root visual of the VisualTarget.  This visual will
                    // now be used to visually compose the scene.
                    HostConnector.RootVisual = value;

                    // Tell the PresentationSource that the root visual has
                    // changed.  This kicks off a bunch of stuff like the
                    // Loaded event.
                    RootChanged(oldRoot, value);

                    // Kickoff layout...
                    if (value is UIElement rootElement)
                    {
                        rootElement.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                        rootElement.Arrange(new Rect(rootElement.DesiredSize));
                    }
                }
            }

            public override bool IsDisposed => m_IsDisposed;

            public void Dispose() => Dispose(true);

            protected override CompositionTarget GetCompositionTargetCore() => HostConnector;

            private void Dispose(bool alsoManaged)
            {
                if (m_IsDisposed) return;
                if (alsoManaged)
                {
                    m_IsDisposed = true;
                    HostConnector.Dispose();
                }
            }
        }
    }
}
