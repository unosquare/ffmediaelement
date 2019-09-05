namespace Unosquare.FFME.Container
{
    using Common;
    using FFmpeg.AutoGen;
    using System;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Represents a wrapper from an unmanaged FFmpeg data frame.
    /// </summary>
    /// <seealso cref="MediaFrame" />
    /// <seealso cref="IDisposable" />
    internal sealed unsafe class DataFrame : MediaFrame
    {
        #region Private Members

        private readonly object DisposeLock = new object();
        private bool IsDisposed = false;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="DataFrame" /> class.
        /// </summary>
        /// <param name="packet">The frame.</param>
        /// <param name="component">The component.</param>
        internal DataFrame(AVPacket* packet, DataComponent component)
            : base(packet, component, MediaType.Data)
        {
            // Compute the timespans
            HasValidStartTime = packet->pts != ffmpeg.AV_NOPTS_VALUE;
            StartTime = packet->pts == ffmpeg.AV_NOPTS_VALUE ?
                TimeSpan.FromTicks(0) :
                TimeSpan.FromTicks(packet->pts.ToTimeSpan(StreamTimeBase).Ticks);

            // this will be equal to 0 very often (data packets do not include duration)
            Duration = TimeSpan.FromTicks(packet->duration.ToTimeSpan(StreamTimeBase).Ticks);

            EndTime = TimeSpan.FromTicks(StartTime.Ticks + Duration.Ticks);

            // Store datas
            Bytes = new byte[packet->size];
            if (packet->size > 0)
            {
                Marshal.Copy((IntPtr)packet->data, Bytes, 0, packet->size);
            }
        }

        #endregion

        #region Properties

        public byte[] Bytes { get; }

        internal AVFrame* Pointer => (AVFrame*)InternalPointer;

        #endregion

        #region IDisposable Support

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        public override void Dispose()
        {
            lock (DisposeLock)
            {
                if (IsDisposed) return;

                InternalPointer = IntPtr.Zero;
                IsDisposed = true;
            }

            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
