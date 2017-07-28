namespace Unosquare.FFME.Decoding
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Represents a Queue of alread-decoded media frames.
    /// </summary>
    internal sealed class MediaFrameQueue
    {
        #region Private Declarations

        private bool IsDisposed = false; // To detect redundant calls
        private readonly List<MediaFrame> Frames = new List<MediaFrame>();
        private readonly object SyncRoot = new object();

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the <see cref="MediaFrame"/> at the specified index.
        /// </summary>
        /// <value>
        /// The <see cref="MediaFrame"/>.
        /// </value>
        /// <param name="index">The index.</param>
        /// <returns></returns>
        internal MediaFrame this[int index]
        {
            get
            {
                lock (SyncRoot)
                    return Frames[index];
            }
            private set
            {
                lock (SyncRoot)
                    Frames[index] = value;
            }
        }

        /// <summary>
        /// Gets the frame count.
        /// </summary>
        public int Count
        {
            get { lock (SyncRoot) return Frames.Count; }

        }

        /// <summary>
        /// Gets the total duration of all the frames contained in this queue.
        /// </summary>
        public TimeSpan Duration { get { lock (SyncRoot) return TimeSpan.FromTicks(EndTime.Ticks - StartTime.Ticks); } }

        /// <summary>
        /// Gets the minimum start time of the frames contained in this queue.
        /// </summary>
        public TimeSpan StartTime { get { lock (SyncRoot) return Frames.Count == 0 ? TimeSpan.Zero : Frames.Min(f => f.StartTime); } }

        /// <summary>
        /// Gets the maximum end time of the frames contained in this queue.
        /// </summary>
        public TimeSpan EndTime { get { lock (SyncRoot) return Frames.Count == 0 ? TimeSpan.Zero : Frames.Max(f => f.EndTime); } }
        #endregion

        #region Methods

        /// <summary>
        /// Peeks the next available frame in the queue without removing it.
        /// If no frames are available, null is returned.
        /// </summary>
        /// <returns></returns>
        public MediaFrame Peek()
        {
            lock (SyncRoot)
            {
                if (Frames.Count <= 0) return null;
                return Frames[0];
            }
        }

        /// <summary>
        /// Pushes the specified frame into the queue.
        /// In other words, enqueues the frame.
        /// </summary>
        /// <param name="frame">The frame.</param>
        public void Push(MediaFrame frame)
        {
            lock (SyncRoot)
                Frames.Add(frame);
        }

        /// <summary>
        /// Dequeues a frame from this queue.
        /// </summary>
        /// <returns></returns>
        public MediaFrame Dequeue()
        {
            lock (SyncRoot)
            {
                if (Frames.Count <= 0) return null;
                var frame = Frames[0];
                Frames.RemoveAt(0);
                return frame;
            }
        }

        /// <summary>
        /// Clears and frees all frames from this queue.
        /// </summary>
        public void Clear()
        {
            lock (SyncRoot)
            {
                while (Frames.Count > 0)
                {
                    var frame = Dequeue();
                    frame.Dispose();
                    frame = null;
                }
            }
        }

        #endregion

        #region IDisposable Support

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="alsoManaged"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        private void Dispose(bool alsoManaged)
        {
            if (!IsDisposed)
            {
                IsDisposed = true;
                if (alsoManaged)
                    Clear();
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        #endregion
    }
}
