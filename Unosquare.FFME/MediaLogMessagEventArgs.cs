namespace Unosquare.FFME
{
    using System;

    /// <summary>
    /// Represents the contents of alogging message that was sent to the log manager.
    /// </summary>
    /// <seealso cref="System.EventArgs" />
    public class MediaLogMessagEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MediaLogMessagEventArgs"/> class.
        /// </summary>
        /// <param name="messageType">Type of the message.</param>
        /// <param name="message">The message.</param>
        public MediaLogMessagEventArgs(MediaLogMessageType messageType, string message)
            : base()
        {
            MessageType = messageType;
            Message = message;
            TimeStamp = TimeSpan.FromTicks(DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// Gets the timestamp.
        /// </summary>
        public TimeSpan TimeStamp { get; }

        /// <summary>
        /// Gets the type of the message.
        /// </summary>
        public MediaLogMessageType MessageType { get; }

        /// <summary>
        /// Gets the contents of the message.
        /// </summary>
        public string Message { get; }
    }

}
