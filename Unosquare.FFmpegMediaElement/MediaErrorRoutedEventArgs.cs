namespace Unosquare.FFmpegMediaElement
{
    using System;
    using System.Windows;

    /// <summary>
    /// Represents an event that occurs when the underlying media stream fails to load or
    /// corrupt packets are found within the stream.
    /// </summary>
    public class MediaErrorRoutedEventArgs : RoutedEventArgs
    {
        internal MediaErrorRoutedEventArgs(RoutedEvent routedEvent, object source, Exception errorException)
            : base(routedEvent, source)
        {
            this.ErrorException = errorException;
        }

        /// <summary>
        /// Gets the error exception.
        /// </summary>
        public Exception ErrorException { get; private set; }
    }
}
