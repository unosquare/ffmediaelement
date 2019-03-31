namespace FFmpeg.AutoGen
{
    using System;
    using System.Collections.Generic;
    using Unosquare.FFME;

    /// <inheritdoc />
    /// <summary>
    /// An AVDictionary management class.
    /// </summary>
    internal sealed unsafe class FFDictionary : IDisposable
    {
        #region Unmanaged Fields

        // These pointers and references are created by unmanaged code
        // there is no need to pin them.
        private IntPtr m_Pointer;

        /// <summary>
        /// To detect redundant Dispose calls.
        /// </summary>
        private bool IsDisposed;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="FFDictionary"/> class.
        /// </summary>
        public FFDictionary()
        {
            // placeholder
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FFDictionary"/> class.
        /// </summary>
        /// <param name="other">The other.</param>
        public FFDictionary(Dictionary<string, string> other)
        {
            Fill(other);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the unmanaged pointer to the dictionary object.
        /// </summary>
        public AVDictionary* Pointer => (AVDictionary*)m_Pointer;

        /// <summary>
        /// Gets the number of elements in the dictionary.
        /// </summary>
        /// <value>
        /// The count.
        /// </value>
        public int Count => m_Pointer == IntPtr.Zero ? 0 : ffmpeg.av_dict_count(Pointer);

        /// <summary>
        /// Gets or sets the value with the specified key.
        /// </summary>
        /// <value>
        /// The <see cref="string"/>.
        /// </value>
        /// <param name="key">The key.</param>
        /// <returns>The entry.</returns>
        public string this[string key]
        {
            get => Get(key);
            set => Set(key, value, false);
        }

        #endregion

        #region Methods

        /// <summary>
        /// Converts the AVDictionary to a regular dictionary.
        /// </summary>
        /// <param name="dictionary">The dictionary to convert from.</param>
        /// <returns>the converted dictionary.</returns>
        public static Dictionary<string, string> ToDictionary(AVDictionary* dictionary)
        {
            var result = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

            var kvpEntry = ffmpeg.av_dict_get(dictionary, string.Empty, null, ffmpeg.AV_DICT_IGNORE_SUFFIX);
            while (kvpEntry != null)
            {
                result[Utilities.PtrToStringUTF8(kvpEntry->key)] = Utilities.PtrToStringUTF8(kvpEntry->value);
                kvpEntry = ffmpeg.av_dict_get(dictionary, string.Empty, kvpEntry, ffmpeg.AV_DICT_IGNORE_SUFFIX);
            }

            return result;
        }

        /// <summary>
        /// A wrapper for the av_dict_get method.
        /// </summary>
        /// <param name="dictionary">The dictionary.</param>
        /// <param name="key">The key.</param>
        /// <param name="matchCase">if set to <c>true</c> [match case].</param>
        /// <returns>The Entry.</returns>
        public static FFDictionaryEntry GetEntry(AVDictionary* dictionary, string key, bool matchCase = true)
        {
            if (dictionary == null)
                return null;

            var entryPointer = ffmpeg.av_dict_get(dictionary, key, null, matchCase ? ffmpeg.AV_DICT_MATCH_CASE : 0);
            return entryPointer == null ? null : new FFDictionaryEntry(entryPointer);
        }

        /// <summary>
        /// Updates the pointer reference after modified.
        /// </summary>
        /// <param name="reference">The reference.</param>
        public void UpdateReference(AVDictionary* reference)
        {
            m_Pointer = new IntPtr(reference);
        }

        /// <summary>
        /// Fills this dictionary with a set of options.
        /// </summary>
        /// <param name="other">The other dictionary (source).</param>
        public void Fill(Dictionary<string, string> other)
        {
            if (other == null) return;
            foreach (var kvp in other)
                this[kvp.Key] = kvp.Value;
        }

        /// <summary>
        /// Gets the first entry. Null if no entries.
        /// </summary>
        /// <returns>The entry.</returns>
        public FFDictionaryEntry First()
        {
            return Next(null);
        }

        /// <summary>
        /// Gets the next entry based on the provided prior entry.
        /// </summary>
        /// <param name="prior">The prior entry.</param>
        /// <returns>The entry.</returns>
        public FFDictionaryEntry Next(FFDictionaryEntry prior)
        {
            if (m_Pointer == IntPtr.Zero)
                return null;

            var priorEntry = prior == null ? null : prior.Pointer;
            var nextEntry = ffmpeg.av_dict_get(Pointer, string.Empty, priorEntry, ffmpeg.AV_DICT_IGNORE_SUFFIX);
            return new FFDictionaryEntry(nextEntry);
        }

        /// <summary>
        /// Determines if the given key exists in the dictionary.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="matchCase">if set to <c>true</c> [match case].</param>
        /// <returns>True or False.</returns>
        public bool HasKey(string key, bool matchCase = true)
        {
            if (m_Pointer == IntPtr.Zero) return false;
            return ffmpeg.av_dict_get(Pointer, key, null, matchCase ? ffmpeg.AV_DICT_MATCH_CASE : 0) != null;
        }

        /// <summary>
        /// Gets the entry given the key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="matchCase">if set to <c>true</c> [match case].</param>
        /// <returns>The entry.</returns>
        public FFDictionaryEntry GetEntry(string key, bool matchCase = true)
        {
            return GetEntry(Pointer, key, matchCase);
        }

        /// <summary>
        /// Gets the value with specified key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>The value.</returns>
        public string Get(string key)
        {
            var entry = GetEntry(key);
            return entry?.Value;
        }

        /// <summary>
        /// Sets the value for the specified key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        public void Set(string key, string value)
        {
            Set(key, value, false);
        }

        /// <summary>
        /// Sets the value for the specified key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="preventOverwrite">if set to <c>true</c> don't overwrite existing value.</param>
        public void Set(string key, string value, bool preventOverwrite)
        {
            var flags = 0;
            if (preventOverwrite) flags |= ffmpeg.AV_DICT_DONT_OVERWRITE;

            var reference = Pointer;
            ffmpeg.av_dict_set(&reference, key, value, flags);
            m_Pointer = new IntPtr(reference);
        }

        /// <summary>
        /// Removes the entry with the specified key.
        /// </summary>
        /// <param name="key">The key.</param>
        public void Remove(string key)
        {
            if (HasKey(key))
                Set(key, null, false);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;
            var reference = Pointer;
            ffmpeg.av_dict_free(&reference);
            m_Pointer = IntPtr.Zero;
        }

        #endregion
    }
}
