namespace Unosquare.FFME.Events
{
    using Shared;
    using System;

    /// <summary>
    /// Provides the audio samples rendering payload as event arguments.
    /// </summary>
    /// <seealso cref="EventArgs" />
    public sealed class RenderingAudioEventArgs : RenderingEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RenderingAudioEventArgs" /> class.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="length">The length.</param>
        /// <param name="engineState">The engine.</param>
        /// <param name="stream">The stream.</param>
        /// <param name="startTime">The start time.</param>
        /// <param name="duration">The duration.</param>
        /// <param name="clock">The clock.</param>
        internal RenderingAudioEventArgs(
            IntPtr buffer, int length, MediaEngineState engineState, StreamInfo stream, TimeSpan startTime, TimeSpan duration, TimeSpan clock)
            : base(engineState, stream, startTime, duration, clock)
        {
            Buffer = buffer;
            BufferLength = length;
            SampleRate = Constants.Audio.SampleRate;
            ChannelCount = Constants.Audio.ChannelCount;
            BitsPerSample = Constants.Audio.BitsPerSample;
        }

        /// <summary>
        /// Gets a pointer to the samples buffer.
        /// Samples are provided in PCM 16-bit signed, interleaved stereo.
        /// </summary>
        public IntPtr Buffer { get; }

        /// <summary>
        /// Gets the length in bytes of the samples buffer.
        /// </summary>
        public int BufferLength { get; }

        /// <summary>
        /// Gets the number of samples in 1 second.
        /// </summary>
        public int SampleRate { get; }

        /// <summary>
        /// Gets the number of channels.
        /// </summary>
        public int ChannelCount { get; }

        /// <summary>
        /// Gets the number of bits per sample.
        /// </summary>
        public int BitsPerSample { get; }

        /// <summary>
        /// Gets the number of samples in the buffer for all channels.
        /// </summary>
        public int Samples => BufferLength / (BitsPerSample / 8);

        /// <summary>
        /// Gets the number of samples in the buffer per channel.
        /// </summary>
        public int SamplesPerChannel => Samples / ChannelCount;
    }
}
