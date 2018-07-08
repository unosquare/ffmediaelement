namespace Unosquare.FFME.Decoding
{
    using Core;
    using FFmpeg.AutoGen;
    using Shared;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Provides audio sample extraction, decoding and scaling functionality.
    /// </summary>
    /// <seealso cref="MediaComponent" />
    internal sealed unsafe class AudioComponent : MediaComponent
    {
        #region Private Declarations

        /// <summary>
        /// Holds a reference to the audio resampler
        /// This resampler gets disposed upon disposal of this object.
        /// </summary>
        private SwrContext* Scaler = null;

        /// <summary>
        /// Used to determine if we have to reset the scaler parameters
        /// </summary>
        private FFAudioParams LastSourceSpec = null;

        private AVFilterGraph* FilterGraph = null;
        private AVFilterContext* SourceFilter = null;
        private AVFilterContext* SinkFilter = null;
        private AVFilterInOut* SinkInput = null;
        private AVFilterInOut* SourceOutput = null;

        private string CurrentFilterArguments = null;
        private string FilterString = null;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioComponent"/> class.
        /// </summary>
        /// <param name="container">The container.</param>
        /// <param name="streamIndex">Index of the stream.</param>
        internal AudioComponent(MediaContainer container, int streamIndex)
            : base(container, streamIndex)
        {
            Channels = Stream->codec->channels;
            SampleRate = Stream->codec->sample_rate;
            BitsPerSample = ffmpeg.av_samples_get_buffer_size(null, 1, 1, Stream->codec->sample_fmt, 1) * 8;
            FilterString = container.MediaOptions.AudioFilter;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the number of audio channels.
        /// </summary>
        public int Channels { get; }

        /// <summary>
        /// Gets the audio sample rate.
        /// </summary>
        public int SampleRate { get; }

        /// <summary>
        /// Gets the bits per sample.
        /// </summary>
        public int BitsPerSample { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Converts decoded, raw frame data in the frame source into a a usable frame. <br />
        /// The process includes performing picture, samples or text conversions
        /// so that the decoded source frame data is easily usable in multimedia applications
        /// </summary>
        /// <param name="input">The source frame to use as an input.</param>
        /// <param name="output">The target frame that will be updated with the source frame. If null is passed the frame will be instantiated.</param>
        /// <param name="siblings">The sibling blocks that may help guess some additional parameters for the input frame.</param>
        /// <returns>
        /// Return the updated output frame
        /// </returns>
        /// <exception cref="ArgumentNullException">input</exception>
        public override bool MaterializeFrame(MediaFrame input, ref MediaBlock output, List<MediaBlock> siblings)
        {
            if (output == null) output = new AudioBlock();
            var source = input as AudioFrame;
            var target = output as AudioBlock;

            if (source == null || target == null)
                throw new ArgumentNullException($"{nameof(input)} and {nameof(output)} are either null or not of a compatible media type '{MediaType}'");

            // Create the source and target ausio specs. We might need to scale from
            // the source to the target
            var sourceSpec = FFAudioParams.CreateSource(source.Pointer);
            var targetSpec = FFAudioParams.CreateTarget(source.Pointer);

            // Initialize or update the audio scaler if required
            if (Scaler == null || LastSourceSpec == null || FFAudioParams.AreCompatible(LastSourceSpec, sourceSpec) == false)
            {
                Scaler = ffmpeg.swr_alloc_set_opts(
                    Scaler,
                    targetSpec.ChannelLayout,
                    targetSpec.Format,
                    targetSpec.SampleRate,
                    sourceSpec.ChannelLayout,
                    sourceSpec.Format,
                    sourceSpec.SampleRate,
                    0,
                    null);

                RC.Current.Add(Scaler, $"109: {nameof(AudioComponent)}.{nameof(MaterializeFrame)}()");
                ffmpeg.swr_init(Scaler);
                LastSourceSpec = sourceSpec;
            }

            // Allocate the unmanaged output buffer and convert to stereo.
            var outputSamplesPerChannel = 0;
            if (target.Allocate(targetSpec.BufferLength) &&
                target.TryAcquireWriterLock(out var writeLock))
            {
                using (writeLock)
                {
                    var outputBufferPtr = (byte*)target.Buffer.ToPointer();

                    // Execute the conversion (audio scaling). It will return the number of samples that were output
                    outputSamplesPerChannel = ffmpeg.swr_convert(
                        Scaler,
                        &outputBufferPtr,
                        targetSpec.SamplesPerChannel,
                        source.Pointer->extended_data,
                        source.Pointer->nb_samples);
                }
            }
            else
            {
                return false;
            }

            // Compute the buffer length
            var outputBufferLength =
                ffmpeg.av_samples_get_buffer_size(null, targetSpec.ChannelCount, outputSamplesPerChannel, targetSpec.Format, 1);

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

            target.SamplesBufferLength = outputBufferLength;
            target.ChannelCount = targetSpec.ChannelCount;

            target.SampleRate = targetSpec.SampleRate;
            target.SamplesPerChannel = outputSamplesPerChannel;
            target.StreamIndex = input.StreamIndex;

            return true;
        }

        /// <summary>
        /// Creates a frame source object given the raw FFmpeg frame reference.
        /// </summary>
        /// <param name="framePointer">The raw FFmpeg frame pointer.</param>
        /// <returns>The media frame</returns>
        protected override unsafe MediaFrame CreateFrameSource(IntPtr framePointer)
        {
            // Validate the audio frame
            var frame = (AVFrame*)framePointer.ToPointer();
            if (frame == null || (*frame).extended_data == null || frame->channels <= 0 ||
                frame->nb_samples <= 0 || frame->sample_rate <= 0)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(FilterString) == false)
                InitializeFilterGraph(frame);

            AVFrame* outputFrame = null;

            // Filtergraph can be changed by issuing a ChangeMedia command
            if (FilterGraph != null)
            {
                // Allocate the output frame
                outputFrame = ffmpeg.av_frame_clone(frame);

                var result = ffmpeg.av_buffersrc_add_frame(SourceFilter, outputFrame);
                while (result >= 0)
                    result = ffmpeg.av_buffersink_get_frame_flags(SinkFilter, outputFrame, 0);

                if (outputFrame->nb_samples <= 0)
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
            if (outputFrame->nb_samples <= 0)
                return null;

            var frameHolder = new AudioFrame(outputFrame, this);
            return frameHolder;
        }

        #endregion

        #region IDisposable Support

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="alsoManaged"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected override void Dispose(bool alsoManaged)
        {
            RC.Current.Remove(Scaler);
            if (Scaler != null)
            {
                fixed (SwrContext** scaler = &Scaler)
                    ffmpeg.swr_free(scaler);
            }

            DestroyFiltergraph();
            base.Dispose(alsoManaged);
        }

        #endregion

        #region Filtering Methods

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

        /// <summary>
        /// Computes the frame filter arguments that are appropriate for the audio filtering chain.
        /// </summary>
        /// <param name="frame">The frame.</param>
        /// <returns>The base filter arguments</returns>
        private string ComputeFilterArguments(AVFrame* frame)
        {
            var hexChannelLayout = BitConverter.ToString(
                BitConverter.GetBytes(frame->channel_layout).Reverse().ToArray()).Replace("-", string.Empty);

            var channelLayout = $"0x{hexChannelLayout.ToLowerInvariant()}";

            var arguments =
                 $"time_base={Stream->time_base.num}/{Stream->time_base.den}:" +
                 $"sample_rate={frame->sample_rate:0}:" +
                 $"sample_fmt={ffmpeg.av_get_sample_fmt_name((AVSampleFormat)frame->format)}:" +
                 $"channel_layout={channelLayout}";

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
             * https://www.ffmpeg.org/doxygen/2.0/doc_2examples_2filtering_audio_8c-example.html
             */

            var frameArguments = ComputeFilterArguments(frame);
            if (string.IsNullOrWhiteSpace(CurrentFilterArguments) || frameArguments.Equals(CurrentFilterArguments) == false)
                DestroyFiltergraph();
            else
                return;

            FilterGraph = ffmpeg.avfilter_graph_alloc();
            RC.Current.Add(FilterGraph, $"264: {nameof(AudioComponent)}.{nameof(InitializeFilterGraph)}()");
            CurrentFilterArguments = frameArguments;

            try
            {
                var result = 0;

                fixed (AVFilterContext** source = &SourceFilter)
                fixed (AVFilterContext** sink = &SinkFilter)
                {
                    result = ffmpeg.avfilter_graph_create_filter(source, ffmpeg.avfilter_get_by_name("abuffer"), "audio_buffer", CurrentFilterArguments, null, FilterGraph);
                    if (result != 0)
                    {
                        throw new MediaContainerException(
                            $"{nameof(ffmpeg.avfilter_graph_create_filter)} (audio_buffer) failed. Error {result}: {FFInterop.DecodeMessage(result)}");
                    }

                    result = ffmpeg.avfilter_graph_create_filter(sink, ffmpeg.avfilter_get_by_name("abuffersink"), "audio_buffersink", null, null, FilterGraph);
                    if (result != 0)
                    {
                        throw new MediaContainerException(
                            $"{nameof(ffmpeg.avfilter_graph_create_filter)} (audio_buffersink) failed. Error {result}: {FFInterop.DecodeMessage(result)}");
                    }
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
                Container.Parent?.Log(MediaLogMessageType.Error, $"Audio filter graph could not be built: {FilterString}.\r\n{ex.Message}");
                DestroyFiltergraph();
            }
        }

        #endregion
    }
}
