namespace Unosquare.FFmpegMediaElement
{
    using FFmpeg.AutoGen;
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Windows.Media.Imaging;

    unsafe partial class FFmpegMedia
    {
        #region Standard Media Properties

        public bool HasAudio { get; private set; }
        public bool HasVideo { get; private set; }

        public long StartDts { get; private set; }
        public MediaFrameType LeadingStreamType { get; private set; }
        public MediaFrameType LaggingStreamType { get; private set; }

        public string VideoCodec { get; private set; }
        public int VideoBitrate { get; private set; }
        public int VideoFrameWidth { get; private set; }
        public int VideoFrameHeight { get; private set; }
        public decimal VideoFrameRate { get; private set; }
        public decimal VideoFrameLength { get; private set; }

        public string AudioCodec { get; private set; }
        public int AudioBitrate { get; private set; }
        public int AudioChannels { get; private set; }
        public int AudioOutputBitsPerSample { get; private set; }
        public int AudioSampleRate { get; private set; }
        public int AudioOutputSampleRate { get; private set; }
        public int AudioBytesPerSample { get; private set; }

        public WriteableBitmap VideoRenderer { get; private set; }
        internal AudioRenderer AudioRenderer { get; private set; }

        #endregion

        #region FFmpeg Decoding Pipeline

        private AVFormatContext* InputFormatContext = null;

        private SwsContext* VideoResampler = null;
        private SwrContext* AudioResampler = null;

        private AVCodecContext* VideoCodecContext = null;
        private AVCodecContext* AudioCodecContext = null;

        private AVFrame* DecodedPictureHolder = null;
        private AVFrame* DecodedWaveHolder = null;

        private AVStream* InputVideoStream = null;
        private AVStream* InputAudioStream = null;

        private int OutputPictureBufferLength = -1;

        #endregion

        #region Media Initialization Methods

        /// <summary>
        /// Initializes the internal transcoder -- This create the input, processing, and output blocks that make
        /// up the video and audio decoding stream.
        /// </summary>
        /// <param name="filePath">The file path.</param>
        /// <param name="inputFormatName">Name of the input format. Leave null or empty to detect automatically</param>
        /// <param name="referer">The referer. Leave null or empty to skip setting it</param>
        /// <param name="userAgent">The user agent. Leave null or empty to skip setting it.</param>
        /// <exception cref="FileFormatException"></exception>
        /// <exception cref="Exception">Could not find stream info
        /// or
        /// Media must contain at least a video or and audio stream</exception>
        /// <exception cref="System.Exception">Could not open file
        /// or
        /// Could not find stream info
        /// or
        /// Media must contain a video stream
        /// or
        /// Media must contain an audio stream
        /// or
        /// Unsupported codec
        /// or
        /// Could not initialize the output conversion context
        /// or
        /// Could not create output codec context from input
        /// or
        /// Could not open codec</exception>
        private void InitializeMedia(string filePath, string inputFormatName, string referer, string userAgent)
        {
            // Create the input format context by opening the file
            InputFormatContext = ffmpeg.avformat_alloc_context();

            AVDictionary* optionsDict = null;

            if (string.IsNullOrWhiteSpace(userAgent) == false)
                ffmpeg.av_dict_set(&optionsDict, "user-agent", userAgent, 0);

            if (string.IsNullOrWhiteSpace(referer) == false)
                ffmpeg.av_dict_set(&optionsDict, "headers", $"Referer:{referer}", 0);

            ffmpeg.av_dict_set_int(&optionsDict, "usetoc", 1, 0);

            AVInputFormat* inputFormat = null;

            if (string.IsNullOrWhiteSpace(inputFormatName) == false)
            inputFormat = ffmpeg.av_find_input_format(inputFormatName);

            fixed (AVFormatContext** inputFormatContextRef = &InputFormatContext)
            {
                if (ffmpeg.avformat_open_input(inputFormatContextRef, filePath, inputFormat, &optionsDict) != 0)
                    throw new FileFormatException(string.Format("Could not open stream or file '{0}'", filePath));
            }

            InputFormatContext->iformat->flags |= ffmpeg.AVFMT_FLAG_NOBUFFER;
            InputFormatContext->iformat->flags |= ffmpeg.AVFMT_FLAG_NOFILLIN;

            ffmpeg.av_dict_free(&optionsDict);
            
            // Extract the stream info headers from the file
            if (ffmpeg.avformat_find_stream_info(InputFormatContext, null) != 0)
                throw new Exception("Could not find stream info");

            // search for the audio and video streams
            for (int i = 0; i < InputFormatContext->nb_streams; i++)
            {
                var codecType = InputFormatContext->streams[i]->codec->codec_type;

                if (codecType == AVMediaType.AVMEDIA_TYPE_VIDEO && InputVideoStream == null)
                {
                    InputVideoStream = InputFormatContext->streams[i];
                    continue;
                }

                if (codecType == AVMediaType.AVMEDIA_TYPE_AUDIO && InputAudioStream == null)
                {
                    InputAudioStream = InputFormatContext->streams[i];
                    continue;
                }
            }

            if (InputVideoStream != null)
            {
                this.InitializeVideo();
                this.HasVideo = VideoBitrate > 0 || VideoFrameRate > 0M || VideoFrameWidth > 0 || VideoFrameHeight > 0;
            }

            if (InputAudioStream != null)
            {
                this.InitializeAudio();
                this.HasAudio = AudioBytesPerSample > 0;
            }

            if (HasAudio == false && HasVideo == false)
            {
                throw new Exception("Media must contain at least a video or and audio stream");
            }
            else
            {
                // General Properties here

                NaturalDuration = Convert.ToDecimal(Convert.ToDouble(InputFormatContext->duration) / Convert.ToDouble(ffmpeg.AV_TIME_BASE));
                IsLiveStream = Helper.IsNoPtsValue(InputFormatContext->duration);
                StartTime = Convert.ToDecimal(Convert.ToDouble(InputFormatContext->start_time) / Convert.ToDouble(ffmpeg.AV_TIME_BASE));
                EndTime = StartTime + NaturalDuration;

                RealtimeClock.Seek(StartTime);
            }
        }

        /// <summary>
        /// Initializes the audio.
        /// </summary>
        /// <exception cref="System.Exception">
        /// Unsupported audio codec
        /// or
        /// Could not create audio output codec context from input
        /// or
        /// Could not open codec
        /// </exception>
        /// <exception cref="System.InvalidOperationException">Could not load media file</exception>
        private void InitializeAudio()
        {
            // Extract wave sample format and codec id
            var inputCodecContext = *(InputAudioStream->codec);
            var inputCodecId = inputCodecContext.codec_id;

            // Get an input decoder for the input codec
            AVCodec* inputDecoder = ffmpeg.avcodec_find_decoder(inputCodecId);
            if (inputDecoder == null)
                throw new Exception("Unsupported audio codec");

            //Create an output codec context. -- We copy the data from the input context and we
            //then proceed to adjust some output parameters.
            // Before it said: var outputCodecContext = &inputCodecContext;
            AudioCodecContext = ffmpeg.avcodec_alloc_context3(inputDecoder);
            if (ffmpeg.avcodec_copy_context(AudioCodecContext, &inputCodecContext) != Constants.SuccessCode)
                throw new Exception("Could not create audio output codec context from input");

            if ((inputDecoder->capabilities & (int)ffmpeg.CODEC_CAP_TRUNCATED) == (int)ffmpeg.CODEC_CAP_TRUNCATED)
                AudioCodecContext->flags |= ffmpeg.AV_CODEC_FLAG_TRUNCATED;

            if (ffmpeg.avcodec_open2(AudioCodecContext, inputDecoder, null) < Constants.SuccessCode)
                throw new Exception("Could not open codec");

            // setup basic properties
            AudioBytesPerSample = ffmpeg.av_get_bytes_per_sample(AudioCodecContext->sample_fmt);
            AudioCodec = inputCodecContext.codec_id.ToString();
            AudioChannels = inputCodecContext.channels;
            AudioBitrate = (int)inputCodecContext.bit_rate;
            AudioOutputBitsPerSample = ffmpeg.av_get_bytes_per_sample(Constants.AudioOutputSampleFormat) * 8;
            AudioSampleRate = inputCodecContext.sample_rate;
            AudioOutputSampleRate = AudioSampleRate > 44100 ? 44100 : AudioSampleRate; // We set a max of 44.1 kHz to save CPU. Anything more is too much (for most people).

            // Reference: http://www.ffmpeg.org/doxygen/2.0/group__lswr.html
            // Used Example: https://github.com/FFmpeg/FFmpeg/blob/7206b94fb893c63b187bcdfe26422b4e026a3ea0/doc/examples/resampling_audio.c
            AudioResampler = ffmpeg.swr_alloc();
            ffmpeg.av_opt_set_int(AudioResampler, "in_channel_layout", (long)AudioCodecContext->channel_layout, 0);
            ffmpeg.av_opt_set_int(AudioResampler, "out_channel_layout", (long)(ffmpeg.AV_CH_FRONT_LEFT | ffmpeg.AV_CH_FRONT_RIGHT), 0);
            ffmpeg.av_opt_set_int(AudioResampler, "in_sample_rate", AudioSampleRate, 0);
            ffmpeg.av_opt_set_int(AudioResampler, "out_sample_rate", AudioOutputSampleRate, 0);
            ffmpeg.av_opt_set_sample_fmt(AudioResampler, "in_sample_fmt", AudioCodecContext->sample_fmt, 0);
            ffmpeg.av_opt_set_sample_fmt(AudioResampler, "out_sample_fmt", Constants.AudioOutputSampleFormat, 0);
            ffmpeg.swr_init(AudioResampler);

            // All output frames will have the same length and will be held by the same structure; the Decoder frame holder.
            DecodedWaveHolder = ffmpeg.av_frame_alloc();

            // Ensure proper audio properties
            if (AudioOutputBitsPerSample <= 0 || AudioSampleRate <= 0)
                throw new InvalidOperationException("Could not load media file");
        }

        private void InitializeVideo()
        {
            // Extract pixel format and codec id
            var inputCodecContext = *(InputVideoStream->codec);
            var inputPixelFormat = inputCodecContext.pix_fmt;
            var inputCodecId = inputCodecContext.codec_id;

            // Populate basic properties
            VideoCodec = inputCodecContext.codec_id.ToString(); // Utils.GetAnsiString(new IntPtr(inputCodecContext.codec_name));
            VideoBitrate = (int)inputCodecContext.bit_rate;
            VideoFrameWidth = inputCodecContext.width;
            VideoFrameHeight = inputCodecContext.height;

            VideoFrameRate = Convert.ToDecimal(Convert.ToDouble(inputCodecContext.framerate.num) / Convert.ToDouble(inputCodecContext.framerate.den));
            VideoFrameLength = VideoFrameRate > 0M ? 1M / VideoFrameRate : 0M;

            // Get an input decoder for the input codec
            AVCodec* inputDecoder = ffmpeg.avcodec_find_decoder(inputCodecId);
            if (inputDecoder == null)
                throw new Exception("Unsupported video codec");

            // Create a Software Sacaling context -- this allows us to do fast colorspace conversion
            VideoResampler = ffmpeg.sws_getContext(
                VideoFrameWidth, VideoFrameHeight, inputPixelFormat,
                VideoFrameWidth, VideoFrameHeight, Constants.VideoOutputPixelFormat,
                (int)ffmpeg.SWS_BILINEAR, null, null, null);

            if (VideoResampler == null)
                throw new Exception("Could not initialize the output conversion context");

            //Create an output codec context. -- We copy the data from the input context and we
            //then proceed to adjust some output parameters.
            // Before it said: var outputCodecContext = &inputCodecContext;
            VideoCodecContext = ffmpeg.avcodec_alloc_context3(inputDecoder);
            if (ffmpeg.avcodec_copy_context(VideoCodecContext, &inputCodecContext) != Constants.SuccessCode)
                throw new Exception("Could not create video output codec context from input");

            if ((inputDecoder->capabilities & (int)ffmpeg.AV_CODEC_CAP_TRUNCATED) == (int)ffmpeg.AV_CODEC_CAP_TRUNCATED)
                VideoCodecContext->flags |= (int)ffmpeg.AV_CODEC_FLAG_TRUNCATED;

            if (ffmpeg.avcodec_open2(VideoCodecContext, inputDecoder, null) < Constants.SuccessCode)
                throw new Exception("Could not open codec");

            // All output frames will have the same length and will be held by the same structure; the Decoder frame holder.
            DecodedPictureHolder = ffmpeg.av_frame_alloc();
            OutputPictureBufferLength = ffmpeg.avpicture_get_size(Constants.VideoOutputPixelFormat, VideoFrameWidth, VideoFrameHeight);
        }

        #endregion

        #region Frame Pulling and Decoding Methods

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private FFmpegMediaFrame CreateMediaFrameFromDecodedWaveHolder()
        {
            // Resample 
            IntPtr bufferPtr = IntPtr.Zero;
            byte[] audioBuffer;

            try
            {
                var inputSampleCount = DecodedWaveHolder->nb_samples;
                var outputDelay = ffmpeg.swr_get_delay(AudioResampler, AudioSampleRate);
                var outputSampleCount = (int)ffmpeg.av_rescale_rnd(outputDelay + inputSampleCount, AudioOutputSampleRate, AudioSampleRate, AVRounding.AV_ROUND_UP);

                var outputLineSize = outputSampleCount * (this.AudioOutputBitsPerSample / 8);
                var maxBufferLength = outputLineSize * Constants.AudioOutputChannelCount;

                bufferPtr = System.Runtime.InteropServices.Marshal.AllocHGlobal(maxBufferLength);
                var bufferNativePtr = (sbyte*)bufferPtr.ToPointer();

                var convertSampleCount = ffmpeg.swr_convert(AudioResampler, &bufferNativePtr, outputSampleCount, DecodedWaveHolder->extended_data, inputSampleCount);
                var outputBufferLength = ffmpeg.av_samples_get_buffer_size(&outputLineSize, Constants.AudioOutputChannelCount, convertSampleCount, Constants.AudioOutputSampleFormat, 1);

                if (outputBufferLength < 0)
                    return null;

                audioBuffer = new byte[outputBufferLength];
                System.Runtime.InteropServices.Marshal.Copy(bufferPtr, audioBuffer, 0, audioBuffer.Length);

            }
            finally
            {
                if (bufferPtr != IntPtr.Zero)
                    System.Runtime.InteropServices.Marshal.FreeHGlobal(bufferPtr);
            }


            // Create the managed audio frame
            var mediaFrame = new FFmpegMediaFrame()
            {
                AudioBuffer = audioBuffer,
                Duration = Helper.TimestampToSeconds(DecodedWaveHolder->pkt_duration, InputAudioStream->time_base),
                CodedPictureNumber = -1,
                Flags = FFmpegMediaFrameFlags.None,
                PictureType = FFmpegPictureType.None,
                StartTime = Helper.TimestampToSeconds(DecodedWaveHolder->best_effort_timestamp, InputAudioStream->time_base),
                StreamIndex = InputAudioStream->index,
                Timestamp = DecodedWaveHolder->best_effort_timestamp,
                Type = MediaFrameType.Audio
            };

            return mediaFrame;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private FFmpegMediaFrame CreateMediaFrameFromDecodedPictureHolder()
        {
            // Create the output picture. Once the DecodeFrameHolder has the frame in YUV, the SWS API is
            // then used to convert to BGR24 and display on the screen.
            var outputPicture = (AVPicture*)ffmpeg.av_frame_alloc();
            var outputPictureBuffer = (sbyte*)ffmpeg.av_malloc((uint)OutputPictureBufferLength);
            ffmpeg.avpicture_fill(outputPicture, outputPictureBuffer, Constants.VideoOutputPixelFormat, VideoFrameWidth, VideoFrameHeight);

            // convert the colorspace from (typically) YUV to BGR24
            sbyte** sourceScan0 = &DecodedPictureHolder->data0;
            sbyte** targetScan0 = &outputPicture->data0;
            ffmpeg.sws_scale(
                VideoResampler, sourceScan0, DecodedPictureHolder->linesize, 0,
                VideoFrameHeight, targetScan0, outputPicture->linesize);

            // Compute data size and data pointer (stride and scan0, respectively)
            var imageStride = outputPicture->linesize[0];
            var imageDataSize = Convert.ToUInt32(VideoFrameHeight * imageStride);
            var imageDataPtr = new IntPtr(outputPicture->data0);

            // Create a MediaFrame object with the info we have -- we will return this 
            var mediaFrame = new FFmpegMediaFrame()
            {
                Picture = outputPicture,
                PictureBuffer = outputPictureBuffer,
                PictureBufferPtr = imageDataPtr,
                PictureBufferLength = imageDataSize,
                StartTime = Helper.TimestampToSeconds(DecodedPictureHolder->best_effort_timestamp, InputVideoStream->time_base),
                Flags = (FFmpegMediaFrameFlags)DecodedPictureHolder->flags,
                PictureType = (FFmpegPictureType)DecodedPictureHolder->pict_type,
                CodedPictureNumber = DecodedPictureHolder->coded_picture_number,
                Duration = Helper.TimestampToSeconds(DecodedPictureHolder->pkt_duration, InputVideoStream->time_base),
                Timestamp = DecodedPictureHolder->best_effort_timestamp,
                Type = MediaFrameType.Video,
                StreamIndex = InputVideoStream->index
            };

            return mediaFrame;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool FillDecodedWaveHolderFrame(AVPacket* readingPacket, bool emptyPacket)
        {
            var receivedFrame = 0;

            if (emptyPacket)
            {
                ffmpeg.av_init_packet(readingPacket);
                readingPacket->stream_index = InputAudioStream->index;
            }

            try
            {
                var decodingPacket = (AVPacket)System.Runtime.InteropServices.Marshal.PtrToStructure(new IntPtr(readingPacket), typeof(AVPacket)); // by-value copy
                while (decodingPacket.size > 0 || emptyPacket)
                {
                    var decodeResult = ffmpeg.avcodec_decode_audio4(AudioCodecContext, DecodedWaveHolder, &receivedFrame, &decodingPacket);
                    if (decodeResult < Constants.SuccessCode)
                    {
                        var errorMessage = Helper.GetFFmpegErrorMessage(decodeResult);
                        break;
                        throw new Exception(string.Format("Error decoding audio packet. Code {0} - {1}", decodeResult, errorMessage));
                    }
                    else
                    {
                        decodingPacket.size -= decodeResult;

                        if (emptyPacket && decodingPacket.size == 0)
                            break;
                    }
                }
            }
            finally
            {
                ffmpeg.av_free_packet(readingPacket);
            }

            return receivedFrame != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool FillDecodedPictureHolderFrame(AVPacket* readingPacket, bool emptyPacket)
        {
            var receivedFrame = 0;

            // Empty packet explanation: http://stackoverflow.com/questions/25526075/multiple-frames-lost-if-i-use-av-read-frame-in-ffmpeg
            if (emptyPacket)
            {
                ffmpeg.av_init_packet(readingPacket);
                readingPacket->stream_index = InputVideoStream->index;
            }

            try
            {
                var decodingPacket = (AVPacket)System.Runtime.InteropServices.Marshal.PtrToStructure(new IntPtr(readingPacket), typeof(AVPacket)); // by-value copy
                while (decodingPacket.size > 0 || emptyPacket)
                {
                    var decodeResult = ffmpeg.avcodec_decode_video2(VideoCodecContext, DecodedPictureHolder, &receivedFrame, readingPacket);
                    if (decodeResult < Constants.SuccessCode)
                    {
                        var errorMessage = Helper.GetFFmpegErrorMessage(decodeResult);
                        throw new Exception(string.Format("Error decoding video packet. Code {0} - {1}", decodeResult, errorMessage));
                    }
                    else
                    {
                        decodingPacket.size -= decodeResult;

                        if (emptyPacket && decodingPacket.size == 0)
                            break;
                    }
                }

            }
            finally
            {
                ffmpeg.av_free_packet(readingPacket);
            }

            return receivedFrame != 0;
        }

        /// <summary>
        /// Pulls the next-available frame. This does not queue the frame in either the video or audio queue.
        /// Please keep in mind that you will need to manually call the Release() method the returned object
        /// are done with it. If working with Media Caches, the cache will automatically release the frame
        /// </summary>
        /// <returns></returns>
        /// <exception cref="System.Exception">Error while decoding frame</exception>
        private FFmpegMediaFrame PullMediaFrame()
        {
            // Setup the holding packet
            var readingPacket = new AVPacket();
            ffmpeg.av_init_packet(&readingPacket);
            var readFrameResult = Constants.SuccessCode;
            FFmpegMediaFrame mediaFrameToReturn = null;
            var emptyPacket = false;
            var receivedFrame = false;
            var attemptDecoding = false;
            var isVideoPacket = false;
            var isAudioPacket = false;

            while (readFrameResult == Constants.SuccessCode || readFrameResult == Constants.EndOfFileErrorCode)
            {
                readFrameResult = ffmpeg.av_read_frame(InputFormatContext, &readingPacket);
                emptyPacket = readFrameResult == Constants.EndOfFileErrorCode;
                attemptDecoding = (readFrameResult >= Constants.SuccessCode || readFrameResult == Constants.EndOfFileErrorCode);
                isVideoPacket = HasVideo && readingPacket.stream_index == InputVideoStream->index;
                isAudioPacket = HasAudio && readingPacket.stream_index == InputAudioStream->index;

                if (attemptDecoding)
                {
                    if (isVideoPacket)
                    {
                        receivedFrame = this.FillDecodedPictureHolderFrame(&readingPacket, emptyPacket);
                        if (receivedFrame)
                        {
                            mediaFrameToReturn = CreateMediaFrameFromDecodedPictureHolder();
                            break;
                        }
                    }
                    else if (isAudioPacket)
                    {
                        receivedFrame = this.FillDecodedWaveHolderFrame(&readingPacket, emptyPacket);
                        if (receivedFrame)
                        {
                            mediaFrameToReturn = CreateMediaFrameFromDecodedWaveHolder();
                            break;
                        }
                    }
                }

                if (receivedFrame == false && readFrameResult == Constants.EndOfFileErrorCode)
                {
                    mediaFrameToReturn = null;
                    break;
                }
            }

            IsAtEndOfStream = readFrameResult == Constants.EndOfFileErrorCode && mediaFrameToReturn == null;
            return mediaFrameToReturn;
        }

        #endregion

        #region IDisposable Implementation

        ~FFmpegMedia()
        {
            this.Dispose();
        }

        /// <summary>
        /// Releases all managed and unmanaged resources
        /// </summary>
        public void Dispose()
        {
            if (IsCancellationPending)
                return;

            this.IsCancellationPending = true;

            this.VideoRenderTimer.Stop();

            if (this.AudioRenderer != null)
            {
                if (this.AudioRenderer.HasInitialized)
                    this.AudioRenderer.Stop();

                this.AudioRenderer.Dispose();
                this.AudioRenderer = null;
            }

            if (MediaFrameExtractorThread != null)
            {
                MediaFrameExtractorThread.Join();
                MediaFrameExtractorThread = null;
            }

            if (MediaFramesExtractedDone != null)
            {
                try
                {
                    MediaFramesExtractedDone.Dispose();
                    MediaFramesExtractedDone = null;
                }
                finally { }
            }

            if (PrimaryFramesCache != null)
            {
                PrimaryFramesCache.Clear();
                PrimaryFramesCache = null;
            }

            if (SecondaryFramesCache != null)
            {
                SecondaryFramesCache.Clear();
                SecondaryFramesCache = null;
            }

            if (VideoCodecContext != null)
            {
                fixed (AVCodecContext** videoCodecContextRef = &VideoCodecContext)
                {
                    ffmpeg.avcodec_close(VideoCodecContext);
                    ffmpeg.avcodec_free_context(videoCodecContextRef);
                    VideoCodecContext = null;
                }
            }

            if (AudioCodecContext != null)
            {
                fixed (AVCodecContext** audioCodecContextRef = &AudioCodecContext)
                {
                    ffmpeg.avcodec_close(AudioCodecContext);
                    ffmpeg.avcodec_free_context(audioCodecContextRef);
                    AudioCodecContext = null;
                }
            }

            if (VideoResampler != null)
            {
                ffmpeg.sws_freeContext(VideoResampler);
                VideoResampler = null;
            }

            if (AudioResampler != null)
            {
                fixed (SwrContext** audioResamplerRef = &AudioResampler)
                {
                    ffmpeg.swr_close(AudioResampler);
                    ffmpeg.swr_free(audioResamplerRef);
                    AudioResampler = null;
                }
            }

            if (InputFormatContext != null)
            {
                fixed (AVFormatContext** inputFormatContextRef = &InputFormatContext)
                {
                    ffmpeg.avformat_close_input(inputFormatContextRef);
                    ffmpeg.avformat_free_context(InputFormatContext);
                    InputFormatContext = null;
                }
            }

            if (DecodedPictureHolder != null)
            {
                ffmpeg.av_free(DecodedPictureHolder);
                DecodedPictureHolder = null;
            }

            if (DecodedWaveHolder != null)
            {
                ffmpeg.av_free(DecodedWaveHolder);
                DecodedWaveHolder = null;
            }

        }

        #endregion
    }
}
