namespace Unosquare.FFME
{
    using Core;
    using System;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Windows;
    using System.Windows.Media.Imaging;
    using System.Windows.Threading;

    partial class MediaElement
    {
        #region Helper Methods

        /// <summary>
        /// Creates a new instance of exception routed event arguments.
        /// This method exists because the constructor has not been made public for that class.
        /// </summary>
        /// <param name="routedEvent">The routed event.</param>
        /// <param name="sender">The sender.</param>
        /// <param name="errorException">The error exception.</param>
        /// <returns></returns>
        private static ExceptionRoutedEventArgs CreateExceptionRoutedEventArgs(RoutedEvent routedEvent, object sender, Exception errorException)
        {
            var constructor = (typeof(ExceptionRoutedEventArgs) as TypeInfo).DeclaredConstructors.First();
            return constructor.Invoke(new object[] { routedEvent, sender, errorException }) as ExceptionRoutedEventArgs;
        }

        /// <summary>
        /// Logs the start of an event
        /// </summary>
        /// <param name="e">The e.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void LogEventStart(RoutedEvent e)
        {
            if (Utils.IsInDebugMode)
                Logger.Log(MediaLogMessageType.Trace, $"EVENT START: {e.Name}");
        }

        /// <summary>
        /// Logs the end of an event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void LogEventDone(RoutedEvent e)
        {
            if (Utils.IsInDebugMode)
                Logger.Log(MediaLogMessageType.Trace, $"EVENT DONE : {e.Name}");
        }

        /// <summary>
        /// Raises the buffering started event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RaiseBufferingStartedEvent()
        {
            LogEventStart(BufferingStartedEvent);
            Utils.UIInvoke(DispatcherPriority.DataBind, () => { RaiseEvent(new RoutedEventArgs(BufferingStartedEvent, this)); });
            LogEventDone(BufferingStartedEvent);
        }

        /// <summary>
        /// Raises the buffering ended event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RaiseBufferingEndedEvent()
        {
            LogEventStart(BufferingEndedEvent);
            Utils.UIInvoke(DispatcherPriority.DataBind, () => { RaiseEvent(new RoutedEventArgs(BufferingEndedEvent, this)); });
            LogEventDone(BufferingEndedEvent);
        }

        /// <summary>
        /// Raises the Seeking started event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RaiseSeekingStartedEvent()
        {
            LogEventStart(SeekingStartedEvent);
            Utils.UIInvoke(DispatcherPriority.DataBind, () => { RaiseEvent(new RoutedEventArgs(SeekingStartedEvent, this)); });
            LogEventDone(SeekingStartedEvent);
        }

        /// <summary>
        /// Raises the Seeking ended event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RaiseSeekingEndedEvent()
        {
            LogEventStart(SeekingEndedEvent);
            Utils.UIInvoke(DispatcherPriority.DataBind, () => { RaiseEvent(new RoutedEventArgs(SeekingEndedEvent, this)); });
            LogEventDone(SeekingEndedEvent);
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
            Utils.UIInvoke(DispatcherPriority.DataBind, () => { RaiseEvent(CreateExceptionRoutedEventArgs(MediaFailedEvent, this, ex)); });
            LogEventDone(MediaFailedEvent);
        }

        /// <summary>
        /// Raises the media opened event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RaiseMediaOpenedEvent()
        {
            LogEventStart(MediaOpenedEvent);
            Utils.UIInvoke(DispatcherPriority.DataBind, () => { RaiseEvent(new RoutedEventArgs(MediaOpenedEvent, this)); });
            LogEventDone(MediaOpenedEvent);
        }

        /// <summary>
        /// Raises the media opening event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RaiseMediaOpeningEvent()
        {
            LogEventStart(MediaOpeningEvent);
            Utils.UIInvoke(DispatcherPriority.DataBind, () =>
            {
                RaiseEvent(new MediaOpeningRoutedEventArgs(MediaOpeningEvent, this, Container.MediaOptions, Container.MediaInfo));
            });

            LogEventDone(MediaOpeningEvent);
        }

        /// <summary>
        /// Raises the media ended event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RaiseMediaEndedEvent()
        {
            LogEventStart(MediaEndedEvent);
            Utils.UIInvoke(DispatcherPriority.DataBind, () => { RaiseEvent(new RoutedEventArgs(MediaEndedEvent, this)); });
            LogEventDone(MediaEndedEvent);
        }

        #endregion

        #region BufferingStarted

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
        /// Occurs when buffering of packets was started
        /// </summary>
        public event RoutedEventHandler BufferingStarted
        {
            add { AddHandler(BufferingStartedEvent, value); }
            remove { RemoveHandler(BufferingStartedEvent, value); }
        }

        #endregion

        #region BufferingEnded

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
        /// Occurs when buffering of packets was Ended
        /// </summary>
        public event RoutedEventHandler BufferingEnded
        {
            add { AddHandler(BufferingEndedEvent, value); }
            remove { RemoveHandler(BufferingEndedEvent, value); }
        }

        #endregion

        #region SeekingStarted

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
        /// Occurs when Seeking of packets was started
        /// </summary>
        public event RoutedEventHandler SeekingStarted
        {
            add { AddHandler(SeekingStartedEvent, value); }
            remove { RemoveHandler(SeekingStartedEvent, value); }
        }

        #endregion

        #region SeekingEnded

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
        /// Occurs when Seeking of packets was Ended
        /// </summary>
        public event RoutedEventHandler SeekingEnded
        {
            add { AddHandler(SeekingEndedEvent, value); }
            remove { RemoveHandler(SeekingEndedEvent, value); }
        }

        #endregion

        #region MediaFailed

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
        /// Raised when the media fails to load or a fatal error has occurred which prevents playback.
        /// </summary>
        public event EventHandler<ExceptionRoutedEventArgs> MediaFailed
        {
            add { AddHandler(MediaFailedEvent, value); }
            remove { RemoveHandler(MediaFailedEvent, value); }
        }

        #endregion

        #region MediaOpened

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
        /// Raised when the media is opened 
        /// </summary> 
        public event RoutedEventHandler MediaOpened
        {
            add { AddHandler(MediaOpenedEvent, value); }
            remove { RemoveHandler(MediaOpenedEvent, value); }
        }

        #endregion

        #region MediaOpening

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
        /// Raised before the input stream of the media is opened.
        /// Use this method to modify the input options.
        /// </summary>
        public event EventHandler<MediaOpeningRoutedEventArgs> MediaOpening
        {
            add { AddHandler(MediaOpeningEvent, value); }
            remove { RemoveHandler(MediaOpeningEvent, value); }
        }

        #endregion

        #region MediaEnded

        /// <summary>
        /// MediaEnded is a routed event 
        /// </summary>
        public static readonly RoutedEvent MediaEndedEvent =
            EventManager.RegisterRoutedEvent(
                            nameof(MediaEnded),
                            RoutingStrategy.Bubble,
                            typeof(RoutedEventHandler),
                            typeof(MediaElement));

        /// <summary> 
        /// Raised when the corresponding media ends.
        /// </summary>
        public event RoutedEventHandler MediaEnded
        {
            add { AddHandler(MediaEndedEvent, value); }
            remove { RemoveHandler(MediaEndedEvent, value); }
        }

        #endregion
    }
}
