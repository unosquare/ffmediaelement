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
        /// The default DirectSound device
        /// </summary>
        public static readonly AudioDeviceInfo<Guid> DefaultDirectSoundDevice = new AudioDeviceInfo<Guid>(
            DirectSoundPlayer.DefaultPlaybackDeviceId, nameof(DefaultDirectSoundDevice), nameof(DirectSoundPlayer), true, Guid.Empty.ToString());

        /// <summary>
        /// The default Windows MME Legacy Audio Device
        /// </summary>
        public static readonly AudioDeviceInfo<int> DefaultLegacyAudioDevice = new AudioDeviceInfo<int>(
            -1, nameof(DefaultLegacyAudioDevice), nameof(LegacyAudioPlayer), true, Guid.Empty.ToString());

        /// <summary>
        /// By default, the audio renderer will skip or wait for samples to
        /// synchronize to video.
        /// </summary>
        public bool AudioDisableSync { get; set; }

        /// <summary>
        /// Gets or sets the DirectSound device identifier. It is the default playback device by default.
        /// Only valid if <see cref="UseLegacyAudioOut"/> is set to false which is the default.
        /// </summary>
        public AudioDeviceInfo<Guid> DirectSoundDevice { get; set; } = DefaultDirectSoundDevice;

        /// <summary>
        /// Gets or sets the wave device identifier. -1 is the default playback device.
        /// Only valid if <see cref="UseLegacyAudioOut"/> is set to true.
        /// </summary>
        public AudioDeviceInfo<int> LegacyAudioDevice { get; set; } = DefaultLegacyAudioDevice;

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
        /// Enumerates the DirectSound devices.
        /// </summary>
        /// <returns>The available DirectSound devices</returns>
        public List<AudioDeviceInfo<Guid>> EnumerateDirectSoundDevices()
        {
            var devices = DirectSoundPlayer.EnumerateDevices();
            var result = new List<AudioDeviceInfo<Guid>>(16) { DefaultDirectSoundDevice };

            foreach (var device in devices)
            {
                result.Add(new AudioDeviceInfo<Guid>(
                    device.Guid, device.Description, nameof(DirectSoundPlayer), false, device.ModuleName));
            }

            return result;
        }

        /// <summary>
        /// Enumerates the (Legacy) Windows Multimedia Extensions devices.
        /// </summary>
        /// <returns>The available MME devices</returns>
        public List<AudioDeviceInfo<int>> EnumerateLegacyAudioDevices()
        {
            var devices = LegacyAudioPlayer.EnumerateDevices();
            var result = new List<AudioDeviceInfo<int>>(16) { DefaultLegacyAudioDevice };

            for (var deviceId = 0; deviceId < devices.Count; deviceId++)
            {
                var device = devices[deviceId];
                result.Add(new AudioDeviceInfo<int>(
                    deviceId, device.ProductName, nameof(LegacyAudioPlayer), false, device.ProductGuid.ToString()));
            }

            return result;
        }
    }
}
