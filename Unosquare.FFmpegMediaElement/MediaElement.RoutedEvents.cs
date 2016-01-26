namespace Unosquare.FFmpegMediaElement
{
    using System;
    using System.Windows;

    partial class MediaElement
    {
        /// <summary>
        /// Defines constants that contain Routed event names
        /// </summary>
        private static class RoutedEventNames
        {
            public const string MediaFailed = "MediaFailed";
            public const string MediaOpened = "MediaOpened";
            public const string MediaEnded = "MediaEnded";
            public const string MediaErrored = "MediaErrored";
        }

        /// <summary>
        /// MediaFailedEvent is a routed event. 
        /// </summary>
        public static readonly RoutedEvent MediaFailedEvent =
            EventManager.RegisterRoutedEvent(
                            RoutedEventNames.MediaFailed,
                            RoutingStrategy.Bubble,
                            typeof(EventHandler<MediaErrorRoutedEventArgs>),
                            typeof(MediaElement));

        /// <summary>
        /// Raised when the media fails to load or a fatal error has occurred which prevents playback.
        /// </summary>
        public event EventHandler<MediaErrorRoutedEventArgs> MediaFailed
        {
            add { AddHandler(MediaFailedEvent, value); }
            remove { RemoveHandler(MediaFailedEvent, value); }
        }

        /// <summary>
        /// MediaErrorEvent is a routed event. 
        /// </summary>
        public static readonly RoutedEvent MediaErroredEvent =
            EventManager.RegisterRoutedEvent(
                    RoutedEventNames.MediaErrored,
                    RoutingStrategy.Bubble,
                    typeof(EventHandler<MediaErrorRoutedEventArgs>),
                    typeof(MediaElement));

        /// <summary>
        /// Raised when a problem with the media is found
        /// </summary>
        public event EventHandler<MediaErrorRoutedEventArgs> MediaErrored
        {
            add { AddHandler(MediaErroredEvent, value); }
            remove { RemoveHandler(MediaErroredEvent, value); }
        }

        /// <summary> 
        /// MediaOpened is a routed event.
        /// </summary> 
        public static readonly RoutedEvent MediaOpenedEvent =
            EventManager.RegisterRoutedEvent(
                            RoutedEventNames.MediaOpened,
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


        /// <summary>
        /// MediaEnded is a routed event 
        /// </summary>
        public static readonly RoutedEvent MediaEndedEvent =
            EventManager.RegisterRoutedEvent(
                            RoutedEventNames.MediaEnded,
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

    }
}
