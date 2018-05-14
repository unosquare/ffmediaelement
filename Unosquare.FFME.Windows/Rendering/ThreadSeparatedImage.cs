namespace Unosquare.FFME.Rendering
{
    using System;
    using System.ComponentModel;
    using System.Threading;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using System.Windows.Threading;

    /// <summary>
    /// Hosts an Image as a thread-separated control
    /// </summary>
    /// <seealso cref="ThreadSeparatedControlHost" />
    internal sealed class ThreadSeparatedImage : ThreadSeparatedControlHost
    {
        private static Dispatcher m_CommonDispatcher;
        private readonly object SyncLock = new object();
        private HorizontalAlignment _horizontalContentAlignment = default;
        private ScaleTransform _scaleTransform;
        private ImageSource _source;
        private Stretch _stretch = Stretch.Uniform;
        private StretchDirection _stretchDirection = StretchDirection.Both;
        private VerticalAlignment _verticalContentAlignment = default;

        /// <summary>
        /// Gets the common dispatcher shared by all thread-separated controls.
        /// Use this dispatcher to call methods on the hosted thread-separated controls.
        /// </summary>
        public static Dispatcher CommonDispatcher
        {
            get
            {
                if (m_CommonDispatcher == null)
                {
                    Thread separateThread = new Thread(() =>
                    {
                        Dispatcher.Run();
                    })
                    {
                        IsBackground = true
                    };
                    separateThread.SetApartmentState(ApartmentState.STA);
                    separateThread.Priority = ThreadPriority.Highest;

                    separateThread.Start();

                    while (Dispatcher.FromThread(separateThread) == null)
                    {
                        Thread.Sleep(50);
                    }
                    m_CommonDispatcher = Dispatcher.FromThread(separateThread);
                }

                return m_CommonDispatcher;
            }
        }

        /// <summary>
        /// Gets the internal image control.
        /// </summary>
        public Image InternalImageControl { get; private set; }

        /// <summary>
        /// Gets or sets the source.
        /// </summary>
        public ImageSource Source
        {
            get
            {
                return _source;
            }
            set
            {
                if (_source != value)
                {
                    _source = value;

                    if (InternalImageControl == null) return;

                    CommonDispatcher.Invoke(new Action(() => { InternalImageControl.Source = value; }));
                }
            }
        }

        /// <summary>
        /// Gets or sets the stretch.
        /// </summary>
        public Stretch Stretch
        {
            get
            {
                return _stretch;
            }
            set
            {
                if (_stretch != value)
                {
                    _stretch = value;

                    if (InternalImageControl == null) return;

                    CommonDispatcher.Invoke(new Action(() => { InternalImageControl.Stretch = value; }));
                }
            }
        }

        /// <summary>
        /// Gets or sets the stretch direction.
        /// </summary>
        public StretchDirection StretchDirection
        {
            get
            {
                return _stretchDirection;
            }
            set
            {
                if (_stretchDirection != value)
                {
                    _stretchDirection = value;

                    if (InternalImageControl == null) return;

                    CommonDispatcher.Invoke(new Action(() => { InternalImageControl.StretchDirection = value; }));
                }
            }
        }

        /// <summary>
        /// Gets or sets the scale transform.
        /// </summary>
        public ScaleTransform ScaleTransform
        {
            get
            {
                return _scaleTransform;
            }
            set
            {
                if (_scaleTransform != value)
                {
                    _scaleTransform = value;

                    if (InternalImageControl == null) return;

                    CommonDispatcher.Invoke(new Action(() => { InternalImageControl.LayoutTransform = value; }));
                }
            }
        }

        /// <summary>
        /// Gets or sets the horizontal content alignment.
        /// </summary>
        public HorizontalAlignment HorizontalContentAlignment
        {
            get
            {
                return _horizontalContentAlignment;
            }
            set
            {
                if (_horizontalContentAlignment != value)
                {
                    _horizontalContentAlignment = value;

                    if (InternalImageControl == null)
                        return;

                    CommonDispatcher.Invoke(
                        new Action(() => { InternalImageControl.HorizontalAlignment = value; }));
                }
            }
        }

        /// <summary>
        /// Gets or sets the vertical content alignment.
        /// </summary>
        public VerticalAlignment VerticalContentAlignment
        {
            get
            {
                return _verticalContentAlignment;
            }
            set
            {
                if (_verticalContentAlignment != value)
                {
                    _verticalContentAlignment = value;

                    if (InternalImageControl == null) return;

                    CommonDispatcher.Invoke(new Action(() => { InternalImageControl.VerticalAlignment = value; }));
                }
            }
        }

        /// <summary>
        /// Creates the thread separated control.
        /// </summary>
        /// <returns>
        /// The target element
        /// </returns>
        protected override FrameworkElement CreateThreadSeparatedControl()
        {
            InternalImageControl = new Image
            {
                Source = Source,
                Stretch = Stretch,
                StretchDirection = StretchDirection,
                HorizontalAlignment = HorizontalContentAlignment,
                VerticalAlignment = VerticalContentAlignment,
                LayoutTransform = ScaleTransform
            };

            return InternalImageControl;
        }

        /// <summary>
        /// Loads the thread separated control.
        /// </summary>
        protected override void LoadThreadSeparatedControl()
        {
            HostVisual = new HostVisual();

            AddLogicalChild(HostVisual);
            AddVisualChild(HostVisual);

            if (DesignerProperties.GetIsInDesignMode(this))
                return;

            CommonDispatcher.BeginInvoke(new Action(() =>
            {
                lock (SyncLock)
                {
                    if (HostVisual == null)
                        return; // can happen if control was loaded then immediately unloaded

                    if (TargetElement == null)
                    {
                        TargetElement = CreateThreadSeparatedControl();
                    }

                    if (TargetElement == null)
                        return;

                    VisualTarget = new VisualTargetPresentationSource(HostVisual)
                    {
                        RootVisual = TargetElement
                    };
                }

                Dispatcher.BeginInvoke(new Action(() => { InvalidateMeasure(); }));
            }));
        }

        /// <summary>
        /// Unloads the thread separated control.
        /// </summary>
        protected override void UnloadThreadSeparatedControl()
        {
            lock (SyncLock)
            {
                RemoveLogicalChild(HostVisual);
                RemoveVisualChild(HostVisual);

                HostVisual = null;
                TargetElement = null;
            }
        }
    }
}
