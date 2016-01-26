namespace Unosquare.FFmpegMediaElement
{
    using System;

    /// <summary>
    /// Enumerates the flags of a decoded frame
    /// </summary>
    [Flags]
    internal enum FFmpegMediaFrameFlags
    {
        None = 0,
        KeyFrame = 1,
        Bos = 2,
        Eos = 4,
    }
}
