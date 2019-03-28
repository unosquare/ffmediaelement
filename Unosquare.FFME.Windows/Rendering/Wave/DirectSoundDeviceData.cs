namespace Unosquare.FFME.Rendering.Wave
{
    using System;

    /// <summary>
    /// Class for enumerating DirectSound devices.
    /// </summary>
    internal sealed class DirectSoundDeviceData
    {
        /// <summary>
        /// The device identifier.
        /// </summary>
        public Guid Guid { get; internal set; }

        /// <summary>
        /// Device description.
        /// </summary>
        public string Description { get; internal set; }

        /// <summary>
        /// Device module name.
        /// </summary>
        public string ModuleName { get; internal set; }
    }
}
