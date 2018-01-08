namespace Unosquare.FFME.Core
{
    using FFmpeg.AutoGen;

    /// <summary>
    /// An AVDictionaryEntry wrapper
    /// </summary>
    internal unsafe class FFDictionaryEntry
    {
        // This ointer is generated in unmanaged code.
#pragma warning disable SA1401 // Fields must be private
        internal readonly AVDictionaryEntry* Pointer;
#pragma warning restore SA1401 // Fields must be private

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
                    FFInterop.PtrToString(Pointer->key) : null;

        /// <summary>
        /// Gets the value.
        /// </summary>
        public string Value => Pointer != null ?
                    FFInterop.PtrToString(Pointer->value) : null;
    }
}
