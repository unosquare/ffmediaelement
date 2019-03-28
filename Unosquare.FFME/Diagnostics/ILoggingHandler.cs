namespace Unosquare.FFME.Diagnostics
{
    /// <summary>
    /// Defines interface methods for logging message handlers.
    /// </summary>
    internal interface ILoggingHandler
    {
        /// <summary>
        /// Handles a log message.
        /// </summary>
        /// <param name="message">The message object contining the data.</param>
        void HandleLogMessage(MediaLogMessage message);
    }
}
