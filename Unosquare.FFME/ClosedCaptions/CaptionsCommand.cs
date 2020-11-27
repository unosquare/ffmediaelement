namespace Unosquare.FFME.ClosedCaptions
{
    /// <summary>
    /// Enumerates the Closed-Captioning misc commands.
    /// </summary>
    public enum CaptionsCommand
    {
        /// <summary>
        /// No command.
        /// </summary>
        None = 0,

        /// <summary>
        /// The resume command.
        /// </summary>
        Resume = 0x20,

        /// <summary>
        /// The backspace command.
        /// </summary>
        Backspace = 0x21,

        /// <summary>
        /// The alarm off command.
        /// </summary>
        AlarmOff = 0x22,

        /// <summary>
        /// The alarm on command.
        /// </summary>
        AlarmOn = 0x23,

        /// <summary>
        /// The clear line command
        /// Delete to end of Row (DER).
        /// </summary>
        ClearLine = 0x24,

        /// <summary>
        /// The roll up 2 command.
        /// </summary>
        RollUp2 = 0x25,

        /// <summary>
        /// The roll up 3 command.
        /// </summary>
        RollUp3 = 0x26,

        /// <summary>
        /// The roll up 4 command.
        /// </summary>
        RollUp4 = 0x27,

        /// <summary>
        /// The start caption command.
        /// </summary>
        StartCaption = 0x29,

        /// <summary>
        /// The start non caption command.
        /// </summary>
        StartNonCaption = 0x2A,

        /// <summary>
        /// The resume non caption command.
        /// </summary>
        ResumeNonCaption = 0x2B,

        /// <summary>
        /// The clear screen command.
        /// </summary>
        ClearScreen = 0x2C,

        /// <summary>
        /// The new line command.
        /// </summary>
        NewLine = 0x2D,

        /// <summary>
        /// The clear buffer command.
        /// </summary>
        ClearBuffer = 0x2E,

        /// <summary>
        /// The end caption command.
        /// </summary>
        EndCaption = 0x2F
    }
}
