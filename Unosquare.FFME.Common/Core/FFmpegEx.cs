namespace Unosquare.FFME.Core
{
    using global::FFmpeg.AutoGen;
    using System;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;

    /// <summary>
    /// Manual additions to API calls not available in FFmpeg.Autogen
    /// </summary>
    internal static class FFmpegEx
    {
#pragma warning disable SA1310 // Field names must not contain underscore
        public const long AV_NOPTS = long.MinValue;
        public static readonly int AVERROR_EOF = -MKTAG('E', 'O', 'F', ' ');
        public static readonly ulong AV_PIX_FMT_FLAG_HWACCEL = 1 << 3;

        // http://www-numi.fnal.gov/offline_software/srt_public_context/WebDocs/Errors/unix_system_errors.html
        // public static readonly AVRational AV_TIME_BASE_Q = new AVRational { num = 1, den = ffmpeg.AV_TIME_BASE };
        // public const int AVERROR_EAGAIN = -11; // http://www-numi.fnal.gov/offline_software/srt_public_context/WebDocs/Errors/unix_system_errors.html
#pragma warning restore SA1310 // Field names must not contain underscore

        /// <summary>
        /// Gets the FFmpeg error mesage based on the error code
        /// </summary>
        /// <param name="code">The code.</param>
        /// <returns>The decoded error message</returns>
        public static unsafe string GetErrorMessage(int code)
        {
            var errorStrBytes = new byte[1024];
            var errorStrPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(byte)) * errorStrBytes.Length);
            ffmpeg.av_strerror(code, (byte*)errorStrPtr, (ulong)errorStrBytes.Length);
            Marshal.Copy(errorStrPtr, errorStrBytes, 0, errorStrBytes.Length);
            Marshal.FreeHGlobal(errorStrPtr);

            var errorMessage = Encoding.GetEncoding(0).GetString(errorStrBytes).Split('\0').FirstOrDefault();
            return errorMessage;
        }

        #region Ported Macros

        private static int MKTAG(params byte[] buff)
        {
            // Port of: ((a) | ((b) << 8) | ((c) << 16) | ((unsigned)(d) << 24))
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

    }
}
