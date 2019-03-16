namespace Unosquare.FFME.Engine
{
    /// <summary>
    /// Defines interface members for a class that
    /// defines a logging message handler <see cref="ILoggingHandler"/>
    /// </summary>
    public interface ILoggingSource
    {
        /// <summary>
        /// Gets the logging handler.
        /// </summary>
        ILoggingHandler LoggingHandler { get; }
    }
}
