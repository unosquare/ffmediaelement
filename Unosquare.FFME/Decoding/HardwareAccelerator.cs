namespace Unosquare.FFME.Decoding
{
    using FFmpeg.AutoGen;
    using System;

    internal unsafe class HardwareAccelerator
    {
        private readonly AVCodecContext_get_format GetFormatCallback;

        static HardwareAccelerator()
        {
            Dxva2 = new HardwareAccelerator
            {
                Name = "DXVA2",
                DeviceType = AVHWDeviceType.AV_HWDEVICE_TYPE_DXVA2,
                PixelFormat = AVPixelFormat.AV_PIX_FMT_DXVA2_VLD,
            };
        }

        private HardwareAccelerator()
        {
            // prevent instantiation outside this class
            GetFormatCallback = new AVCodecContext_get_format(GetFormat);
        }

        public static HardwareAccelerator Dxva2 { get; private set; }

        public string Name { get; private set; }

        public AVPixelFormat PixelFormat { get; private set; }

        public AVHWDeviceType DeviceType { get; private set; }

        public void AttachDevice(AVCodecContext* codecContext)
        {
            var result = ffmpeg.av_hwdevice_ctx_create(&codecContext->hw_device_ctx, DeviceType, "auto", null, 0);
            if (result < 0)
                throw new Exception($"Unable to initialize hardware context for device {Name}");

            codecContext->get_format = GetFormatCallback;
        }

        public AVFrame* ExchangeFrame(AVCodecContext* codecContext, AVFrame* input)
        {
            if (codecContext->hw_device_ctx == null)
                return input;

            var outputFormat = PixelFormat;

            // Nothing to do.
            if (input->format == (int)outputFormat)
                return input;

            var output = ffmpeg.av_frame_alloc();
            output->format = (int)outputFormat;

            var result = ffmpeg.av_hwframe_transfer_data(output, input, 0);
            if (result < 0)
                throw new Exception("Failed to transfer data to output frame");

            try
            {
                result = ffmpeg.av_frame_copy_props(output, input);
                if (result < 0)
                {
                    ffmpeg.av_frame_unref(input);
                    throw new Exception("Failed to copy frame properties to output frame!");
                }

                ffmpeg.av_frame_unref(input);
                ffmpeg.av_frame_move_ref(input, output);
                ffmpeg.av_frame_free(&output);
            }
            catch (Exception)
            {
                ffmpeg.av_frame_free(&output);
                throw;
            }

            return output;
        }

        private AVPixelFormat GetFormat(AVCodecContext* avctx, AVPixelFormat* pix_fmts)
        {
            while (*pix_fmts != AVPixelFormat.AV_PIX_FMT_NONE)
            {
                if (*pix_fmts == PixelFormat)
                    return PixelFormat;

                pix_fmts++;
            }
            
            return AVPixelFormat.AV_PIX_FMT_NONE;
        }
    }
}
