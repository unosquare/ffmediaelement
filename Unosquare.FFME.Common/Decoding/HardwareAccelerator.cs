namespace Unosquare.FFME.Decoding
{
    using Core;
    using FFmpeg.AutoGen;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    internal unsafe class HardwareAccelerator
    {
        /// <summary>
        /// The get format callback
        /// </summary>
        private readonly AVCodecContext_get_format GetFormatCallback;

        private VideoComponent Component;

        /// <summary>
        /// Prevents a default instance of the <see cref="HardwareAccelerator"/> class from being created.
        /// </summary>
        private HardwareAccelerator()
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
        /// <returns>Whether or not the hardware accelerator was attached</returns>
        public static bool Attach(VideoComponent component)
        {
            var configs = GetCompatibleConfigs(component);
            if (configs.Count <= 0) return false;

            var selectedConfig = default(AVCodecHWConfig?);
            var deviceTypePriority = new AVHWDeviceType[]
            {
                AVHWDeviceType.AV_HWDEVICE_TYPE_CUDA,
                AVHWDeviceType.AV_HWDEVICE_TYPE_D3D11VA,
                AVHWDeviceType.AV_HWDEVICE_TYPE_DXVA2
            };

            foreach (var deviceType in deviceTypePriority)
            {
                var entry = configs.FirstOrDefault(c => c.device_type == deviceType);
                if (entry.device_type != AVHWDeviceType.AV_HWDEVICE_TYPE_NONE)
                {
                    selectedConfig = entry;
                    break;
                }
            }

            if (selectedConfig == null)
                selectedConfig = configs[0];

            var result = new HardwareAccelerator
            {
                Component = component,
                Name = ffmpeg.av_hwdevice_get_type_name(selectedConfig.Value.device_type),
                DeviceType = selectedConfig.Value.device_type,
                PixelFormat = selectedConfig.Value.pix_fmt
            };

            result.InitializeHardwareContext();
            return true;
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
            RC.Current.Add(output, $"86: {nameof(HardwareAccelerator)}[{PixelFormat}].{nameof(ExchangeFrame)}()");

            return output;
        }

        /// <summary>
        /// Gets the supported hardware decoder device types.
        /// </summary>
        /// <param name="component">The component.</param>
        /// <returns>A list of hardware device decoders compatible with the codec</returns>
        private static List<AVCodecHWConfig> GetCompatibleConfigs(VideoComponent component)
        {
            const int AV_CODEC_HW_CONFIG_METHOD_HW_DEVICE_CTX = 0x01;
            var codec = ffmpeg.avcodec_find_decoder(component.CodecContext->codec_id);
            var result = new List<AVCodecHWConfig>(64);
            var configIndex = 0;
            while (true)
            {
                var config = ffmpeg.avcodec_get_hw_config(codec, configIndex);
                if (config == null) break;

                if ((config->methods & AV_CODEC_HW_CONFIG_METHOD_HW_DEVICE_CTX) != 0
                    && config->device_type != AVHWDeviceType.AV_HWDEVICE_TYPE_NONE)
                {
                    var configCopy = new AVCodecHWConfig
                    {
                        device_type = config->device_type,
                        methods = config->methods,
                        pix_fmt = config->pix_fmt
                    };

                    result.Add(configCopy);
                }

                configIndex++;
            }

            return result;
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