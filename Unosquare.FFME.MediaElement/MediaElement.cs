namespace Unosquare.FFME
{
    using Engine;
    using System;
    using Events;
    using System.ComponentModel;

#if WINDOWS_UWP
    using Windows.UI.Xaml.Media;
#else
    using System.Windows.Controls;
#endif

    public partial class MediaElement : ILoggingHandler, ILoggingSource, INotifyPropertyChanged
    {
        #region Events

        /// <summary>
        /// Occurs when a logging message from the FFmpeg library has been received.
        /// This is shared across all instances of Media Elements.
        /// </summary>
        /// <remarks>
        /// This event is raised on a background thread.
        /// All interaction with UI elements requires calls on their corresponding dispatcher.
        /// </remarks>
        public static event EventHandler<MediaLogMessageEventArgs> FFmpegMessageLogged;

        /// <summary>
        /// Occurs when a logging message has been logged.
        /// This does not include FFmpeg messages.
        /// </summary>
        /// <remarks>
        /// This event is raised on a background thread.
        /// All interaction with UI elements requires calls on their corresponding dispatcher.
        /// </remarks>
        public event EventHandler<MediaLogMessageEventArgs> MessageLogged;

        /// <summary>
        /// Raised before the input stream of the media is initialized.
        /// Use this method to modify the input options.
        /// </summary>
        /// <remarks>
        /// This event is raised on a background thread.
        /// All interaction with UI elements requires calls on their corresponding dispatcher.
        /// </remarks>
        public event EventHandler<MediaInitializingEventArgs> MediaInitializing;

        /// <summary>
        /// Raised before the input stream of the media is opened.
        /// Use this method to modify the media options and select streams.
        /// </summary>
        /// <remarks>
        /// This event is raised on a background thread.
        /// All interaction with UI elements requires calls on their corresponding dispatcher.
        /// </remarks>
        public event EventHandler<MediaOpeningEventArgs> MediaOpening;

        /// <summary>
        /// Raised before a change in media options is applied.
        /// Use this method to modify the selected streams.
        /// </summary>
        /// <remarks>
        /// This event is raised on a background thread.
        /// All interaction with UI elements requires calls on their corresponding dispatcher.
        /// </remarks>
        public event EventHandler<MediaOpeningEventArgs> MediaChanging;

        /// <summary>
        /// Raised when a packet is read from the input stream. Useful for capturing streams.
        /// This event is not raised on the UI thread and the pointers in the event arguments
        /// are only valid for the call. If you need to keep a queue you will need to clone and
        /// release the allocated memory yourself by using clone and release methods in the native
        /// FFmpeg API.
        /// </summary>
        public event EventHandler<PacketReadEventArgs> PacketRead;

        /// <summary>
        /// Raised when an audio frame is decoded from input stream. Useful for capturing streams.
        /// This event is not raised on the UI thread and the pointers in the event arguments
        /// are only valid for the call. If you need to keep a queue you will need to clone and
        /// release the allocated memory yourself by using clone and release methods in the native
        /// FFmpeg API.
        /// </summary>
        public event EventHandler<FrameDecodedEventArgs> AudioFrameDecoded;

        /// <summary>
        /// Raised when a video frame is decoded from input stream. Useful for capturing streams.
        /// This event is not raised on the UI thread and the pointers in the event arguments
        /// are only valid for the call. If you need to keep a queue you will need to clone and
        /// release the allocated memory yourself by using clone and release methods in the native
        /// FFmpeg API.
        /// </summary>
        public event EventHandler<FrameDecodedEventArgs> VideoFrameDecoded;

        /// <summary>
        /// Raised when a subtitle is decoded from input stream. Useful for capturing streams.
        /// This event is not raised on the UI thread and the pointers in the event arguments
        /// are only valid for the call. If you need to keep a queue you will need to clone and
        /// release the allocated memory yourself by using clone and release methods in the native
        /// FFmpeg API.
        /// </summary>
        public event EventHandler<SubtitleDecodedEventArgs> SubtitleDecoded;

        /// <inheritdoc />
        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        /// <inheritdoc />
        ILoggingHandler ILoggingSource.LoggingHandler => this;

        /// <summary>
        /// Provides access to the underlying media engine driving this control.
        /// This property is intended for advanced usages only.
        /// </summary>
        internal MediaEngine MediaCore { get; private set; }

        /// <inheritdoc />
        void ILoggingHandler.HandleLogMessage(MediaLogMessage message) =>
            RaiseMessageLoggedEvent(message);

#if WINDOWS_UWP
        internal static MediaElementState PlaybackStatusToMediaState(PlaybackStatus status)
        {
            switch (status)
            {
                case PlaybackStatus.Close:
                    return MediaElementState.Closed;
                case PlaybackStatus.Manual:
                    return MediaElementState.Buffering;
                case PlaybackStatus.Pause:
                    return MediaElementState.Paused;
                case PlaybackStatus.Play:
                    return MediaElementState.Playing;
                case PlaybackStatus.Stop:
                    return MediaElementState.Stopped;
                default:
                    return MediaElementState.Closed;
            }
        }
#else
        internal static MediaState PlaybackStatusToMediaState(PlaybackStatus status) => (MediaState)status;
#endif
    }
}
