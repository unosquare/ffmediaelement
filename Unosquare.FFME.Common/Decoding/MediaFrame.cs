namespace Unosquare.FFME.Decoding
{
    using Core;
    using FFmpeg.AutoGen;
    using Shared;
    using System;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Represents a wrapper for an unmanaged frame.
    /// Derived classes implement the specifics of each media type.
    /// </summary>
    /// <seealso cref="IDisposable" />
    internal abstract unsafe class MediaFrame : IDisposable, IComparable<MediaFrame>
    {
        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaFrame" /> class.
        /// </summary>
        /// <param name="pointer">The pointer.</param>
        /// <param name="component">The component.</param>
        /// <param name="mediaType">Type of the media.</param>
        internal MediaFrame(AVFrame* pointer, MediaComponent component, MediaType mediaType)
            : this((void*)pointer, component, mediaType)
        {
            var packetSize = pointer->pkt_size;
            CompressedSize = packetSize > 0 ? packetSize : 0;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaFrame"/> class.
        /// </summary>
        /// <param name="pointer">The pointer.</param>
        /// <param name="component">The component.</param>
        internal MediaFrame(AVSubtitle* pointer, MediaComponent component)
            : this((void*)pointer, component, MediaType.Subtitle)
        {
            // TODO: Compressed size is simply an estimate
            CompressedSize = (int)pointer->num_rects * 256;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaFrame" /> class.
        /// </summary>
        /// <param name="pointer">The pointer.</param>
        /// <param name="component">The component.</param>
        /// <param name="mediaType">Type of the media.</param>
        private MediaFrame(void* pointer, MediaComponent component, MediaType mediaType)
        {
            InternalPointer = new IntPtr(pointer);
            StreamTimeBase = component.Stream->time_base;
            StreamIndex = component.StreamIndex;
            MediaType = mediaType;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the type of the media.
        /// </summary>
        /// <value>
        /// The type of the media.
        /// </value>
        public MediaType MediaType { get; }

        /// <summary>
        /// Gets the size of the compressed packets that created this frame.
        /// </summary>
        public int CompressedSize { get; }

        /// <summary>
        /// Gets the start time of the frame.
        /// </summary>
        public TimeSpan StartTime { get; protected set; }

        /// <summary>
        /// Gets the end time of the frame
        /// </summary>
        public TimeSpan EndTime { get; protected set; }

        /// <summary>
        /// Gets the index of the stream from which this frame was decoded.
        /// </summary>
        public int StreamIndex { get; protected set; }

        /// <summary>
        /// Gets the amount of time this data has to be presented
        /// </summary>
        public TimeSpan Duration { get; protected set; }

        /// <summary>
        /// Gets or sets a value indicating whether this frame obtained its start time
        /// form a valid frame pts value
        /// </summary>
        public bool HasValidStartTime { get; protected set; } = true;

        /// <summary>
        /// When the unmanaged frame is released (freed from unmanaged memory)
        /// this property will return true.
        /// </summary>
        public bool IsStale => InternalPointer == IntPtr.Zero;

        /// <summary>
        /// Gets the time base of the stream that generated this frame.
        /// </summary>
        internal AVRational StreamTimeBase { get; }

        /// <summary>
        /// Gets or sets the internal pointer.
        /// </summary>
        protected IntPtr InternalPointer { get; set; }

        #endregion

        #region Methods

        /// <summary>
        /// Compares the current instance with another object of the same type and returns an integer that indicates whether the current instance precedes, follows, or occurs in the same position in the sort order as the other object.
        /// </summary>
        /// <param name="other">An object to compare with this instance.</param>
        /// <returns>
        /// A value that indicates the relative order of the objects being compared. The return value has these meanings: Value Meaning Less than zero This instance precedes <paramref name="other" /> in the sort order.  Zero This instance occurs in the same position in the sort order as <paramref name="other" />. Greater than zero This instance follows <paramref name="other" /> in the sort order.
        /// </returns>
        public int CompareTo(MediaFrame other)
        {
            return StartTime.CompareTo(other.StartTime);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public abstract void Dispose();

        /// <summary>
        /// Creates a frame used for Audio or Video
        /// </summary>
        /// <returns>The frame allocated in unmanaged memory</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static AVFrame* CreateAVFrame()
        {
            var frame = ffmpeg.av_frame_alloc();
            RC.Current.Add(frame, $"119: {nameof(MediaFrame)}.{nameof(CreateAVFrame)}()");
            return frame;
        }

        /// <summary>
        /// Releases a previously allocated frame used for Audio or Video
        /// </summary>
        /// <param name="frame">The frame.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void ReleaseAVFrame(AVFrame* frame)
        {
            RC.Current.Remove(frame);
            ffmpeg.av_frame_free(&frame);
        }

        /// <summary>
        /// Creates a deep copy of the specified source
        /// </summary>
        /// <param name="source">The source frame.</param>
        /// <returns>The cloned frame</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static AVFrame* CloneAVFrame(AVFrame* source)
        {
            var frame = ffmpeg.av_frame_clone(source);
            RC.Current.Add(frame, $"119: {nameof(MediaFrame)}.{nameof(CloneAVFrame)}()");
            return frame;
        }

        /// <summary>
        /// Allocates an AVSubtitle struct in unmanaged memory,
        /// </summary>
        /// <returns>The subtitle struct pointer</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static AVSubtitle* CreateAVSubtitle()
        {
            return (AVSubtitle*)ffmpeg.av_malloc((ulong)Marshal.SizeOf(typeof(AVSubtitle)));
        }

        /// <summary>
        /// Deallocates the subtitle struct used to create in managed memory.
        /// </summary>
        /// <param name="frame">The frame.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void ReleaseAVSubtitle(AVSubtitle* frame)
        {
            if (frame == null) return;
            ffmpeg.avsubtitle_free(frame);
            ffmpeg.av_free(frame);
        }

        #endregion
    }
}
