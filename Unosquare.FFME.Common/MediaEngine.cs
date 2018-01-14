namespace Unosquare.FFME
{
    using Commands;
    using Core;
    using Primitives;
    using Shared;
    using System;
    using System.Runtime.CompilerServices;
    using System.Threading;

    /// <summary>
    /// Represents a Media Engine that contains underlying streams of audio and/or video.
    /// It uses the fantastic FFmpeg library to perform reading and decoding of media streams.
    /// </summary>
    /// <seealso cref="Unosquare.FFME.Shared.IMediaLogger" />
    /// <seealso cref="System.IDisposable" />
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
        private Timer PropertyUpdateTimer = null;

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

            var isRunningPropertyUpdates = false;

            // The Property update timer is responsible for timely updates to properties outside of the worker threads
            PropertyUpdateTimer = new Timer((s) =>
            {
                if (isRunningPropertyUpdates || m_IsDisposing.Value)
                    return;

                isRunningPropertyUpdates = true;

                try
                {
                    UpdatePosition(IsOpen ? Clock?.Position ?? TimeSpan.Zero : TimeSpan.Zero);
                    UpdateBufferingProperties();
                }
                catch (Exception ex)
                {
                    Log(MediaLogMessageType.Error, $"{nameof(PropertyUpdateTimer)} callabck failed. {ex.GetType()}: {ex.Message}");
                }
                finally
                {
                    isRunningPropertyUpdates = false;
                }
            },
            this, // the state argument passed on to the ticker
            (int)Defaults.TimerMediumPriorityInterval.TotalMilliseconds,
            (int)Defaults.TimerMediumPriorityInterval.TotalMilliseconds);
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

        #endregion

        #region Methods

        /// <summary>
        /// Logs the specified message into the logger queue.
        /// </summary>
        /// <param name="messageType">Type of the message.</param>
        /// <param name="message">The message.</param>
        public void Log(MediaLogMessageType messageType, string message)
        {
            LoggingWorker.Log(this, messageType, message);
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

            try
            {
                IsPositionUpdating = true;
                Position = value;
            }
            catch { }
            finally
            {
                IsPositionUpdating = false;
            }
        }

        /// <summary>
        /// Resets all the buffering properties to their defaults.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ResetBufferingProperties()
        {
            const int MinimumBitrate = 512 * 1024 * 8;

            BufferBytesPerSecond = default(ulong?);

            if (Container == null)
            {
                IsBuffering = false;
                BufferCacheLength = 0;
                DownloadCacheLength = 0;
                BufferingProgress = 0;
                DownloadProgress = 0;
                return;
            }

            if (Container.MediaBitrate > MinimumBitrate)
            {
                BufferCacheLength = (int)Container.MediaBitrate / 8;
                BufferBytesPerSecond = (ulong)BufferCacheLength;
            }
            else
            {
                BufferCacheLength = MinimumBitrate / 8;
            }

            DownloadCacheLength = BufferCacheLength * (IsLiveStream ? 30 : 4);
            IsBuffering = false;
            BufferingProgress = 0;
            DownloadProgress = 0;
        }

        /// <summary>
        /// Updates the buffering properties: IsBuffering, BufferingProgress, DownloadProgress.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void UpdateBufferingProperties()
        {
            var packetBufferLength = Container?.Components?.PacketBufferLength ?? 0d;

            // Update the buffering progress
            var bufferingProgress = Math.Min(
                1d, Math.Round(packetBufferLength / BufferCacheLength, 3));
            BufferingProgress = double.IsNaN(bufferingProgress) ? 0 : bufferingProgress;

            // Update the download progress
            var downloadProgress = Math.Min(
                1d, Math.Round(packetBufferLength / DownloadCacheLength, 3));
            DownloadProgress = double.IsNaN(downloadProgress) ? 0 : downloadProgress;

            // IsBuffering and BufferingProgress
            if (HasMediaEnded == false && CanReadMorePackets && (IsOpening || IsOpen))
            {
                var wasBuffering = IsBuffering;
                var isNowBuffering = packetBufferLength < BufferCacheLength;
                IsBuffering = isNowBuffering;

                if (wasBuffering == false && isNowBuffering)
                    SendOnBufferingStarted();
                else if (wasBuffering && isNowBuffering == false)
                    SendOnBufferingEnded();
            }
            else
            {
                IsBuffering = false;
            }
        }

        /// <summary>
        /// Guesses the bitrate of the input stream.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void GuessBufferingProperties()
        {
            if (BufferBytesPerSecond != null || Container == null || Container.Components == null)
                return;

            // Capture the read bytes of a 1-second buffer
            var bytesRead = Container.Components.TotalBytesRead;
            var shortestDuration = TimeSpan.MaxValue;
            var currentDuration = TimeSpan.Zero;

            foreach (var t in Container.Components.MediaTypes)
            {
                if (t != MediaType.Audio && t != MediaType.Video)
                    continue;

                currentDuration = Blocks[t].HistoricalDuration;

                if (currentDuration.TotalSeconds < 1)
                {
                    shortestDuration = TimeSpan.Zero;
                    break;
                }

                if (currentDuration < shortestDuration)
                    shortestDuration = currentDuration;
            }

            if (shortestDuration.TotalSeconds >= 1 && shortestDuration != TimeSpan.MaxValue)
            {
                BufferBytesPerSecond = (ulong)(bytesRead / shortestDuration.TotalSeconds);
                BufferCacheLength = Convert.ToInt32(BufferBytesPerSecond);
                DownloadCacheLength = BufferCacheLength * (IsLiveStream ? 30 : 4);
            }
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
            SendOnPropertyChanged(propertyName);
            return true;
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

                    if (PropertyUpdateTimer != null)
                    {
                        PropertyUpdateTimer.Dispose();
                        PropertyUpdateTimer = null;
                    }

                    // Dispose the RTC
                    Clock?.Dispose();

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
