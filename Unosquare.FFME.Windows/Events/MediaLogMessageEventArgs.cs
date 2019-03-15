namespace Unosquare.FFME.Events
{
    using Engine;
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
            TimestampUtc = message.TimestampUtc;
            MessageType = message.MessageType;
            Message = message.Message;
            AspectName = message.AspectName;
        }

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

        /// <inheritdoc />
        public override string ToString() =>
            $"[{TimestampUtc.Minute:00}:{TimestampUtc.Second:00}.{TimestampUtc.Millisecond:000} " +
            $"| {GetTypePrefix()} | {AspectName,-20}] {Message}";

        /// <summary>
        /// Gets the type prefix.
        /// </summary>
        /// <returns>A 3-letter abbreviation</returns>
        private string GetTypePrefix()
        {
            switch (MessageType)
            {
                case MediaLogMessageType.Debug:
                    return "DBG";
                case MediaLogMessageType.Error:
                    return "ERR";
                case MediaLogMessageType.Info:
                    return "INF";
                case MediaLogMessageType.None:
                    return "NON";
                case MediaLogMessageType.Trace:
                    return "TRC";
                case MediaLogMessageType.Warning:
                    return "WRN";
                default:
                    return "INV";
            }
        }
    }
}
