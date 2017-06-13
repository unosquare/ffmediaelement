namespace Unosquare.FFME.Decoding
{
    using FFmpeg.AutoGen;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A data structure containing a quque of packets to process.
    /// This class is thread safe and disposable.
    /// Enqueued, unmanaged packets are disposed automatically by this queue.
    /// Dequeued packets are the responsibility of the calling code.
    /// </summary>
    internal sealed unsafe class PacketQueue : IDisposable
    {
        #region Private Declarations

        private bool IsDisposed = false; // To detect redundant calls
        private readonly List<IntPtr> PacketPointers = new List<IntPtr>();
        private readonly object SyncRoot = new object();

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the <see cref="AVPacket"/> at the specified index.
        /// </summary>
        /// <value>
        /// The <see cref="AVPacket"/>.
        /// </value>
        /// <param name="index">The index.</param>
        /// <returns></returns>
        private AVPacket* this[int index]
        {
            get
            {
                lock (SyncRoot)
                    return (AVPacket*)PacketPointers[index];
            }
            set
            {
                lock (SyncRoot)
                    PacketPointers[index] = (IntPtr)value;
            }
        }

        /// <summary>
        /// Gets the packet count.
        /// </summary>
        public int Count
        {
            get
            {
                lock (SyncRoot)
                    return PacketPointers.Count;
            }
        }

        /// <summary>
        /// Gets the sum of all the packet sizes contained
        /// by this queue.
        /// </summary>
        public int BufferLength { get; private set; }

        /// <summary>
        /// Gets the total duration in stream TimeBase units.
        /// </summary>
        public long Duration { get; private set; }

        #endregion

        #region Methods

        /// <summary>
        /// Peeks the next available packet in the queue without removing it.
        /// If no packets are available, null is returned.
        /// </summary>
        /// <returns></returns>
        public AVPacket* Peek()
        {
            lock (SyncRoot)
            {
                if (PacketPointers.Count <= 0) return null;
                return (AVPacket*)PacketPointers[0];
            }
        }

        /// <summary>
        /// Pushes the specified packet into the queue.
        /// In other words, enqueues the packet.
        /// </summary>
        /// <param name="packet">The packet.</param>
        public void Push(AVPacket* packet)
        {
            lock (SyncRoot)
            {
                PacketPointers.Add((IntPtr)packet);
                BufferLength += packet->size;
                Duration += packet->duration;
            }

        }

        /// <summary>
        /// Dequeues a packet from this queue.
        /// </summary>
        /// <returns></returns>
        public AVPacket* Dequeue()
        {
            lock (SyncRoot)
            {
                if (PacketPointers.Count <= 0) return null;
                var result = PacketPointers[0];
                PacketPointers.RemoveAt(0);

                var packet = (AVPacket*)result;
                BufferLength -= packet->size;
                Duration -= packet->duration;
                return packet;
            }
        }

        /// <summary>
        /// Clears and frees all the unmanaged packets from this queue.
        /// </summary>
        public void Clear()
        {
            lock (SyncRoot)
            {
                while (PacketPointers.Count > 0)
                {
                    var packet = Dequeue();
                    ffmpeg.av_packet_free(&packet);
                }

                BufferLength = 0;
                Duration = 0;
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
