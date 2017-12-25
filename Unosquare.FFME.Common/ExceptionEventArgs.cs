namespace Unosquare.FFME
{
    using System;

    /// <summary>
    /// Represents an Exception event arguments
    /// </summary>
    /// <seealso cref="System.EventArgs" />
    public class ExceptionEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExceptionEventArgs"/> class.
        /// </summary>
        /// <param name="e">The e.</param>
        public ExceptionEventArgs(Exception e)
        {
            Exception = e;
        }

        /// <summary>
        /// Gets the exception.
        /// </summary>
        /// <value>
        /// The exception.
        /// </value>
        public Exception Exception { get; }
    }
}
