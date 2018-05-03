namespace Unosquare.FFME.Shared
{
    using FFmpeg.AutoGen;

    /// <summary>
    /// Represents a hardware configuration pair of device and pixel format
    /// </summary>
    public unsafe class HardwareDeviceInfo
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HardwareDeviceInfo"/> class.
        /// </summary>
        /// <param name="config">The source configuration.</param>
        internal HardwareDeviceInfo(AVCodecHWConfig* config)
        {
            DeviceType = config->device_type;
            PixelFormat = config->pix_fmt;
        }

        /// <summary>
        /// Gets the type of hardware device.
        /// </summary>
        public AVHWDeviceType DeviceType { get; internal set; }

        /// <summary>
        /// Gets the hardware output pixel format.
        /// </summary>
        public AVPixelFormat PixelFormat { get; internal set; }
    }
}
