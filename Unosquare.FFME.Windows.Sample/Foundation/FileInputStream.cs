namespace Unosquare.FFME.Windows.Sample.Foundation
{
    using FFmpeg.AutoGen;
    using Shared;
    using System;
    using System.IO;

    /// <summary>
    /// Provides an example of a very simple custom input stream.
    /// </summary>
    /// <seealso cref="IMediaInputStream" />
    public sealed unsafe class FileInputStream : IMediaInputStream, IDisposable
    {
        private readonly FileStream BackingStream;
        private readonly object ReadLock = new object();
        private readonly byte[] ReadBuffer;

        /// <summary>
        /// Initializes a new instance of the <see cref="FileInputStream"/> class.
        /// </summary>
        /// <param name="path">The path.</param>
        public FileInputStream(string path)
        {
            path = Path.GetFullPath(path);

            BackingStream = File.OpenRead(path);
            var uri = new Uri(path);
            StreamUri = new Uri(uri.ToString().Replace("file://", Scheme));
            CanSeek = true;
            ReadBuffer = new byte[ReadBufferLength];
        }

        /// <summary>
        /// The custom file scheme (URL prefix) including ://
        /// </summary>
        public static string Scheme => "customfile://";

        /// <summary>
        /// Gets the stream URI. This is just a pseudo URI to identify the stream.
        /// </summary>
        public Uri StreamUri { get; }

        /// <summary>
        /// Gets a value indicating whether this stream is seekable.
        /// </summary>
        public bool CanSeek { get; }

        /// <summary>
        /// Gets the length in bytes of the read buffer that will be allocated.
        /// Something like 4096 is recommended
        /// </summary>
        public int ReadBufferLength => 1024 * 16;

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            BackingStream?.Dispose();
        }

        /// <summary>
        /// Reads from the underlying stream and writes up to <paramref name="targetBufferLength" /> bytes
        /// to the <paramref name="targetBuffer" />. Returns the number of bytes that were written.
        /// </summary>
        /// <param name="opaque">The opaque.</param>
        /// <param name="targetBuffer">The target buffer.</param>
        /// <param name="targetBufferLength">Length of the target buffer.</param>
        /// <returns>
        /// The number of bytes that have been read
        /// </returns>
        public unsafe int Read(void* opaque, byte* targetBuffer, int targetBufferLength)
        {
            lock (ReadLock)
            {
                try
                {
                    var readCount = BackingStream.Read(ReadBuffer, 0, ReadBuffer.Length);
                    if (readCount > 0)
                    {
                        fixed (byte* sourceBuffer = &ReadBuffer[0])
                            Buffer.MemoryCopy(sourceBuffer, targetBuffer, targetBufferLength, readCount);
                    }

                    return readCount;
                }
                catch (Exception)
                {
                    return ffmpeg.AVERROR_EOF;
                }
            }
        }

        /// <summary>
        /// Seeks to the specified offset. The offsect can be in byte position or in time units.
        /// This is specified by the whence parameter which is one of the AVSEEK prefixed constants.
        /// </summary>
        /// <param name="opaque">The opaque.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="whence">The whence.</param>
        /// <returns>
        /// The position in bytes or time scale that has been read
        /// </returns>
        public unsafe long Seek(void* opaque, long offset, int whence)
        {
            lock (ReadLock)
            {
                try
                {
                    if (whence == ffmpeg.AVSEEK_SIZE)
                        return BackingStream.Length;

                    return BackingStream.Seek(offset, SeekOrigin.Begin);
                }
                catch
                {
                    return ffmpeg.AVERROR_EOF;
                }
            }
        }
    }
}
