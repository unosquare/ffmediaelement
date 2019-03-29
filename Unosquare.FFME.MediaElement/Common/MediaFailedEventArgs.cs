namespace Unosquare.FFME.Common
{
    using System;

    /// <summary>
    /// The Media failed event arguments.
    /// </summary>
    public sealed class MediaFailedEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MediaFailedEventArgs"/> class.
        /// </summary>
        /// <param name="errorException">The exception.</param>
        internal MediaFailedEventArgs(Exception errorException)
        {
            ErrorException = errorException;
        }

        /// <summary>
        /// Gets the error exception.
        /// </summary>
        public Exception ErrorException { get; }
    }
}
