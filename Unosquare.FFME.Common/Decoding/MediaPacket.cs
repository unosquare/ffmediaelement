namespace Unosquare.FFME.Decoding
{
    using FFmpeg.AutoGen;
    using Primitives;
    using System;
    using System.Runtime.CompilerServices;
    using Unosquare.FFME.Core;

    /// <summary>
    /// Represents a managed packet wrapper for the <see cref="AVPacket"/> struct.
    /// </summary>
    /// <seealso cref="IDisposable" />
    internal sealed unsafe class MediaPacket : IDisposable
    {
        /// <summary>
        /// The flush packet data pointer
        /// </summary>
        private static readonly IntPtr FlushPacketData = (IntPtr)ffmpeg.av_malloc(0);

        private readonly AtomicBoolean m_IsDisposed = new AtomicBoolean(false);
        private readonly IntPtr m_Pointer;

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaPacket" /> class.
        /// </summary>
        /// <param name="packet">The packet.</param>
        private MediaPacket(AVPacket* packet)
        {
            m_Pointer = new IntPtr(packet);
        }

        /// <summary>
        /// Gets the <see cref="AVPacket"/> pointer.
        /// </summary>
        public AVPacket* Pointer => m_IsDisposed.Value ? null : (AVPacket*)m_Pointer;

        /// <summary>
        /// Gets the <see cref="AVPacket"/> safe pointer.
        /// </summary>
        public IntPtr SafePointer => m_IsDisposed.Value ? IntPtr.Zero : m_Pointer;

        /// <summary>
        /// Gets the size in bytes.
        /// </summary>
        public int Size => m_IsDisposed.Value ? 0 : ((AVPacket*)m_Pointer)->size;

        /// <summary>
        /// Gets the byte position of the packet -1 if unknown.
        /// </summary>
        public long Position => m_IsDisposed.Value ? 0 : ((AVPacket*)m_Pointer)->pos;

        /// <summary>
        /// Gets the stream index this packet belongs to.
        /// </summary>
        public int StreamIndex => m_IsDisposed.Value ? -1 : ((AVPacket*)m_Pointer)->stream_index;

        /// <summary>
        /// Gets the duration in stream timebase units.
        /// </summary>
        public long Duration => m_IsDisposed.Value ? -1 : ((AVPacket*)m_Pointer)->duration;

        /// <summary>
        /// Gets a value indicating whether the specified packet is a flush packet.
        /// These flush packets are used to clear the internal decoder buffers
        /// </summary>
        public bool IsFlushPacket => m_IsDisposed.Value ? false
            : (IntPtr)((AVPacket*)m_Pointer)->data == FlushPacketData;

        /// <summary>
        /// Gets a value indicating whether this instance is disposed.
        /// </summary>
        public bool IsDisposed => m_IsDisposed.Value;

        /// <summary>
        /// Allocates a default readable packet
        /// </summary>
        /// <returns>
        /// A packet used for receiving data
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MediaPacket CreateReadPacket()
        {
            var packet = new MediaPacket(ffmpeg.av_packet_alloc());
            RC.Current.Add(packet.Pointer, $"174: {nameof(MediaPacket)}.{nameof(CreateReadPacket)}()");
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
        public static MediaPacket CreateEmptyPacket(int streamIndex)
        {
            var packet = new MediaPacket(ffmpeg.av_packet_alloc());
            RC.Current.Add(packet.Pointer, $"184: {nameof(MediaPacket)}.{nameof(CreateEmptyPacket)}({streamIndex})");
            ffmpeg.av_init_packet(packet.Pointer);
            packet.Pointer->data = null;
            packet.Pointer->size = 0;
            packet.Pointer->stream_index = streamIndex;
            return packet;
        }

        /// <summary>
        /// Creates a flush packet.
        /// </summary>
        /// <param name="streamIndex">The stream index this packet belongs to.</param>
        /// <returns>A special packet that makes the decoder flush its buffers</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MediaPacket CreateFlushPacket(int streamIndex)
        {
            var packet = new MediaPacket(ffmpeg.av_packet_alloc());
            RC.Current.Add(packet.Pointer, $"202: {nameof(MediaPacket)}.{nameof(CreateFlushPacket)}({streamIndex})");
            ffmpeg.av_init_packet(packet.Pointer);
            packet.Pointer->data = (byte*)FlushPacketData;
            packet.Pointer->size = 0;
            packet.Pointer->stream_index = streamIndex;

            return packet;
        }

        /// <summary>
        /// Clones the packet.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <returns>The packet clone</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MediaPacket ClonePacket(AVPacket* source)
        {
            var packet = new MediaPacket(ffmpeg.av_packet_clone(source));
            RC.Current.Add(packet.Pointer, $"160: {nameof(MediaPacket)}.{nameof(ClonePacket)}()");
            return packet;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (m_IsDisposed.Value == true) return;
            m_IsDisposed.Value = true;

            if (m_Pointer == IntPtr.Zero) return;

            var packetPointer = (AVPacket*)m_Pointer;
            RC.Current.Remove(packetPointer);
            ffmpeg.av_packet_free(&packetPointer);
        }
    }
}
