namespace Unosquare.FFME.Common
{
    /// <summary>
    /// Provides access to various internal media renderer options.
    /// </summary>
    public sealed class RendererOptions
    {
        /// <summary>
        /// By default, the audio renderer will skip or wait for samples to
        /// synchronize to video.
        /// </summary>
        public bool AudioDisableSync { get; set; }

        /// <summary>
        /// Gets or sets the DirectSound device identifier. It is the default playback device by default.
        /// Only valid if <see cref="UseLegacyAudioOut"/> is set to false which is the default.
        /// </summary>
        public DirectSoundDeviceInfo DirectSoundDevice { get; set; } = Library.DefaultDirectSoundDevice;

        /// <summary>
        /// Gets or sets the wave device identifier. -1 is the default playback device.
        /// Only valid if <see cref="UseLegacyAudioOut"/> is set to true.
        /// </summary>
        public LegacyAudioDeviceInfo LegacyAudioDevice { get; set; } = Library.DefaultLegacyAudioDevice;

        /// <summary>
        /// Gets or sets a value indicating whether the legacy MME (WinMM) should be used
        /// as an audio output device as opposed to DirectSound. This defaults to false.
        /// </summary>
        public bool UseLegacyAudioOut { get; set; }

        /// <summary>
        /// Gets or sets the frame refresh rate limit for the video renderer.
        /// Defaults to 0 and means no limit. Units are in frames per second.
        /// </summary>
        public int VideoRefreshRateLimit { get; set; }

        /// <summary>
        /// Gets or sets which image type is used for the video renderer.
        /// Use WriteableBitmap for tear-free scenarios, or use the InteropBitmap for
        /// faster, lower CPU usage. InteropBitmap might introduce some tearing.
        /// </summary>
        public VideoRendererImageType VideoImageType { get; set; } = VideoRendererImageType.WriteableBitmap;
    }
}
