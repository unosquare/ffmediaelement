namespace Unosquare.FFME.Diagnostics
{
    /// <summary>
    /// Defines interface members for a class that
    /// defines a logging message handler <see cref="ILoggingHandler"/>
    /// </summary>
    internal interface ILoggingSource
    {
        /// <summary>
        /// Gets the logging handler.
        /// </summary>
        ILoggingHandler LoggingHandler { get; }
    }
}
