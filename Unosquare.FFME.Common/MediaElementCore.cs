namespace Unosquare.FFME
{
    using Commands;
    using Core;
    using System;
    using System.ComponentModel;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Represents a Media Engine that contains underlying streams of audio and/or video.
    /// It the FFmpeg library to perform reading and decoding of media streams.
    /// </summary>
    /// <seealso cref="System.IDisposable" />
    /// <seealso cref="System.ComponentModel.INotifyPropertyChanged" />
    public partial class MediaElementCore : IDisposable
    {
        #region Fields and Property Backing

        /// <summary>
        /// The initialize lock
        /// </summary>
        private static readonly object InitLock = new object();

        /// <summary>
        /// The has intialized flag
        /// </summary>
        private static bool IsIntialized = default(bool);

        /// <summary>
        /// The ffmpeg directory
        /// </summary>
        private static string m_FFmpegDirectory = null;

        /// <summary>
        /// To detect redundant calls
        /// </summary>
        private bool m_IsDisposed = default(bool);

        /// <summary>
        /// The position update timer
        /// </summary>
        private IDispatcherTimer UIPropertyUpdateTimer = null;

        /// <summary>
        /// When position is being set from within this control, this field will
        /// be set to true. This is useful to detect if the user is setting the position
        /// or if the Position property is being driven from within
        /// </summary>
        private AtomicBoolean m_IsPositionUpdating = new AtomicBoolean();

        /// <summary>
        /// Flag when disposing process start but not finished yet
        /// </summary>
        private AtomicBoolean m_IsDisposing = new AtomicBoolean();

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaElementCore" /> class.
        /// </summary>
        /// <param name="parent">The parent.</param>
        /// <param name="isInDesignTime">if set to <c>true</c> [is in design time].</param>
        /// <param name="connector">The connector.</param>
        /// <exception cref="InvalidOperationException">Thrown when the static Initialize method has not been called.</exception>
        internal MediaElementCore(object parent, bool isInDesignTime, IEventConnector connector)
        {
            Parent = parent;
            Logger = new GenericMediaLogger<MediaElementCore>(this);
            Commands = new MediaCommandManager(this);
            Connector = connector;

            // Don't start up timers or any other stoff if we are in design-time
            if (isInDesignTime) return;

            // Check initialization has taken place
            lock (InitLock)
            {
                if (IsIntialized == false)
                {
                    throw new InvalidOperationException(
                        $"{nameof(MediaElementCore)} not initialized. Call the static method {nameof(Initialize)}");
                }
            }

            // The UI Property update timer is responsible for timely updates to properties outside of the worker threads
            // We use the loaded priority because it is the priority right below the Render one.
            UIPropertyUpdateTimer = Platform.CreateTimer(CoreDispatcherPriority.Loaded);
            UIPropertyUpdateTimer.Interval = Constants.UIPropertyUpdateInterval;

            // The tick callback performs the updates
            UIPropertyUpdateTimer.Tick += (s, e) =>
            {
                UpdatePosition(IsOpen ? Clock?.Position ?? TimeSpan.Zero : TimeSpan.Zero);

                if (HasMediaEnded == false && CanReadMorePackets && (IsOpening || IsOpen))
                {
                    var bufferedLength = Container?.Components?.PacketBufferLength ?? 0d;
                    BufferingProgress = Math.Min(1d, bufferedLength / BufferCacheLength);
                    var oldIsBugffering = IsBuffering;
                    var newIsBuffering = bufferedLength < BufferCacheLength;

                    if (oldIsBugffering == false && newIsBuffering)
                        RaiseBufferingStartedEvent();
                    else if (oldIsBugffering && newIsBuffering == false)
                        RaiseBufferingEndedEvent();

                    IsBuffering = HasMediaEnded == false && newIsBuffering;
                }
                else
                {
                    BufferingProgress = 0;
                    IsBuffering = false;
                }

                var downloadProgress = Math.Min(1d, Math.Round((Container?.Components.PacketBufferLength ?? 0d) / DownloadCacheLength, 3));
                if (double.IsNaN(downloadProgress)) downloadProgress = 0;
                DownloadProgress = downloadProgress;
            };

            // Go ahead and fire up the continuous updates
            UIPropertyUpdateTimer.IsEnabled = true;
            UIPropertyUpdateTimer.Start();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the FFmpeg path from which to load the FFmpeg binaries.
        /// You must set this path before setting the Source property for the first time on any instance of this control.
        /// Settng this property when FFmpeg binaries have been registered will throw an exception.
        /// </summary>
        public static string FFmpegDirectory
        {
            get => m_FFmpegDirectory;
            set
            {
                if (IsFFmpegLoaded.Value == false)
                {
                    m_FFmpegDirectory = value;
                    return;
                }

                if ((value?.Equals(m_FFmpegDirectory) ?? false) == false)
                    throw new InvalidOperationException($"Unable to set a new FFmpeg registration path: {value}. FFmpeg binaries have already been registered.");
            }
        }

        /// <summary>
        /// Gets the parent control (platform specific).
        /// </summary>
        public object Parent { get; }

        /// <summary>
        /// Gets a value indicating whether this instance is disposed.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is disposed; otherwise, <c>false</c>.
        /// </value>
        public bool IsDisposed => m_IsDisposed;

        /// <summary>
        /// Gets the platform-specific callbacks.
        /// </summary>
        internal static IPlatform Platform { get; private set; }

        /// <summary>
        /// Gets whether FFmpeg is logged or not
        /// </summary>
        internal static AtomicBoolean IsFFmpegLoaded { get; } = new AtomicBoolean();

        /// <summary>
        /// The logger
        /// </summary>
        internal IMediaLogger Logger { get; }

        /// <summary>
        /// Gets the event connector (platform specific).
        /// </summary>
        internal IEventConnector Connector { get; }

        /// <summary>
        /// When position is being set from within this control, this field will
        /// be set to true. This is useful to detect if the user is setting the position
        /// or if the Position property is being driven from within
        /// </summary>
        internal bool IsPositionUpdating
        {
            get => m_IsPositionUpdating.Value;
            set => m_IsPositionUpdating.Value = value;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Initializes the MedieElementCore.
        /// </summary>
        /// <param name="platform">The platform-specific implementation.</param>
        internal static void Initialize(IPlatform platform)
        {
            lock (InitLock)
            {
                if (IsIntialized)
                    return;

                Platform = platform;
                IsIntialized = true;
            }
        }

        /// <summary>
        /// Updates the position property signaling the update is
        /// coming internally. This is to distinguish between user/binding 
        /// written value to the Position Porperty and value set by this control's
        /// internal clock.
        /// </summary>
        /// <param name="value">The current position.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void UpdatePosition(TimeSpan value)
        {
            if (IsPositionUpdating || IsSeeking)
                return;

            IsPositionUpdating = true;
            Platform.UIEnqueueInvoke(
                CoreDispatcherPriority.DataBind,
                (Action<TimeSpan>)((v) =>
                {
                    if (Position != v)
                    {
                        Position = v;
                        RaisePositionChangedEvent(v);
                    }

                    IsPositionUpdating = false;
                }),
                new object[] { value });
        }

        #endregion

        #region INotifyPropertyChanged Implementation

        /// <summary>
        /// Checks if a property already matches a desired value.  Sets the property and
        /// notifies listeners only when necessary.
        /// </summary>
        /// <typeparam name="T">Type of the property.</typeparam>
        /// <param name="storage">Reference to a property with both getter and setter.</param>
        /// <param name="value">Desired value for the property.</param>
        /// <param name="propertyName">Name of the property used to notify listeners.  This
        /// value is optional and can be provided automatically when invoked from compilers that
        /// support CallerMemberName.</param>
        /// <returns>True if the value was changed, false if the existing value matched the
        /// desired value.</returns>
        private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(storage, value))
                return false;

            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        /// <summary>
        /// Notifies listeners that a property value has changed.
        /// </summary>
        /// <param name="propertyName">Name of the property used to notify listeners.  This
        /// value is optional and can be provided automatically when invoked from compilers
        /// that support <see cref="CallerMemberNameAttribute"/>.</param>
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            Platform.UIInvoke(CoreDispatcherPriority.DataBind, () =>
            {
                Connector?.OnPropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            });
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// Please not that this call is non-blocking/asynchronous.
        /// </summary>
        /// <param name="alsoManaged"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        private async void Dispose(bool alsoManaged)
        {
            if (m_IsDisposed) return;

            if (alsoManaged)
            {
                m_IsDisposing.Value = true;

                // free managed resources -- This is done asynchronously
                await Commands.CloseAsync().ContinueWith(d =>
                {
                    if (Container != null)
                    {
                        Container.Dispose();
                        Container = null;
                    }

                    if (UIPropertyUpdateTimer != null)
                    {
                        UIPropertyUpdateTimer.Stop();
                        UIPropertyUpdateTimer.IsEnabled = false;
                        UIPropertyUpdateTimer = null;
                    }

                    // Dispose the ManualResetEvent objects as they are
                    // backed by unmanaged code
                    m_PacketReadingCycle.Dispose();
                    m_FrameDecodingCycle.Dispose();
                    m_BlockRenderingCycle.Dispose();
                    m_SeekingDone.Dispose();
                });
            }

            m_IsDisposed = true;
        }

        #endregion
    }
}
