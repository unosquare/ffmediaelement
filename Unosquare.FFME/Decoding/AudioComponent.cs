namespace Unosquare.FFME.Decoding
{
    using Core;
    using FFmpeg.AutoGen;
    using System;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Provides audio sample extraction, decoding and scaling functionality.
    /// </summary>
    /// <seealso cref="Unosquare.FFME.MediaComponent" />
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
        private AudioParams LastSourceSpec = null;

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
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the number of audio channels.
        /// </summary>
        public int Channels { get; private set; }

        /// <summary>
        /// Gets the audio sample rate.
        /// </summary>
        public int SampleRate { get; private set; }

        /// <summary>
        /// Gets the bits per sample.
        /// </summary>
        public int BitsPerSample { get; private set; }

        #endregion

        #region Methods

        /// <summary>
        /// Creates a frame source object given the raw FFmpeg frame reference.
        /// </summary>
        /// <param name="frame">The raw FFmpeg frame pointer.</param>
        /// <returns></returns>
        protected override unsafe MediaFrame CreateFrameSource(AVFrame* frame)
        {
            var frameHolder = new AudioFrame(frame, this);
            return frameHolder;
        }

        /// <summary>
        /// Converts decoded, raw frame data in the frame source into a a usable frame. <br />
        /// The process includes performing picture, samples or text conversions
        /// so that the decoded source frame data is easily usable in multimedia applications
        /// </summary>
        /// <param name="input">The source frame to use as an input.</param>
        /// <param name="output">The target frame that will be updated with the source frame. If null is passed the frame will be instantiated.</param>
        /// <returns>
        /// Return the updated output frame
        /// </returns>
        /// <exception cref="System.ArgumentNullException">input</exception>
        internal override MediaBlock MaterializeFrame(MediaFrame input, ref MediaBlock output)
        {
            if (output == null) output = new AudioBlock();
            var source = input as AudioFrame;
            var target = output as AudioBlock;

            if (source == null || target == null)
                throw new ArgumentNullException($"{nameof(input)} and {nameof(output)} are either null or not of a compatible media type '{MediaType}'");

            // Create the source and target ausio specs. We might need to scale from
            // the source to the target
            var sourceSpec = AudioParams.CreateSource(source.Pointer);
            var targetSpec = AudioParams.CreateTarget(source.Pointer);

            // Initialize or update the audio scaler if required
            if (Scaler == null || LastSourceSpec == null || AudioParams.AreCompatible(LastSourceSpec, sourceSpec) == false)
            {
                Scaler = ffmpeg.swr_alloc_set_opts(Scaler, targetSpec.ChannelLayout, targetSpec.Format, targetSpec.SampleRate,
                    sourceSpec.ChannelLayout, sourceSpec.Format, sourceSpec.SampleRate, 0, null);

                ffmpeg.swr_init(Scaler);
                LastSourceSpec = sourceSpec;
            }

            // Allocate the unmanaged output buffer
            if (target.AudioBufferLength != targetSpec.BufferLength)
            {
                if (target.AudioBuffer != IntPtr.Zero)
                    Marshal.FreeHGlobal(target.AudioBuffer);

                target.AudioBufferLength = targetSpec.BufferLength;
                target.AudioBuffer = Marshal.AllocHGlobal(targetSpec.BufferLength);
            }

            var outputBufferPtr = (byte*)target.AudioBuffer;

            // Execute the conversion (audio scaling). It will return the number of samples that were output
            var outputSamplesPerChannel =
                ffmpeg.swr_convert(Scaler, &outputBufferPtr, targetSpec.SamplesPerChannel,
                    source.Pointer->extended_data, source.Pointer->nb_samples);

            // Compute the buffer length
            var outputBufferLength =
                ffmpeg.av_samples_get_buffer_size(null, targetSpec.ChannelCount, outputSamplesPerChannel, targetSpec.Format, 1);

            // set the target properties
            target.StartTime = source.StartTime;
            target.EndTime = source.EndTime;
            target.BufferLength = outputBufferLength;
            target.ChannelCount = targetSpec.ChannelCount;
            target.Duration = source.Duration;
            target.SampleRate = targetSpec.SampleRate;
            target.SamplesPerChannel = outputSamplesPerChannel;

            return target;

        }

        #endregion

        #region IDisposable Support

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="alsoManaged"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected override void Dispose(bool alsoManaged)
        {
            base.Dispose(alsoManaged);

            if (Scaler != null)
                fixed (SwrContext** scaler = &Scaler)
                    ffmpeg.swr_free(scaler);
        }

        #endregion

    }
}
