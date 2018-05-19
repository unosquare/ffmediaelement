namespace Unosquare.FFME.Rendering
{
    using System;
    using System.Collections;
    using System.ComponentModel;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Threading;

    /// <summary>
    /// Provides a base class for a frameowrk element that is capable of
    /// being hosted on its own dispatcher. This allows for mutithreaded
    /// UI compistion.
    /// </summary>
    /// <typeparam name="T">The contained framework element</typeparam>
    /// <seealso cref="FrameworkElement" />
    internal abstract class ElementHostBase<T> : FrameworkElement
        where T : FrameworkElement
    {
        /// <summary>
        /// The thread separated control loaded event
        /// </summary>
        public static readonly RoutedEvent ElementLoadedEvent = EventManager.RegisterRoutedEvent(
            nameof(ElementLoaded),
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(ElementHostBase<T>));

        /// <summary>
        /// Initializes a new instance of the <see cref="ElementHostBase{T}"/> class.
        /// </summary>
        /// <param name="hasOwnDispatcher">if set to <c>true</c>, it creates its own separate thread and associated dispatcher</param>
        protected ElementHostBase(bool hasOwnDispatcher)
        {
            var isInDesignMode = DesignerProperties.GetIsInDesignMode(this);
            HasOwnDispatcher = isInDesignMode ? false : hasOwnDispatcher;
        }

        /// <summary>
        /// Occurs when the thread separated control loads.
        /// </summary>
        public event RoutedEventHandler ElementLoaded
        {
            add { AddHandler(ElementLoadedEvent, value); }
            remove { RemoveHandler(ElementLoadedEvent, value); }
        }

        /// <summary>
        /// Gets a value indicating whether this instance is running on its own dispatcher.
        /// </summary>
        public bool HasOwnDispatcher { get; }

        /// <summary>
        /// Gets the dispatcher this element is hosted on.
        /// </summary>
        public Dispatcher ElementDispatcher { get; private set; }

        /// <summary>
        /// PRovides access to the framework element hosted within this element
        /// </summary>
        public T Element { get; private set; }

        /// <summary>
        /// Gets the available render area
        /// </summary>
        protected Size AvailableSize { get; private set; }

        /// <summary>
        /// Gets the host visual. This becomes the root element of this control
        /// that glues the presentation source running on a different dispatcher
        /// to the main UI dispatcher.
        /// </summary>
        protected HostVisual Host { get; private set; }

        /// <summary>
        /// Gets the number of visual child elements within this element.
        /// </summary>
        protected override int VisualChildrenCount
        {
            get
            {
                if (HasOwnDispatcher)
                    return Host == null ? 0 : 1;
                else
                    return Element == null ? 0 : 1;
            }
        }

        /// <summary>
        /// Gets an enumerator for logical child elements of this element.
        /// </summary>
        protected override IEnumerator LogicalChildren
        {
            get
            {
                if (HasOwnDispatcher)
                {
                    if (Host != null)
                        yield return Host;
                }
                else
                {
                    if (Element != null)
                        yield return Element;
                }
            }
        }

        /// <summary>
        /// Gets or sets the presentation source which roots the visual elements on
        /// the independent dispatcher.
        /// </summary>
        private HostedPresentationSource PresentationSource { get; set; }

        /// <summary>
        /// Invokes the specified action on the hosted visual element's dispatcher.
        /// </summary>
        /// <param name="action">The action.</param>
        /// <returns>The awaitable operation</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task InvokeAsync(Action action)
        {
            return InvokeAsync(DispatcherPriority.Normal, action);
        }

        /// <summary>
        /// Invokes the specified action on the hosted visual element's dispatcher.
        /// </summary>
        /// <param name="priority">The priority.</param>
        /// <param name="action">The action.</param>
        /// <returns>The awaitable operation</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task InvokeAsync(DispatcherPriority priority, Action action)
        {
            if (action == null)
                return Task.CompletedTask;

            if (ElementDispatcher == null || ElementDispatcher.HasShutdownStarted || ElementDispatcher.HasShutdownFinished)
                return Task.CompletedTask;

            if (Thread.CurrentThread != ElementDispatcher?.Thread)
                return ElementDispatcher?.BeginInvoke(action, priority).Task;

            action?.Invoke();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Raises the <see cref="E:System.Windows.FrameworkElement.Initialized" /> event.
        /// This method is invoked whenever <see cref="P:System.Windows.FrameworkElement.IsInitialized" /> is set to true internally.
        /// </summary>
        /// <param name="e">The <see cref="T:System.Windows.RoutedEventArgs" /> that contains the event data.</param>
        protected override void OnInitialized(EventArgs e)
        {
            if (HasOwnDispatcher)
            {
                Host = new HostVisual();
                AddVisualChild(Host);
                AddLogicalChild(Host);
                Loaded += HandleLoadedEvent;
                Unloaded += HandleUnloadedEvent;
            }
            else
            {
                ElementDispatcher = Dispatcher.CurrentDispatcher;
                Element = CreateHostedElement();
                Element.Loaded += (sender, args) =>
                {
                    InvalidateMeasure();
                    RaiseEvent(new RoutedEventArgs(ElementLoadedEvent, this));
                };

                AddVisualChild(Element);
                AddLogicalChild(Element);
            }

            base.OnInitialized(e);
        }

        /// <summary>
        /// Overrides <see cref="M:System.Windows.Media.Visual.GetVisualChild(System.Int32)" />, and returns a child at the specified index from a collection of child elements.
        /// </summary>
        /// <param name="index">The zero-based index of the requested child element in the collection.</param>
        /// <returns>
        /// The requested child element. This should not return null; if the provided index is out of range, an exception is thrown.
        /// </returns>
        protected override Visual GetVisualChild(int index)
        {
            if (HasOwnDispatcher)
                return Host;
            else
                return Element;
        }

        /// <summary>
        /// Creates the element contained by this host
        /// </summary>
        /// <returns>An instance of the framework element to be hosted</returns>
        protected abstract T CreateHostedElement();

        /// <summary>
        /// When overridden in a derived class, positions child elements and determines a size for a <see cref="T:System.Windows.FrameworkElement" /> derived class.
        /// </summary>
        /// <param name="finalSize">The final area within the parent that this element should use to arrange itself and its children.</param>
        /// <returns>
        /// The actual size used.
        /// </returns>
        /// <inheritdoc />
        protected override Size ArrangeOverride(Size finalSize)
        {
            if (HasOwnDispatcher)
            {
                InvokeAsync(DispatcherPriority.DataBind, () => Element?.Arrange(new Rect(finalSize)));
                return finalSize;
            }

            Element?.Arrange(new Rect(finalSize));
            return finalSize;
        }

        /// <summary>
        /// When overridden in a derived class, measures the size in layout required for child elements and determines a size for the <see cref="T:System.Windows.FrameworkElement" />-derived class.
        /// </summary>
        /// <param name="newAvailableSize">The available size that this element can give to child elements. Infinity can be specified as a value to indicate that the element will size to whatever content is available.</param>
        /// <returns>
        /// The size that this element determines it needs during layout, based on its calculations of child element sizes.
        /// </returns>
        protected override Size MeasureOverride(Size newAvailableSize)
        {
            var previousAvailableSize = AvailableSize;
            var previousDesiredSize = Element?.DesiredSize ?? default;
            var availableSizeChanged = previousAvailableSize != newAvailableSize;

            AvailableSize = newAvailableSize;
            if (HasOwnDispatcher == false)
            {
                Element?.Measure(newAvailableSize);
            }
            else
            {
                InvokeAsync(DispatcherPriority.DataBind, () =>
                {
                    Element?.Measure(newAvailableSize);
                    var desiredSizeChanged = previousDesiredSize != (Element?.DesiredSize ?? default);

                    if (availableSizeChanged || desiredSizeChanged)
                        Dispatcher.InvokeAsync(() => InvalidateMeasure(), DispatcherPriority.Render);
                });

                if (availableSizeChanged)
                    return previousDesiredSize;
            }

            return Element?.DesiredSize ?? default;
        }

        /// <summary>
        /// Handles the unloaded event.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected virtual void HandleUnloadedEvent(object sender, EventArgs e)
        {
            if (ElementDispatcher == null)
                return;

            ElementDispatcher.InvokeShutdown();
            RemoveLogicalChild(Host);
            RemoveVisualChild(Host);
            ElementDispatcher = null;
            Element = null;
        }

        /// <summary>
        /// Gets the element property.
        /// </summary>
        /// <typeparam name="V">The property type</typeparam>
        /// <param name="property">The property.</param>
        /// <returns>The value</returns>
        protected V GetElementProperty<V>(DependencyProperty property)
        {
            if (HasOwnDispatcher)
            {
                var result = default(V);
                if (Element != null)
                {
                    InvokeAsync(DispatcherPriority.DataBind, () =>
                    {
                        result = (V)Element.GetValue(property);
                    })?.Wait();
                }

                return result;
            }

            return (V)Element.GetValue(property);
        }

        /// <summary>
        /// Sets the element property.
        /// </summary>
        /// <typeparam name="V">The value type</typeparam>
        /// <param name="property">The property.</param>
        /// <param name="value">The value.</param>
        protected void SetElementProperty<V>(DependencyProperty property, V value)
        {
            if (HasOwnDispatcher && Element != null)
            {
                InvokeAsync(DispatcherPriority.DataBind, () =>
                {
                    Element?.SetValue(property, value);
                });

                return;
            }

            Element?.SetValue(property, value);
        }

        /// <summary>
        /// Handles the loaded event.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
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
                Element.SizeChanged += (snd, eva) =>
                {
                    if (eva.PreviousSize == default || eva.NewSize == default)
                        Dispatcher.Invoke(() => InvalidateMeasure());
                };

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
                Thread.Sleep(50);

            ElementDispatcher = Dispatcher.FromThread(thread);
            Dispatcher.BeginInvoke(new Action(() => { InvalidateMeasure(); }));
            RaiseEvent(new RoutedEventArgs(ElementLoadedEvent, this));
        }

        /// <summary>
        /// A presentation source class to root a Visual (a HostVisual) on to its own visual tree
        /// </summary>
        /// <seealso cref="FrameworkElement" />
        private sealed class HostedPresentationSource : PresentationSource, IDisposable
        {
            private readonly VisualTarget HostConnector;
            private bool m_IsDisposed = false;

            /// <summary>
            /// Initializes a new instance of the <see cref="HostedPresentationSource"/> class.
            /// </summary>
            /// <param name="host">The host.</param>
            public HostedPresentationSource(HostVisual host)
            {
                HostConnector = new VisualTarget(host);
            }

            /// <summary>
            /// When overridden in a derived class, gets or sets the root visual being presented in the source.
            /// </summary>
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

            /// <summary>
            /// When overridden in a derived class, gets a value that declares whether the object is disposed.
            /// </summary>
            public override bool IsDisposed => m_IsDisposed;

            /// <summary>
            /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
            /// </summary>
            public void Dispose() => Dispose(true);

            /// <summary>
            /// When overridden in a derived class, returns a visual target for the given source.
            /// </summary>
            /// <returns>
            /// Returns a <see cref="T:System.Windows.Media.CompositionTarget" /> that is target for rendering the visual.
            /// </returns>
            protected override CompositionTarget GetCompositionTargetCore() => HostConnector;

            /// <summary>
            /// Releases unmanaged and - optionally - managed resources.
            /// </summary>
            /// <param name="alsoManaged"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
            private void Dispose(bool alsoManaged)
            {
                if (m_IsDisposed) return;
                if (alsoManaged)
                {
                    m_IsDisposed = true;
                    HostConnector?.Dispose();
                }
            }
        }
    }
}
