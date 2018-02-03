namespace Unosquare.FFME.Decoding
{
    using Core;
    using FFmpeg.AutoGen;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;

    internal unsafe class HardwareAccelerator
    {
        /// <summary>
        /// The get format callback
        /// </summary>
        private readonly AVCodecContext_get_format GetFormatCallback;

        /// <summary>
        /// Initializes static members of the <see cref="HardwareAccelerator"/> class.
        /// </summary>
        static HardwareAccelerator()
        {
            Dxva2 = new HardwareAccelerator
            {
                Name = "DXVA2",
                DeviceType = AVHWDeviceType.AV_HWDEVICE_TYPE_DXVA2,
                PixelFormat = AVPixelFormat.AV_PIX_FMT_DXVA2_VLD,
                RequiresTransfer = true,
            };

            Cuda = new HardwareAccelerator
            {
                Name = "CUVID",
                DeviceType = AVHWDeviceType.AV_HWDEVICE_TYPE_CUDA,
                PixelFormat = AVPixelFormat.AV_PIX_FMT_CUDA,
                RequiresTransfer = false,
            };

            All = new ReadOnlyDictionary<AVPixelFormat, HardwareAccelerator>(
                new Dictionary<AVPixelFormat, HardwareAccelerator>()
                {
                    { Dxva2.PixelFormat, Dxva2 },
                    { Cuda.PixelFormat, Cuda }
                });
        }

        /// <summary>
        /// Prevents a default instance of the <see cref="HardwareAccelerator"/> class from being created.
        /// </summary>
        private HardwareAccelerator()
        {
            // prevent instantiation outside this class
            GetFormatCallback = new AVCodecContext_get_format(GetPixelFormat);
        }

        /// <summary>
        /// A dicitionary containing all Accelerators by pixel format
        /// </summary>
        public static ReadOnlyDictionary<AVPixelFormat, HardwareAccelerator> All { get; }

        /// <summary>
        /// Gets the dxva2 accelerator.
        /// </summary>
        public static HardwareAccelerator Dxva2 { get; }

        /// <summary>
        /// Gets the CUDA video accelerator.
        /// </summary>
        public static HardwareAccelerator Cuda { get; }

        /// <summary>
        /// Gets the name of the HW accelerator.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the frame requires the transfer from
        /// the hardware to RAM
        /// </summary>
        public bool RequiresTransfer { get; private set; }

        /// <summary>
        /// Gets the hardware output pixel format.
        /// </summary>
        public AVPixelFormat PixelFormat { get; private set; }

        /// <summary>
        /// Gets the type of the hardware device.
        /// </summary>
        public AVHWDeviceType DeviceType { get; private set; }

        /// <summary>
        /// Attaches a hardware device context to the specified video component.
        /// </summary>
        /// <param name="component">The component.</param>
        /// <exception cref="Exception">Throws when unable to initialize the hardware device</exception>
        public void AttachDevice(VideoComponent component)
        {
            var result = 0;

            fixed (AVBufferRef** devContextRef = &component.HardwareDeviceContext)
            {
                result = ffmpeg.av_hwdevice_ctx_create(devContextRef, DeviceType, null, null, 0);
                if (result < 0)
                    throw new Exception($"Unable to initialize hardware context for device {Name}");
            }

            component.HardwareAccelerator = this;
            component.CodecContext->hw_device_ctx = ffmpeg.av_buffer_ref(component.HardwareDeviceContext);
            component.CodecContext->get_format = GetFormatCallback;
        }

        /// <summary>
        /// Detaches and disposes the hardware device context from the specified video component
        /// </summary>
        /// <param name="component">The component.</param>
        public void DetachDevice(VideoComponent component)
        {
            // TODO: (Floyd) Check the below code in the future because I am not sure
            // how to uninitialize the hardware device context
            if (component.CodecContext != null)
            {
                ffmpeg.av_buffer_unref(&component.CodecContext->hw_device_ctx);
                component.CodecContext->hw_device_ctx = null;
            }

            if (component.HardwareDeviceContext != null)
            {
                fixed (AVBufferRef** hwdc = &component.HardwareDeviceContext)
                {
                    ffmpeg.av_buffer_unref(hwdc);
                    component.HardwareDeviceContext = null;
                    component.HardwareAccelerator = null;
                }
            }
        }

        /// <summary>
        /// Downloads the frame from the hardware into a software frame if possible.
        /// The input hardware frame gets freed and the return value will point to the new software frame
        /// </summary>
        /// <param name="codecContext">The codec context.</param>
        /// <param name="input">The input.</param>
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

            if (RequiresTransfer == false)
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