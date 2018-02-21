namespace Unosquare.FFME.Decoding
{
    using Core;
    using FFmpeg.AutoGen;
    using Primitives;
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

        private readonly List<IntPtr> PacketPointers = new List<IntPtr>();
        private ISyncLocker Locker = SyncLockerFactory.Create(useSlim: true);
        private bool IsDisposed = false; // To detect redundant calls
        private int m_BufferLength = default;
        private long m_Duration = default;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the packet count.
        /// </summary>
        public int Count
        {
            get
            {
                using (Locker.AcquireReaderLock())
                {
                    return PacketPointers.Count;
                }
            }
        }

        /// <summary>
        /// Gets the sum of all the packet sizes contained
        /// by this queue.
        /// </summary>
        public int BufferLength
        {
            get
            {
                using (Locker.AcquireReaderLock())
                {
                    return m_BufferLength;
                }
            }
        }

        /// <summary>
        /// Gets the total duration in stream TimeBase units.
        /// </summary>
        public long Duration
        {
            get
            {
                using (Locker.AcquireReaderLock())
                {
                    return m_Duration;
                }
            }
        }

        /// <summary>
        /// Gets or sets the <see cref="AVPacket"/> at the specified index.
        /// </summary>
        /// <value>
        /// The <see cref="AVPacket"/>.
        /// </value>
        /// <param name="index">The index.</param>
        /// <returns>The packet reference</returns>
        private AVPacket* this[int index]
        {
            get
            {
                using (Locker.AcquireReaderLock())
                {
                    return (AVPacket*)PacketPointers[index];
                }
            }
            set
            {
                using (Locker.AcquireWriterLock())
                {
                    PacketPointers[index] = (IntPtr)value;
                }
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Peeks the next available packet in the queue without removing it.
        /// If no packets are available, null is returned.
        /// </summary>
        /// <returns>The packet</returns>
        public AVPacket* Peek()
        {
            using (Locker.AcquireReaderLock())
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
            // avoid pushing null packets
            if (packet == null) return;

            using (Locker.AcquireWriterLock())
            {
                PacketPointers.Add((IntPtr)packet);
                m_BufferLength += packet->size;
                m_Duration += packet->duration;
            }
        }

        /// <summary>
        /// Dequeues a packet from this queue.
        /// </summary>
        /// <returns>The dequeued packet</returns>
        public AVPacket* Dequeue()
        {
            using (Locker.AcquireWriterLock())
            {
                if (PacketPointers.Count <= 0) return null;
                var result = PacketPointers[0];
                PacketPointers.RemoveAt(0);

                var packet = (AVPacket*)result;
                m_BufferLength -= packet->size;
                m_Duration -= packet->duration;
                return packet;
            }
        }

        /// <summary>
        /// Clears and frees all the unmanaged packets from this queue.
        /// </summary>
        public void Clear()
        {
            using (Locker.AcquireWriterLock())
            {
                while (PacketPointers.Count > 0)
                {
                    var packet = Dequeue();
                    RC.Current.Remove(packet);
                    ffmpeg.av_packet_free(&packet);
                }

                m_BufferLength = 0;
                m_Duration = 0;
            }
        }

        #endregion

        #region IDisposable Support

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

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
                {
                    Clear();
                    Locker.Dispose();
                }

                Locker = null;
            }
        }

        #endregion
    }
}
