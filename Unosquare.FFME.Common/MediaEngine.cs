namespace Unosquare.FFME
{
    using Commands;
    using Core;
    using Primitives;
    using Shared;
    using System;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Represents a Media Engine that contains underlying streams of audio and/or video.
    /// It uses the fantastic FFmpeg library to perform reading and decoding of media streams.
    /// </summary>
    /// <seealso cref="IMediaLogger" />
    /// <seealso cref="IDisposable" />
    public partial class MediaEngine : IDisposable, IMediaLogger
    {
        #region Fields and Property Backing

        /// <summary>
        /// To detect redundant calls
        /// </summary>
        private bool m_IsDisposed = default(bool);

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
            // Associate the parent as the media connector that implements the callbacks
            Parent = parent;
            Connector = connector;
            Commands = new MediaCommandManager(this);
            State = new MediaEngineState(this);

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
        }

        #endregion

        #region Properties

        /// <summary>
        /// Contains the Media Status
        /// </summary>
        public MediaEngineState State { get; }

        /// <summary>
        /// Gets the internal real time clock position.
        /// This is different from the position property and it is useful
        /// in computing the latency between position and the wall clock.
        /// </summary>
        public TimeSpan WallClock => Clock?.Position ?? TimeSpan.Zero;

        /// <summary>
        /// Provides stream, chapter and program info of the underlying media.
        /// Returns null when no media is loaded.
        /// </summary>
        public MediaInfo MediaInfo => Container?.MediaInfo;

        /// <summary>
        /// Gets a value indicating whether this instance is disposed.
        /// </summary>
        public bool IsDisposed
        {
            get => m_IsDisposed;
            private set => m_IsDisposed = value;
        }

        /// <summary>
        /// Gets a value indicating whether this instance is disposing.
        /// </summary>
        public bool IsDisposing
        {
            get => m_IsDisposing.Value;
            private set => m_IsDisposing.Value = value;
        }

        /// <summary>
        /// Gets the associated parent object.
        /// </summary>
        public object Parent { get; }

        /// <summary>
        /// Gets the event connector (platform specific).
        /// </summary>
        internal IMediaConnector Connector { get; }

        /// <summary>
        /// Gets the guessed buffered bytes in the packet queue per second.
        /// If bitrate information is available, then it returns the bitrate converted to byte rate.
        /// Returns null if it has not been guessed.
        /// </summary>
        internal ulong? GuessedByteRate { get; set; }

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
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            // TODO: Looks like MediaElement is not calling this when closing the container?
            Dispose(true);
        }

        /// <summary>
        /// Resets all the buffering properties to their defaults.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ResetBufferingProperties()
        {
            const int MinimumValidBitrate = 512 * 1024; // 524kbps
            const int StartingCacheLength = 512 * 1024; // Half a megabyte

            GuessedByteRate = default(ulong?);

            if (Container == null)
            {
                State.IsBuffering = false;
                State.BufferCacheLength = 0;
                State.DownloadCacheLength = 0;
                State.BufferingProgress = 0;
                State.DownloadProgress = 0;
                return;
            }

            if (Container.MediaBitrate > MinimumValidBitrate)
            {
                State.BufferCacheLength = (int)Container.MediaBitrate / 8;
                GuessedByteRate = (ulong)State.BufferCacheLength;
            }
            else
            {
                State.BufferCacheLength = StartingCacheLength;
            }

            State.DownloadCacheLength = State.BufferCacheLength * (State.IsLiveStream ? 30 : 4);
            State.IsBuffering = false;
            State.BufferingProgress = 0;
            State.DownloadProgress = 0;
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
                1d, Math.Round(packetBufferLength / State.BufferCacheLength, 3));
            State.BufferingProgress = double.IsNaN(bufferingProgress) ? 0 : bufferingProgress;

            // Update the download progress
            var downloadProgress = Math.Min(
                1d, Math.Round(packetBufferLength / State.DownloadCacheLength, 3));
            State.DownloadProgress = double.IsNaN(downloadProgress) ? 0 : downloadProgress;

            // IsBuffering and BufferingProgress
            if (State.HasMediaEnded == false && CanReadMorePackets && (State.IsOpening || State.IsOpen))
            {
                var wasBuffering = State.IsBuffering;
                var isNowBuffering = packetBufferLength < State.BufferCacheLength;
                State.IsBuffering = isNowBuffering;

                if (wasBuffering == false && isNowBuffering)
                    SendOnBufferingStarted();
                else if (wasBuffering && isNowBuffering == false)
                    SendOnBufferingEnded();
            }
            else
            {
                State.IsBuffering = false;
            }
        }

        /// <summary>
        /// Guesses the bitrate of the input stream.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void GuessBufferingProperties()
        {
            if (GuessedByteRate != null || Container == null || Container.Components == null)
                return;

            // Capture the read bytes of a 1-second buffer
            var bytesReadSoFar = Container.Components.LifetimeBytesRead;
            var shortestDuration = TimeSpan.MaxValue;
            var currentDuration = TimeSpan.Zero;

            foreach (var t in Container.Components.MediaTypes)
            {
                if (t != MediaType.Audio && t != MediaType.Video)
                    continue;

                currentDuration = Blocks[t].LifetimeBlockDuration;

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
                GuessedByteRate = (ulong)(1.2 * bytesReadSoFar / shortestDuration.TotalSeconds);
                State.BufferCacheLength = Convert.ToInt32(GuessedByteRate);
                State.DownloadCacheLength = State.BufferCacheLength * (State.IsLiveStream ? 30 : 4);
            }
        }

        /// <summary>
        /// Updates the posiion property if not seeking.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void UpdatePosiionProperty()
        {
            if (State.IsSeeking) return;
            State.Position = State.IsOpen ? Clock?.Position ?? TimeSpan.Zero : TimeSpan.Zero;
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
            if (IsDisposed) return;

            IsDisposing = true;
            if (alsoManaged)
            {
                // free managed resources -- This is done asynchronously
                await Commands.CloseAsync().ContinueWith(d =>
                {
                    // Dispose the container
                    Container?.Dispose();
                    Container = null;

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

            IsDisposed = true;
        }

        #endregion
    }
}
