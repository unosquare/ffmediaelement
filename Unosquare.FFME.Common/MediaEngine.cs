namespace Unosquare.FFME
{
    using Commands;
    using Core;
    using Shared;
    using System;

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
        private bool m_IsDisposed = default;

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
        /// in computing things like real-time latency in a render cycle.
        /// </summary>
        public TimeSpan WallClock => State.IsOpen ? Clock.Position : TimeSpan.Zero;

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
        /// Gets the associated parent object.
        /// </summary>
        public object Parent { get; }

        /// <summary>
        /// Gets the event connector (platform specific).
        /// </summary>
        internal IMediaConnector Connector { get; }

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
        public void Dispose() => Dispose(true);

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Disposes the preloaded subtitles.
        /// </summary>
        internal void DisposePreloadedSubtitles()
        {
            m_PreloadedSubtitles?.Dispose();
            m_PreloadedSubtitles = null;
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// Please not that this call is non-blocking/asynchronous.
        /// </summary>
        /// <param name="alsoManaged"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        private void Dispose(bool alsoManaged)
        {
            if (IsDisposed) return;

            // Run the close command immediately
            if (BeginSynchronousCommand() == false) return;

            // Dispose the wait handle: No more command accepted from this point forward.
            SynchronousCommandDone.Dispose();

            try
            {
                var closeCommand = new CloseCommand(Commands);
                closeCommand.RunSynchronously();
            }
            catch { throw; }
            finally
            {
                IsDisposed = true;
            }

            // Dispose the container
            Container?.Dispose();
            Container = null;

            // Dispose the RTC
            Clock.Dispose();

            // Dispose the Wait Event objects as they are
            // backed by unmanaged code
            m_PacketReadingCycle.Dispose();
            m_FrameDecodingCycle.Dispose();
            m_BlockRenderingCycle.Dispose();
            m_SeekingDone.Dispose();
        }

        #endregion
    }
}
