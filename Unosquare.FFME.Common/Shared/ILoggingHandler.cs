namespace Unosquare.FFME.Shared
{
    /// <summary>
    /// Defines interface methods for logging message handlers
    /// </summary>
    public interface ILoggingHandler
    {
        /// <summary>
        /// Handles a log message.
        /// </summary>
        /// <param name="message">The message object contining the data.</param>
        void HandleLogMessage(MediaLogMessage message);
    }
}
