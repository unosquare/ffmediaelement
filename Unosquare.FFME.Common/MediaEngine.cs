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
        private readonly object DisposeLock = new object();
        private bool m_IsDisposed = false;

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
            Commands = new CommandManager(this);
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
        public TimeSpan WallClock => State.IsOpen || State.IsOpening ? Clock.Position : TimeSpan.Zero;

        /// <summary>
        /// Provides stream, chapter and program info of the underlying media.
        /// Returns null when no media is loaded.
        /// </summary>
        public MediaInfo MediaInfo => Container?.MediaInfo;

        /// <summary>
        /// Gets a value indicating whether this instance is disposed.
        /// </summary>
        public bool IsDisposed { get { lock (DisposeLock) return m_IsDisposed; } }

        /// <summary>
        /// Gets the associated parent object.
        /// </summary>
        public object Parent { get; }

        /// <summary>
        /// Represents a real-time time measuring device.
        /// Rendering media should occur as requested by the clock.
        /// </summary>
        internal RealTimeClock Clock { get; } = new RealTimeClock();

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
            PreloadedSubtitles?.Dispose();
            PreloadedSubtitles = null;
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// Please not that this call is non-blocking/asynchronous.
        /// </summary>
        /// <param name="alsoManaged"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        private void Dispose(bool alsoManaged)
        {
            lock (DisposeLock)
            {
                if (m_IsDisposed) return;

                try
                {
                    // Dispose of commands. This closes the
                    // Media automatically and signals an exit
                    // This also causes the Container to get disposed.
                    Commands.Dispose();

                    // Dispose the RTC
                    Clock.Dispose();

                    // Dispose the Wait Event objects as they are
                    // backed by unmanaged code
                    PacketReadingCycle.Dispose();
                    FrameDecodingCycle.Dispose();
                    BlockRenderingCycle.Dispose();
                }
                catch { throw; }
                finally
                {
                    m_IsDisposed = true;
                }
            }
        }

        #endregion
    }
}
