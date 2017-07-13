namespace Unosquare.FFME.Decoding.ClosedCaptions
{
    /// <summary>
    /// Defines Closed-Captioning Packet types
    /// </summary>
    public enum CCPacketType
    {
        Unrecognized,
        NullPad,
        XdsClass,
        MiscCommand,
        Text,
        MidRow,
        Preamble,
        Color,
        Charset,
        Tabs,
    }
}
