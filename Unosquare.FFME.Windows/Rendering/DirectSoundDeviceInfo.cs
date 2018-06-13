namespace Unosquare.FFME.Rendering
{
    using System;

    /// <summary>
    /// Class for enumerating DirectSound devices
    /// </summary>
    public class DirectSoundDeviceInfo
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DirectSoundDeviceInfo"/> class.
        /// </summary>
        internal DirectSoundDeviceInfo()
        {
            // placeholder
        }

        /// <summary>
        /// The device identifier
        /// </summary>
        public Guid Guid { get; internal set; }

        /// <summary>
        /// Device description
        /// </summary>
        public string Description { get; internal set; }

        /// <summary>
        /// Device module name
        /// </summary>
        public string ModuleName { get; internal set; }
    }
}
