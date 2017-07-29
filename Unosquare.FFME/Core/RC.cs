namespace Unosquare.FFME.Core
{
    using FFmpeg.AutoGen;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A reference counter to keep track of unmanaged objects
    /// </summary>
    internal unsafe class RC
    {
        /// <summary>
        /// The synchronization lock
        /// </summary>
        private static readonly object SyncLock = new object();

        /// <summary>
        /// The current reference counter instance
        /// </summary>
        private static RC m_Current;

        /// <summary>
        /// The instances
        /// </summary>
        private readonly Dictionary<IntPtr, ReferenceEntry> Instances = new Dictionary<IntPtr, ReferenceEntry>();

        /// <summary>
        /// The types of tracked unmanaged types
        /// </summary>
        public enum UnmanagedType
        {
            /// <summary>
            /// The packet
            /// </summary>
            Packet,

            /// <summary>
            /// The frame
            /// </summary>
            Frame,

            /// <summary>
            /// The filter graph
            /// </summary>
            FilterGraph,

            /// <summary>
            /// The SWR context
            /// </summary>
            SwrContext,

            /// <summary>
            /// The codec context
            /// </summary>
            CodecContext,

            /// <summary>
            /// The SWS context
            /// </summary>
            SwsContext,
        }

        /// <summary>
        /// Gets the singleton instance of the reference counter
        /// </summary>
        public static RC Current
        {
            get
            {
                lock (SyncLock)
                {
                    if (m_Current == null) m_Current = new RC();
                    return m_Current;
                }
            }
        }

        /// <summary>
        /// Gets the number of instances by location.
        /// </summary>
        public Dictionary<string, int> InstancesByLocation
        {
            get
            {
                lock (SyncLock)
                {
                    var result = new Dictionary<string, int>();
                    foreach (var kvp in Instances)
                    {
                        var loc = $"T: {kvp.Value.Type} | L: {kvp.Value.Location}";
                        if (result.ContainsKey(loc) == false)
                            result[loc] = 1;
                        else
                            result[loc] += 1;
                    }

                    return result;
                }
            }
        }

        /// <summary>
        /// Adds the specified unmanaged object reference.
        /// </summary>
        /// <param name="t">The t.</param>
        /// <param name="ptr">The r.</param>
        /// <param name="location">The location.</param>
        public void Add(UnmanagedType t, IntPtr ptr, string location)
        {
#if DEBUG
            lock (SyncLock) Instances[ptr] =
                new ReferenceEntry() { Instance = ptr, Type = t, Location = location };
#endif
        }

        /// <summary>
        /// Removes the specified unmanaged object reference
        /// </summary>
        /// <param name="ptr">The PTR.</param>
        public void Remove(IntPtr ptr)
        {
#if DEBUG
            lock (SyncLock)
                Instances.Remove(ptr);
#endif
        }

        /// <summary>
        /// Removes the specified unmanaged object reference.
        /// </summary>
        /// <param name="ptr">The unmanaged object reference.</param>
        public void Remove(void* ptr)
        {
            Remove(new IntPtr(ptr));
        }

        /// <summary>
        /// Adds the specified packet.
        /// </summary>
        /// <param name="packet">The packet.</param>
        /// <param name="location">The location.</param>
        public void Add(AVPacket* packet, string location)
        {
            Add(UnmanagedType.Packet, new IntPtr(packet), location);
        }

        /// <summary>
        /// Adds the specified context.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="location">The location.</param>
        public void Add(SwrContext* context, string location)
        {
            Add(UnmanagedType.SwrContext, new IntPtr(context), location);
        }

        /// <summary>
        /// Adds the specified context.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="location">The location.</param>
        public void Add(SwsContext* context, string location)
        {
            Add(UnmanagedType.SwsContext, new IntPtr(context), location);
        }

        /// <summary>
        /// Adds the specified codec.
        /// </summary>
        /// <param name="codec">The codec.</param>
        /// <param name="location">The location.</param>
        public void Add(AVCodecContext* codec, string location)
        {
            Add(UnmanagedType.CodecContext, new IntPtr(codec), location);
        }

        /// <summary>
        /// Adds the specified frame.
        /// </summary>
        /// <param name="frame">The frame.</param>
        /// <param name="location">The location.</param>
        public void Add(AVFrame* frame, string location)
        {
            Add(UnmanagedType.Frame, new IntPtr(frame), location);
        }

        /// <summary>
        /// Adds the specified filtergraph.
        /// </summary>
        /// <param name="filtergraph">The filtergraph.</param>
        /// <param name="location">The location.</param>
        public void Add(AVFilterGraph* filtergraph, string location)
        {
            Add(UnmanagedType.FilterGraph, new IntPtr(filtergraph), location);
        }

        /// <summary>
        /// A reference entry
        /// </summary>
        public class ReferenceEntry
        {
            public UnmanagedType Type { get; set; } = default(UnmanagedType);
            public string Location { get; set; } = null;
            public IntPtr Instance { get; set; } = IntPtr.Zero;
        }
    }
}