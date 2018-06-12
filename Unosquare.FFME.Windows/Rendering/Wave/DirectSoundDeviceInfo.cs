namespace Unosquare.FFME.Rendering.Wave
{
    using System;

    /// <summary>
    /// Class for enumerating DirectSound devices
    /// </summary>
    internal class DirectSoundDeviceInfo
    {
        /// <summary>
        /// The device identifier
        /// </summary>
        public Guid Guid { get; set; }

        /// <summary>
        /// Device description
        /// </summary>
        public string Description { get; set; }
        
        /// <summary>
        /// Device module name
        /// </summary>
        public string ModuleName { get; set; }
    }
}
