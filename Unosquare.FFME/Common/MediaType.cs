﻿namespace Unosquare.FFME.Common
{
    using FFmpeg.AutoGen;

    /// <summary>
    /// Enumerates the different Media Types compatible with AVMEDIATYPE_* constants
    /// defined by FFmpeg.
    /// </summary>
    public enum MediaType
    {
        /// <summary>
        /// Represents an un-existing media type (-1)
        /// </summary>
        None = AVMediaType.AVMEDIA_TYPE_UNKNOWN,

        /// <summary>
        /// The video media type (0)
        /// </summary>
        Video = AVMediaType.AVMEDIA_TYPE_VIDEO,

        /// <summary>
        /// The audio media type (1)
        /// </summary>
        Audio = AVMediaType.AVMEDIA_TYPE_AUDIO,

        /// <summary>
        /// The subtitle media type (3)
        /// </summary>
        Subtitle = AVMediaType.AVMEDIA_TYPE_SUBTITLE
    }
}
