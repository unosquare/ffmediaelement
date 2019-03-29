namespace Unosquare.FFME
{
    using Media;
    using Rendering.Wave;
    using System;
    using System.Collections.Generic;

    public static partial class Library
    {
        /// <summary>
        /// The default DirectSound device.
        /// </summary>
        public static readonly DirectSoundDeviceInfo DefaultDirectSoundDevice = new DirectSoundDeviceInfo(
            DirectSoundPlayer.DefaultPlaybackDeviceId, nameof(DefaultDirectSoundDevice), nameof(DirectSoundPlayer), true, Guid.Empty.ToString());

        /// <summary>
        /// The default Windows MME Legacy Audio Device.
        /// </summary>
        public static readonly LegacyAudioDeviceInfo DefaultLegacyAudioDevice = new LegacyAudioDeviceInfo(
            -1, nameof(DefaultLegacyAudioDevice), nameof(LegacyAudioPlayer), true, Guid.Empty.ToString());

        /// <summary>
        /// Enumerates the DirectSound devices.
        /// </summary>
        /// <returns>The available DirectSound devices.</returns>
        public static IEnumerable<DirectSoundDeviceInfo> EnumerateDirectSoundDevices()
        {
            var devices = DirectSoundPlayer.EnumerateDevices();
            var result = new List<DirectSoundDeviceInfo>(16) { DefaultDirectSoundDevice };

            foreach (var device in devices)
            {
                result.Add(new DirectSoundDeviceInfo(
                    device.Guid, device.Description, nameof(DirectSoundPlayer), false, device.ModuleName));
            }

            return result;
        }

        /// <summary>
        /// Enumerates the (Legacy) Windows Multimedia Extensions devices.
        /// </summary>
        /// <returns>The available MME devices.</returns>
        public static IEnumerable<LegacyAudioDeviceInfo> EnumerateLegacyAudioDevices()
        {
            var devices = LegacyAudioPlayer.EnumerateDevices();
            var result = new List<LegacyAudioDeviceInfo>(16) { DefaultLegacyAudioDevice };

            for (var deviceId = 0; deviceId < devices.Count; deviceId++)
            {
                var device = devices[deviceId];
                result.Add(new LegacyAudioDeviceInfo(
                    deviceId, device.ProductName, nameof(LegacyAudioPlayer), false, device.ProductGuid.ToString()));
            }

            return result;
        }
    }
}
