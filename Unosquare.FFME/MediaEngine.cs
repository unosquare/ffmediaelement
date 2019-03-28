namespace Unosquare.FFME
{
    using Commands;
    using Diagnostics;
    using Engine;
    using Primitives;
    using System;
    using Workers;

    /// <summary>
    /// Represents a Media Engine that contains underlying streams of audio and/or video.
    /// It uses the fantastic FFmpeg library to perform reading and decoding of media streams.
    /// </summary>
    /// <seealso cref="ILoggingHandler" />
    /// <seealso cref="IDisposable" />
    internal sealed partial class MediaEngine : IDisposable, ILoggingSource, ILoggingHandler
    {
        private readonly AtomicBoolean m_IsDisposed = new AtomicBoolean(false);

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
            Timing = new TimingController(this);

            // Don't start up timers or any other stuff if we are in design-time
            if (Platform.IsInDesignTime) return;

            // Check initialization has taken place
            lock (InitLock)
            {
                if (IsInitialized == false)
                {
                    throw new InvalidOperationException(
                        $"{nameof(MediaEngine)} not initialized. Call the static method {nameof(Initialize)}");
                }
            }
        }

        #endregion

        #region Properties

        /// <inheritdoc />
        ILoggingHandler ILoggingSource.LoggingHandler => this;

        /// <summary>
        /// Contains the Media Status.
        /// </summary>
        public MediaEngineState State { get; }

        /// <summary>
        /// Provides stream, chapter and program info of the underlying media.
        /// Returns null when no media is loaded.
        /// </summary>
        public MediaInfo MediaInfo => Container?.MediaInfo;

        /// <summary>
        /// Gets a value indicating whether this instance is disposed.
        /// </summary>
        public bool IsDisposed => m_IsDisposed.Value;

        /// <summary>
        /// Gets the associated parent object.
        /// </summary>
        public object Parent { get; }

        /// <summary>
        /// Gets the real-time playback clock position.
        /// </summary>
        public TimeSpan PlaybackPosition => Timing.Position();

        /// <summary>
        /// Represents a real-time time clock controller.
        /// </summary>
        internal TimingController Timing { get; }

        /// <summary>
        /// Gets the media options. Do not modify the properties of this object directly
        /// as it may cause unstable playback or crashes.
        /// </summary>
        internal MediaOptions MediaOptions => Container?.MediaOptions;

        /// <summary>
        /// Gets the event connector (platform specific).
        /// </summary>
        internal IMediaConnector Connector { get; }

        #endregion

        #region Methods

        /// <inheritdoc />
        void ILoggingHandler.HandleLogMessage(MediaLogMessage message) =>
            SendOnMessageLogged(message);

        /// <inheritdoc />
        public void Dispose()
        {
            if (m_IsDisposed == true) return;
            m_IsDisposed.Value = true;

            // Dispose of commands. This closes the
            // Media automatically and signals an exit
            // This also causes the Container to get disposed.
            Commands.Dispose();

            // Reset the RTC
            ResetPlaybackPosition();
        }

        #endregion
    }
}
