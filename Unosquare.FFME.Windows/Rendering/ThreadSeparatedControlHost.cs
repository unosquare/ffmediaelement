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
    /// <seealso cref="System.Windows.FrameworkElement" />
    internal abstract class ThreadSeparatedControlHost : FrameworkElement
    {
        /// <summary>
        /// The thread separated control loaded event
        /// </summary>
        public static readonly RoutedEvent ThreadSeparatedControlLoadedEvent = EventManager.RegisterRoutedEvent(
            nameof(ThreadSeparatedControlLoaded),
            RoutingStrategy.Bubble,
            typeof(EventHandler<ThreadSeparatedControlLoadedRoutedEventArgs>),
            typeof(ThreadSeparatedControlHost));

        /// <summary>
        /// Occurs when the thread separated control loads.
        /// </summary>
        public event RoutedEventHandler ThreadSeparatedControlLoaded
        {
            add { AddHandler(ThreadSeparatedControlLoadedEvent, value); }
            remove { RemoveHandler(ThreadSeparatedControlLoadedEvent, value); }
        }

        /// <summary>
        /// Gets or sets the target element.
        /// </summary>
        public FrameworkElement TargetElement { get; protected set; }

        /// <summary>
        /// Gets or sets the host visual.
        /// </summary>
        public HostVisual HostVisual { get; protected set; }

        /// <summary>
        /// Gets or sets the visual target.
        /// </summary>
        public VisualTargetPresentationSource VisualTarget { get; protected set; }

        /// <summary>
        /// Gets the separate thread dispatcher.
        /// </summary>
        public Dispatcher SeparateThreadDispatcher
        {
            get { return TargetElement == null ? null : TargetElement.Dispatcher; }
        }

        /// <inheritdoc/>
        protected override int VisualChildrenCount
        {
            get { return HostVisual != null ? 1 : 0; }
        }

        /// <inheritdoc/>
        protected override IEnumerator LogicalChildren
        {
            get
            {
                if (HostVisual != null)
                {
                    yield return HostVisual;
                }
            }
        }

        /// <summary>
        /// Creates the thread separated control.
        /// </summary>
        /// <returns>The target element</returns>
        protected abstract FrameworkElement CreateThreadSeparatedControl();

        /// <summary>
        /// Loads the thread separated control.
        /// </summary>
        protected virtual void LoadThreadSeparatedControl()
        {
            if (SeparateThreadDispatcher != null)
                return;

            var controlCreated = new AutoResetEvent(false);
            HostVisual = new HostVisual();
            AddLogicalChild(HostVisual);
            AddVisualChild(HostVisual);

            if (DesignerProperties.GetIsInDesignMode(this))
                return;

            var thread = new Thread(() =>
            {
                TargetElement = CreateThreadSeparatedControl();

                if (TargetElement == null)
                    return;

                VisualTarget = new VisualTargetPresentationSource(HostVisual)
                {
                    RootVisual = TargetElement
                };

                Dispatcher.BeginInvoke(new Action(() => { InvalidateMeasure(); }));
                controlCreated.Set();
                Dispatcher.Run();
                VisualTarget.Dispose();
            })
            {
                IsBackground = true
            };

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            controlCreated.WaitOne();
            controlCreated.Dispose();
        }

        /// <summary>
        /// Unloads the thread separated control.
        /// </summary>
        protected virtual void UnloadThreadSeparatedControl()
        {
            if (SeparateThreadDispatcher == null)
                return;

            SeparateThreadDispatcher.InvokeShutdown();
            RemoveLogicalChild(HostVisual);
            RemoveVisualChild(HostVisual);

            HostVisual = null;
            TargetElement = null;
        }

        /// <inheritdoc/>
        protected override void OnInitialized(EventArgs e)
        {
            Loaded += (sender, args) =>
            {
                LoadThreadSeparatedControl();
                RaiseEvent(new ThreadSeparatedControlLoadedRoutedEventArgs(ThreadSeparatedControlLoadedEvent, this));
            };
            Unloaded += (sender, args) => { UnloadThreadSeparatedControl(); };

            base.OnInitialized(e);
        }

        /// <inheritdoc/>
        protected override Size MeasureOverride(System.Windows.Size constraint)
        {
            var targetSize = default(Size);

            if (TargetElement != null)
            {
                TargetElement.Dispatcher.Invoke(DispatcherPriority.Normal,
                    new Action(() => TargetElement.Measure(constraint)));
                targetSize = TargetElement.DesiredSize;
            }

            return targetSize;
        }

        /// <inheritdoc/>
        protected override Size ArrangeOverride(Size finalSize)
        {
            if (TargetElement != null)
            {
                TargetElement.Dispatcher.Invoke(DispatcherPriority.Normal,
                    new Action(() => TargetElement.Arrange(new Rect(finalSize))));
            }

            return finalSize;
        }

        /// <inheritdoc/>
        protected override Visual GetVisualChild(int index)
        {
            if (index == 0)
            {
                return HostVisual;
            }

            throw new IndexOutOfRangeException("index");
        }
    }

    /// <summary>
    /// The loaded event arguments for thread-separated controls.
    /// </summary>
    /// <seealso cref="RoutedEventArgs" />
    internal class ThreadSeparatedControlLoadedRoutedEventArgs : RoutedEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ThreadSeparatedControlLoadedRoutedEventArgs"/> class.
        /// </summary>
        /// <param name="routedEvent">The routed event identifier for this instance of the <see cref="T:System.Windows.RoutedEventArgs" /> class.</param>
        /// <param name="source">An alternate source that will be reported when the event is handled. This pre-populates the <see cref="P:System.Windows.RoutedEventArgs.Source" /> property.</param>
        public ThreadSeparatedControlLoadedRoutedEventArgs(RoutedEvent routedEvent, object source)
            : base(routedEvent, source) { }
    }
}
