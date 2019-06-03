namespace Unosquare.FFME.Container
{
    using ClosedCaptions;
    using Common;
    using Diagnostics;
    using FFmpeg.AutoGen;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Performs video picture decoding, scaling and extraction logic.
    /// </summary>
    /// <seealso cref="MediaComponent" />
    internal sealed unsafe class VideoComponent : MediaComponent
    {
        #region Private State Variables

        private readonly AVRational BaseFrameRateQ;
        private string AppliedFilterString;
        private string CurrentFilterArguments;

        private SwsContext* Scaler = null;
        private AVFilterGraph* FilterGraph = null;
        private AVFilterContext* SourceFilter = null;
        private AVFilterContext* SinkFilter = null;
        private AVFilterInOut* SinkInput = null;
        private AVFilterInOut* SourceOutput = null;
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
            BaseFrameRateQ = Stream->r_frame_rate;

            if (BaseFrameRateQ.den == 0 || BaseFrameRateQ.num == 0)
                BaseFrameRateQ = ffmpeg.av_guess_frame_rate(container.InputContext, Stream, null);

            if (BaseFrameRateQ.den == 0 || BaseFrameRateQ.num == 0)
            {
                this.LogWarning(Aspects.Component,
                    $"{nameof(VideoComponent)} was unable to extract valid frame rate. Will use 25fps (40ms)");

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

            var seekIndex = container.MediaOptions.VideoSeekIndex;
            SeekIndex = seekIndex != null && seekIndex.StreamIndex == StreamIndex ?
                new ReadOnlyCollection<VideoSeekIndexEntry>(seekIndex.Entries) :
                new ReadOnlyCollection<VideoSeekIndexEntry>(new List<VideoSeekIndexEntry>(0));
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the video scaler flags used to perform color space conversion (if needed).
        /// Point / nearest-neighbor is the default and it is the cheapest. This is by design as
        /// we don't change the dimensions of the image. We only do color conversion.
        /// </summary>
        public static int ScalerFlags { get; internal set; } = ffmpeg.SWS_POINT;

        /// <summary>
        /// Gets the base frame rate as reported by the stream component.
        /// All discrete timestamps can be represented in this frame rate.
        /// </summary>
        public double BaseFrameRate { get; }

        /// <summary>
        /// Gets the stream's average frame rate.
        /// </summary>
        public double AverageFrameRate { get; }

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

        /// <summary>
        /// Gets the video seek index for this component.
        /// Returns null if it was not set in the media options.
        /// </summary>
        public ReadOnlyCollection<VideoSeekIndexEntry> SeekIndex { get; }

        /// <summary>
        /// Provides access to the VideoFilter string of the container's MediaOptions.
        /// </summary>
        private string FilterString => Container?.MediaOptions?.VideoFilter;

        #endregion

        #region Methods

        /// <summary>
        /// Attaches a hardware accelerator to this video component.
        /// </summary>
        /// <param name="selectedConfig">The selected configuration.</param>
        /// <returns>
        /// Whether or not the hardware accelerator was attached.
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
                var initResultCode = ffmpeg.av_hwdevice_ctx_create(&devContextRef, accelerator.DeviceType, null, null, 0);
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
                this.LogError(Aspects.Component, "Could not attach hardware decoder.", ex);
                return false;
            }
        }

        /// <summary>
        /// Releases the hardware device context.
        /// </summary>
        public void ReleaseHardwareDevice()
        {
            if (HardwareDeviceContext == null) return;

            var context = HardwareDeviceContext;
            ffmpeg.av_buffer_unref(&context);
            HardwareDeviceContext = null;
            HardwareAccelerator = null;
        }

        /// <inheritdoc />
        public override bool MaterializeFrame(MediaFrame input, ref MediaBlock output, MediaBlock previousBlock)
        {
            if (output == null) output = new VideoBlock();
            if (input is VideoFrame == false || output is VideoBlock == false)
                throw new ArgumentNullException($"{nameof(input)} and {nameof(output)} are either null or not of a compatible media type '{MediaType}'");

            var source = (VideoFrame)input;
            var target = (VideoBlock)output;

            // Retrieve a suitable scaler or create it on the fly
            var newScaler = ffmpeg.sws_getCachedContext(
                Scaler,
                source.Pointer->width,
                source.Pointer->height,
                NormalizePixelFormat(source.Pointer),
                source.Pointer->width,
                source.Pointer->height,
                Constants.VideoPixelFormat,
                ScalerFlags,
                null,
                null,
                null);

            // if it's the first time we set the scaler, simply assign it.
            if (Scaler == null)
            {
                Scaler = newScaler;
                RC.Current.Add(Scaler);
            }

            // Reassign to the new scaler and remove the reference to the existing one
            // The get cached context function automatically frees the existing scaler.
            if (Scaler != newScaler)
            {
                RC.Current.Remove(Scaler);
                Scaler = newScaler;
            }

            // Perform scaling and save the data to our unmanaged buffer pointer
            if (target.Allocate(source, Constants.VideoPixelFormat)
                && target.TryAcquireWriterLock(out var writeLock))
            {
                using (writeLock)
                {
                    var targetStride = new[] { target.PictureBufferStride };
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

                    if (outputHeight <= 0)
                        return false;
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
            if (source.HasValidStartTime == false && previousBlock != null)
            {
                // Get timing information from the previous block
                target.StartTime = TimeSpan.FromTicks(previousBlock.EndTime.Ticks + 1);
                target.Duration = source.Duration.Ticks > 0 ? source.Duration : previousBlock.Duration;
                target.EndTime = TimeSpan.FromTicks(target.StartTime.Ticks + target.Duration.Ticks);

                // Guess picture number and SMTPE
                var frameRate = ffmpeg.av_guess_frame_rate(Container.InputContext, Stream, source.Pointer);
                target.DisplayPictureNumber = Utilities.ComputePictureNumber(StartTime, target.StartTime, frameRate);
                target.SmtpeTimeCode = Utilities.ComputeSmtpeTimeCode(target.DisplayPictureNumber, frameRate);
            }
            else
            {
                // We set the target properties directly from the source
                target.StartTime = source.StartTime;
                target.Duration = source.Duration;
                target.EndTime = source.EndTime;

                // Copy picture number and SMTPE
                target.DisplayPictureNumber = source.DisplayPictureNumber;
                target.SmtpeTimeCode = source.SmtpeTimeCode;
            }

            // Fill out other properties
            target.IsHardwareFrame = source.IsHardwareFrame;
            target.HardwareAcceleratorName = source.HardwareAcceleratorName;
            target.CompressedSize = source.CompressedSize;
            target.CodedPictureNumber = source.CodedPictureNumber;
            target.StreamIndex = source.StreamIndex;
            target.ClosedCaptions = new ReadOnlyCollection<ClosedCaptionPacket>(source.ClosedCaptions);

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

        /// <inheritdoc />
        protected override MediaFrame CreateFrameSource(IntPtr framePointer)
        {
            // Validate the video frame
            var frame = (AVFrame*)framePointer;

            if (framePointer == IntPtr.Zero || frame->width <= 0 || frame->height <= 0)
                return null;

            // Move the frame from hardware (GPU) memory to RAM (CPU)
            if (HardwareAccelerator != null)
            {
                frame = HardwareAccelerator.ExchangeFrame(CodecContext, frame, out var isHardwareFrame);
                IsUsingHardwareDecoding = isHardwareFrame;
            }

            // Init the filter graph for the frame
            InitializeFilterGraph(frame);

            AVFrame* outputFrame;

            // Changes in the filter graph can be applied by calling the ChangeMedia command
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
                    // therefore, we need to release the original
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

        /// <inheritdoc />
        protected override void Dispose(bool alsoManaged)
        {
            if (Scaler != null)
            {
                RC.Current.Remove(Scaler);
                ffmpeg.sws_freeContext(Scaler);
                Scaler = null;
            }

            DestroyFilterGraph();
            ReleaseHardwareDevice();
            base.Dispose(alsoManaged);
        }

        /// <summary>
        /// Gets the pixel format replacing deprecated pixel formats.
        /// AV_PIX_FMT_YUVJ.
        /// </summary>
        /// <param name="frame">The frame.</param>
        /// <returns>A normalized pixel format.</returns>
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
        /// <returns>The angle to rotate.</returns>
        private static double ComputeRotation(byte* matrixArrayRef)
        {
            const int displayMatrixLength = 9;

            if (matrixArrayRef == null) return 0;

            var matrix = new List<int>(displayMatrixLength);

            double rotation;
            var scale = new double[2];

            for (var i = 0; i < displayMatrixLength * sizeof(int); i += sizeof(int))
            {
                matrix.Add(BitConverter.ToInt32(new[]
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

                scale[0] = Math.Abs(scale[0]) <= double.Epsilon ? 1 : scale[0];
                scale[1] = Math.Abs(scale[1]) <= double.Epsilon ? 1 : scale[1];

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
        /// <returns>The length of the hypotenuse.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double ComputeHypotenuse(double a, double b) => Math.Sqrt((a * a) + (b * b));

        /// <summary>
        /// Computes the frame filter arguments that are appropriate for the video filtering chain.
        /// </summary>
        /// <param name="frame">The frame.</param>
        /// <returns>The base filter arguments.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        /// If necessary, disposes the existing filter graph and creates a new one based on the frame arguments.
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
        /// avfilter_graph_config.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InitializeFilterGraph(AVFrame* frame)
        {
            /*
             * References:
             * http://libav-users.943685.n4.nabble.com/Libav-user-yadif-deinterlace-how-td3606561.html
             * https://www.ffmpeg.org/doxygen/trunk/filtering_8c-source.html
             * https://raw.githubusercontent.com/FFmpeg/FFmpeg/release/3.2/ffplay.c
             */

            const string SourceFilterName = "buffer";
            const string SourceFilterInstance = "video_buffer";
            const string SinkFilterName = "buffersink";
            const string SinkFilterInstance = "video_buffersink";

            // Get a snapshot of the FilterString
            var filterString = FilterString;

            // For empty filter strings ensure filtegraph is destroyed
            if (string.IsNullOrWhiteSpace(filterString))
            {
                DestroyFilterGraph();
                return;
            }

            // Recreate the filtergraph if we have to
            if (filterString != AppliedFilterString)
                DestroyFilterGraph();

            // Ensure the filtergraph is compatible with the frame
            var filterArguments = ComputeFilterArguments(frame);
            if (filterArguments != CurrentFilterArguments)
                DestroyFilterGraph();
            else
                return;

            FilterGraph = ffmpeg.avfilter_graph_alloc();
            RC.Current.Add(FilterGraph);

            try
            {
                // Get a couple of pointers for source and sink buffers
                AVFilterContext* sourceFilterRef = null;
                AVFilterContext* sinkFilterRef = null;

                // Create the source filter
                var result = ffmpeg.avfilter_graph_create_filter(
                    &sourceFilterRef, ffmpeg.avfilter_get_by_name(SourceFilterName), SourceFilterInstance, filterArguments, null, FilterGraph);

                // Check filter creation
                if (result != 0)
                {
                    throw new MediaContainerException(
                        $"{nameof(ffmpeg.avfilter_graph_create_filter)} ({SourceFilterName}) failed. " +
                        $"Error {result}: {FFInterop.DecodeMessage(result)}");
                }

                // Create the sink filter
                result = ffmpeg.avfilter_graph_create_filter(
                    &sinkFilterRef, ffmpeg.avfilter_get_by_name(SinkFilterName), SinkFilterInstance, null, null, FilterGraph);

                // Check filter creation
                if (result != 0)
                {
                    throw new MediaContainerException(
                        $"{nameof(ffmpeg.avfilter_graph_create_filter)} ({SinkFilterName}) failed. " +
                        $"Error {result}: {FFInterop.DecodeMessage(result)}");
                }

                // Save the filter references
                SourceFilter = sourceFilterRef;
                SinkFilter = sinkFilterRef;

                // TODO: from ffplay, ffmpeg.av_opt_set_int_list(sink, "pixel_formats", (byte*)&f0, 1, ffmpeg.AV_OPT_SEARCH_CHILDREN)
                if (string.IsNullOrWhiteSpace(filterString))
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

                    result = ffmpeg.avfilter_graph_parse(FilterGraph, filterString, SinkInput, SourceOutput, null);
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
                this.LogError(Aspects.Component, $"Video filter graph could not be built: {filterString}.", ex);
                DestroyFilterGraph();
            }
            finally
            {
                CurrentFilterArguments = filterArguments;
                AppliedFilterString = filterString;
            }
        }

        #endregion

        #region IDisposable Support

        /// <summary>
        /// Destroys the filter graph releasing unmanaged resources.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DestroyFilterGraph()
        {
            try
            {
                if (FilterGraph == null) return;
                RC.Current.Remove(FilterGraph);
                var filterGraphRef = FilterGraph;
                ffmpeg.avfilter_graph_free(&filterGraphRef);

                FilterGraph = null;
                SinkInput = null;
                SourceOutput = null;
            }
            finally
            {
                AppliedFilterString = null;
                CurrentFilterArguments = null;
            }
        }

        #endregion
    }
}
