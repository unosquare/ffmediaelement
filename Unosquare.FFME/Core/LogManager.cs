namespace Unosquare.FFME.Core
{
    using System;
    using Unosquare.FFME.Decoding;

    /// <summary>
    /// Provides universal logging extensions
    /// </summary>
    internal static class LogManager
    {
        /// <summary>
        /// Logs the specified message type.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="messageType">Type of the message.</param>
        /// <param name="message">The message.</param>
        /// <exception cref="System.ArgumentNullException">
        /// sender
        /// or
        /// sender
        /// </exception>
        public static void Log(this MediaElement sender, MediaLogMessageType messageType, string message)
        {
            if (sender == null) throw new ArgumentNullException(nameof(sender));
            if (string.IsNullOrWhiteSpace(message)) throw new ArgumentNullException(nameof(sender));
            try { sender?.LogMessageCallback?.Invoke(messageType, message); }
            catch { }
        }

        /// <summary>
        /// Logs the specified message type.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="messageType">Type of the message.</param>
        /// <param name="message">The message.</param>
        /// <exception cref="System.ArgumentNullException">
        /// sender
        /// or
        /// sender
        /// </exception>
        public static void Log(this MediaContainer sender, MediaLogMessageType messageType, string message)
        {
            if (sender == null) throw new ArgumentNullException(nameof(sender));
            if (string.IsNullOrWhiteSpace(message)) throw new ArgumentNullException(nameof(sender));

            try { sender?.MediaOptions?.LogMessageCallback?.Invoke(messageType, message); }
            catch { }
        }

    }
}
