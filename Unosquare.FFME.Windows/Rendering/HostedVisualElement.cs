namespace Unosquare.FFME.Rendering
{
    using System;
    using System.Collections;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Threading;

    internal abstract class HostedVisualElement<T> : FrameworkElement
        where T : FrameworkElement
    {
        /// <summary>
        /// The thread separated control loaded event
        /// </summary>
        public static readonly RoutedEvent ElementLoadedEvent = EventManager.RegisterRoutedEvent(
            nameof(ElementLoaded),
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(HostedVisualElement<T>));

        public HostedVisualElement()
        {
            Host = new HostVisual();
        }

        /// <summary>
        /// Occurs when the thread separated control loads.
        /// </summary>
        public event RoutedEventHandler ElementLoaded
        {
            add { AddHandler(ElementLoadedEvent, value); }
            remove { RemoveHandler(ElementLoadedEvent, value); }
        }

        public Dispatcher HostDispatcher { get; private set; }

        public T Element { get; private set; }

        protected HostVisual Host { get; }

        protected override int VisualChildrenCount => Host == null ? 0 : 1;

        protected override IEnumerator LogicalChildren
        {
            get { if (Host != null) yield return Host; }
        }

        private HostedPresentationSource PresentationSource { get; set; }

        /// <summary>
        /// Invokes the specified action on the hosted visual element's dispatcher.
        /// </summary>
        /// <param name="action">The action.</param>
        /// <returns>The awaitable operation</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DispatcherOperation Invoke(Action action)
        {
            return Invoke(DispatcherPriority.Normal, action);
        }

        /// <summary>
        /// Invokes the specified action on the hosted visual element's dispatcher.
        /// </summary>
        /// <param name="priority">The priority.</param>
        /// <param name="action">The action.</param>
        /// <returns>The awaitable operation</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DispatcherOperation Invoke(DispatcherPriority priority, Action action)
        {
            if (HostDispatcher == null || HostDispatcher.HasShutdownStarted || HostDispatcher.HasShutdownFinished)
                return null;

            if (action == null)
                return null;

            return HostDispatcher?.BeginInvoke(action, priority);
        }

        protected override void OnInitialized(EventArgs e)
        {
            AddLogicalChild(Host);
            AddVisualChild(Host);
            Loaded += HandleLoadedEvent;
            Unloaded += HandleUnloadedEvent;
            LayoutUpdated += HandleLayoutUpdatedEvent;
            base.OnInitialized(e);
        }

        protected override Visual GetVisualChild(int index) => index == 0 ? Host : null;

        protected abstract T CreateHostedElement();

        /// <inheritdoc/>
        protected override Size ArrangeOverride(Size finalSize)
        {
            Invoke(() => { Element?.Arrange(new Rect(finalSize)); });
            return finalSize;
        }

        protected virtual void HandleUnloadedEvent(object sender, EventArgs e)
        {
            if (HostDispatcher == null)
                return;

            HostDispatcher.InvokeShutdown();
            RemoveLogicalChild(Host);
            RemoveVisualChild(Host);
            HostDispatcher = null;
            Element = null;
        }

        protected V GetElementProperty<V>(DependencyProperty property)
        {
            var result = default(V);
            Invoke(() => { result = (V)Element.GetValue(property); }).Wait();
            return result;
        }

        protected void SetElementProperty<V>(DependencyProperty property, V value)
        {
            Invoke(() => { Element.SetValue(property, value); });
        }

        private void HandleLayoutUpdatedEvent(object sender, EventArgs e)
        {
            Invoke(() => { Element?.Measure(DesiredSize); });
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
                PresentationSource.Dispose();
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
            RaiseEvent(new RoutedEventArgs(ElementLoadedEvent, this));
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
