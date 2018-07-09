namespace Unosquare.FFME.Decoding
{
    using Core;
    using FFmpeg.AutoGen;
    using Primitives;
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// A data structure containing a quque of packets to process.
    /// This class is thread safe and disposable.
    /// Enqueued, unmanaged packets are disposed automatically by this queue.
    /// Dequeued packets are the responsibility of the calling code.
    /// </summary>
    internal sealed unsafe class PacketQueue : IDisposable
    {
        #region Private Declarations

        /// <summary>
        /// The flush packet data pointer
        /// </summary>
        internal static readonly IntPtr FlushPacketData = new IntPtr(ffmpeg.av_malloc(0));

        private readonly List<IntPtr> PacketPointers = new List<IntPtr>(2048);
        private ISyncLocker Locker = SyncLockerFactory.Create(useSlim: true);
        private bool IsDisposed = false; // To detect redundant calls
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
        private AVPacket* this[int index]
        {
            get { using (Locker.AcquireReaderLock()) return (AVPacket*)PacketPointers[index]; }
            set { using (Locker.AcquireWriterLock()) PacketPointers[index] = (IntPtr)value; }
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
                m_BufferLength += packet->size < 0 ? default : (ulong)packet->size;
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
                m_BufferLength -= packet->size < 0 ? default : (ulong)packet->size;
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
                    ReleasePacket(packet);
                }

                m_BufferLength = 0;
            }
        }

        #endregion

        #region IDisposable Support

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose() =>
            Dispose(true);

        /// <summary>
        /// Releases the packet from memory
        /// </summary>
        /// <param name="packet">The packet.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void ReleasePacket(AVPacket* packet)
        {
            RC.Current.Remove(packet);
            ffmpeg.av_packet_free(&packet);
        }

        /// <summary>
        /// Create a new packet that references the same data as the source packet.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <returns>A clone of the packet</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static AVPacket* ClonePacket(AVPacket* source)
        {
            var packet = ffmpeg.av_packet_clone(source);
            RC.Current.Add(packet, $"160: {nameof(PacketQueue)}.{nameof(ClonePacket)}()");
            return packet;
        }

        /// <summary>
        /// Allocates a default readable packet
        /// </summary>
        /// <returns>
        /// A packet used for receiving data
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static AVPacket* CreateReadPacket()
        {
            var packet = ffmpeg.av_packet_alloc();
            RC.Current.Add(packet, $"174: {nameof(PacketQueue)}.{nameof(CreateReadPacket)}()");
            return packet;
        }

        /// <summary>
        /// Creates the empty packet.
        /// </summary>
        /// <param name="streamIndex">The stream index this packet belongs to.</param>
        /// <returns>
        /// The special empty packet that instructs the decoder to enter draining mode
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static AVPacket* CreateEmptyPacket(int streamIndex)
        {
            var packet = ffmpeg.av_packet_alloc();
            RC.Current.Add(packet, $"184: {nameof(PacketQueue)}.{nameof(CreateEmptyPacket)}({streamIndex})");
            ffmpeg.av_init_packet(packet);
            packet->data = null;
            packet->size = 0;
            packet->stream_index = streamIndex;

            return packet;
        }

        /// <summary>
        /// Creates a flush packet.
        /// </summary>
        /// <param name="streamIndex">The stream index this packet belongs to.</param>
        /// <returns>A special packet that makes the decoder flush its buffers</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static AVPacket* CreateFlushPacket(int streamIndex)
        {
            var packet = ffmpeg.av_packet_alloc();
            RC.Current.Add(packet, $"202: {nameof(PacketQueue)}.{nameof(CreateFlushPacket)}({streamIndex})");
            ffmpeg.av_init_packet(packet);
            packet->data = (byte*)FlushPacketData;
            packet->size = 0;
            packet->stream_index = streamIndex;

            return packet;
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
