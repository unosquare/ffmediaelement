// ReSharper disable ConvertToAutoPropertyWhenPossible
#pragma warning disable IDE0032 // Use auto property
#pragma warning disable 414 // Field is assigned but its value is never used

namespace Unosquare.FFME.Rendering.Wave
{
    using System;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Represents a Wave file format
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 2)]
    internal class WaveFormat : IEquatable<WaveFormat>
    {
        /// <summary>The format tag -- always 0x0001 PCM</summary>
        private readonly short formatTag = 0x0001;

        /// <summary>number of channels</summary>
        private readonly short channels;

        /// <summary>sample rate</summary>
        private readonly int sampleRate;

        /// <summary>for buffer estimation</summary>
        private readonly int averageBytesPerSecond;

        /// <summary>block size of data</summary>
        private readonly short blockAlign;

        /// <summary>number of bits per sample of mono data</summary>
        private readonly short bitsPerSample;

        /// <summary>number of following bytes</summary>
        private readonly short extraSize;

        /// <summary>
        /// Initializes a new instance of the <see cref="WaveFormat"/> class.
        /// PCM 48Khz stereo 16 bit signed, interleaved, 2-channel format
        /// </summary>
        public WaveFormat()
            : this(44100, 16, 2)
        {
            // placeholder
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WaveFormat"/> class.
        /// </summary>
        /// <param name="sampleRate">Sample Rate</param>
        /// <param name="channels">Number of channels</param>
        public WaveFormat(int sampleRate, int channels)
            : this(sampleRate, 16, channels)
        {
            // placeholder
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WaveFormat"/> class.
        /// </summary>
        /// <param name="rate">The rate.</param>
        /// <param name="bits">The bits.</param>
        /// <param name="channels">The channels.</param>
        /// <exception cref="ArgumentOutOfRangeException">channels - channels</exception>
        public WaveFormat(int rate, int bits, int channels)
        {
            if (channels < 1)
                throw new ArgumentOutOfRangeException(nameof(channels), $"{nameof(channels)} must be greater than or equal to 1");

            // minimum 16 bytes, sometimes 18 for PCM
            this.channels = Convert.ToInt16(channels);
            sampleRate = rate;
            bitsPerSample = Convert.ToInt16(bits);
            extraSize = 0;

            blockAlign = Convert.ToInt16(channels * (bits / 8));
            averageBytesPerSecond = sampleRate * blockAlign;
        }

        #region Properties

        /// <summary>
        /// Returns the number of channels (1=mono,2=stereo etc)
        /// </summary>
        public int Channels => channels;

        /// <summary>
        /// Returns the sample rate (samples per second)
        /// </summary>
        public int SampleRate => sampleRate;

        /// <summary>
        /// Returns the average number of bytes used per second
        /// </summary>
        public int AverageBytesPerSecond => averageBytesPerSecond;

        /// <summary>
        /// Returns the block alignment
        /// </summary>
        public virtual int BlockAlign => blockAlign;

        /// <summary>
        /// Returns the number of bits per sample (usually 16 or 32, sometimes 24 or 8)
        /// Can be 0 for some codecs
        /// </summary>
        public int BitsPerSample => bitsPerSample;

        /// <summary>
        /// Returns the number of extra bytes used by this wave format. Often 0,
        /// except for compressed formats which store extra data after the WAVEFORMATEX header
        /// </summary>
        public int ExtraSize => extraSize;

        #endregion

        /// <summary>
        /// Gets the size of a wave buffer equivalent to the latency in milliseconds.
        /// </summary>
        /// <param name="milliseconds">The milliseconds.</param>
        /// <returns>The size</returns>
        public int ConvertMillisToByteSize(double milliseconds)
        {
            var byteCount = Convert.ToUInt64((AverageBytesPerSecond / 1000.0d) * milliseconds);
            var byteBlockAlign = (ulong)BlockAlign;
            if (byteCount % byteBlockAlign != 0)
            {
                // Return the upper BlockAligned
                byteCount = byteCount + byteBlockAlign - (byteCount % byteBlockAlign);
            }

            return byteCount >= int.MaxValue ? int.MaxValue : Convert.ToInt32(byteCount);
        }

        /// <summary>
        /// Converts from byte size to duration
        /// </summary>
        /// <param name="byteSize">Size of the byte.</param>
        /// <returns>The duration given the byte size</returns>
        public TimeSpan ConvertByteSizeToDuration(int byteSize) =>
            TimeSpan.FromTicks(Convert.ToInt64(TimeSpan.TicksPerSecond * (double)byteSize / averageBytesPerSecond));

        /// <summary>
        /// Reports this WaveFormat as a string
        /// </summary>
        /// <returns>String describing the wave format</returns>
        public override string ToString()
        {
            return $"{bitsPerSample} bit PCM: {sampleRate / 1000}kHz {channels} channels";
        }

        /// <summary>
        /// Compares with another WaveFormat object
        /// </summary>
        /// <param name="obj">Object to compare to</param>
        /// <returns>True if the objects are the same</returns>
        public override bool Equals(object obj)
        {
            if (obj is WaveFormat other)
            {
                return
                    channels == other.channels &&
                    sampleRate == other.sampleRate &&
                    averageBytesPerSecond == other.averageBytesPerSecond &&
                    blockAlign == other.blockAlign &&
                    bitsPerSample == other.bitsPerSample;
            }

            return false;
        }

        /// <summary>
        /// Provides a Hashcode for this WaveFormat
        /// </summary>
        /// <returns>A hashcode</returns>
        public override int GetHashCode()
        {
            return
                channels ^
                sampleRate ^
                averageBytesPerSecond ^
                blockAlign ^
                bitsPerSample;
        }

        /// <inheritdoc />
        public bool Equals(WaveFormat other) => other != null && GetHashCode() == other.GetHashCode();
    }
}
#pragma warning restore 414 // Field is assigned but its value is never used
#pragma warning restore IDE0032 // Use auto property
