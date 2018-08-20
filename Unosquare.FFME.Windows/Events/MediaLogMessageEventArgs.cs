namespace Unosquare.FFME.Events
{
    using Shared;
    using System;

    /// <summary>
    /// Contains the Message Logged Event Arguments
    /// </summary>
    /// <seealso cref="EventArgs" />
    public class MediaLogMessageEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MediaLogMessageEventArgs"/> class.
        /// </summary>
        /// <param name="message">The message.</param>
        public MediaLogMessageEventArgs(MediaLogMessage message)
        {
            Source = message.Source;
            TimestampUtc = message.TimestampUtc;
            MessageType = message.MessageType;
            Message = message.Message;
        }

        /// <summary>
        /// Gets the intance of the MediaElement that generated this message.
        /// When null, it means FFmpeg generated this message.
        /// </summary>
        public MediaEngine Source { get; }

        /// <summary>
        /// Gets the timestamp.
        /// </summary>
        public DateTime TimestampUtc { get; }

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
