namespace Unosquare.FFME.Decoding.ClosedCaptions
{
    /// <summary>
    /// Enumerates the Closed-Captioning misc commands
    /// </summary>
    public enum CCMiscCommandType
    {
        None = 0,
        Resume = 0x20,
        Backspace = 0x21,
        AlarmOff = 0x22,
        AlarmOn = 0x23,
        ClearLine = 0x24,
        RollUp2 = 0x25,
        RollUp3 = 0x26,
        RollUp4 = 0x27,
        StartCaption = 0x29,
        StarNonCaption = 0x2A,
        ResumeNonCaption = 0x2B,
        ClearScreen = 0x2C,
        NewLine = 0x2D,
        ClearBuffer = 0x2E,
        EndCaption = 0x2F
    }
}
