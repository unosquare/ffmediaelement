namespace Unosquare.FFME.Diagnostics
{
    using FFmpeg.AutoGen;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// A reference counter to keep track of unmanaged objects.
    /// </summary>
    internal unsafe class RC
    {
        /// <summary>
        /// The synchronization lock.
        /// </summary>
        private static readonly object SyncLock = new();

        /// <summary>
        /// The current reference counter instance.
        /// </summary>
        private static RC m_Current;

        /// <summary>
        /// The instances.
        /// </summary>
        private readonly Dictionary<IntPtr, ReferenceEntry> Instances = new();

        /// <summary>
        /// The types of tracked unmanaged types.
        /// </summary>
        public enum UnmanagedType
        {
            /// <summary>
            /// No media type.
            /// </summary>
            None,

            /// <summary>
            /// The packet.
            /// </summary>
            Packet,

            /// <summary>
            /// The frame.
            /// </summary>
            Frame,

            /// <summary>
            /// The filter graph.
            /// </summary>
            FilterGraph,

            /// <summary>
            /// The SWR context.
            /// </summary>
            SwrContext,

            /// <summary>
            /// The codec context.
            /// </summary>
            CodecContext,

            /// <summary>
            /// The SWS context.
            /// </summary>
            SwsContext
        }

        /// <summary>
        /// Gets the singleton instance of the reference counter.
        /// </summary>
        public static RC Current
        {
            get
            {
                lock (SyncLock)
                {
                    return m_Current ??= new RC();
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
                    var result = new Dictionary<string, int>(256);
                    foreach (var kvp in Instances)
                    {
                        var loc = $"{kvp.Value}";
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
        /// Removes the specified unmanaged object reference.
        /// </summary>
        /// <param name="ptr">The PTR.</param>
        public void Remove(IntPtr ptr)
        {
            if (!Debugger.IsAttached) return;

            lock (SyncLock)
                Instances.Remove(ptr);
        }

        /// <summary>
        /// Removes the specified unmanaged object reference.
        /// </summary>
        /// <param name="ptr">The unmanaged object reference.</param>
        public void Remove(void* ptr)
        {
            Remove((IntPtr)ptr);
        }

        /// <summary>
        /// Adds the specified pointer.
        /// </summary>
        /// <param name="pointer">The pointer.</param>
        /// <param name="memberName">Name of the member.</param>
        /// <param name="filePath">The file path.</param>
        /// <param name="lineNumber">The line number.</param>
        public void Add(AVPacket* pointer,
            [CallerMemberName] string memberName = null,
            [CallerFilePath] string filePath = null,
            [CallerLineNumber] int lineNumber = 0) =>
            AddInternal(UnmanagedType.Packet, (IntPtr)pointer, memberName, filePath, lineNumber);

        /// <summary>
        /// Adds the specified pointer.
        /// </summary>
        /// <param name="pointer">The pointer.</param>
        /// <param name="memberName">Name of the member.</param>
        /// <param name="filePath">The file path.</param>
        /// <param name="lineNumber">The line number.</param>
        public void Add(SwrContext* pointer,
            [CallerMemberName] string memberName = null,
            [CallerFilePath] string filePath = null,
            [CallerLineNumber] int lineNumber = 0) =>
            AddInternal(UnmanagedType.SwrContext, (IntPtr)pointer, memberName, filePath, lineNumber);

        /// <summary>
        /// Adds the specified pointer.
        /// </summary>
        /// <param name="pointer">The pointer.</param>
        /// <param name="memberName">Name of the member.</param>
        /// <param name="filePath">The file path.</param>
        /// <param name="lineNumber">The line number.</param>
        public void Add(SwsContext* pointer,
            [CallerMemberName] string memberName = null,
            [CallerFilePath] string filePath = null,
            [CallerLineNumber] int lineNumber = 0) =>
            AddInternal(UnmanagedType.SwsContext, (IntPtr)pointer, memberName, filePath, lineNumber);

        /// <summary>
        /// Adds the specified pointer.
        /// </summary>
        /// <param name="pointer">The pointer.</param>
        /// <param name="memberName">Name of the member.</param>
        /// <param name="filePath">The file path.</param>
        /// <param name="lineNumber">The line number.</param>
        public void Add(AVCodecContext* pointer,
            [CallerMemberName] string memberName = null,
            [CallerFilePath] string filePath = null,
            [CallerLineNumber] int lineNumber = 0) =>
            AddInternal(UnmanagedType.CodecContext, (IntPtr)pointer, memberName, filePath, lineNumber);

        /// <summary>
        /// Adds the specified pointer.
        /// </summary>
        /// <param name="pointer">The pointer.</param>
        /// <param name="memberName">Name of the member.</param>
        /// <param name="filePath">The file path.</param>
        /// <param name="lineNumber">The line number.</param>
        public void Add(AVFrame* pointer,
            [CallerMemberName] string memberName = null,
            [CallerFilePath] string filePath = null,
            [CallerLineNumber] int lineNumber = 0) =>
            AddInternal(UnmanagedType.Frame, (IntPtr)pointer, memberName, filePath, lineNumber);

        /// <summary>
        /// Adds the specified pointer.
        /// </summary>
        /// <param name="pointer">The pointer.</param>
        /// <param name="memberName">Name of the member.</param>
        /// <param name="filePath">The file path.</param>
        /// <param name="lineNumber">The line number.</param>
        public void Add(AVFilterGraph* pointer,
            [CallerMemberName] string memberName = null,
            [CallerFilePath] string filePath = null,
            [CallerLineNumber] int lineNumber = 0) =>
            AddInternal(UnmanagedType.FilterGraph, (IntPtr)pointer, memberName, filePath, lineNumber);

        /// <summary>
        /// Adds the specified unmanaged object reference.
        /// </summary>
        /// <param name="unmanagedType">Type of the unmanaged.</param>
        /// <param name="pointer">The pointer.</param>
        /// <param name="memberName">Name of the member.</param>
        /// <param name="filePath">The file path.</param>
        /// <param name="lineNumber">The line number.</param>
        private void AddInternal(UnmanagedType unmanagedType, IntPtr pointer, string memberName, string filePath, int lineNumber)
        {
            if (!Debugger.IsAttached) return;

            lock (SyncLock)
            {
                Instances[pointer] = new ReferenceEntry(
                    unmanagedType, pointer, memberName, filePath, lineNumber);
            }
        }

        /// <summary>
        /// A reference entry.
        /// </summary>
        public class ReferenceEntry
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="ReferenceEntry"/> class.
            /// </summary>
            /// <param name="unmanagedType">Type of the unmanaged.</param>
            /// <param name="pointer">The pointer.</param>
            /// <param name="memberName">Name of the member.</param>
            /// <param name="filePath">The file path.</param>
            /// <param name="lineNumber">The line number.</param>
            public ReferenceEntry(UnmanagedType unmanagedType, IntPtr pointer, string memberName, string filePath, int lineNumber)
            {
                Type = unmanagedType;
                Pointer = pointer;
                MemberName = memberName;
                FilePath = filePath;
                LineNumber = lineNumber;
                FileName = (string.IsNullOrWhiteSpace(filePath) == false) ?
                    Path.GetFileName(filePath) : string.Empty;
            }

            /// <summary>
            /// Gets the unmanaged type.
            /// </summary>
            public UnmanagedType Type { get; }

            /// <summary>
            /// Gets the pointer to the memory location of the unmanaged object.
            /// </summary>
            public IntPtr Pointer { get; }

            /// <summary>
            /// Gets the name of the member that created the unmanaged object.
            /// </summary>
            public string MemberName { get; }

            /// <summary>
            /// Gets the file path of the code that created the unmanaged object.
            /// </summary>
            public string FilePath { get; }

            /// <summary>
            /// Gets the file name of the code that created the unmanaged object.
            /// </summary>
            public string FileName { get; }

            /// <summary>
            /// Gets the line number of the code that created the unmanaged object.
            /// </summary>
            public int LineNumber { get; }

            /// <inheritdoc />
            public override string ToString() =>
                $"{Type} - {FileName}; Line: {LineNumber}, Member: {MemberName}";
        }
    }
}