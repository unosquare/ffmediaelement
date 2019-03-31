namespace Unosquare.FFME.ClosedCaptions
{
    /// <summary>
    /// Defines Closed-Captioning XDS Packet Classes.
    /// </summary>
    public enum CaptionsXdsClass
    {
        /// <summary>
        /// The none XDS Class.
        /// </summary>
        None = 0x00,

        /// <summary>
        /// The current start XDS Class.
        /// </summary>
        CurrentStart = 0x01,

        /// <summary>
        /// The current continue XDS Class.
        /// </summary>
        CurrentContinue = 0x02,

        /// <summary>
        /// The future start XDS Class.
        /// </summary>
        FutureStart = 0x03,

        /// <summary>
        /// The future continue XDS Class.
        /// </summary>
        FutureContinue = 0x04,

        /// <summary>
        /// The channel start XDS Class.
        /// </summary>
        ChannelStart = 0x05,

        /// <summary>
        /// The channel continue XDS Class.
        /// </summary>
        ChannelContinue = 0x06,

        /// <summary>
        /// The misc start XDS Class.
        /// </summary>
        MiscStart = 0x07,

        /// <summary>
        /// The misc continue XDS Class.
        /// </summary>
        MiscContinue = 0x08,

        /// <summary>
        /// The public service start XDS Class.
        /// </summary>
        PublicServiceStart = 0x09,

        /// <summary>
        /// The public service continue XDS Class.
        /// </summary>
        PublicServiceContinue = 0x0A,

        /// <summary>
        /// The reserved start XDS Class.
        /// </summary>
        ReservedStart = 0x0B,

        /// <summary>
        /// The reserved continue XDS Class.
        /// </summary>
        ReservedContinue = 0x0C,

        /// <summary>
        /// The private start XDS Class.
        /// </summary>
        PrivateStart = 0x0D,

        /// <summary>
        /// The private continue XDS Class.
        /// </summary>
        PrivateContinue = 0x0E,

        /// <summary>
        /// The end all XDS Class.
        /// </summary>
        EndAll = 0x0F
    }
}
