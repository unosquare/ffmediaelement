namespace Unosquare.FFME.Shared
{
    using System;

    /// <inheritdoc cref="Exception"/>
    /// <summary>
    /// A Media Container Exception
    /// </summary>
    [Serializable]
    public class MediaContainerException : Exception
    {
        // TODO: Add error code property and enumerate error codes.

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaContainerException"/> class.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public MediaContainerException(string message)
            : base(message)
        {
            // placeholder
        }
    }
}
