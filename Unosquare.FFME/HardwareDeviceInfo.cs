namespace Unosquare.FFME
{
    using FFmpeg.AutoGen;

    /// <summary>
    /// Represents a hardware configuration pair of device and pixel format.
    /// </summary>
    public sealed unsafe class HardwareDeviceInfo
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HardwareDeviceInfo"/> class.
        /// </summary>
        /// <param name="config">The source configuration.</param>
        internal HardwareDeviceInfo(AVCodecHWConfig* config)
        {
            DeviceType = config->device_type;
            PixelFormat = config->pix_fmt;
            DeviceTypeName = ffmpeg.av_hwdevice_get_type_name(DeviceType);
            PixelFormatName = ffmpeg.av_get_pix_fmt_name(PixelFormat);
        }

        /// <summary>
        /// Gets the type of hardware device.
        /// </summary>
        public AVHWDeviceType DeviceType { get; }

        /// <summary>
        /// Gets the name of the device type.
        /// </summary>
        public string DeviceTypeName { get; }

        /// <summary>
        /// Gets the hardware output pixel format.
        /// </summary>
        public AVPixelFormat PixelFormat { get; }

        /// <summary>
        /// Gets the name of the pixel format.
        /// </summary>
        public string PixelFormatName { get; }

        /// <summary>
        /// Returns a <see cref="string" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="string" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return $"Device {DeviceTypeName}: {PixelFormatName}";
        }
    }
}
