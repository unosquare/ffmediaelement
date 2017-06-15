namespace Unosquare.FFME.Core
{
    using FFmpeg.AutoGen;
    using System;
    using System.Collections.ObjectModel;
    using System.Linq;

    /// <summary>
    /// Defines library-wide constants
    /// </summary>
    internal static class Constants
    {
        #region Ported Methods

        private static int MKTAG(params byte[] buff)
        {
            //  ((a) | ((b) << 8) | ((c) << 16) | ((unsigned)(d) << 24))
            if (BitConverter.IsLittleEndian == false)
                buff = buff.Reverse().ToArray();

            return BitConverter.ToInt32(buff, 0);
        }

        private static int MKTAG(byte a, char b, char c, char d)
        {
            return MKTAG(new byte[] { a, (byte)b, (byte)c, (byte)d });
        }

        private static int MKTAG(char a, char b, char c, char d)
        {
            return MKTAG(new byte[] { (byte)a, (byte)b, (byte)c, (byte)d });
        }

        #endregion

        public const long AV_NOPTS = long.MinValue;
        public static readonly AVRational AV_TIME_BASE_Q = new AVRational { num = 1, den = ffmpeg.AV_TIME_BASE };
        public static readonly int AVERROR_EOF = -MKTAG('E', 'O', 'F', ' '); // http://www-numi.fnal.gov/offline_software/srt_public_context/WebDocs/Errors/unix_system_errors.html
        public const int AVERROR_EAGAIN = -11; // http://www-numi.fnal.gov/offline_software/srt_public_context/WebDocs/Errors/unix_system_errors.html


        public const double DefaultSpeedRatio = 1.0d;
        public const double DefaultBalance = 0.0d;
        public const double DefaultVolume = 1.0d;

        public const double MinSpeedRatio = 0.0d;
        public const double MaxSpeedRatio = 8.0d;

        public const double MinBalance = -1.0d;
        public const double MaxBalance = 1.0d;

        public const double MaxVolume = 1.0d;
        public const double MinVolume = 0.0d;

        public static readonly ReadOnlyCollection<MediaType> MediaTypes 
            = new ReadOnlyCollection<MediaType>(Enum.GetValues(typeof(MediaType)).Cast<MediaType>().ToArray());
    }
}
