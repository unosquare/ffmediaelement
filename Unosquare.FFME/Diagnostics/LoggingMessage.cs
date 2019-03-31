namespace Unosquare.FFME.Diagnostics
{
    using Common;
    using System;

    /// <summary>
    /// Represents the contents of a logging message that was sent to the log manager.
    /// </summary>
    internal sealed class LoggingMessage
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LoggingMessage" /> class.
        /// </summary>
        /// <param name="loggingHandler">The object that shall handle the message when it is output by the queue.</param>
        /// <param name="messageType">Type of the message.</param>
        /// <param name="messageText">The message text.</param>
        /// <param name="aspectName">Name of the code aspect the message came from.</param>
        internal LoggingMessage(ILoggingHandler loggingHandler, MediaLogMessageType messageType, string messageText, string aspectName)
        {
            MessageType = messageType;
            Message = messageText;
            TimestampUtc = DateTime.UtcNow;
            Handler = loggingHandler;
            AspectName = aspectName;
        }

        /// <summary>
        /// Gets the object that shall handle the message when it is output by the queue.
        /// </summary>
        public ILoggingHandler Handler { get; }

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

        /// <summary>
        /// Gets the aspect or feature that sent the logged message.
        /// May or may not be available.
        /// </summary>
        public string AspectName { get; }
    }
}
