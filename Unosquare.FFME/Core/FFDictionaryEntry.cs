namespace Unosquare.FFME.Core
{
    using FFmpeg.AutoGen;

    /// <summary>
    /// An AVDictionaryEntry wrapper
    /// </summary>
    internal unsafe class FFDictionaryEntry
    {
        // This ointer is generated in unmanaged code.
        internal readonly AVDictionaryEntry* Pointer;

        /// <summary>
        /// Initializes a new instance of the <see cref="FFDictionaryEntry"/> class.
        /// </summary>
        /// <param name="entryPointer">The entry pointer.</param>
        public FFDictionaryEntry(AVDictionaryEntry* entryPointer)
        {
            Pointer = entryPointer;
        }

        /// <summary>
        /// Gets the key.
        /// </summary>
        public string Key => Pointer != null ?
                    Utils.PtrToString(Pointer->key) : null;

        /// <summary>
        /// Gets the value.
        /// </summary>
        public string Value => Pointer != null ?
                    Utils.PtrToString(Pointer->value) : null;
    }
}
