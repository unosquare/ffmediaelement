namespace Unosquare.FFME.Shared
{
    /// <summary>
    /// A very simple and standard interface for message logging
    /// </summary>
    internal interface IMediaLogger
    {
        /// <summary>
        /// Logs the specified message of the given type.
        /// </summary>
        /// <param name="messageType">Type of the message.</param>
        /// <param name="message">The message.</param>
        void Log(MediaLogMessageType messageType, string message);
    }
}
