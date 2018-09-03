namespace Unosquare.FFME.Events
{
    using Shared;
    using System;
    using System.Windows;

    /// <summary>
    /// Contains the position changed routed event args
    /// </summary>
    /// <seealso cref="RoutedEventArgs" />
    public class PositionChangedRoutedEventArgs : RoutedEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PositionChangedRoutedEventArgs" /> class.
        /// </summary>
        /// <param name="routedEvent">The routed event.</param>
        /// <param name="source">The source.</param>
        /// <param name="engineState">State of the engine.</param>
        /// <param name="oldPosition">The old position.</param>
        /// <param name="newPosition">The new position.</param>
        public PositionChangedRoutedEventArgs(RoutedEvent routedEvent, object source, IMediaEngineState engineState, TimeSpan oldPosition, TimeSpan newPosition)
            : base(routedEvent, source)
        {
            Position = newPosition;
            OldPosition = oldPosition;
            EngineState = engineState;
        }

        /// <summary>
        /// Gets the current position.
        /// </summary>
        public TimeSpan Position { get; }

        /// <summary>
        /// Gets the old position.
        /// </summary>
        public TimeSpan OldPosition { get; }

        /// <summary>
        /// Provides access to the underlying media engine state
        /// </summary>
        public IMediaEngineState EngineState { get; }
    }
}
