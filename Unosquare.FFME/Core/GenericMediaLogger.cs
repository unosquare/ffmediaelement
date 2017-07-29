namespace Unosquare.FFME.Core
{
    /// <summary>
    /// Represents a generic Logger
    /// </summary>
    /// <typeparam name="T">The sender's concrete type</typeparam>
    /// <seealso cref="Unosquare.FFME.Core.IMediaLogger" />
    internal class GenericMediaLogger<T> : IMediaLogger
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GenericMediaLogger{T}"/> class.
        /// </summary>
        /// <param name="sender">The sender.</param>
        public GenericMediaLogger(T sender)
        {
            Sender = sender;
        }

        /// <summary>
        /// Holds a reference to the sender.
        /// </summary>
        public T Sender { get; }

        /// <summary>
        /// Logs the specified message.
        /// </summary>
        /// <param name="messageType">Type of the message.</param>
        /// <param name="message">The message.</param>
        public void Log(MediaLogMessageType messageType, string message)
        {
            Utils.Log(Sender, messageType, message);
        }
    }
}
