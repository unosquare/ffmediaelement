namespace Unosquare.FFME.Decoding
{
    using FFmpeg.AutoGen;
    using Primitives;
    using Shared;
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

        private readonly List<MediaPacket> PacketPointers = new List<MediaPacket>(2048);
        private readonly ISyncLocker Locker = SyncLockerFactory.Create(useSlim: true);
        private ulong m_BufferLength = default;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the packet count.
        /// </summary>
        public int Count
        {
            get { using (Locker.AcquireReaderLock()) return PacketPointers.Count; }
        }

        /// <summary>
        /// Gets the sum of all the packet sizes contained
        /// by this queue.
        /// </summary>
        public ulong BufferLength
        {
            get { using (Locker.AcquireReaderLock()) return m_BufferLength; }
        }

        /// <summary>
        /// Gets or sets the <see cref="AVPacket"/> at the specified index.
        /// </summary>
        /// <value>
        /// The <see cref="AVPacket"/>.
        /// </value>
        /// <param name="index">The index.</param>
        /// <returns>The packet reference</returns>
        private MediaPacket this[int index]
        {
            get { using (Locker.AcquireReaderLock()) return PacketPointers[index]; }
            set { using (Locker.AcquireWriterLock()) PacketPointers[index] = value; }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Gets the duration in stream time base units.
        /// </summary>
        /// <param name="timeBase">The time base.</param>
        /// <returns>The total duration</returns>
        public TimeSpan GetDuration(AVRational timeBase)
        {
            var packetDuration = 0L;
            var totalDuration = 0L;
            using (Locker.AcquireReaderLock())
            {
                foreach (var packet in PacketPointers)
                {
                    if (packet == null) continue;
                    packetDuration = packet.Duration;
                    if (packetDuration > 0)
                        totalDuration += packetDuration;
                }
            }

            return totalDuration.ToTimeSpan(timeBase);
        }

        /// <summary>
        /// Peeks the next available packet in the queue without removing it.
        /// If no packets are available, null is returned.
        /// </summary>
        /// <returns>The packet</returns>
        public MediaPacket Peek()
        {
            using (Locker.AcquireReaderLock())
            {
                if (PacketPointers.Count <= 0) return null;
                return PacketPointers[0];
            }
        }

        /// <summary>
        /// Pushes the specified packet into the queue.
        /// In other words, enqueues the packet.
        /// </summary>
        /// <param name="packet">The packet.</param>
        public void Push(MediaPacket packet)
        {
            // avoid pushing null packets
            if (packet == null) return;

            using (Locker.AcquireWriterLock())
            {
                PacketPointers.Add(packet);
                m_BufferLength += packet.Size < 0 ? default : (ulong)packet.Size;
            }
        }

        /// <summary>
        /// Dequeues a packet from this queue.
        /// </summary>
        /// <returns>The dequeued packet</returns>
        public MediaPacket Dequeue()
        {
            using (Locker.AcquireWriterLock())
            {
                if (PacketPointers.Count <= 0) return null;
                var result = PacketPointers[0];
                PacketPointers.RemoveAt(0);

                var packet = result;
                m_BufferLength -= packet.Size < 0 ? default : (ulong)packet.Size;
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
                    packet.Dispose();
                }

                m_BufferLength = 0;
            }
        }

        #endregion

        #region IDisposable Support

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose() => Dispose(true);

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="alsoManaged"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        private void Dispose(bool alsoManaged)
        {
            if (Locker.IsDisposed) return;
            if (alsoManaged == false) return;

            Clear();
            Locker.Dispose();
        }

        #endregion
    }
}
