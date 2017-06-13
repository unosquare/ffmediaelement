namespace Unosquare.FFME
{
    /// <summary>
    /// Defines the different log message types received by the log handler
    /// </summary>
    public enum MediaLogMessageType
    {
        //
        // Summary:
        //     The none message type
        None = 0,
        //
        // Summary:
        //     The information message type
        Info = 1,
        //
        // Summary:
        //     The debug message type
        Debug = 2,
        //
        // Summary:
        //     The trace message type
        Trace = 4,
        //
        // Summary:
        //     The error message type
        Error = 8,
        //
        // Summary:
        //     The warning message type
        Warning = 16
    }
}
