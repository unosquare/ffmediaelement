namespace Unosquare.FFME.Windows.Sample.Foundation
{
    using Common;
    using FFmpeg.AutoGen;
    using System;
    using System.IO;
    using System.Runtime.InteropServices;

    /// <inheritdoc />
    /// <summary>
    /// Provides an example of a very simple custom input stream.
    /// </summary>
    /// <seealso cref="IMediaInputStream" />
    public sealed unsafe class FileInputStream : IMediaInputStream
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
            var fullPath = Path.GetFullPath(path);
            BackingStream = File.OpenRead(fullPath);
            var uri = new Uri(fullPath);
            StreamUri = new Uri(uri.ToString().ReplaceOrdinal("file://", Scheme));
            CanSeek = true;
            ReadBuffer = new byte[ReadBufferLength];
        }

        /// <summary>
        /// The custom file scheme (URL prefix) including the :// sequence.
        /// </summary>
        public static string Scheme => "customfile://";

        /// <inheritdoc />
        public Uri StreamUri { get; }

        /// <inheritdoc />
        public bool CanSeek { get; }

        /// <inheritdoc />
        public int ReadBufferLength => 1024 * 16;

        /// <inheritdoc />
        public InputStreamInitializing OnInitializing { get; }

        /// <inheritdoc />
        public InputStreamInitialized OnInitialized { get; }

        /// <inheritdoc />
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
        /// The number of bytes that have been read.
        /// </returns>
        public int Read(void* opaque, byte* targetBuffer, int targetBufferLength)
        {
            lock (ReadLock)
            {
                try
                {
                    var readCount = BackingStream.Read(ReadBuffer, 0, ReadBuffer.Length);
                    if (readCount > 0)
                        Marshal.Copy(ReadBuffer, 0, (IntPtr)targetBuffer, readCount);

                    return readCount;
                }
                catch (Exception)
                {
                    return ffmpeg.AVERROR_EOF;
                }
            }
        }

        /// <inheritdoc />
        public long Seek(void* opaque, long offset, int whence)
        {
            lock (ReadLock)
            {
                try
                {
                    return whence == ffmpeg.AVSEEK_SIZE ?
                        BackingStream.Length : BackingStream.Seek(offset, SeekOrigin.Begin);
                }
                catch
                {
                    return ffmpeg.AVERROR_EOF;
                }
            }
        }
    }
}
