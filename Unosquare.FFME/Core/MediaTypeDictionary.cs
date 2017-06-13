namespace Unosquare.FFME.Core
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents a very simple dictionary for MediaType keys
    /// </summary>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <seealso cref="System.Collections.Generic.Dictionary{TKey, TValue}" />
    internal sealed class MediaTypeDictionary<TValue>
        : Dictionary<MediaType, TValue>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MediaTypeDictionary{TValue}"/> class.
        /// </summary>
        public MediaTypeDictionary()
            : base(Enum.GetValues(typeof(MediaType)).Length)
        {
            // placeholder
        }

        /// <summary>
        /// Gets or sets the <see cref="TValue"/> with the specified key.
        /// return the default value of the value type when the key does not exist.
        /// </summary>
        /// <value>
        /// The <see cref="TValue"/>.
        /// </value>
        /// <param name="key">The key.</param>
        /// <returns></returns>
        public new TValue this[MediaType key]
        {
            get
            {
                if (ContainsKey(key) == false)
                    return default(TValue);

                return base[key];
            }
            set
            {
                base[key] = value;
            }
        }
    }
}
