namespace Unosquare.FFmpegMediaElement
{
    using FFmpeg.AutoGen;
    using System;
    using System.Collections.Concurrent;
    using System.Threading;

    /// <summary>
    /// Enumerates the different media frame types
    /// </summary>
    internal enum MediaFrameType
    {
        Unknown,
        Audio,
        Video
    }

    /// <summary>
    /// Represents a video or audio frame.
    /// </summary>
    internal sealed unsafe class FFmpegMediaFrame : IDisposable
    {
        #region Custom Frames Garbage Collector

        private static readonly Thread GarbageFramesCollectorThread;
        private static readonly ConcurrentQueue<FFmpegMediaFrame> GarbageFramesQueue = new ConcurrentQueue<FFmpegMediaFrame>();

        static FFmpegMediaFrame()
        {
            GarbageFramesCollectorThread = new System.Threading.Thread(() =>
            {

                var lastCollectionDate = DateTime.UtcNow;
                while (true)
                {
                    var msSinceLastRelease = DateTime.UtcNow.Subtract(lastCollectionDate).TotalMilliseconds;
                    var frameCount = GarbageFramesQueue.Count;
                    var needsForcedRelease = frameCount >= Constants.FrameCollectorForcedReleaseFrameCount;

                    if (frameCount == 0 || (needsForcedRelease == false && msSinceLastRelease < Constants.FrameCollectorReleaseInterval))
                    {
                        System.Threading.Thread.Sleep(Constants.FrameCollectorSleepTime);
                        continue;
                    }

                    FFmpegMediaFrame garbageFrame = null;
                    var releasedCount = 0;
                    while (GarbageFramesQueue.TryDequeue(out garbageFrame))
                    {
                        if (garbageFrame == null)
                            continue;

                        garbageFrame.InternalRelease();
                        releasedCount++;
                    }

                    lastCollectionDate = DateTime.UtcNow;
                }

            }) { IsBackground = true, Priority = ThreadPriority.Lowest };

            GarbageFramesCollectorThread.Start();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the flags.
        /// </summary>
        public FFmpegMediaFrameFlags Flags { get; internal set; }

        /// <summary>
        /// For video frames, gets or sets the type of the picture.
        /// </summary>
        public FFmpegPictureType PictureType { get; internal set; }

        /// <summary>
        /// Gets the frame's best effort PTS in seconds
        /// </summary>
        public decimal StartTime { get; internal set; }

        /// <summary>
        /// For video frames, gets the pointer to the decoded picture buffer
        /// </summary>
        public IntPtr PictureBufferPtr { get; internal set; }

        /// <summary>
        /// For video frames, gets the length of the decoded picture buffer
        /// </summary>
        public uint PictureBufferLength { get; internal set; }

        /// <summary>
        /// For video frames, gets the coded picture number.
        /// </summary>
        public int CodedPictureNumber { get; internal set; }

        /// <summary>
        /// Gets the duration in seconds of the frame.
        /// </summary>
        public decimal Duration { get; internal set; }

        /// <summary>
        /// Gets the frame's best effort PTS
        /// </summary>
        public long Timestamp { get; internal set; }

        /// <summary>
        /// Gets the type of frame
        /// </summary>
        public MediaFrameType Type { get; internal set; }

        /// <summary>
        /// Gets the stream index this frame belongs to
        /// </summary>
        public int StreamIndex { get; internal set; }

        /// <summary>
        /// For audio frames, holds the bytes array with the decoded waveform
        /// </summary>
        public byte[] AudioBuffer { get; internal set; }

        /// <summary>
        /// For video frames, A pointer to the decodes picture
        /// </summary>
        internal AVPicture* Picture = null;

        /// <summary>
        /// For video frames, A Pointer to the decoded picture buffer
        /// </summary>
        internal sbyte* PictureBuffer = null;

        #endregion

        /// <summary>
        /// Synchronized lock for frame disposal
        /// </summary>
        private readonly object ReleaseLock = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="FFmpegMediaFrame"/> class.
        /// All properties need to be set immediately after instantiation
        /// </summary>
        public FFmpegMediaFrame()
        {
            // placeholder
        }

        #region Release MEthods

        public void EnqueueRelease()
        {
            GarbageFramesQueue.Enqueue(this);
        }

        public void InternalRelease()
        {
            lock (ReleaseLock)
            {
                if (Picture != null)
                    ffmpeg.av_free(Picture);

                if (PictureBuffer != null)
                    ffmpeg.av_free(PictureBuffer);

                Picture = null;
                PictureBuffer = null;
                PictureBufferPtr = IntPtr.Zero;
                PictureBufferLength = 0;
            }
        }

        #endregion

        #region IDisposable Implementation

        ~FFmpegMediaFrame()
        {
            this.Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool alsoManaged)
        {
            if (alsoManaged)
            {
                // free managed resources
                if (this.AudioBuffer != null)
                    this.AudioBuffer = null;
            }

            // free native resources
            this.InternalRelease();
        }

        #endregion

    }

}
