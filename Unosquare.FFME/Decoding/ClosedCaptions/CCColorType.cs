namespace Unosquare.FFME.Decoding.ClosedCaptions
{
    /// <summary>
    /// Enumerates the differen Closed-Captioning Colors
    /// </summary>
    public enum CCColorType
    {
        None = 0x00,
        White = 0x20,
        WhiteTransparent = 0x21,
        Green = 0x22,
        GreenTransparent = 0x23,
        Blue = 0x24,
        BlueTransparent = 0x25,
        Cyan = 0x26,
        CyanTransparent = 0x27,
        Red = 0x28,
        RedTransparent = 0x29,
        Yellow = 0x2A,
        YellowTransparent = 0x2B,
        Magenta = 0x2C,
        MagentaTransparent = 0x2D,
        WhiteItalics = 0x2E,
        WhiteItalicsTransparent = 0x2F,

        BackgroundTransparent = 0x2D00,
        ForegroundBlack = 0x2E00,
        ForegroundBlackUnderline = 0x2F00,
    }
}
