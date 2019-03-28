namespace Unosquare.FFME.Container
{
    using FFmpeg.AutoGen;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Encapsulates Hardware Accelerator Properties.
    /// </summary>
    internal sealed unsafe class HardwareAccelerator
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HardwareAccelerator"/> class.
        /// </summary>
        /// <param name="component">The component this accelerator is attached to.</param>
        /// <param name="selectedConfig">The selected hardware device configuration.</param>
        public HardwareAccelerator(VideoComponent component, HardwareDeviceInfo selectedConfig)
        {
            Component = component;
            Name = selectedConfig.DeviceTypeName;
            DeviceType = selectedConfig.DeviceType;
            PixelFormat = selectedConfig.PixelFormat;
            GetFormatCallback = GetPixelFormat;
        }

        /// <summary>
        /// Gets the name of the HW accelerator.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the component this accelerator is attached to..
        /// </summary>
        public VideoComponent Component { get; }

        /// <summary>
        /// Gets the hardware output pixel format.
        /// </summary>
        public AVPixelFormat PixelFormat { get; }

        /// <summary>
        /// Gets the type of the hardware device.
        /// </summary>
        public AVHWDeviceType DeviceType { get; }

        /// <summary>
        /// Gets the callback used to resolve the hardware pixel format.
        /// </summary>
        public AVCodecContext_get_format GetFormatCallback { get; }

        /// <summary>
        /// Gets the supported hardware decoder device types for the given codec.
        /// </summary>
        /// <param name="codecId">The codec identifier.</param>
        /// <returns>
        /// A list of hardware device decoders compatible with the codec.
        /// </returns>
        public static List<HardwareDeviceInfo> GetCompatibleDevices(AVCodecID codecId)
        {
            const int AV_CODEC_HW_CONFIG_METHOD_HW_DEVICE_CTX = 0x01;
            var codec = ffmpeg.avcodec_find_decoder(codecId);
            var result = new List<HardwareDeviceInfo>(64);
            var configIndex = 0;

            // skip unsupported configs
            if (codec == null || codecId == AVCodecID.AV_CODEC_ID_NONE)
                return result;

            while (true)
            {
                var config = ffmpeg.avcodec_get_hw_config(codec, configIndex);
                if (config == null) break;

                if ((config->methods & AV_CODEC_HW_CONFIG_METHOD_HW_DEVICE_CTX) != 0
                    && config->device_type != AVHWDeviceType.AV_HWDEVICE_TYPE_NONE)
                {
                    result.Add(new HardwareDeviceInfo(config));
                }

                configIndex++;
            }

            return result;
        }

        /// <summary>
        /// Downloads the frame from the hardware into a software frame if possible.
        /// The input hardware frame gets freed and the return value will point to the new software frame.
        /// </summary>
        /// <param name="codecContext">The codec context.</param>
        /// <param name="input">The input frame coming from the decoder (may or may not be hardware).</param>
        /// <param name="isHardwareFrame">if set to <c>true</c> [comes from hardware] otherwise, hardware decoding was not performed.</param>
        /// <returns>
        /// The frame downloaded from the device into RAM.
        /// </returns>
        /// <exception cref="Exception">Failed to transfer data to output frame.</exception>
        public AVFrame* ExchangeFrame(AVCodecContext* codecContext, AVFrame* input, out bool isHardwareFrame)
        {
            isHardwareFrame = false;

            if (codecContext->hw_device_ctx == null)
                return input;

            isHardwareFrame = true;

            if (input->format != (int)PixelFormat)
                return input;

            var output = MediaFrame.CreateAVFrame();

            var result = ffmpeg.av_hwframe_transfer_data(output, input, 0);
            ffmpeg.av_frame_copy_props(output, input);
            if (result < 0)
            {
                MediaFrame.ReleaseAVFrame(output);
                throw new MediaContainerException("Failed to transfer data to output frame");
            }

            MediaFrame.ReleaseAVFrame(input);

            return output;
        }

        /// <summary>
        /// Gets the pixel format.
        /// Port of (get_format) method in ffmpeg.c.
        /// </summary>
        /// <param name="context">The codec context.</param>
        /// <param name="pixelFormats">The pixel formats.</param>
        /// <returns>The real pixel format that the codec will be using.</returns>
        private AVPixelFormat GetPixelFormat(AVCodecContext* context, AVPixelFormat* pixelFormats)
        {
            // The default output is the first pixel format found.
            var output = *pixelFormats;

            // Iterate throughout the different pixel formats provided by the codec
            for (var p = pixelFormats; *p != AVPixelFormat.AV_PIX_FMT_NONE; p++)
            {
                // Try to select a hardware output pixel format that matches the HW device
                if (*p == PixelFormat)
                {
                    output = PixelFormat;
                    break;
                }

                // Otherwise, just use the default SW pixel format
                output = *p;
            }

            // Return the current pixel format.
            return output;
        }
    }
}