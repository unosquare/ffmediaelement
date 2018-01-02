namespace Unosquare.FFME.Shared
{
    /// <summary>
    /// Defines the different log message types received by the log handler
    /// </summary>
    public enum MediaLogMessageType
    {
        /// <summary>
        /// The none messge type
        /// </summary>
        None = 0,

        /// <summary>
        /// The information messge type
        /// </summary>
        Info = 1,

        /// <summary>
        /// The debug messge type
        /// </summary>
        Debug = 2,

        /// <summary>
        /// The trace messge type
        /// </summary>
        Trace = 4,

        /// <summary>
        /// The error messge type
        /// </summary>
        Error = 8,

        /// <summary>
        /// The warning messge type
        /// </summary>
        Warning = 16,
    }
}
