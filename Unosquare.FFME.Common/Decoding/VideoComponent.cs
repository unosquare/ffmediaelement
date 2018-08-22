namespace Unosquare.FFME.Decoding
{
    using Core;
    using FFmpeg.AutoGen;
    using Shared;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;

    /// <summary>
    /// Performs video picture decoding, scaling and extraction logic.
    /// </summary>
    /// <seealso cref="MediaComponent" />
    internal sealed unsafe class VideoComponent : MediaComponent
    {
        #region Private State Variables

        private readonly string FilterString;

        private readonly AVRational BaseFrameRateQ;
        private SwsContext* Scaler = null;

        private AVFilterGraph* FilterGraph = null;
        private AVFilterContext* SourceFilter = null;
        private AVFilterContext* SinkFilter = null;
        private AVFilterInOut* SinkInput = null;
        private AVFilterInOut* SourceOutput = null;

        private string CurrentFilterArguments = null;
        private AVBufferRef* HardwareDeviceContext = null;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="VideoComponent"/> class.
        /// </summary>
        /// <param name="container">The container.</param>
        /// <param name="streamIndex">Index of the stream.</param>
        internal VideoComponent(MediaContainer container, int streamIndex)
            : base(container, streamIndex)
        {
            FilterString = container.MediaOptions.VideoFilter;
            BaseFrameRateQ = Stream->r_frame_rate;

            if (BaseFrameRateQ.den == 0 || BaseFrameRateQ.num == 0)
                BaseFrameRateQ = ffmpeg.av_guess_frame_rate(container.InputContext, Stream, null);

            if (BaseFrameRateQ.den == 0 || BaseFrameRateQ.num == 0)
            {
                container.Parent.Log(MediaLogMessageType.Warning,
                    $"{nameof(VideoComponent)} - Unable to extract valid framerate. Will use 25fps (40ms)");
                BaseFrameRateQ.num = 25;
                BaseFrameRateQ.den = 1;
            }

            BaseFrameRate = BaseFrameRateQ.ToDouble();

            if (Stream->avg_frame_rate.den > 0 && Stream->avg_frame_rate.num > 0)
                AverageFrameRate = Stream->avg_frame_rate.ToDouble();
            else
                AverageFrameRate = BaseFrameRate;

            FrameWidth = Stream->codec->width;
            FrameHeight = Stream->codec->height;

            // Retrieve Matrix Rotation
            var displayMatrixRef = ffmpeg.av_stream_get_side_data(Stream, AVPacketSideDataType.AV_PKT_DATA_DISPLAYMATRIX, null);
            DisplayRotation = ComputeRotation(displayMatrixRef);

            var aspectRatio = ffmpeg.av_d2q((double)FrameWidth / FrameHeight, int.MaxValue);
            DisplayAspectWidth = aspectRatio.num;
            DisplayAspectHeight = aspectRatio.den;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the video scaler flags used to perfom colorspace conversion (if needed).
        /// Point / nearest-neighbor is the default and it is the cheapest. This is by design as
        /// we don't change the dimensions of the image. We only do color conversion.
        /// </summary>
        public static int ScalerFlags { get; internal set; } = ffmpeg.SWS_POINT;

        /// <summary>
        /// Gets the base frame rate as reported by the stream component.
        /// All discrete timestamps can be represented in this framerate.
        /// </summary>
        public double BaseFrameRate { get; private set; }

        /// <summary>
        /// Gets the stream's average framerate
        /// </summary>
        public double AverageFrameRate { get; private set; }

        /// <summary>
        /// Gets the width of the picture frame.
        /// </summary>
        public int FrameWidth { get; }

        /// <summary>
        /// Gets the height of the picture frame.
        /// </summary>
        public int FrameHeight { get; }

        /// <summary>
        /// Gets the display rotation.
        /// </summary>
        public double DisplayRotation { get; }

        /// <summary>
        /// Gets the display aspect width.
        /// This is NOT the pixel aspect width.
        /// </summary>
        public int DisplayAspectWidth { get; }

        /// <summary>
        /// Gets the display aspect height.
        /// This si NOT the pixel aspect height.
        /// </summary>
        public int DisplayAspectHeight { get; }

        /// <summary>
        /// Gets the hardware accelerator.
        /// </summary>
        public HardwareAccelerator HardwareAccelerator { get; private set; }

        /// <summary>
        /// Gets a value indicating whether this component is using hardware-assisted decoding.
        /// </summary>
        public bool IsUsingHardwareDecoding { get; private set; }

        #endregion

        #region Methods

        /// <summary>
        /// Attaches a hardware accelerator to this video component.
        /// </summary>
        /// <param name="selectedConfig">The selected configuration.</param>
        /// <returns>
        /// Whether or not the hardware accelerator was attached
        /// </returns>
        public bool AttachHardwareDevice(HardwareDeviceInfo selectedConfig)
        {
            // Check for no device selection
            if (selectedConfig == null)
                return false;

            try
            {
                var accelerator = new HardwareAccelerator(this, selectedConfig);

                AVBufferRef* devContextRef = null;
                var initResultCode = 0;
                initResultCode = ffmpeg.av_hwdevice_ctx_create(&devContextRef, accelerator.DeviceType, null, null, 0);
                if (initResultCode < 0)
                    throw new MediaContainerException($"Unable to initialize hardware context for device {accelerator.Name}");

                HardwareDeviceContext = devContextRef;
                HardwareAccelerator = accelerator;
                CodecContext->hw_device_ctx = ffmpeg.av_buffer_ref(HardwareDeviceContext);
                CodecContext->get_format = accelerator.GetFormatCallback;

                return true;
            }
            catch (Exception ex)
            {
                Container.Parent?.Log(MediaLogMessageType.Error, $"Could not attach hardware decoder. {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Releases the hardware device context.
        /// </summary>
        public void ReleaseHardwareDevice()
        {
            if (HardwareDeviceContext == null) return;

            var hwdc = HardwareDeviceContext;
            ffmpeg.av_buffer_unref(&hwdc);
            HardwareDeviceContext = null;
            HardwareAccelerator = null;
        }

        /// <summary>
        /// Converts decoded, raw frame data in the frame source into a a usable frame. <br />
        /// The process includes performing picture, samples or text conversions
        /// so that the decoded source frame data is easily usable in multimedia applications
        /// </summary>
        /// <param name="input">The source frame to use as an input.</param>
        /// <param name="output">The target frame that will be updated with the source frame. If null is passed the frame will be instantiated.</param>
        /// <param name="siblings">The siblings to help guess additional frame parameters.</param>
        /// <returns>
        /// Returns True if successful. False otherwise.
        /// </returns>
        /// <exception cref="ArgumentNullException">input</exception>
        public override bool MaterializeFrame(MediaFrame input, ref MediaBlock output, List<MediaBlock> siblings)
        {
            if (output == null) output = new VideoBlock();
            var source = input as VideoFrame;
            var target = output as VideoBlock;

            if (source == null || target == null)
                throw new ArgumentNullException($"{nameof(input)} and {nameof(output)} are either null or not of a compatible media type '{MediaType}'");

            // Retrieve a suitable scaler or create it on the fly
            var newScaler = ffmpeg.sws_getCachedContext(
                Scaler,
                source.Pointer->width,
                source.Pointer->height,
                NormalizePixelFormat(source.Pointer),
                source.Pointer->width,
                source.Pointer->height,
                Constants.Video.VideoPixelFormat,
                ScalerFlags,
                null,
                null,
                null);

            // if it's the first time we set the scaler, simply assign it.
            if (Scaler == null)
            {
                Scaler = newScaler;
                RC.Current.Add(Scaler, $"311: {nameof(VideoComponent)}.{nameof(MaterializeFrame)}()");
            }

            // Reassign to the new scaler and remove the reference to the existing one
            // The get cached context function automatically frees the existing scaler.
            if (Scaler != newScaler)
            {
                RC.Current.Remove(Scaler);
                Scaler = newScaler;
            }

            // Perform scaling and save the data to our unmanaged buffer pointer
            if (target.Allocate(source, Constants.Video.VideoPixelFormat)
                && target.TryAcquireWriterLock(out var writeLock))
            {
                using (writeLock)
                {
                    var targetStride = new int[] { target.PictureBufferStride };
                    var targetScan = default(byte_ptrArray8);
                    targetScan[0] = (byte*)target.Buffer;

                    // The scaling is done here
                    var outputHeight = ffmpeg.sws_scale(
                        Scaler,
                        source.Pointer->data,
                        source.Pointer->linesize,
                        0,
                        source.Pointer->height,
                        targetScan,
                        targetStride);
                }
            }
            else
            {
                return false;
            }

            // After scaling, we need to copy and guess some of the block properties
            // Flag the block if we have to
            target.IsStartTimeGuessed = source.HasValidStartTime == false;

            // Try to fix the start time, duration and End time if we don't have valid data
            if (source.HasValidStartTime == false && siblings != null && siblings.Count > 0)
            {
                // Get timing information from the last sibling
                var lastSibling = siblings[siblings.Count - 1];

                // We set the target properties
                target.StartTime = lastSibling.EndTime;
                target.Duration = source.Duration.Ticks > 0 ? source.Duration : lastSibling.Duration;
                target.EndTime = TimeSpan.FromTicks(target.StartTime.Ticks + target.Duration.Ticks);

                // Guess picture number and SMTPE
                var timeBase = ffmpeg.av_guess_frame_rate(Container.InputContext, Stream, source.Pointer);
                target.DisplayPictureNumber = Extensions.ComputePictureNumber(target.StartTime, target.Duration, 1);
                target.SmtpeTimecode = Extensions.ComputeSmtpeTimeCode(StartTimeOffset, target.Duration, timeBase, target.DisplayPictureNumber);
            }
            else
            {
                // We set the target properties directly from the source
                target.StartTime = source.StartTime;
                target.Duration = source.Duration;
                target.EndTime = source.EndTime;

                // Copy picture number and SMTPE
                target.DisplayPictureNumber = source.DisplayPictureNumber;
                target.SmtpeTimecode = source.SmtpeTimecode;
            }

            // Fill out other properties
            target.IsHardwareFrame = source.IsHardwareFrame;
            target.HardwareAcceleratorName = source.HardwareAcceleratorName;
            target.CompressedSize = source.CompressedSize;
            target.CodedPictureNumber = source.CodedPictureNumber;
            target.StreamIndex = source.StreamIndex;
            target.ClosedCaptions = new ReadOnlyCollection<ClosedCaptions.ClosedCaptionPacket>(source.ClosedCaptions);

            // Update the stream info object if we get Closed Caption Data
            if (StreamInfo.HasClosedCaptions == false && target.ClosedCaptions.Count > 0)
                StreamInfo.HasClosedCaptions = true;

            // Process the aspect ratio
            var aspectRatio = ffmpeg.av_guess_sample_aspect_ratio(Container.InputContext, Stream, source.Pointer);
            if (aspectRatio.num == 0 || aspectRatio.den == 0)
            {
                target.PixelAspectWidth = 1;
                target.PixelAspectHeight = 1;
            }
            else
            {
                target.PixelAspectWidth = aspectRatio.num;
                target.PixelAspectHeight = aspectRatio.den;
            }

            return true;
        }

        /// <summary>
        /// Creates a frame source object given the raw FFmpeg frame reference.
        /// </summary>
        /// <param name="framePointer">The raw FFmpeg frame pointer.</param>
        /// <returns>Create a managed fraome from an unmanaged one.</returns>
        protected override unsafe MediaFrame CreateFrameSource(IntPtr framePointer)
        {
            // Validate the video frame
            var frame = (AVFrame*)framePointer;

            if (framePointer == IntPtr.Zero || frame->width <= 0 || frame->height <= 0)
                return null;

            // Move the frame from hardware (GPU) memory to RAM (CPU)
            if (HardwareAccelerator != null)
            {
                frame = HardwareAccelerator.ExchangeFrame(CodecContext, frame, out bool isHardwareFrame);
                IsUsingHardwareDecoding = isHardwareFrame;
            }

            // Init the filtergraph for the frame
            if (string.IsNullOrWhiteSpace(FilterString) == false)
                InitializeFilterGraph(frame);

            AVFrame* outputFrame;

            // Changes in the filtergraph can be applied by calling the ChangeMedia command
            if (FilterGraph != null)
            {
                // Allocate the output frame
                outputFrame = MediaFrame.CloneAVFrame(frame);

                var result = ffmpeg.av_buffersrc_add_frame(SourceFilter, outputFrame);
                while (result >= 0)
                    result = ffmpeg.av_buffersink_get_frame_flags(SinkFilter, outputFrame, 0);

                if (outputFrame->width <= 0 || outputFrame->height <= 0)
                {
                    // If we don't have a valid output frame simply release it and
                    // return the original input frame
                    MediaFrame.ReleaseAVFrame(outputFrame);
                    outputFrame = frame;
                }
                else
                {
                    // the output frame is the new valid frame (output frame).
                    // threfore, we need to release the original
                    MediaFrame.ReleaseAVFrame(frame);
                }
            }
            else
            {
                outputFrame = frame;
            }

            // Check if the output frame is valid
            if (outputFrame->width <= 0 || outputFrame->height <= 0)
                return null;

            // Create the frame holder object and return it.
            return new VideoFrame(outputFrame, this);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="alsoManaged">
        /// <c>true</c> to release both managed and unmanaged resources;
        /// <c>false</c> to release only unmanaged resources.
        /// </param>
        protected override void Dispose(bool alsoManaged)
        {
            if (Scaler != null)
            {
                RC.Current.Remove(Scaler);
                ffmpeg.sws_freeContext(Scaler);
                Scaler = null;
            }

            DestroyFiltergraph();
            ReleaseHardwareDevice();
            base.Dispose(alsoManaged);
        }

        /// <summary>
        /// Gets the pixel format replacing deprecated pixel formats.
        /// AV_PIX_FMT_YUVJ
        /// </summary>
        /// <param name="frame">The frame.</param>
        /// <returns>A normalized pixel format</returns>
        private static AVPixelFormat NormalizePixelFormat(AVFrame* frame)
        {
            var currentFormat = (AVPixelFormat)frame->format;
            switch (currentFormat)
            {
                case AVPixelFormat.AV_PIX_FMT_YUVJ411P: return AVPixelFormat.AV_PIX_FMT_YUV411P;
                case AVPixelFormat.AV_PIX_FMT_YUVJ420P: return AVPixelFormat.AV_PIX_FMT_YUV420P;
                case AVPixelFormat.AV_PIX_FMT_YUVJ422P: return AVPixelFormat.AV_PIX_FMT_YUV422P;
                case AVPixelFormat.AV_PIX_FMT_YUVJ440P: return AVPixelFormat.AV_PIX_FMT_YUV440P;
                case AVPixelFormat.AV_PIX_FMT_YUVJ444P: return AVPixelFormat.AV_PIX_FMT_YUV444P;
                default: return currentFormat;
            }
        }

        /// <summary>
        /// Computes the Frame rotation property from side data.
        /// </summary>
        /// <param name="matrixArrayRef">The matrix array reference.</param>
        /// <returns>The angle to rotate</returns>
        private static double ComputeRotation(byte* matrixArrayRef)
        {
            const int displayMatrixLength = 9;

            if (matrixArrayRef == null) return 0;

            var matrix = new List<int>(displayMatrixLength);

            double rotation;
            var scale = new double[2];

            for (var i = 0; i < displayMatrixLength * sizeof(int); i += sizeof(int))
            {
                matrix.Add(BitConverter.ToInt32(new byte[]
                {
                    matrixArrayRef[i + 0],
                    matrixArrayRef[i + 1],
                    matrixArrayRef[i + 2],
                    matrixArrayRef[i + 3]
                }, 0));
            }

            // port of av_display_rotation_get
            {
                scale[0] = ComputeHypotenuse(Convert.ToDouble(matrix[0]), Convert.ToDouble(matrix[3]));
                scale[1] = ComputeHypotenuse(Convert.ToDouble(matrix[1]), Convert.ToDouble(matrix[4]));

                scale[0] = scale[0] == 0 ? 1 : scale[0];
                scale[1] = scale[1] == 0 ? 1 : scale[1];

                rotation = Math.Atan2(
                    Convert.ToDouble(matrix[1]) / scale[1],
                    Convert.ToDouble(matrix[0]) / scale[0]) * 180 / Math.PI;
            }

            // port of double get_rotation(AVStream *st)
            {
                rotation -= 360 * Math.Floor((rotation / 360) + (0.9 / 360));
            }

            return rotation;
        }

        /// <summary>
        /// Computes the hypotenuse (right-angle triangles only).
        /// </summary>
        /// <param name="a">a.</param>
        /// <param name="b">The b.</param>
        /// <returns>The length of the hypotenuse</returns>
        private static double ComputeHypotenuse(double a, double b)
        {
            return Math.Sqrt((a * a) + (b * b));
        }

        /// <summary>
        /// Computes the frame filter arguments that are appropriate for the video filtering chain.
        /// </summary>
        /// <param name="frame">The frame.</param>
        /// <returns>The base filter arguments</returns>
        private string ComputeFilterArguments(AVFrame* frame)
        {
            var arguments =
                 $"video_size={frame->width}x{frame->height}:pix_fmt={frame->format}:" +
                 $"time_base={Stream->time_base.num}/{Stream->time_base.den}:" +
                 $"pixel_aspect={CodecContext->sample_aspect_ratio.num}/{Math.Max(CodecContext->sample_aspect_ratio.den, 1)}";

            if (BaseFrameRateQ.num != 0 && BaseFrameRateQ.den != 0)
                arguments = $"{arguments}:frame_rate={BaseFrameRateQ.num}/{BaseFrameRateQ.den}";

            return arguments;
        }

        /// <summary>
        /// If necessary, disposes the existing filtergraph and creates a new one based on the frame arguments.
        /// </summary>
        /// <param name="frame">The frame.</param>
        /// <exception cref="MediaContainerException">
        /// avfilter_graph_create_filter
        /// or
        /// avfilter_graph_create_filter
        /// or
        /// avfilter_link
        /// or
        /// avfilter_graph_parse
        /// or
        /// avfilter_graph_config
        /// </exception>
        private void InitializeFilterGraph(AVFrame* frame)
        {
            /*
             * References:
             * http://libav-users.943685.n4.nabble.com/Libav-user-yadif-deinterlace-how-td3606561.html
             * https://www.ffmpeg.org/doxygen/trunk/filtering_8c-source.html
             * https://raw.githubusercontent.com/FFmpeg/FFmpeg/release/3.2/ffplay.c
             */

            var frameArguments = ComputeFilterArguments(frame);
            if (string.IsNullOrWhiteSpace(CurrentFilterArguments) || frameArguments.Equals(CurrentFilterArguments) == false)
                DestroyFiltergraph();
            else
                return;

            FilterGraph = ffmpeg.avfilter_graph_alloc();
            RC.Current.Add(FilterGraph, $"144: {nameof(VideoComponent)}.{nameof(InitializeFilterGraph)}()");
            CurrentFilterArguments = frameArguments;

            try
            {
                var result = 0;

                // Get a couple of pointers for source and sink buffers
                AVFilterContext* sourceFileterRef = null;
                AVFilterContext* sinkFilterRef = null;

                // Create the source filter
                result = ffmpeg.avfilter_graph_create_filter(
                    &sourceFileterRef, ffmpeg.avfilter_get_by_name("buffer"), "video_buffer", CurrentFilterArguments, null, FilterGraph);

                // Check filter creation
                if (result != 0)
                {
                    throw new MediaContainerException(
                        $"{nameof(ffmpeg.avfilter_graph_create_filter)} (buffer) failed. " +
                        $"Error {result}: {FFInterop.DecodeMessage(result)}");
                }

                // Create the sink filter
                result = ffmpeg.avfilter_graph_create_filter(
                    &sinkFilterRef, ffmpeg.avfilter_get_by_name("buffersink"), "video_buffersink", null, null, FilterGraph);

                // Check filter creation
                if (result != 0)
                {
                    throw new MediaContainerException(
                        $"{nameof(ffmpeg.avfilter_graph_create_filter)} (buffersink) failed. " +
                        $"Error {result}: {FFInterop.DecodeMessage(result)}");
                }

                // Save the filter references
                SourceFilter = sourceFileterRef;
                SinkFilter = sinkFilterRef;

                // TODO: from ffplay, ffmpeg.av_opt_set_int_list(sink, "pix_fmts", (byte*)&f0, 1, ffmpeg.AV_OPT_SEARCH_CHILDREN);
                if (string.IsNullOrWhiteSpace(FilterString))
                {
                    result = ffmpeg.avfilter_link(SourceFilter, 0, SinkFilter, 0);
                    if (result != 0)
                    {
                        throw new MediaContainerException(
                            $"{nameof(ffmpeg.avfilter_link)} failed. " +
                            $"Error {result}: {FFInterop.DecodeMessage(result)}");
                    }
                }
                else
                {
                    var initFilterCount = FilterGraph->nb_filters;

                    SourceOutput = ffmpeg.avfilter_inout_alloc();
                    SourceOutput->name = ffmpeg.av_strdup("in");
                    SourceOutput->filter_ctx = SourceFilter;
                    SourceOutput->pad_idx = 0;
                    SourceOutput->next = null;

                    SinkInput = ffmpeg.avfilter_inout_alloc();
                    SinkInput->name = ffmpeg.av_strdup("out");
                    SinkInput->filter_ctx = SinkFilter;
                    SinkInput->pad_idx = 0;
                    SinkInput->next = null;

                    result = ffmpeg.avfilter_graph_parse(FilterGraph, FilterString, SinkInput, SourceOutput, null);
                    if (result != 0)
                        throw new MediaContainerException($"{nameof(ffmpeg.avfilter_graph_parse)} failed. Error {result}: {FFInterop.DecodeMessage(result)}");

                    // Reorder the filters to ensure that inputs of the custom filters are merged first
                    for (var i = 0; i < FilterGraph->nb_filters - initFilterCount; i++)
                    {
                        var sourceAddress = FilterGraph->filters[i];
                        var targetAddress = FilterGraph->filters[i + initFilterCount];
                        FilterGraph->filters[i] = targetAddress;
                        FilterGraph->filters[i + initFilterCount] = sourceAddress;
                    }
                }

                result = ffmpeg.avfilter_graph_config(FilterGraph, null);
                if (result != 0)
                    throw new MediaContainerException($"{nameof(ffmpeg.avfilter_graph_config)} failed. Error {result}: {FFInterop.DecodeMessage(result)}");
            }
            catch (Exception ex)
            {
                Container.Parent?.Log(MediaLogMessageType.Error, $"Video filter graph could not be built: {FilterString}.\r\n{ex.Message}");
                DestroyFiltergraph();
            }
        }

        #endregion

        #region IDisposable Support

        /// <summary>
        /// Destroys the filtergraph releasing unmanaged resources.
        /// </summary>
        private void DestroyFiltergraph()
        {
            if (FilterGraph != null)
            {
                RC.Current.Remove(FilterGraph);
                var filterGraphRef = FilterGraph;
                ffmpeg.avfilter_graph_free(&filterGraphRef);

                FilterGraph = null;
                SinkInput = null;
                SourceOutput = null;
            }
        }

        #endregion
    }
}
