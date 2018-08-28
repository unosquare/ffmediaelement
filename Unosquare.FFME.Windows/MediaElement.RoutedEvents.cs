namespace Unosquare.FFME
{
    using Events;
    using Platform;
    using Shared;
    using System;
    using System.ComponentModel;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Windows;
    using System.Windows.Controls;

    public partial class MediaElement
    {
        #region Routed Event Registrations

        /// <summary>
        /// BufferingStarted is a routed event
        /// </summary>
        public static readonly RoutedEvent BufferingStartedEvent =
            EventManager.RegisterRoutedEvent(
                    nameof(BufferingStarted),
                    RoutingStrategy.Bubble,
                    typeof(RoutedEventHandler),
                    typeof(MediaElement));

        /// <summary>
        /// BufferingEnded is a routed event
        /// </summary>
        public static readonly RoutedEvent BufferingEndedEvent =
            EventManager.RegisterRoutedEvent(
                    nameof(BufferingEnded),
                    RoutingStrategy.Bubble,
                    typeof(RoutedEventHandler),
                    typeof(MediaElement));

        /// <summary>
        /// SeekingStarted is a routed event
        /// </summary>
        public static readonly RoutedEvent SeekingStartedEvent =
            EventManager.RegisterRoutedEvent(
                    nameof(SeekingStarted),
                    RoutingStrategy.Bubble,
                    typeof(RoutedEventHandler),
                    typeof(MediaElement));

        /// <summary>
        /// SeekingEnded is a routed event
        /// </summary>
        public static readonly RoutedEvent SeekingEndedEvent =
            EventManager.RegisterRoutedEvent(
                    nameof(SeekingEnded),
                    RoutingStrategy.Bubble,
                    typeof(RoutedEventHandler),
                    typeof(MediaElement));

        /// <summary>
        /// MediaFailedEvent is a routed event.
        /// </summary>
        public static readonly RoutedEvent MediaFailedEvent =
            EventManager.RegisterRoutedEvent(
                            nameof(MediaFailed),
                            RoutingStrategy.Bubble,
                            typeof(EventHandler<ExceptionRoutedEventArgs>),
                            typeof(MediaElement));

        /// <summary>
        /// MediaOpened is a routed event.
        /// </summary>
        public static readonly RoutedEvent MediaOpenedEvent =
            EventManager.RegisterRoutedEvent(
                            nameof(MediaOpened),
                            RoutingStrategy.Bubble,
                            typeof(EventHandler<MediaOpenedRoutedEventArgs>),
                            typeof(MediaElement));

        /// <summary>
        /// MediaClosed is a routed event.
        /// </summary>
        public static readonly RoutedEvent MediaClosedEvent =
            EventManager.RegisterRoutedEvent(
                            nameof(MediaClosed),
                            RoutingStrategy.Bubble,
                            typeof(RoutedEventHandler),
                            typeof(MediaElement));

        /// <summary>
        /// MediaChangedEvent is a routed event.
        /// </summary>
        public static readonly RoutedEvent MediaChangedEvent =
            EventManager.RegisterRoutedEvent(
                            nameof(MediaChanged),
                            RoutingStrategy.Bubble,
                            typeof(EventHandler<MediaOpenedRoutedEventArgs>),
                            typeof(MediaElement));

        /// <summary>
        /// PositionChanged is a routed event
        /// </summary>
        public static readonly RoutedEvent PositionChangedEvent =
            EventManager.RegisterRoutedEvent(
                            nameof(PositionChanged),
                            RoutingStrategy.Bubble,
                            typeof(EventHandler<PositionChangedRoutedEventArgs>),
                            typeof(MediaElement));

        /// <summary>
        /// MediaStateChanged is a routed event
        /// </summary>
        public static readonly RoutedEvent MediaStateChangedEvent =
            EventManager.RegisterRoutedEvent(
                            nameof(MediaStateChanged),
                            RoutingStrategy.Bubble,
                            typeof(EventHandler<MediaStateChangedRoutedEventArgs>),
                            typeof(MediaElement));

        /// <summary>
        /// MediaEnded is a routed event
        /// </summary>
        public static readonly RoutedEvent MediaEndedEvent =
            EventManager.RegisterRoutedEvent(
                            nameof(MediaEnded),
                            RoutingStrategy.Bubble,
                            typeof(RoutedEventHandler),
                            typeof(MediaElement));

        #endregion

        #region CLR Accessors

        /// <summary>
        /// Occurs when buffering of packets was started
        /// </summary>
        public event RoutedEventHandler BufferingStarted
        {
            add => AddHandler(BufferingStartedEvent, value);
            remove => RemoveHandler(BufferingStartedEvent, value);
        }

        /// <summary>
        /// Occurs when buffering of packets was Ended
        /// </summary>
        public event RoutedEventHandler BufferingEnded
        {
            add => AddHandler(BufferingEndedEvent, value);
            remove => RemoveHandler(BufferingEndedEvent, value);
        }

        /// <summary>
        /// Occurs when Seeking of packets was started
        /// </summary>
        public event RoutedEventHandler SeekingStarted
        {
            add => AddHandler(SeekingStartedEvent, value);
            remove => RemoveHandler(SeekingStartedEvent, value);
        }

        /// <summary>
        /// Occurs when Seeking of packets was Ended
        /// </summary>
        public event RoutedEventHandler SeekingEnded
        {
            add => AddHandler(SeekingEndedEvent, value);
            remove => RemoveHandler(SeekingEndedEvent, value);
        }

        /// <summary>
        /// Raised when the media is opened
        /// </summary>
        public event EventHandler<MediaOpenedRoutedEventArgs> MediaOpened
        {
            add => AddHandler(MediaOpenedEvent, value);
            remove => RemoveHandler(MediaOpenedEvent, value);
        }

        /// <summary>
        /// Raised after a change in media options and components is applied.
        /// </summary>
        public event EventHandler<MediaOpenedRoutedEventArgs> MediaChanged
        {
            add => AddHandler(MediaChangedEvent, value);
            remove => RemoveHandler(MediaChangedEvent, value);
        }

        /// <summary>
        /// Raised when the media is closed
        /// </summary>
        public event RoutedEventHandler MediaClosed
        {
            add => AddHandler(MediaClosedEvent, value);
            remove => RemoveHandler(MediaClosedEvent, value);
        }

        /// <summary>
        /// Raised when the corresponding media ends.
        /// </summary>
        public event RoutedEventHandler MediaEnded
        {
            add => AddHandler(MediaEndedEvent, value);
            remove => RemoveHandler(MediaEndedEvent, value);
        }

        /// <summary>
        /// Occurs when media position is changed
        /// </summary>
        public event EventHandler<PositionChangedRoutedEventArgs> PositionChanged
        {
            add => AddHandler(PositionChangedEvent, value);
            remove => RemoveHandler(PositionChangedEvent, value);
        }

        /// <summary>
        /// Occurs when media state is changed
        /// </summary>
        public event EventHandler<MediaStateChangedRoutedEventArgs> MediaStateChanged
        {
            add => AddHandler(MediaStateChangedEvent, value);
            remove => RemoveHandler(MediaStateChangedEvent, value);
        }

        /// <summary>
        /// Raised when the media fails to load or a fatal error has occurred which prevents playback.
        /// </summary>
        public event EventHandler<ExceptionRoutedEventArgs> MediaFailed
        {
            add => AddHandler(MediaFailedEvent, value);
            remove => RemoveHandler(MediaFailedEvent, value);
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Creates a new instance of exception routed event arguments.
        /// This method exists because the constructor has not been made public for that class.
        /// </summary>
        /// <param name="routedEvent">The routed event.</param>
        /// <param name="sender">The sender.</param>
        /// <param name="errorException">The error exception.</param>
        /// <returns>The event arguments</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ExceptionRoutedEventArgs CreateExceptionRoutedEventArgs(RoutedEvent routedEvent, object sender, Exception errorException)
        {
            var constructor = (typeof(ExceptionRoutedEventArgs) as TypeInfo)?.DeclaredConstructors.First();
            return constructor?.Invoke(new[] { routedEvent, sender, errorException }) as ExceptionRoutedEventArgs;
        }

        #endregion

        #region Non-UI Event Raisers

        /// <summary>
        /// Raises the FFmpeg message logged.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="MediaLogMessage"/> instance containing the event data.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void RaiseFFmpegMessageLogged(object sender, MediaLogMessage e) =>
            FFmpegMessageLogged?.Invoke(sender, new MediaLogMessageEventArgs(e));

        /// <summary>
        /// Raises the message logged event.
        /// </summary>
        /// <param name="e">The <see cref="MediaLogMessage"/> instance containing the event data.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RaiseMessageLoggedEvent(MediaLogMessage e) =>
            MessageLogged?.Invoke(this, new MediaLogMessageEventArgs(e));

        /// <summary>
        /// Raises the media initializing event.
        /// </summary>
        /// <param name="config">The container configuration options.</param>
        /// <param name="url">The URL.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RaiseMediaInitializingEvent(ContainerConfiguration config, string url) =>
            MediaInitializing?.Invoke(this, new MediaInitializingEventArgs(config, url));

        /// <summary>
        /// Raises the media opening event.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="mediaInfo">The media information.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RaiseMediaOpeningEvent(MediaOptions options, MediaInfo mediaInfo) =>
            MediaOpening?.Invoke(this, new MediaOpeningEventArgs(options, mediaInfo));

        /// <summary>
        /// Raises the media changing event.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="mediaInfo">The media information.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RaiseMediaChangingEvent(MediaOptions options, MediaInfo mediaInfo) =>
            MediaChanging?.Invoke(this, new MediaOpeningEventArgs(options, mediaInfo));

        #endregion

        #region UI Event Raisers

        /// <summary>
        /// Raises the media failed event.
        /// </summary>
        /// <param name="ex">The ex.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void PostMediaFailedEvent(Exception ex)
        {
            LogEventStart(MediaFailedEvent);
            MediaCore?.Log(MediaLogMessageType.Error, $"Media Failure - {ex?.GetType()}: {ex?.Message}");
            GuiContext.Current.EnqueueInvoke(() =>
            {
                RaiseEvent(CreateExceptionRoutedEventArgs(
                    MediaFailedEvent, this, ex));
                LogEventDone(MediaFailedEvent);
            });
        }

        /// <summary>
        /// Raises the media opened event.
        /// </summary>
        /// <param name="mediaInfo">The media information.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void PostMediaOpenedEvent(MediaInfo mediaInfo)
        {
            LogEventStart(MediaOpenedEvent);
            GuiContext.Current.EnqueueInvoke(() =>
            {
                RaiseEvent(new MediaOpenedRoutedEventArgs(
                    MediaOpenedEvent, this, mediaInfo));
                LogEventDone(MediaOpenedEvent);
            });
        }

        /// <summary>
        /// Raises the media closed event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void PostMediaClosedEvent()
        {
            LogEventStart(MediaClosedEvent);
            GuiContext.Current.EnqueueInvoke(() =>
            {
                RaiseEvent(new RoutedEventArgs(MediaClosedEvent, this));
                LogEventDone(MediaClosedEvent);
            });
        }

        /// <summary>
        /// Raises the media changed event.
        /// </summary>
        /// <param name="mediaInfo">The media information.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void PostMediaChangedEvent(MediaInfo mediaInfo)
        {
            LogEventStart(MediaChangedEvent);
            GuiContext.Current.EnqueueInvoke(() =>
            {
                RaiseEvent(new MediaOpenedRoutedEventArgs(
                    MediaChangedEvent, this, mediaInfo));
                LogEventDone(MediaChangedEvent);
            });
        }

        /// <summary>
        /// Raises the position changed event.
        /// </summary>
        /// <param name="oldValue">The old value.</param>
        /// <param name="newValue">The new value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void PostPositionChangedEvent(TimeSpan oldValue, TimeSpan newValue)
        {
            // Event logging disabled because this happens too often.
            GuiContext.Current.EnqueueInvoke(() =>
            {
                RaiseEvent(new PositionChangedRoutedEventArgs(
                    PositionChangedEvent, this, MediaCore.State, oldValue, newValue));
            });
        }

        /// <summary>
        /// Raises the media state changed event.
        /// </summary>
        /// <param name="oldValue">The old value.</param>
        /// <param name="newValue">The new value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void PostMediaStateChangedEvent(MediaState oldValue, MediaState newValue)
        {
            LogEventStart(MediaStateChangedEvent);
            GuiContext.Current.EnqueueInvoke(() =>
            {
                RaiseEvent(new MediaStateChangedRoutedEventArgs(
                    MediaStateChangedEvent, this, oldValue, newValue));
                LogEventDone(MediaStateChangedEvent);
            });
        }

        /// <summary>
        /// Raises the buffering started event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void PostBufferingStartedEvent()
        {
            LogEventStart(BufferingStartedEvent);
            GuiContext.Current.EnqueueInvoke(() =>
            {
                RaiseEvent(new RoutedEventArgs(BufferingStartedEvent, this));
                LogEventDone(BufferingStartedEvent);
            });
        }

        /// <summary>
        /// Raises the buffering ended event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void PostBufferingEndedEvent()
        {
            LogEventStart(BufferingEndedEvent);
            GuiContext.Current.EnqueueInvoke(() =>
            {
                RaiseEvent(new RoutedEventArgs(BufferingEndedEvent, this));
                LogEventDone(BufferingEndedEvent);
            });
        }

        /// <summary>
        /// Raises the Seeking started event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void PostSeekingStartedEvent()
        {
            LogEventStart(SeekingStartedEvent);
            GuiContext.Current.EnqueueInvoke(() =>
            {
                RaiseEvent(new RoutedEventArgs(SeekingStartedEvent, this));
                LogEventDone(SeekingStartedEvent);
            });
        }

        /// <summary>
        /// Raises the Seeking ended event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void PostSeekingEndedEvent()
        {
            LogEventStart(SeekingEndedEvent);
            GuiContext.Current.EnqueueInvoke(() =>
            {
                RaiseEvent(new RoutedEventArgs(SeekingEndedEvent, this));
                LogEventDone(SeekingEndedEvent);
            });
        }

        /// <summary>
        /// Raises the media ended event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void PostMediaEndedEvent()
        {
            LogEventStart(MediaEndedEvent);
            GuiContext.Current.EnqueueInvoke(() =>
            {
                RaiseEvent(new RoutedEventArgs(MediaEndedEvent, this));
                LogEventDone(MediaEndedEvent);
            });
        }

        /// <summary>
        /// Notifies listeners that a property value has changed.
        /// </summary>
        /// <param name="propertyName">Name of the property used to notify listeners.  This
        /// value is optional and can be provided automatically when invoked from compilers
        /// that support <see cref="CallerMemberNameAttribute"/>.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void NotifyPropertyChangedEvent(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        #endregion

        #region Event Logging

        /// <summary>
        /// Logs the start of an event
        /// </summary>
        /// <param name="e">The event.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void LogEventStart(RoutedEvent e)
        {
            if (WindowsPlatform.Instance.IsInDebugMode)
                MediaCore?.Log(MediaLogMessageType.Trace, $"EVENT START: {e.Name}");
        }

        /// <summary>
        /// Logs the end of an event.
        /// </summary>
        /// <param name="e">The event.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void LogEventDone(RoutedEvent e)
        {
            if (WindowsPlatform.Instance.IsInDebugMode)
                MediaCore?.Log(MediaLogMessageType.Trace, $"EVENT DONE : {e.Name}");
        }

        #endregion
    }
}
