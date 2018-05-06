namespace Unosquare.FFME.Decoding
{
    using Core;
    using FFmpeg.AutoGen;
    using Shared;
    using System;
    using System.Collections.Generic;

    internal unsafe class HardwareAcceleration
    {
        /// <summary>
        /// The get format callback
        /// </summary>
        private readonly AVCodecContext_get_format GetFormatCallback;

        private VideoComponent Component;

        /// <summary>
        /// Prevents a default instance of the <see cref="HardwareAcceleration"/> class from being created.
        /// </summary>
        private HardwareAcceleration()
        {
            // prevent instantiation outside this class
            GetFormatCallback = new AVCodecContext_get_format(GetPixelFormat);
        }

        /// <summary>
        /// Gets the name of the HW accelerator.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Gets the hardware output pixel format.
        /// </summary>
        public AVPixelFormat PixelFormat { get; private set; }

        /// <summary>
        /// Gets the type of the hardware device.
        /// </summary>
        public AVHWDeviceType DeviceType { get; private set; }

        /// <summary>
        /// Attaches a hardware accelerator to the specified component.
        /// </summary>
        /// <param name="component">The component.</param>
        /// <param name="selectedConfig">The selected configuration.</param>
        /// <returns>
        /// Whether or not the hardware accelerator was attached
        /// </returns>
        public static bool Attach(VideoComponent component, HardwareDeviceInfo selectedConfig)
        {
            try
            {
                var result = new HardwareAcceleration
                {
                    Component = component,
                    Name = selectedConfig.DeviceTypeName,
                    DeviceType = selectedConfig.DeviceType,
                    PixelFormat = selectedConfig.PixelFormat,
                };

                result.InitializeHardwareContext();
                return true;
            }
            catch (Exception ex)
            {
                component.Container.Parent?.Log(MediaLogMessageType.Error, $"Could not attach hardware decoder. {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets the supported hardware decoder device types for the given codec.
        /// </summary>
        /// <param name="codecId">The codec identifier.</param>
        /// <returns>
        /// A list of hardware device decoders compatible with the codec
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
        /// Detaches and disposes the hardware device context from the specified video component
        /// </summary>
        public void Release()
        {
            if (Component.HardwareDeviceContext != null)
            {
                fixed (AVBufferRef** hwdc = &Component.HardwareDeviceContext)
                {
                    ffmpeg.av_buffer_unref(hwdc);
                    Component.HardwareDeviceContext = null;
                    Component.HardwareAccelerator = null;
                }
            }
        }

        /// <summary>
        /// Downloads the frame from the hardware into a software frame if possible.
        /// The input hardware frame gets freed and the return value will point to the new software frame
        /// </summary>
        /// <param name="codecContext">The codec context.</param>
        /// <param name="input">The input frame coming from the decoder (may or may not be hardware).</param>
        /// <param name="comesFromHardware">if set to <c>true</c> [comes from hardware] otherwise, hardware decoding was not perfomred.</param>
        /// <returns>
        /// The frame downloaded from the device into RAM
        /// </returns>
        /// <exception cref="Exception">Failed to transfer data to output frame</exception>
        public AVFrame* ExchangeFrame(AVCodecContext* codecContext, AVFrame* input, out bool comesFromHardware)
        {
            comesFromHardware = false;

            if (codecContext->hw_device_ctx == null)
                return input;

            comesFromHardware = true;

            if (input->format != (int)PixelFormat)
                return input;

            var output = ffmpeg.av_frame_alloc();

            var result = ffmpeg.av_hwframe_transfer_data(output, input, 0);
            ffmpeg.av_frame_copy_props(output, input);
            if (result < 0)
            {
                ffmpeg.av_frame_free(&output);
                throw new Exception("Failed to transfer data to output frame");
            }

            ffmpeg.av_frame_free(&input);
            RC.Current.Remove((IntPtr)input);
            RC.Current.Add(output, $"86: {nameof(HardwareAcceleration)}[{PixelFormat}].{nameof(ExchangeFrame)}()");

            return output;
        }

        /// <summary>
        /// Attaches a hardware device context to the specified video component.
        /// </summary>
        /// <exception cref="Exception">Throws when unable to initialize the hardware device</exception>
        private void InitializeHardwareContext()
        {
            fixed (AVBufferRef** devContextRef = &Component.HardwareDeviceContext)
            {
                var initResultCode = 0;
                initResultCode = ffmpeg.av_hwdevice_ctx_create(devContextRef, DeviceType, null, null, 0);
                if (initResultCode < 0)
                    throw new Exception($"Unable to initialize hardware context for device {Name}");
            }

            Component.HardwareAccelerator = this;
            Component.CodecContext->hw_device_ctx = ffmpeg.av_buffer_ref(Component.HardwareDeviceContext);
            Component.CodecContext->get_format = GetFormatCallback;
        }

        /// <summary>
        /// Gets the pixel format.
        /// Port of (get_format) method in ffmpeg.c
        /// </summary>
        /// <param name="avctx">The codec context.</param>
        /// <param name="pix_fmts">The pixel formats.</param>
        /// <returns>The real pixel format that the codec will be using</returns>
        private AVPixelFormat GetPixelFormat(AVCodecContext* avctx, AVPixelFormat* pix_fmts)
        {
            // The default output is the first pixel format found.
            var output = *pix_fmts;

            // Iterate throught the different pixel formats provided by the codec
            for (var p = pix_fmts; *p != AVPixelFormat.AV_PIX_FMT_NONE; p++)
            {
                // Try to select a hardware output pixel format that matches the HW device
                if (*pix_fmts == PixelFormat)
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