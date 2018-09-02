namespace Unosquare.FFME.Rendering.Wave
{
    using System;

    /// <summary>
    /// Flags indicating what features this WaveOut device supports
    /// </summary>
    [Flags]
    internal enum WaveOutSupport
    {
        /// <summary>supports pitch control</summary>
        Pitch = 0x0001,

        /// <summary>supports playback rate control</summary>
        PlaybackRate = 0x0002,

        /// <summary>supports volume control (WAVECAPS_VOLUME)</summary>
        Volume = 0x0004,

        /// <summary>supports separate left-right volume control</summary>
        LRVolume = 0x0008,

        /// <summary>Sync</summary>
        Sync = 0x0010,

        /// <summary>Sample-Accurate</summary>
        SampleAccurate = 0x0020
    }
}
