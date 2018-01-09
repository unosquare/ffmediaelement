namespace Unosquare.FFME.Decoding
{
    using Core;
    using FFmpeg.AutoGen;
    using Shared;
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Performs video picture decoding, scaling and extraction logic.
    /// </summary>
    /// <seealso cref="Unosquare.FFME.Decoding.MediaComponent" />
    internal sealed unsafe class VideoComponent : MediaComponent
    {
        #region Private State Variables

#pragma warning disable SA1401 // Fields must be private
        internal AVBufferRef* HardwareDeviceContext = null;
        internal HardwareAccelerator HardwareAccelerator = null;
        internal bool IsUsingHardwareDecoding = false;
#pragma warning restore SA1401 // Fields must be private

        /// <summary>
        /// Holds a reference to the video scaler
        /// </summary>
        private SwsContext* Scaler = null;
        private AVRational BaseFrameRateQ;

        private AVFilterGraph* FilterGraph = null;
        private AVFilterContext* SourceFilter = null;
        private AVFilterContext* SinkFilter = null;
        private AVFilterInOut* SinkInput = null;
        private AVFilterInOut* SourceOutput = null;

        private string CurrentFilterArguments = null;
        private string FilterString = null;

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
            BaseFrameRateQ = ffmpeg.av_guess_frame_rate(container.InputContext, Stream, null);
            FilterString = container.MediaOptions.VideoFilter;

            if (double.IsNaN(BaseFrameRate))
                BaseFrameRateQ = Stream->r_frame_rate;

            CurrentFrameRate = BaseFrameRate;
            if (double.IsNaN(CurrentFrameRate))
                CurrentFrameRate = Stream->avg_frame_rate.ToDouble();

            FrameWidth = Stream->codec->width;
            FrameHeight = Stream->codec->height;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the video scaler flags used to perfom colorspace conversion (if needed).
        /// </summary>
        public static int ScalerFlags { get; internal set; } = ffmpeg.SWS_BICUBIC;

        /// <summary>
        /// Gets the base frame rate as reported by the stream component.
        /// All discrete timestamps can be represented in this framerate.
        /// </summary>
        public double BaseFrameRate => BaseFrameRateQ.ToDouble();

        /// <summary>
        /// Gets the current frame rate as guessed by the last processed frame.
        /// Variable framerate might report different values at different times.
        /// </summary>
        public double CurrentFrameRate { get; private set; }

        /// <summary>
        /// Gets the width of the picture frame.
        /// </summary>
        public int FrameWidth { get; }

        /// <summary>
        /// Gets the height of the picture frame.
        /// </summary>
        public int FrameHeight { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Converts decoded, raw frame data in the frame source into a a usable frame. <br />
        /// The process includes performing picture, samples or text conversions
        /// so that the decoded source frame data is easily usable in multimedia applications
        /// </summary>
        /// <param name="input">The source frame to use as an input.</param>
        /// <param name="output">The target frame that will be updated with the source frame. If null is passed the frame will be instantiated.</param>
        /// <param name="siblings">The siblings to help guess additional frame parameters.</param>
        /// <returns>
        /// Return the updated output frame
        /// </returns>
        /// <exception cref="System.ArgumentNullException">input</exception>
        public override MediaBlock MaterializeFrame(MediaFrame input, ref MediaBlock output, List<MediaBlock> siblings)
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
                Defaults.VideoPixelFormat,
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
            var targetBufferStride = ffmpeg.av_image_get_linesize(
                Defaults.VideoPixelFormat, source.Pointer->width, 0);
            var targetStride = new int[] { targetBufferStride };
            var targetLength = ffmpeg.av_image_get_buffer_size(
                Defaults.VideoPixelFormat, source.Pointer->width, source.Pointer->height, 1);

            // Ensure proper allocation of the buffer
            // If there is a size mismatch between the wanted buffer length and the existing one,
            // then let's reallocate the buffer and set the new size (dispose of the existing one if any)
            if (target.PictureBufferLength != targetLength)
            {
                if (target.PictureBuffer != IntPtr.Zero)
                    Marshal.FreeHGlobal(target.PictureBuffer);

                target.PictureBufferLength = targetLength;
                target.PictureBuffer = Marshal.AllocHGlobal(target.PictureBufferLength);
            }

            var targetScan = default(byte_ptrArray8);
            targetScan[0] = (byte*)target.PictureBuffer;

            // The scaling is done here
            var outputHeight = ffmpeg.sws_scale(Scaler, source.Pointer->data, source.Pointer->linesize, 0, source.Pointer->height, targetScan, targetStride);

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
            }
            else
            {
                // We set the target properties directly from the source
                target.StartTime = source.StartTime;
                target.Duration = source.Duration;
                target.EndTime = source.EndTime;
            }

            target.StreamIndex = input.StreamIndex;
            target.SmtpeTimecode = source.SmtpeTimecode;
            target.DisplayPictureNumber = source.DisplayPictureNumber;
            target.CodedPictureNumber = source.DisplayPictureNumber;
            target.ClosedCaptions = source.ClosedCaptions;
            target.BufferStride = targetStride[0];
            
            target.PixelHeight = source.Pointer->height;
            target.PixelWidth = source.Pointer->width;

            var aspectRatio = source.Pointer->sample_aspect_ratio;
            if (aspectRatio.num == 0 || aspectRatio.den == 0)
            {
                target.AspectWidth = 1;
                target.AspectHeight = 1;
            }
            else
            {
                target.AspectWidth = aspectRatio.num;
                target.AspectHeight = aspectRatio.den;
            }

            return target;
        }

        /// <summary>
        /// Creates a frame source object given the raw FFmpeg frame reference.
        /// </summary>
        /// <param name="frame">The raw FFmpeg frame pointer.</param>
        /// <returns>Create a managed fraome from an unmanaged one.</returns>
        protected override unsafe MediaFrame CreateFrameSource(ref AVFrame* frame)
        {
            if (string.IsNullOrWhiteSpace(FilterString) == false)
                InitializeFilterGraph(frame);

            if (HardwareAccelerator != null)
                frame = HardwareAccelerator.ExchangeFrame(CodecContext, frame, out IsUsingHardwareDecoding);

            AVFrame* outputFrame;

            // TODO: Support real-time changes in Video Filtergraph by checking if MediaOptions.VideoFilterGraph has changed
            // Maybe expose the VideoFilterGraph string as a MediaElement Control Property
            if (FilterGraph != null)
            {
                // Allocate the output frame
                outputFrame = ffmpeg.av_frame_clone(frame);

                var result = ffmpeg.av_buffersrc_add_frame(SourceFilter, outputFrame);
                while (result >= 0)
                    result = ffmpeg.av_buffersink_get_frame_flags(SinkFilter, outputFrame, 0);

                if (outputFrame->width <= 0 || outputFrame->height <= 0)
                {
                    // If we don't have a valid output frame simply release it and 
                    // return the original input frame
                    RC.Current.Remove(outputFrame);
                    ffmpeg.av_frame_free(&outputFrame);
                    outputFrame = frame;
                }
                else
                {
                    // the output frame is the new valid frame (output frame).
                    // threfore, we need to release the original
                    RC.Current.Remove(frame);
                    var framePtr = frame;
                    ffmpeg.av_frame_free(&framePtr);
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
            var frameHolder = new VideoFrame(outputFrame, this);
            CurrentFrameRate = ffmpeg.av_guess_frame_rate(Container.InputContext, Stream, outputFrame).ToDouble();
            return frameHolder;
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="alsoManaged"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected override void Dispose(bool alsoManaged)
        {
            if (Scaler != null)
            {
                RC.Current.Remove(Scaler);
                ffmpeg.sws_freeContext(Scaler);
                Scaler = null;
            }

            DestroyFiltergraph();
            HardwareAccelerator?.DetachDevice(this);
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

                fixed (AVFilterContext** source = &SourceFilter)
                fixed (AVFilterContext** sink = &SinkFilter)
                {
                    result = ffmpeg.avfilter_graph_create_filter(source, ffmpeg.avfilter_get_by_name("buffer"), "video_buffer", CurrentFilterArguments, null, FilterGraph);
                    if (result != 0)
                        throw new MediaContainerException($"{nameof(ffmpeg.avfilter_graph_create_filter)} (buffer) failed. Error {result}: {FFInterop.DecodeMessage(result)}");

                    result = ffmpeg.avfilter_graph_create_filter(sink, ffmpeg.avfilter_get_by_name("buffersink"), "video_buffersink", null, null, FilterGraph);
                    if (result != 0)
                        throw new MediaContainerException($"{nameof(ffmpeg.avfilter_graph_create_filter)} (buffersink) failed. Error {result}: {FFInterop.DecodeMessage(result)}");

                    // TODO: from ffplay, ffmpeg.av_opt_set_int_list(sink, "pix_fmts", (byte*)&f0, 1, ffmpeg.AV_OPT_SEARCH_CHILDREN);
                }

                if (string.IsNullOrWhiteSpace(FilterString))
                {
                    result = ffmpeg.avfilter_link(SourceFilter, 0, SinkFilter, 0);
                    if (result != 0)
                        throw new MediaContainerException($"{nameof(ffmpeg.avfilter_link)} failed. Error {result}: {FFInterop.DecodeMessage(result)}");
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
                fixed (AVFilterGraph** filterGraph = &FilterGraph)
                    ffmpeg.avfilter_graph_free(filterGraph);

                FilterGraph = null;
                SinkInput = null;
                SourceOutput = null;
            }
        }

        #endregion
    }
}
