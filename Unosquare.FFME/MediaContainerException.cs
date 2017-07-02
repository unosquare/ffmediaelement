namespace Unosquare.FFME
{
    using System;

    /// <summary>
    /// A Media Container Exception
    /// </summary>
    /// <seealso cref="System.Exception" />
    [Serializable]
    public class MediaContainerException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MediaContainerException"/> class.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public MediaContainerException(string message) : base(message) { }

        // TODO: Add error code property and enumerate error codes.

    }
}
