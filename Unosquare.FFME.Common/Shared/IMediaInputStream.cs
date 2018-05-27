namespace Unosquare.FFME.Shared
{
    using System;

    /// <summary>
    /// Defines the properties and methods necessary for implementing a
    /// custom media input stream.
    /// </summary>
    public unsafe interface IMediaInputStream : IDisposable
    {
        /// <summary>
        /// Gets the stream URI. This is just a pseudo URI to identify the stream.
        /// </summary>
        Uri StreamUri { get; }

        /// <summary>
        /// Gets a value indicating whether this stream is seekable.
        /// </summary>
        bool CanSeek { get; }

        /// <summary>
        /// Gets the length in bytes of the read buffer that will be allocated.
        /// Something like 4096 is recommended
        /// </summary>
        int ReadBufferLength { get; }

        /// <summary>
        /// Reads from the underlying stream and writes up to <paramref name="targetBufferLength"/> bytes
        /// to the <paramref name="targetBuffer"/>. Returns the number of bytes that were written.
        /// </summary>
        /// <param name="opaque">The opaque.</param>
        /// <param name="targetBuffer">The target buffer.</param>
        /// <param name="targetBufferLength">Length of the target buffer.</param>
        /// <returns>The number of bytes that have been read</returns>
        int Read(void* opaque, byte* targetBuffer, int targetBufferLength);

        /// <summary>
        /// Seeks to the specified offset. The offsect can be in byte position or in time units.
        /// This is specified by the whence parameter which is one of the AVSEEK prefixed constants.
        /// </summary>
        /// <param name="opaque">The opaque.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="whence">The whence.</param>
        /// <returns>The position in bytes or time scale that has been read</returns>
        long Seek(void* opaque, long offset, int whence);
    }
}
