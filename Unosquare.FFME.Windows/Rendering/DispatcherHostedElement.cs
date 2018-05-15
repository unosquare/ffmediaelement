namespace Unosquare.FFME.Rendering
{
    using System;
    using System.Collections;
    using System.ComponentModel;
    using System.Threading;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Threading;

    /// <summary>
    /// A WPF control that runs on its own dispatcher but maintains
    /// composition capabilities with the rest of the UI
    /// </summary>
    /// <seealso cref="FrameworkElement" />
    internal abstract class DispatcherHostedElement : FrameworkElement
    {
        /// <summary>
        /// The thread separated control loaded event
        /// </summary>
        public static readonly RoutedEvent HostedElementLoadedEvent = EventManager.RegisterRoutedEvent(
            nameof(HostedElementLoaded),
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(DispatcherHostedElement));

        /// <summary>
        /// Occurs when the thread separated control loads.
        /// </summary>
        public event RoutedEventHandler HostedElementLoaded
        {
            add { AddHandler(HostedElementLoadedEvent, value); }
            remove { RemoveHandler(HostedElementLoadedEvent, value); }
        }

        /// <summary>
        /// This is the element that is disconnected from the main UI thread
        /// but that is still connected to the composition of the main UI via the
        /// host visual (Connected Visual)
        /// </summary>
        public FrameworkElement HostedElement { get; protected set; }

        /// <summary>
        /// Gets or sets the host visual connected to the main UI dispatcher.
        /// This is the composition gateway
        /// </summary>
        public HostVisual ConnectedVisual { get; protected set; }

        /// <summary>
        /// Gets or sets the visual target.
        /// </summary>
        public DispatcherPresentationSource PresentationSource { get; protected set; }

        /// <summary>
        /// Gets the separate thread dispatcher of the hosted element.
        /// </summary>
        public Dispatcher HostedDispatcher => HostedElement?.Dispatcher;

        /// <inheritdoc/>
        protected override int VisualChildrenCount
        {
            get { return ConnectedVisual != null ? 1 : 0; }
        }

        /// <inheritdoc/>
        protected override IEnumerator LogicalChildren
        {
            get
            {
                if (ConnectedVisual != null)
                {
                    yield return ConnectedVisual;
                }
            }
        }

        /// <summary>
        /// Creates the thread separated control.
        /// </summary>
        /// <returns>The target element</returns>
        protected abstract FrameworkElement CreateHostedElement();

        /// <summary>
        /// Loads the thread separated control.
        /// </summary>
        protected virtual void LoadHostedElement()
        {
            if (HostedDispatcher != null)
                return;

            var controlCreated = new AutoResetEvent(false);
            ConnectedVisual = new HostVisual();
            AddLogicalChild(ConnectedVisual);
            AddVisualChild(ConnectedVisual);

            if (DesignerProperties.GetIsInDesignMode(this))
                return;

            var thread = new Thread(() =>
            {
                HostedElement = CreateHostedElement();

                if (HostedElement == null)
                    return;

                PresentationSource = new DispatcherPresentationSource(ConnectedVisual)
                {
                    RootVisual = HostedElement
                };

                Dispatcher.BeginInvoke(new Action(() => { InvalidateMeasure(); }));
                controlCreated.Set();
                Dispatcher.Run();
                PresentationSource.Dispose();
            })
            {
                IsBackground = true
            };

            thread.SetApartmentState(ApartmentState.STA);
            thread.Priority = ThreadPriority.Highest;
            thread.Start();

            controlCreated.WaitOne();
            controlCreated.Dispose();
        }

        /// <summary>
        /// Unloads the thread separated control.
        /// </summary>
        protected virtual void UnloadHostedElement()
        {
            if (HostedDispatcher == null)
                return;

            HostedDispatcher.InvokeShutdown();
            RemoveLogicalChild(ConnectedVisual);
            RemoveVisualChild(ConnectedVisual);

            ConnectedVisual = null;
            HostedElement = null;
        }

        /// <inheritdoc/>
        protected override void OnInitialized(EventArgs e)
        {
            Loaded += (sender, args) =>
            {
                LoadHostedElement();
                RaiseEvent(new RoutedEventArgs(HostedElementLoadedEvent, this));
            };

            Unloaded += (sender, args) => { UnloadHostedElement(); };

            base.OnInitialized(e);
        }

        /// <inheritdoc/>
        protected override Size MeasureOverride(Size constraint)
        {
            var targetSize = default(Size);

            HostedDispatcher?.InvokeAsync(new Action(() =>
            {
                HostedElement?.Measure(constraint);
                targetSize = HostedElement?.DesiredSize ?? default;
            })).Wait(TimeSpan.FromMilliseconds(50));

            return targetSize;
        }

        /// <inheritdoc/>
        protected override Size ArrangeOverride(Size finalSize)
        {
            HostedDispatcher?.InvokeAsync(new Action(() => { HostedElement?.Arrange(new Rect(finalSize)); }));
            return finalSize;
        }

        /// <inheritdoc/>
        protected override Visual GetVisualChild(int index)
        {
            if (index == 0)
            {
                return ConnectedVisual;
            }

            throw new IndexOutOfRangeException(nameof(index));
        }
    }
}
