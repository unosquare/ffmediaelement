namespace Unosquare.FFME.Common
{
    /// <summary>
    /// Defines the different log message types received by the log handler.
    /// </summary>
    public enum MediaLogMessageType
    {
        /// <summary>
        /// The none message type
        /// </summary>
        None = 0,

        /// <summary>
        /// The information message type
        /// </summary>
        Info = 1,

        /// <summary>
        /// The debug message type
        /// </summary>
        Debug = 2,

        /// <summary>
        /// The trace message type
        /// </summary>
        Trace = 4,

        /// <summary>
        /// The error message type
        /// </summary>
        Error = 8,

        /// <summary>
        /// The warning message type
        /// </summary>
        Warning = 16
    }
}
