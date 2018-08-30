namespace Unosquare.FFME.Shared
{
    using System;

    /// <summary>
    /// Represents the contents of a logging message that was sent to the log manager.
    /// </summary>
    public class MediaLogMessage
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MediaLogMessage" /> class.
        /// </summary>
        /// <param name="mediaElement">The media element.</param>
        /// <param name="messageType">Type of the message.</param>
        /// <param name="message">The message.</param>
        public MediaLogMessage(MediaEngine mediaElement, MediaLogMessageType messageType, string message)
        {
            MessageType = messageType;
            Message = message;
            TimestampUtc = DateTime.UtcNow;
            Source = mediaElement;
        }

        /// <summary>
        /// Gets the instance of the MediaElement that generated this message.
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
