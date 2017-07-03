#if REFCOUNTER
namespace Unosquare.FFME.Core
{
    using FFmpeg.AutoGen;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A reference counter to keep track of unmanaged objects
    /// </summary>
    internal static unsafe class ReferenceCounter
    {
        /// <summary>
        /// The synchronization lock
        /// </summary>
        private static readonly object SyncLock = new object();

        /// <summary>
        /// The types of tracked unmanaged types
        /// </summary>
        public enum UnmanagedType
        {
            Packet,
            Frame
        }

        /// <summary>
        /// A reference entry
        /// </summary>
        public class CounterEntry
        {
            public UnmanagedType Type;
            public string Location;
            public IntPtr Instance;
        }

        /// <summary>
        /// The instances
        /// </summary>
        public static readonly Dictionary<IntPtr, CounterEntry> Instances = new Dictionary<IntPtr, CounterEntry>();

        /// <summary>
        /// Adds the specified packet.
        /// </summary>
        /// <param name="packet">The packet.</param>
        /// <param name="location">The location.</param>
        public static void Add(AVPacket* packet, string location)
        {
            lock (SyncLock) Instances[new IntPtr(packet)] = new CounterEntry() { Instance = new IntPtr(packet), Type = UnmanagedType.Packet, Location = location };
        }

        /// <summary>
        /// Adds the specified frame.
        /// </summary>
        /// <param name="frame">The frame.</param>
        /// <param name="location">The location.</param>
        public static void Add(AVFrame* frame, string location)
        {
            lock (SyncLock) Instances[new IntPtr(frame)] = new CounterEntry() { Instance = new IntPtr(frame), Type = UnmanagedType.Frame, Location = location };
        }

        /// <summary>
        /// Subtracts the specified packet.
        /// </summary>
        /// <param name="packet">The packet.</param>
        public static void Subtract(AVPacket* packet)
        {
            lock (SyncLock) Instances.Remove(new IntPtr(packet));
        }

        /// <summary>
        /// Subtracts the specified frame.
        /// </summary>
        /// <param name="frame">The frame.</param>
        public static void Subtract(AVFrame* frame)
        {
            lock (SyncLock) Instances.Remove(new IntPtr(frame));
        }

        /// <summary>
        /// Gets the instances by location.
        /// </summary>
        /// <value>
        /// The instances by location.
        /// </value>
        public static Dictionary<string, List<CounterEntry>> InstancesByLocation
        {
            get
            {
                lock (SyncLock)
                {
                    var result = new Dictionary<string, List<CounterEntry>>();
                    foreach (var kvp in Instances)
                    {
                        if (result.ContainsKey(kvp.Value.Location) == false)
                            result[kvp.Value.Location] = new List<CounterEntry>();

                        result[kvp.Value.Location].Add(kvp.Value);
                    }

                    return result;
                }
            }
        }
    }
}
#endif