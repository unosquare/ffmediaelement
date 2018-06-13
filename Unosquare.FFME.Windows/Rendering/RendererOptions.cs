namespace Unosquare.FFME.Rendering
{
    using System;
    using System.Collections.Generic;
    using Wave;

    /// <summary>
    /// Provides access to various internal media renderer options
    /// </summary>
    public sealed class RendererOptions
    {
        /// <summary>
        /// By default, the audio renderer will skip or wait for samples to
        /// synchronize to video.
        /// </summary>
        public bool AudioDisableSync { get; set; } = false;

        /// <summary>
        /// Gets or sets the DirectSound device identifier. It is the default playback device by default.
        /// Only valid if <see cref="UseLegacyWaveOut"/> is set to false which is the default.
        /// </summary>
        public Guid DirectSoundDeviceId { get; set; } = DirectSoundPlayer.DefaultPlaybackDeviceId;

        /// <summary>
        /// Gets or sets the wave device identifier. -1 is the default playback device.
        /// Only valid if <see cref="UseLegacyWaveOut"/> is set to true.
        /// </summary>
        public int LegacyWaveDeviceId { get; set; } = -1;

        /// <summary>
        /// Gets or sets a value indicating whether the legacy MME (WinMM) should be used
        /// as an audio output device as opposed to DirectSound. This defaults to false.
        /// </summary>
        public bool UseLegacyWaveOut { get; set; } = false;

        /// <summary>
        /// Enumerates the DirectSound devices.
        /// </summary>
        /// <returns>The available DirectSound devices</returns>
        public IEnumerable<DirectSoundDeviceInfo> EnumerateDirectSoundDevices() =>
            DirectSoundPlayer.EnumerateDevices();

        /// <summary>
        /// Enumerates the (Legacy) Windows Multimedia Extensions devices.
        /// </summary>
        /// <returns>The available MME devices</returns>
        public IEnumerable<LegacyWaveDeviceInfo> EnumerateLegacyAudioDevices() =>
            LegacyWavePlayer.EnumerateDevices();
    }
}
