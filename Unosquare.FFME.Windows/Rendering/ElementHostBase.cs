namespace Unosquare.FFME.Rendering
{
    using Primitives;
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
    /// Provides a base class for a framework element that is capable of
    /// being hosted on its own dispatcher. This allows for multi threaded
    /// UI composition.
    /// </summary>
    /// <typeparam name="T">The contained framework element.</typeparam>
    /// <seealso cref="FrameworkElement" />
    internal abstract class ElementHostBase<T> : FrameworkElement
        where T : FrameworkElement
    {
        /// <summary>
        /// The thread separated control loaded event.
        /// </summary>
        public static readonly RoutedEvent ElementLoadedEvent = EventManager.RegisterRoutedEvent(
            nameof(ElementLoaded),
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(ElementHostBase<T>));

        /// <summary>
        /// Initializes a new instance of the <see cref="ElementHostBase{T}"/> class.
        /// </summary>
        /// <param name="hasOwnDispatcher">if set to <c>true</c>, it creates its own separate thread and associated dispatcher.</param>
        protected ElementHostBase(bool hasOwnDispatcher)
        {
            var isInDesignMode = DesignerProperties.GetIsInDesignMode(this);
            HasOwnDispatcher = !isInDesignMode && hasOwnDispatcher;
        }

        /// <summary>
        /// Occurs when the thread separated control loads.
        /// </summary>
        public event RoutedEventHandler ElementLoaded
        {
            add => AddHandler(ElementLoadedEvent, value);
            remove => RemoveHandler(ElementLoadedEvent, value);
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
        /// Provides access to the framework element hosted within this element.
        /// </summary>
        public T Element { get; private set; }

        /// <summary>
        /// Gets the available render area.
        /// </summary>
        protected Size AvailableSize { get; private set; }

        /// <summary>
        /// Gets the host visual. This becomes the root element of this control
        /// that glues the presentation source running on a different dispatcher
        /// to the main UI dispatcher.
        /// </summary>
        protected HostVisual Host { get; private set; }

        /// <inheritdoc />
        protected override int VisualChildrenCount
        {
            get
            {
                if (HasOwnDispatcher)
                    return Host == null ? 0 : 1;
                return Element == null ? 0 : 1;
            }
        }

        /// <inheritdoc />
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
        /// <returns>The awaitable operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task InvokeAsync(Action action) => InvokeAsync(DispatcherPriority.Normal, action);

        /// <summary>
        /// Invokes the specified action on the hosted visual element's dispatcher.
        /// </summary>
        /// <param name="priority">The priority.</param>
        /// <param name="action">The action.</param>
        /// <returns>The awaitable operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task InvokeAsync(DispatcherPriority priority, Action action)
        {
            if (action == null)
                return Task.CompletedTask;

            if (ElementDispatcher == null || ElementDispatcher.HasShutdownStarted || ElementDispatcher.HasShutdownFinished)
                return Task.CompletedTask;

            if (Thread.CurrentThread != ElementDispatcher?.Thread)
                return ElementDispatcher?.BeginInvoke(action, priority).Task;

            action();
            return Task.CompletedTask;
        }

        /// <inheritdoc />
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

        /// <inheritdoc />
        protected override Visual GetVisualChild(int index) => HasOwnDispatcher ? Host : (Visual)Element;

        /// <summary>
        /// Creates the element contained by this host.
        /// </summary>
        /// <returns>An instance of the framework element to be hosted.</returns>
        protected abstract T CreateHostedElement();

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

        /// <inheritdoc />
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
                        Dispatcher.InvokeAsync(InvalidateMeasure, DispatcherPriority.Render);
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
        /// <typeparam name="TValue">The property type.</typeparam>
        /// <param name="property">The property.</param>
        /// <returns>The value.</returns>
        protected TValue GetElementProperty<TValue>(DependencyProperty property)
        {
            if (!HasOwnDispatcher)
                return (TValue)Element.GetValue(property);

            var result = default(TValue);
            if (Element != null)
            {
                InvokeAsync(DispatcherPriority.DataBind, () =>
                {
                    result = (TValue)Element.GetValue(property);
                })?.Wait();
            }

            return result;
        }

        /// <summary>
        /// Sets the element property.
        /// </summary>
        /// <typeparam name="TValue">The value type.</typeparam>
        /// <param name="property">The property.</param>
        /// <param name="value">The value.</param>
        protected void SetElementProperty<TValue>(DependencyProperty property, TValue value)
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

            var doneCreating = WaitEventFactory.Create(isCompleted: false, useSlim: true);
            var thread = new Thread(() =>
            {
                PresentationSource = new HostedPresentationSource(Host);
                doneCreating.Complete();
                Element = CreateHostedElement();
                PresentationSource.RootVisual = Element;
                Element.SizeChanged += (snd, eva) =>
                {
                    if (eva.PreviousSize == default || eva.NewSize == default)
                        Dispatcher.Invoke(InvalidateMeasure);
                };

                // Running the dispatcher makes it run on its own thread
                // and blocks until dispatcher is requested an exit.
                Dispatcher.Run();

                // After the dispatcher is done, dispose the objects
                doneCreating.Dispose();
                PresentationSource.Dispose();
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Priority = ThreadPriority.Highest;
            thread.Start();
            doneCreating.Wait();

            while (Dispatcher.FromThread(thread) == null)
                Thread.Sleep(50);

            ElementDispatcher = Dispatcher.FromThread(thread);
            Dispatcher.BeginInvoke(new Action(InvalidateMeasure));
            RaiseEvent(new RoutedEventArgs(ElementLoadedEvent, this));
        }

        /// <summary>
        /// A presentation source class to root a Visual (a HostVisual) on to its own visual tree.
        /// </summary>
        /// <seealso cref="FrameworkElement" />
        private sealed class HostedPresentationSource : PresentationSource, IDisposable
        {
            private readonly VisualTarget HostConnector;
            private readonly AtomicBoolean m_IsDisposed = new AtomicBoolean(false);

            /// <summary>
            /// Initializes a new instance of the <see cref="HostedPresentationSource"/> class.
            /// </summary>
            /// <param name="host">The host.</param>
            public HostedPresentationSource(HostVisual host) => HostConnector = new VisualTarget(host);

            /// <inheritdoc />
            public override Visual RootVisual
            {
                get => HostConnector.RootVisual;
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
                    if (value is UIElement == false) return;

                    var rootElement = (UIElement)value;
                    rootElement.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    rootElement.Arrange(new Rect(rootElement.DesiredSize));
                }
            }

            /// <inheritdoc />
            public override bool IsDisposed => m_IsDisposed.Value;

            /// <inheritdoc />
            public void Dispose() => Dispose(true);

            /// <inheritdoc />
            protected override CompositionTarget GetCompositionTargetCore() => HostConnector;

            /// <summary>
            /// Releases unmanaged and - optionally - managed resources.
            /// </summary>
            /// <param name="alsoManaged"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
            private void Dispose(bool alsoManaged)
            {
                if (m_IsDisposed.Value) return;
                if (!alsoManaged) return;

                m_IsDisposed.Value = true;
                HostConnector?.Dispose();
            }
        }
    }
}
