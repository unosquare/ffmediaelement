﻿namespace Unosquare.FFME.Core
{
    using FFmpeg.AutoGen;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// An AVDictionary management class
    /// </summary>
    internal unsafe class FFDictionary : IDisposable
    {
        #region Unmanaged Fields

        // These pointers and references are created by unmanaged code
        // there is no need to pin them.
#pragma warning disable SA1401 // Fields must be private
        public AVDictionary* Pointer;
#pragma warning restore SA1401 // Fields must be private

        /// <summary>
        /// To detect redundant Dispose calls
        /// </summary>
        private bool IsDisposed = false;

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
        /// Gets the number of elements in the dictionary
        /// </summary>
        /// <value>
        /// The count.
        /// </value>
        public int Count
        {
            get
            {
                if (Pointer == null) return 0;
                return ffmpeg.av_dict_count(Pointer);
            }
        }

        /// <summary>
        /// Gets or sets the value with the specified key.
        /// </summary>
        /// <value>
        /// The <see cref="System.String"/>.
        /// </value>
        /// <param name="key">The key.</param>
        /// <returns>The entry</returns>
        public string this[string key]
        {
            get
            {
                return Get(key);
            }
            set
            {
                Set(key, value, false);
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Converts the AVDictionary to a regular dictionary.
        /// </summary>
        /// <param name="dictionary">The dictionary to convert from.</param>
        /// <returns>the converterd dictionary</returns>
        public static Dictionary<string, string> ToDictionary(AVDictionary* dictionary)
        {
            var result = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

            var metadataEntry = ffmpeg.av_dict_get(dictionary, string.Empty, null, ffmpeg.AV_DICT_IGNORE_SUFFIX);
            while (metadataEntry != null)
            {
                result[Utils.PtrToString(metadataEntry->key)] = Utils.PtrToString(metadataEntry->value);
                metadataEntry = ffmpeg.av_dict_get(dictionary, string.Empty, metadataEntry, ffmpeg.AV_DICT_IGNORE_SUFFIX);
            }

            return result;
        }

        /// <summary>
        /// A wrapper for the av_dict_get method
        /// </summary>
        /// <param name="dictionary">The dictionary.</param>
        /// <param name="key">The key.</param>
        /// <param name="matchCase">if set to <c>true</c> [match case].</param>
        /// <returns>The Entry</returns>
        public static FFDictionaryEntry GetEntry(AVDictionary* dictionary, string key, bool matchCase = true)
        {
            if (dictionary == null)
                return null;

            var entryPointer = ffmpeg.av_dict_get(dictionary, key, null, matchCase ? ffmpeg.AV_DICT_MATCH_CASE : 0);
            if (entryPointer == null) return null;
            return new FFDictionaryEntry(entryPointer);
        }

        /// <summary>
        /// Fills this dictionary with a set of options
        /// </summary>
        /// <param name="other">The other dictionary (source)</param>
        public void Fill(Dictionary<string, string> other)
        {
            if (other == null) return;
            foreach (var kvp in other)
                this[kvp.Key] = kvp.Value;
        }

        /// <summary>
        /// Gets the first entry. Null if no entries.
        /// </summary>
        /// <returns>The entry</returns>
        public FFDictionaryEntry First()
        {
            return Next(null);
        }

        /// <summary>
        /// Gets the next entry based on the provided prior entry.
        /// </summary>
        /// <param name="prior">The prior entry.</param>
        /// <returns>The entry</returns>
        public FFDictionaryEntry Next(FFDictionaryEntry prior)
        {
            if (Pointer == null)
                return null;

            var priorEntry = prior == null ? null : prior.Pointer;
            var nextEntry = ffmpeg.av_dict_get(Pointer, string.Empty, priorEntry, ffmpeg.AV_DICT_IGNORE_SUFFIX);
            return new FFDictionaryEntry(nextEntry);
        }

        /// <summary>
        /// Determines if the given key exists in the dictionary
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="matchCase">if set to <c>true</c> [match case].</param>
        /// <returns>True or False</returns>
        public bool HasKey(string key, bool matchCase = true)
        {
            if (Pointer == null) return false;
            return ffmpeg.av_dict_get(Pointer, key, null, matchCase ? ffmpeg.AV_DICT_MATCH_CASE : 0) != null;
        }

        /// <summary>
        /// Gets the entry given the key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="matchCase">if set to <c>true</c> [match case].</param>
        /// <returns>The entry</returns>
        public FFDictionaryEntry GetEntry(string key, bool matchCase = true)
        {
            return GetEntry(Pointer, key, matchCase);
        }

        /// <summary>
        /// Gets the value with specified key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>The value</returns>
        public string Get(string key)
        {
            var entry = GetEntry(key, true);
            return entry == null ? null : entry.Value;
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
        /// <param name="dontOverwrite">if set to <c>true</c> [dont overwrite].</param>
        public void Set(string key, string value, bool dontOverwrite)
        {
            var flags = 0;
            if (dontOverwrite) flags |= ffmpeg.AV_DICT_DONT_OVERWRITE;

            fixed (AVDictionary** reference = &Pointer)
            {
                ffmpeg.av_dict_set(reference, key, value, flags);
                Pointer = *reference;
            }
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

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        #endregion

        #region IDisposable Support

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="alsoManaged"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool alsoManaged)
        {
            if (!IsDisposed)
            {
                if (alsoManaged)
                {
                    fixed (AVDictionary** reference = &Pointer)
                        ffmpeg.av_dict_free(reference);
                }

                IsDisposed = true;
            }
        }

        #endregion

    }
}
