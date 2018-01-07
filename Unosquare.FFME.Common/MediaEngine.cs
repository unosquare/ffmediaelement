namespace Unosquare.FFME
{
    using Commands;
    using Core;
    using Primitives;
    using Shared;
    using System;
    using System.ComponentModel;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Represents a Media Engine that contains underlying streams of audio and/or video.
    /// It the FFmpeg library to perform reading and decoding of media streams.
    /// </summary>
    /// <seealso cref="System.IDisposable" />
    /// <seealso cref="System.ComponentModel.INotifyPropertyChanged" />
    public partial class MediaEngine : IDisposable, IMediaLogger
    {
        #region Fields and Property Backing

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
        private AtomicBoolean m_IsPositionUpdating = new AtomicBoolean(false);

        /// <summary>
        /// Flag when disposing process start but not finished yet
        /// </summary>
        private AtomicBoolean m_IsDisposing = new AtomicBoolean(false);

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaEngine" /> class.
        /// </summary>
        /// <param name="parent">The associated parent object.</param>
        /// <param name="connector">The parent implementing connector methods.</param>
        /// <exception cref="InvalidOperationException">Thrown when the static Initialize method has not been called.</exception>
        public MediaEngine(object parent, IMediaConnector connector)
        {
            // Assiciate the parent as the media connector that implements the callbacks
            Parent = parent;
            Connector = connector;
            Commands = new MediaCommandManager(this);

            // Don't start up timers or any other stuff if we are in design-time
            if (Platform.IsInDesignTime) return;

            // Check initialization has taken place
            lock (InitLock)
            {
                if (IsIntialized == false)
                {
                    throw new InvalidOperationException(
                        $"{nameof(MediaEngine)} not initialized. Call the static method {nameof(Initialize)}");
                }
            }

            // The UI Property update timer is responsible for timely updates to properties outside of the worker threads
            // We use the loaded priority because it is the priority right below the Render one.
            UIPropertyUpdateTimer = MediaEngine.Platform.CreateGuiTimer(ActionPriority.Loaded);
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
        /// Gets the associated parent object.
        /// </summary>
        public object Parent { get; }

        /// <summary>
        /// Gets the event connector (platform specific).
        /// </summary>
        public IMediaConnector Connector { get; }

        /// <summary>
        /// Gets a value indicating whether this instance is disposed.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is disposed; otherwise, <c>false</c>.
        /// </value>
        public bool IsDisposed => m_IsDisposed;

        /// <summary>
        /// Gets whether FFmpeg is logged or not
        /// </summary>
        internal static AtomicBoolean IsFFmpegLoaded { get; } = new AtomicBoolean(false);

        #endregion

        #region Methods

        /// <summary>
        /// Logs the specified message into the logger queue.
        /// </summary>
        /// <param name="messageType">Type of the message.</param>
        /// <param name="message">The message.</param>
        public void Log(MediaLogMessageType messageType, string message)
        {
            Utils.Log(this, messageType, message);
        }

        /// <summary>
        /// Retrieves the registered renderer for the given media type.
        /// </summary>
        /// <param name="mediaType">Type of the media.</param>
        /// <returns>The media renderer</returns>
        public IMediaRenderer RetrieveRenderer(MediaType mediaType)
        {
            return Renderers[mediaType];
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
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
            Platform.GuiEnqueueInvoke(
                ActionPriority.DataBind,
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
            Platform.GuiInvoke(ActionPriority.DataBind, (Action)(() =>
            {
                this.Connector?.OnPropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }));
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
