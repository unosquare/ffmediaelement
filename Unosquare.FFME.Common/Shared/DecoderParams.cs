namespace Unosquare.FFME.Shared
{
    using FFmpeg.AutoGen;

    /// <summary>
    /// Contains metadata on the output fotmat of the media engine decoder
    /// </summary>
    public static class DecoderParams
    {
        /// <summary>
        /// The audio buffer padding
        /// </summary>
        public const int AudioBufferPadding = 256;

        /// <summary>
        /// The audio bits per sample (1 channel only)
        /// </summary>
        public const int AudioBitsPerSample = 16;

        /// <summary>
        /// The audio sample format
        /// </summary>
        public const AVSampleFormat AudioSampleFormat = AVSampleFormat.AV_SAMPLE_FMT_S16;

        /// <summary>
        /// The audio channel count
        /// </summary>
        public const int AudioChannelCount = 2;

        /// <summary>
        /// The audio sample rate (per channel)
        /// </summary>
        public const int AudioSampleRate = 48000;

        /// <summary>
        /// The video bits per component
        /// </summary>
        public const int VideoBitsPerComponent = 8;

        /// <summary>
        /// The video bits per pixel
        /// </summary>
        public const int VideoBitsPerPixel = 32;

        /// <summary>
        /// The video bytes per pixel
        /// </summary>
        public const int VideoBytesPerPixel = 4;

        /// <summary>
        /// The video pixel format. BGRX, 32bit
        /// </summary>
        public const AVPixelFormat VideoPixelFormat = AVPixelFormat.AV_PIX_FMT_BGR0;
    }
}
