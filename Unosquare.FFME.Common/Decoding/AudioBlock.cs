namespace Unosquare.FFME.Decoding
{
    using Core;
    using System;
    using System.Runtime.InteropServices;

    /// <summary>
    /// A scaled, preallocated audio frame container.
    /// The buffer is in 16-bit signed, interleaved sample data
    /// </summary>
    internal sealed class AudioBlock : MediaBlock
    {
        #region Private Members

        private bool IsDisposed = false; // To detect redundant calls

        #endregion

        #region Constructors and Destructors

        /// <summary>
        /// Finalizes an instance of the <see cref="AudioBlock"/> class.
        /// </summary>
        ~AudioBlock()
        {
            Dispose(false);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets a pointer to the first byte of the data buffer.
        /// The format signed 16-bits per sample, channel interleaved
        /// </summary>
        public IntPtr Buffer
        {
            get { return AudioBuffer; }
        }

        /// <summary>
        /// Gets the length of the buffer in bytes.
        /// </summary>
        public int BufferLength { get; internal set; }

        /// <summary>
        /// Gets the sample rate.
        /// </summary>
        public int SampleRate { get; internal set; }

        /// <summary>
        /// Gets the channel count.
        /// </summary>
        public int ChannelCount { get; internal set; }

        /// <summary>
        /// Gets the available samples per channel.
        /// </summary>
        public int SamplesPerChannel { get; internal set; }

        /// <summary>
        /// Gets the media type of the data
        /// </summary>
        public override MediaType MediaType => MediaType.Audio;

        /// <summary>
        /// The picture buffer length of the last allocated buffer
        /// </summary>
        internal int AudioBufferLength { get; set; }

        /// <summary>
        /// Holds a reference to the last allocated buffer
        /// </summary>
        internal IntPtr AudioBuffer { get; set; }

        #endregion

        #region IDisposable Support

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="alsoManaged"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        private void Dispose(bool alsoManaged)
        {
            if (!IsDisposed)
            {
                if (alsoManaged)
                {
                    // no code for managed dispose
                }

                if (AudioBuffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(AudioBuffer);
                    AudioBuffer = IntPtr.Zero;
                    AudioBufferLength = 0;
                }

                IsDisposed = true;
            }
        }

        #endregion
    }
}
