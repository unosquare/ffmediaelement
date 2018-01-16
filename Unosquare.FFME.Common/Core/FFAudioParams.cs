namespace Unosquare.FFME.Core
{
    using FFmpeg.AutoGen;
    using Shared;
    using System;

    /// <summary>
    /// Contains audio format properties essential
    /// to audio processing and resampling in FFmpeg libraries
    /// </summary>
    internal sealed unsafe class FFAudioParams
    {
        #region Constant Definitions

        /// <summary>
        /// The standard output audio spec
        /// </summary>
        public static readonly FFAudioParams Output;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes static members of the <see cref="FFAudioParams"/> class.
        /// </summary>
        static FFAudioParams()
        {
            Output = new FFAudioParams()
            {
                ChannelCount = Constants.Audio.ChannelCount,
                SampleRate = Constants.Audio.SampleRate,
                Format = Constants.Audio.SampleFormat
            };

            Output.ChannelLayout = ffmpeg.av_get_default_channel_layout(Output.ChannelCount);
            Output.SamplesPerChannel = Output.SampleRate;
            Output.BufferLength = ffmpeg.av_samples_get_buffer_size(
                null, Output.ChannelCount, Output.SamplesPerChannel + Constants.Audio.BufferPadding, Output.Format, 1);
        }

        /// <summary>
        /// Prevents a default instance of the <see cref="FFAudioParams"/> class from being created.
        /// </summary>
        private FFAudioParams()
        {
            // placeholder
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FFAudioParams"/> class.
        /// </summary>
        /// <param name="frame">The frame.</param>
        private FFAudioParams(AVFrame* frame)
        {
            ChannelCount = ffmpeg.av_frame_get_channels(frame);
            ChannelLayout = ffmpeg.av_frame_get_channel_layout(frame);
            Format = (AVSampleFormat)frame->format;
            SamplesPerChannel = frame->nb_samples;
            BufferLength = ffmpeg.av_samples_get_buffer_size(null, ChannelCount, SamplesPerChannel, Format, 1);
            SampleRate = frame->sample_rate;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the channel count.
        /// </summary>
        public int ChannelCount { get; private set; }

        /// <summary>
        /// Gets the channel layout.
        /// </summary>
        public long ChannelLayout { get; private set; }

        /// <summary>
        /// Gets the samples per channel.
        /// </summary>
        public int SamplesPerChannel { get; private set; }

        /// <summary>
        /// Gets the audio sampling rate.
        /// </summary>
        public int SampleRate { get; private set; }

        /// <summary>
        /// Gets the sample format.
        /// </summary>
        public AVSampleFormat Format { get; private set; }

        /// <summary>
        /// Gets the length of the buffer required to store
        /// the samples in the current format.
        /// </summary>
        public int BufferLength { get; private set; }

        #endregion

        #region Methods

        /// <summary>
        /// Creates a source audio spec based on the info in the given audio frame
        /// </summary>
        /// <param name="frame">The frame.</param>
        /// <returns>The audio parameters</returns>
        internal static FFAudioParams CreateSource(AVFrame* frame)
        {
            var spec = new FFAudioParams(frame);
            if (spec.ChannelLayout == 0)
                spec.ChannelLayout = ffmpeg.av_get_default_channel_layout(spec.ChannelCount);

            return spec;
        }

        /// <summary>
        /// Creates a target audio spec using the sample quantities provided
        /// by the given source audio frame
        /// </summary>
        /// <param name="frame">The frame.</param>
        /// <returns>The audio parameters</returns>
        internal static FFAudioParams CreateTarget(AVFrame* frame)
        {
            var spec = new FFAudioParams
            {
                ChannelCount = Output.ChannelCount,
                Format = Output.Format,
                SampleRate = Output.SampleRate,
                ChannelLayout = Output.ChannelLayout
            };

            // The target transform is just a ratio of the source frame's sample. This is how many samples we desire
            spec.SamplesPerChannel = (int)Math.Round((double)frame->nb_samples * spec.SampleRate / frame->sample_rate, 0);
            spec.BufferLength = ffmpeg.av_samples_get_buffer_size(
                null, spec.ChannelCount, spec.SamplesPerChannel + Constants.Audio.BufferPadding, spec.Format, 1);
            return spec;
        }

        /// <summary>
        /// Determines if the audio specs are compatible between them.
        /// They must share format, channel count, layout and sample rate
        /// </summary>
        /// <param name="a">a.</param>
        /// <param name="b">The b.</param>
        /// <returns>True if the params are compatible, flase otherwise.</returns>
        internal static bool AreCompatible(FFAudioParams a, FFAudioParams b)
        {
            if (a.Format != b.Format) return false;
            if (a.ChannelCount != b.ChannelCount) return false;
            if (a.ChannelLayout != b.ChannelLayout) return false;
            if (a.SampleRate != b.SampleRate) return false;

            return true;
        }

        #endregion

    }
}
