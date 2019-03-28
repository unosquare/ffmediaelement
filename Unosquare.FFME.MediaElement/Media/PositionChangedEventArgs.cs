namespace Unosquare.FFME.Media
{
    using Platform;
    using System;

    /// <summary>
    /// Contains the position changed routed event args.
    /// </summary>
    /// <seealso cref="EventArgs" />
    public class PositionChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PositionChangedEventArgs" /> class.
        /// </summary>
        /// <param name="engineState">State of the engine.</param>
        /// <param name="oldPosition">The old position.</param>
        /// <param name="newPosition">The new position.</param>
        internal PositionChangedEventArgs(IMediaEngineState engineState, TimeSpan oldPosition, TimeSpan newPosition)
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
        /// Provides access to the underlying media engine state.
        /// </summary>
        public IMediaEngineState EngineState { get; }
    }
}
