namespace Unosquare.FFME.Events
{
    using System;
    using System.Windows;

    /// <summary>
    /// Contains the position changed routed event args
    /// </summary>
    /// <seealso cref="System.Windows.RoutedEventArgs" />
    public class PositionChangedRoutedEventArgs : RoutedEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PositionChangedRoutedEventArgs"/> class.
        /// </summary>
        /// <param name="routedEvent">The routed event.</param>
        /// <param name="source">The source.</param>
        /// <param name="position">The position.</param>
        public PositionChangedRoutedEventArgs(RoutedEvent routedEvent, object source, TimeSpan position)
            : base(routedEvent, source)
        {
            Position = position;
        }

        /// <summary>
        /// Gets the position.
        /// </summary>
        public TimeSpan Position { get; }
    }
}
