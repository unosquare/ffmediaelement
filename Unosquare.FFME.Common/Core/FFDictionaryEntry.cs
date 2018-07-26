namespace Unosquare.FFME.Core
{
    using FFmpeg.AutoGen;
    using System;

    /// <summary>
    /// An AVDictionaryEntry wrapper
    /// </summary>
    internal unsafe class FFDictionaryEntry
    {
        // This pointer is generated in unmanaged code.
        private readonly IntPtr m_Pointer;

        /// <summary>
        /// Initializes a new instance of the <see cref="FFDictionaryEntry"/> class.
        /// </summary>
        /// <param name="entryPointer">The entry pointer.</param>
        public FFDictionaryEntry(AVDictionaryEntry* entryPointer)
        {
            m_Pointer = new IntPtr(entryPointer);
        }

        /// <summary>
        /// Gets the unmanaged pointer.
        /// </summary>
        public AVDictionaryEntry* Pointer => (AVDictionaryEntry*)m_Pointer;

        /// <summary>
        /// Gets the key.
        /// </summary>
        public string Key => m_Pointer != IntPtr.Zero ? FFInterop.PtrToStringUTF8(Pointer->key) : null;

        /// <summary>
        /// Gets the value.
        /// </summary>
        public string Value => m_Pointer != IntPtr.Zero ? FFInterop.PtrToStringUTF8(Pointer->value) : null;
    }
}
