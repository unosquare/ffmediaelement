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
    using System.Threading.Tasks;
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
        /// MediaOpeningEvent is a routed event.
        /// </summary>
        public static readonly RoutedEvent MediaInitializingEvent =
            EventManager.RegisterRoutedEvent(
                            nameof(MediaInitializing),
                            RoutingStrategy.Bubble,
                            typeof(EventHandler<MediaInitializingRoutedEventArgs>),
                            typeof(MediaElement));

        /// <summary>
        /// MediaOpeningEvent is a routed event.
        /// </summary>
        public static readonly RoutedEvent MediaOpeningEvent =
            EventManager.RegisterRoutedEvent(
                            nameof(MediaOpening),
                            RoutingStrategy.Bubble,
                            typeof(EventHandler<MediaOpeningRoutedEventArgs>),
                            typeof(MediaElement));

        /// <summary>
        /// MediaChangingEvent is a routed event.
        /// </summary>
        public static readonly RoutedEvent MediaChangingEvent =
            EventManager.RegisterRoutedEvent(
                            nameof(MediaChanging),
                            RoutingStrategy.Bubble,
                            typeof(EventHandler<MediaOpeningRoutedEventArgs>),
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
            add { AddHandler(BufferingStartedEvent, value); }
            remove { RemoveHandler(BufferingStartedEvent, value); }
        }

        /// <summary>
        /// Occurs when buffering of packets was Ended
        /// </summary>
        public event RoutedEventHandler BufferingEnded
        {
            add { AddHandler(BufferingEndedEvent, value); }
            remove { RemoveHandler(BufferingEndedEvent, value); }
        }

        /// <summary>
        /// Occurs when Seeking of packets was started
        /// </summary>
        public event RoutedEventHandler SeekingStarted
        {
            add { AddHandler(SeekingStartedEvent, value); }
            remove { RemoveHandler(SeekingStartedEvent, value); }
        }

        /// <summary>
        /// Occurs when Seeking of packets was Ended
        /// </summary>
        public event RoutedEventHandler SeekingEnded
        {
            add { AddHandler(SeekingEndedEvent, value); }
            remove { RemoveHandler(SeekingEndedEvent, value); }
        }

        /// <summary>
        /// Raised when the media fails to load or a fatal error has occurred which prevents playback.
        /// </summary>
        public event EventHandler<ExceptionRoutedEventArgs> MediaFailed
        {
            add { AddHandler(MediaFailedEvent, value); }
            remove { RemoveHandler(MediaFailedEvent, value); }
        }

        /// <summary>
        /// Raised when the media is opened
        /// </summary>
        public event EventHandler<MediaOpenedRoutedEventArgs> MediaOpened
        {
            add { AddHandler(MediaOpenedEvent, value); }
            remove { RemoveHandler(MediaOpenedEvent, value); }
        }

        /// <summary>
        /// Raised when the media is closed
        /// </summary>
        public event RoutedEventHandler MediaClosed
        {
            add { AddHandler(MediaClosedEvent, value); }
            remove { RemoveHandler(MediaClosedEvent, value); }
        }

        /// <summary>
        /// Raised before the input stream of the media is opened.
        /// Use this method to modify the media options and select streams.
        /// </summary>
        public event EventHandler<MediaOpeningRoutedEventArgs> MediaOpening
        {
            add { AddHandler(MediaOpeningEvent, value); }
            remove { RemoveHandler(MediaOpeningEvent, value); }
        }

        /// <summary>
        /// Raised before a change in media options is applied.
        /// Use this method to modify the selected streams.
        /// </summary>
        public event EventHandler<MediaOpeningRoutedEventArgs> MediaChanging
        {
            add { AddHandler(MediaChangingEvent, value); }
            remove { RemoveHandler(MediaChangingEvent, value); }
        }

        /// <summary>
        /// Raised after a change in media options and components is applied.
        /// </summary>
        public event EventHandler<MediaOpenedRoutedEventArgs> MediaChanged
        {
            add { AddHandler(MediaChangedEvent, value); }
            remove { RemoveHandler(MediaChangedEvent, value); }
        }

        /// <summary>
        /// Raised before the input stream of the media is initialized.
        /// Use this method to modify the input options.
        /// </summary>
        public event EventHandler<MediaInitializingRoutedEventArgs> MediaInitializing
        {
            add { AddHandler(MediaInitializingEvent, value); }
            remove { RemoveHandler(MediaInitializingEvent, value); }
        }

        /// <summary>
        /// Raised when the corresponding media ends.
        /// </summary>
        public event RoutedEventHandler MediaEnded
        {
            add { AddHandler(MediaEndedEvent, value); }
            remove { RemoveHandler(MediaEndedEvent, value); }
        }

        /// <summary>
        /// Occurs when media position is changed
        /// </summary>
        public event EventHandler<PositionChangedRoutedEventArgs> PositionChanged
        {
            add { AddHandler(PositionChangedEvent, value); }
            remove { RemoveHandler(PositionChangedEvent, value); }
        }

        /// <summary>
        /// Occurs when media state is changed
        /// </summary>
        public event EventHandler<MediaStateChangedRoutedEventArgs> MediaStateChanged
        {
            add { AddHandler(MediaStateChangedEvent, value); }
            remove { RemoveHandler(MediaStateChangedEvent, value); }
        }

        #endregion

        #region Helper Methods

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
            var constructor = (typeof(ExceptionRoutedEventArgs) as TypeInfo).DeclaredConstructors.First();
            return constructor.Invoke(new object[] { routedEvent, sender, errorException }) as ExceptionRoutedEventArgs;
        }

        /// <summary>
        /// Raises the FFmpeg message logged.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="MediaLogMessage"/> instance containing the event data.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void RaiseFFmpegMessageLogged(object sender, MediaLogMessage e)
        {
            FFmpegMessageLogged?.Invoke(sender, new MediaLogMessageEventArgs(e));
        }

        /// <summary>
        /// Raises the message logged event.
        /// </summary>
        /// <param name="e">The <see cref="MediaLogMessage"/> instance containing the event data.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RaiseMessageLoggedEvent(MediaLogMessage e)
        {
            MessageLogged?.Invoke(this, new MediaLogMessageEventArgs(e));
        }

        /// <summary>
        /// Raises the media failed event.
        /// </summary>
        /// <param name="ex">The ex.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Task RaiseMediaFailedEvent(Exception ex)
        {
            LogEventStart(MediaFailedEvent);
            MediaCore?.Log(MediaLogMessageType.Error, $"Media Failure - {ex?.GetType()}: {ex?.Message}");
            return GuiContext.Current.EnqueueInvoke(() =>
            {
                RaiseEvent(CreateExceptionRoutedEventArgs(MediaFailedEvent, this, ex));
                LogEventDone(MediaFailedEvent);
            });
        }

        /// <summary>
        /// Raises the media opened event.
        /// </summary>
        /// <param name="mediaInfo">The media information.</param>
        /// <returns>
        /// A <see cref="Task" /> representing the asynchronous operation.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Task RaiseMediaOpenedEvent(MediaInfo mediaInfo)
        {
            LogEventStart(MediaOpenedEvent);
            return GuiContext.Current.EnqueueInvoke(() =>
            {
                RaiseEvent(new MediaOpenedRoutedEventArgs(
                    MediaOpenedEvent,
                    this,
                    mediaInfo));
                LogEventDone(MediaOpenedEvent);
            });
        }

        /// <summary>
        /// Raises the media closed event.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Task RaiseMediaClosedEvent()
        {
            LogEventStart(MediaClosedEvent);
            return GuiContext.Current.EnqueueInvoke(() =>
            {
                RaiseEvent(new RoutedEventArgs(MediaClosedEvent, this));
                LogEventDone(MediaClosedEvent);
            });
        }

        /// <summary>
        /// Raises the media opening event.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="mediaInfo">The media information.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Task RaiseMediaOpeningEvent(MediaOptions options, MediaInfo mediaInfo)
        {
            LogEventStart(MediaOpeningEvent);
            return GuiContext.Current.EnqueueInvoke(() =>
            {
                RaiseEvent(new MediaOpeningRoutedEventArgs(
                    MediaOpeningEvent,
                    this,
                    options,
                    mediaInfo));

                LogEventDone(MediaOpeningEvent);
            });
        }

        /// <summary>
        /// Raises the media changing event.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="mediaInfo">The media information.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Task RaiseMediaChangingEvent(MediaOptions options, MediaInfo mediaInfo)
        {
            LogEventStart(MediaChangingEvent);
            return GuiContext.Current.EnqueueInvoke(() =>
            {
                RaiseEvent(new MediaOpeningRoutedEventArgs(
                    MediaChangingEvent,
                    this,
                    options,
                    mediaInfo));

                LogEventDone(MediaChangingEvent);
            });
        }

        /// <summary>
        /// Raises the media changed event.
        /// </summary>
        /// <param name="mediaInfo">The media information.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Task RaiseMediaChangedEvent(MediaInfo mediaInfo)
        {
            LogEventStart(MediaChangedEvent);
            return GuiContext.Current.EnqueueInvoke(() =>
            {
                RaiseEvent(new MediaOpenedRoutedEventArgs(
                    MediaChangedEvent,
                    this,
                    mediaInfo));

                LogEventDone(MediaChangedEvent);
            });
        }

        /// <summary>
        /// Raises the media opening event.
        /// </summary>
        /// <param name="config">The container configuration options.</param>
        /// <param name="url">The URL.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Task RaiseMediaInitializingEvent(ContainerConfiguration config, string url)
        {
            LogEventStart(MediaInitializingEvent);
            return GuiContext.Current.EnqueueInvoke(() =>
            {
                RaiseEvent(new MediaInitializingRoutedEventArgs(
                    MediaInitializingEvent,
                    this,
                    config,
                    url));

                LogEventDone(MediaInitializingEvent);
            });
        }

        /// <summary>
        /// Raises the position changed event.
        /// </summary>
        /// <param name="oldValue">The old value.</param>
        /// <param name="newValue">The new value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RaisePositionChangedEvent(TimeSpan oldValue, TimeSpan newValue)
        {
            GuiContext.Current.EnqueueInvoke(() =>
            {
                RaiseEvent(new PositionChangedRoutedEventArgs(PositionChangedEvent, this, MediaCore.State, oldValue, newValue));
            });
        }

        /// <summary>
        /// Raises the media state changed event.
        /// </summary>
        /// <param name="oldValue">The old value.</param>
        /// <param name="newValue">The new value.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Task RaiseMediaStateChangedEvent(MediaState oldValue, MediaState newValue)
        {
            return GuiContext.Current.EnqueueInvoke(() =>
            {
                RaiseEvent(new MediaStateChangedRoutedEventArgs(
                    MediaStateChangedEvent, this, oldValue, newValue));
            });
        }

        /// <summary>
        /// Raises the buffering started event.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Task RaiseBufferingStartedEvent()
        {
            LogEventStart(BufferingStartedEvent);
            return GuiContext.Current.EnqueueInvoke(() =>
            {
                RaiseEvent(new RoutedEventArgs(BufferingStartedEvent, this));
                LogEventDone(BufferingStartedEvent);
            });
        }

        /// <summary>
        /// Raises the buffering ended event.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Task RaiseBufferingEndedEvent()
        {
            LogEventStart(BufferingEndedEvent);
            return GuiContext.Current.EnqueueInvoke(() =>
            {
                RaiseEvent(new RoutedEventArgs(BufferingEndedEvent, this));
                LogEventDone(BufferingEndedEvent);
            });
        }

        /// <summary>
        /// Raises the Seeking started event.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Task RaiseSeekingStartedEvent()
        {
            LogEventStart(SeekingStartedEvent);
            return GuiContext.Current.EnqueueInvoke(() =>
            {
                RaiseEvent(new RoutedEventArgs(SeekingStartedEvent, this));
                LogEventDone(SeekingStartedEvent);
            });
        }

        /// <summary>
        /// Raises the Seeking ended event.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Task RaiseSeekingEndedEvent()
        {
            LogEventStart(SeekingEndedEvent);
            return GuiContext.Current.EnqueueInvoke(() =>
            {
                RaiseEvent(new RoutedEventArgs(SeekingEndedEvent, this));
                LogEventDone(SeekingEndedEvent);
            });
        }

        /// <summary>
        /// Raises the media ended event.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Task RaiseMediaEndedEvent()
        {
            LogEventStart(MediaEndedEvent);
            return GuiContext.Current.EnqueueInvoke(() =>
            {
                RaiseEvent(new RoutedEventArgs(MediaEndedEvent, this));
                LogEventDone(MediaEndedEvent);
            });
        }

        /// <summary>
        /// Notifies listeners that a property value has changed.
        /// This must be called from a UI thread.
        /// </summary>
        /// <param name="propertyName">Name of the property used to notify listeners.  This
        /// value is optional and can be provided automatically when invoked from compilers
        /// that support <see cref="CallerMemberNameAttribute"/>.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RaisePropertyChangedEvent(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

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
