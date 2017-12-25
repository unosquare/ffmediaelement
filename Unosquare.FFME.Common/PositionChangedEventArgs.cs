namespace Unosquare.FFME
{
    using System;

    /// <summary>
    /// Contains position information upon rasing the PositionChanged Event
    /// </summary>
    /// <seealso cref="System.EventArgs" />
    public class PositionChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PositionChangedEventArgs"/> class.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="position">The position.</param>
        public PositionChangedEventArgs(MediaElementCore source, TimeSpan position)
        {
            Position = position;
            Source = source;
        }

        /// <summary>
        /// Gets the Media Element Core instance that raised the event
        /// </summary>
        public MediaElementCore Source { get; }

        /// <summary>
        /// Gets the position value when the event was raised.
        /// </summary>
        public TimeSpan Position { get; }
    }
}
