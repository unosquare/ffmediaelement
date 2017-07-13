namespace Unosquare.FFME.Decoding.ClosedCaptions
{
    /// <summary>
    /// Defines Closed-Captioning XDS Packet Classes
    /// </summary>
    public enum CCXdsClassType
    {
        None = 0x00,
        CurrentStart = 0x01,
        CurrentContinue = 0x02,
        FutureStart = 0x03,
        FutureContinue = 0x04,
        ChannelStart = 0x05,
        ChannelContinue = 0x06,
        MiscStart = 0x07,
        MiscContinue = 0x08,
        PublicServiceStart = 0x09,
        PublicServiceContinue = 0x0A,
        ReservedStart = 0x0B,
        ReservedContinue = 0x0C,
        PrivateStart = 0x0D,
        PrivateContinue = 0x0E,
        EndAll = 0x0F,
    }
}
