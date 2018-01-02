namespace Unosquare.FFME
{
    using Core;
    using Platform;
    using Shared;
    using System;
    using System.ComponentModel;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Windows;
    using System.Windows.Threading;

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
                            typeof(RoutedEventHandler),
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
        public static readonly RoutedEvent MediaOpeningEvent =
            EventManager.RegisterRoutedEvent(
                            nameof(MediaOpening),
                            RoutingStrategy.Bubble,
                            typeof(EventHandler<MediaOpeningRoutedEventArgs>),
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
        public event RoutedEventHandler MediaOpened
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
        /// Use this method to modify the input options.
        /// </summary>
        public event EventHandler<MediaOpeningRoutedEventArgs> MediaOpening
        {
            add { AddHandler(MediaOpeningEvent, value); }
            remove { RemoveHandler(MediaOpeningEvent, value); }
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

        #endregion

        #region Helper Methods

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
        /// Creates a new instance of exception routed event arguments.
        /// This method exists because the constructor has not been made public for that class.
        /// </summary>
        /// <param name="routedEvent">The routed event.</param>
        /// <param name="sender">The sender.</param>
        /// <param name="errorException">The error exception.</param>
        /// <returns>The event arguments</returns>
        internal static ExceptionRoutedEventArgs CreateExceptionRoutedEventArgs(RoutedEvent routedEvent, object sender, Exception errorException)
        {
            var constructor = (typeof(ExceptionRoutedEventArgs) as TypeInfo).DeclaredConstructors.First();
            return constructor.Invoke(new object[] { routedEvent, sender, errorException }) as ExceptionRoutedEventArgs;
        }

        /// <summary>
        /// Raises the media failed event.
        /// </summary>
        /// <param name="ex">The ex.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RaiseMediaFailedEvent(Exception ex)
        {
            LogEventStart(MediaFailedEvent);
            Logger.Log(MediaLogMessageType.Error, $"Media Failure - {ex?.GetType()}: {ex?.Message}");
            WindowsGui.UIInvoke(DispatcherPriority.DataBind, () => { RaiseEvent(CreateExceptionRoutedEventArgs(MediaFailedEvent, this, ex)); });
            LogEventDone(MediaFailedEvent);
        }

        /// <summary>
        /// Raises the media opened event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RaiseMediaOpenedEvent()
        {
            LogEventStart(MediaOpenedEvent);
            WindowsGui.UIInvoke(DispatcherPriority.DataBind, () => { RaiseEvent(new RoutedEventArgs(MediaOpenedEvent, this)); });
            LogEventDone(MediaOpenedEvent);
        }

        /// <summary>
        /// Raises the media closed event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RaiseMediaClosedEvent()
        {
            LogEventStart(MediaClosedEvent);
            WindowsGui.UIInvoke(DispatcherPriority.DataBind, () => { RaiseEvent(new RoutedEventArgs(MediaClosedEvent, this)); });
            LogEventDone(MediaClosedEvent);
        }

        /// <summary>
        /// Raises the media opening event.
        /// </summary>
        /// <param name="mediaOptions">The media options.</param>
        /// <param name="mediaInfo">The media information.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RaiseMediaOpeningEvent(MediaOptions mediaOptions, MediaInfo mediaInfo)
        {
            LogEventStart(MediaOpeningEvent);
            WindowsGui.UIInvoke(DispatcherPriority.DataBind, () =>
            {
                RaiseEvent(new MediaOpeningRoutedEventArgs(
                    MediaOpeningEvent, 
                    this, 
                    mediaOptions, 
                    mediaInfo));
            });

            LogEventDone(MediaOpeningEvent);
        }

        /// <summary>
        /// Raises the position changed event.
        /// </summary>
        /// <param name="position">The position.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RaisePositionChangedEvent(TimeSpan position)
        {
            WindowsGui.UIInvoke(DispatcherPriority.DataBind, () =>
            {
                RaiseEvent(new PositionChangedRoutedEventArgs(
                    PositionChangedEvent,
                    this,
                    position));
            });
        }

        /// <summary>
        /// Raises the buffering started event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RaiseBufferingStartedEvent()
        {
            LogEventStart(BufferingStartedEvent);
            WindowsGui.UIInvoke(DispatcherPriority.DataBind, () => { RaiseEvent(new RoutedEventArgs(BufferingStartedEvent, this)); });
            LogEventDone(BufferingStartedEvent);
        }

        /// <summary>
        /// Raises the buffering ended event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RaiseBufferingEndedEvent()
        {
            LogEventStart(BufferingEndedEvent);
            WindowsGui.UIInvoke(DispatcherPriority.DataBind, () => { RaiseEvent(new RoutedEventArgs(BufferingEndedEvent, this)); });
            LogEventDone(BufferingEndedEvent);
        }

        /// <summary>
        /// Raises the Seeking started event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RaiseSeekingStartedEvent()
        {
            LogEventStart(SeekingStartedEvent);
            WindowsGui.UIInvoke(DispatcherPriority.DataBind, () => { RaiseEvent(new RoutedEventArgs(SeekingStartedEvent, this)); });
            LogEventDone(SeekingStartedEvent);
        }

        /// <summary>
        /// Raises the Seeking ended event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RaiseSeekingEndedEvent()
        {
            LogEventStart(SeekingEndedEvent);
            WindowsGui.UIInvoke(DispatcherPriority.DataBind, () => { RaiseEvent(new RoutedEventArgs(SeekingEndedEvent, this)); });
            LogEventDone(SeekingEndedEvent);
        }

        /// <summary>
        /// Raises the media ended event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RaiseMediaEndedEvent()
        {
            LogEventStart(MediaEndedEvent);
            WindowsGui.UIInvoke(DispatcherPriority.DataBind, () => { RaiseEvent(new RoutedEventArgs(MediaEndedEvent, this)); });
            LogEventDone(MediaEndedEvent);
        }

        /// <summary>
        /// Notifies listeners that a property value has changed.
        /// </summary>
        /// <param name="propertyName">Name of the property used to notify listeners.  This
        /// value is optional and can be provided automatically when invoked from compilers
        /// that support <see cref="CallerMemberNameAttribute"/>.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RaisePropertyChangedEvent(string propertyName)
        {
            WindowsGui.UIInvoke(DispatcherPriority.DataBind, () =>
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            });
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

        #endregion

        #region Event Logging

        /// <summary>
        /// Logs the start of an event
        /// </summary>
        /// <param name="e">The event.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void LogEventStart(RoutedEvent e)
        {
            if (Utils.IsInDebugMode)
                Logger.Log(MediaLogMessageType.Trace, $"EVENT START: {e.Name}");
        }

        /// <summary>
        /// Logs the end of an event.
        /// </summary>
        /// <param name="e">The event.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void LogEventDone(RoutedEvent e)
        {
            if (Utils.IsInDebugMode)
                Logger.Log(MediaLogMessageType.Trace, $"EVENT DONE : {e.Name}");
        }

        #endregion
    }
}
