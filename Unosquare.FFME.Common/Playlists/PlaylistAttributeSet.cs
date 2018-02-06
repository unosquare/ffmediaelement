namespace Unosquare.FFME.Playlists
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Linq;

    /// <summary>
    /// Represents an observable dictionary of key-value pairs
    /// </summary>
    [DebuggerDisplay("Count={Count}")]
    public class PlaylistAttributeSet :
        ICollection<KeyValuePair<string, string>>, IDictionary<string, string>,
        INotifyCollectionChanged, INotifyPropertyChanged
    {
        private readonly IDictionary<string, string> dictionary;

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="PlaylistAttributeSet"/> class.
        /// </summary>
        public PlaylistAttributeSet()
            : this(new Dictionary<string, string>())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PlaylistAttributeSet"/> class.
        /// </summary>
        /// <param name="dictionary">The dictionary.</param>
        public PlaylistAttributeSet(IDictionary<string, string> dictionary)
        {
            this.dictionary = dictionary;
        }

        #endregion

        #region Events

        /// <summary>Event raised when the collection changes.</summary>
        public event NotifyCollectionChangedEventHandler CollectionChanged = (sender, args) => { };

        /// <summary>Event raised when a property on the collection changes.</summary>
        public event PropertyChangedEventHandler PropertyChanged = (sender, args) => { };

        #endregion

        #region Properties

        /// <summary>
        /// Gets an <see cref="T:System.Collections.Generic.ICollection`1"></see> containing the keys of the <see cref="T:System.Collections.Generic.IDictionary`2"></see>.
        /// </summary>
        public ICollection<string> Keys => dictionary.Keys;

        /// <summary>
        /// Gets an <see cref="T:System.Collections.Generic.ICollection`1" /> containing the values in the <see cref="T:System.Collections.Generic.IDictionary`2" />.
        /// </summary>
        /// <returns>An <see cref="T:System.Collections.Generic.ICollection`1" /> containing the values in the object that implements <see cref="T:System.Collections.Generic.IDictionary`2" />.</returns>
        public ICollection<string> Values => dictionary.Values;

        /// <summary>
        /// Gets the number of elements contained in the <see cref="T:System.Collections.Generic.ICollection`1"></see>.
        /// </summary>
        int ICollection<KeyValuePair<string, string>>.Count
        {
            get { return dictionary.Count; }
        }

        /// <summary>
        /// Gets a value indicating whether the <see cref="T:System.Collections.Generic.ICollection`1"></see> is read-only.
        /// </summary>
        bool ICollection<KeyValuePair<string, string>>.IsReadOnly
        {
            get { return dictionary.IsReadOnly; }
        }

        /// <summary>
        /// Gets or sets the element with the specified key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>The value for the given key</returns>
        public string this[string key]
        {
            get { return dictionary[key]; }
            set { UpdateWithNotification(key, value); }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Adds an element with the provided key and value to the <see cref="T:System.Collections.Generic.IDictionary`2" />.
        /// </summary>
        /// <param name="key">The object to use as the key of the element to add.</param>
        /// <param name="value">The object to use as the value of the element to add.</param>
        public void Add(string key, string value)
        {
            AddWithNotification(key, value);
        }

        /// <summary>
        /// Determines whether the <see cref="T:System.Collections.Generic.IDictionary`2" /> contains an element with the specified key.
        /// </summary>
        /// <param name="key">The key to locate in the <see cref="T:System.Collections.Generic.IDictionary`2" />.</param>
        /// <returns>
        /// true if the <see cref="T:System.Collections.Generic.IDictionary`2" /> contains an element with the key; otherwise, false.
        /// </returns>
        public bool ContainsKey(string key)
        {
            return dictionary.ContainsKey(key);
        }

        /// <summary>
        /// Removes the element with the specified key from the <see cref="T:System.Collections.Generic.IDictionary`2"></see>.
        /// </summary>
        /// <param name="key">The key of the element to remove.</param>
        /// <returns>
        /// true if the element is successfully removed; otherwise, false.  This method also returns false if <paramref name="key">key</paramref> was not found in the original <see cref="T:System.Collections.Generic.IDictionary`2"></see>.
        /// </returns>
        public bool Remove(string key)
        {
            return RemoveWithNotification(key);
        }

        /// <summary>
        /// Gets the value associated with the specified key.
        /// </summary>
        /// <param name="key">The key whose value to get.</param>
        /// <param name="value">When this method returns, the value associated with the specified key, if the key is found; otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
        /// <returns>
        /// true if the object that implements <see cref="T:System.Collections.Generic.IDictionary`2"></see> contains an element with the specified key; otherwise, false.
        /// </returns>
        public bool TryGetValue(string key, out string value)
        {
            return dictionary.TryGetValue(key, out value);
        }

        /// <summary>
        /// Adds an item to the <see cref="T:System.Collections.Generic.ICollection`1"></see>.
        /// </summary>
        /// <param name="item">The object to add to the <see cref="T:System.Collections.Generic.ICollection`1"></see>.</param>
        void ICollection<KeyValuePair<string, string>>.Add(KeyValuePair<string, string> item)
        {
            AddWithNotification(item);
        }

        /// <summary>
        /// Removes all items from the <see cref="T:System.Collections.Generic.ICollection`1"></see>.
        /// </summary>
        void ICollection<KeyValuePair<string, string>>.Clear()
        {
            dictionary.Clear();

            CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            PropertyChanged(this, new PropertyChangedEventArgs(nameof(ICollection.Count)));
            PropertyChanged(this, new PropertyChangedEventArgs(nameof(Keys)));
            PropertyChanged(this, new PropertyChangedEventArgs(nameof(Values)));
        }

        /// <summary>
        /// Determines whether the <see cref="T:System.Collections.Generic.ICollection`1"></see> contains a specific value.
        /// </summary>
        /// <param name="item">The object to locate in the <see cref="T:System.Collections.Generic.ICollection`1"></see>.</param>
        /// <returns>
        /// true if <paramref name="item">item</paramref> is found in the <see cref="T:System.Collections.Generic.ICollection`1"></see>; otherwise, false.
        /// </returns>
        bool ICollection<KeyValuePair<string, string>>.Contains(KeyValuePair<string, string> item)
        {
            return dictionary.Contains(item);
        }

        /// <summary>
        /// Copies the elements of the <see cref="T:System.Collections.Generic.ICollection`1"></see> to an <see cref="T:System.Array"></see>, starting at a particular <see cref="T:System.Array"></see> index.
        /// </summary>
        /// <param name="array">The one-dimensional <see cref="T:System.Array"></see> that is the destination of the elements copied from <see cref="T:System.Collections.Generic.ICollection`1"></see>. The <see cref="T:System.Array"></see> must have zero-based indexing.</param>
        /// <param name="arrayIndex">The zero-based index in array at which copying begins.</param>
        void ICollection<KeyValuePair<string, string>>.CopyTo(KeyValuePair<string, string>[] array, int arrayIndex)
        {
            dictionary.CopyTo(array, arrayIndex);
        }

        /// <summary>
        /// Removes the first occurrence of a specific object from the <see cref="T:System.Collections.Generic.ICollection`1"></see>.
        /// </summary>
        /// <param name="item">The object to remove from the <see cref="T:System.Collections.Generic.ICollection`1"></see>.</param>
        /// <returns>
        /// true if <paramref name="item">item</paramref> was successfully removed from the <see cref="T:System.Collections.Generic.ICollection`1"></see>; otherwise, false. This method also returns false if <paramref name="item">item</paramref> is not found in the original <see cref="T:System.Collections.Generic.ICollection`1"></see>.
        /// </returns>
        bool ICollection<KeyValuePair<string, string>>.Remove(KeyValuePair<string, string> item)
        {
            return RemoveWithNotification(item.Key);
        }

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>
        /// An enumerator that can be used to iterate through the collection.
        /// </returns>
        IEnumerator<KeyValuePair<string, string>> IEnumerable<KeyValuePair<string, string>>.GetEnumerator()
        {
            return dictionary.GetEnumerator();
        }

        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns>
        /// An <see cref="T:System.Collections.IEnumerator"></see> object that can be used to iterate through the collection.
        /// </returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return dictionary.GetEnumerator();
        }

        /// <summary>
        /// Returns a <see cref="string" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="string" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return string.Join(" ", dictionary.Select(kvp => EntryToString(kvp)).ToArray());
        }

        #endregion

        #region Protected Methods

        /// <summary>
        /// Allows derived classes to raise custom property changed events.
        /// </summary>
        /// <param name="args">The <see cref="PropertyChangedEventArgs"/> instance containing the event data.</param>
        protected void RaisePropertyChanged(PropertyChangedEventArgs args)
        {
            PropertyChanged(this, args);
        }

        #endregion

        #region Private Methods

        private static string EntryToString(KeyValuePair<string, string> kvp)
        {
            return $"{kvp.Key?.Trim().Replace(" ", "-")}=\"{kvp.Value?.Trim().Replace("\"", "\"\"")}\"";
        }

        private void AddWithNotification(KeyValuePair<string, string> item)
        {
            AddWithNotification(item.Key, item.Value);
        }

        private void AddWithNotification(string key, string value)
        {
            dictionary.Add(key, value);

            CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add,
                new KeyValuePair<string, string>(key, value)));
            PropertyChanged(this, new PropertyChangedEventArgs(nameof(ICollection.Count)));
            PropertyChanged(this, new PropertyChangedEventArgs(nameof(Keys)));
            PropertyChanged(this, new PropertyChangedEventArgs(nameof(Values)));
        }

        private bool RemoveWithNotification(string key)
        {
            if (dictionary.TryGetValue(key, out string value) && dictionary.Remove(key))
            {
                CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove,
                    new KeyValuePair<string, string>(key, value)));
                PropertyChanged(this, new PropertyChangedEventArgs(nameof(ICollection.Count)));
                PropertyChanged(this, new PropertyChangedEventArgs(nameof(Keys)));
                PropertyChanged(this, new PropertyChangedEventArgs(nameof(Values)));

                return true;
            }

            return false;
        }

        private void UpdateWithNotification(string key, string value)
        {
            if (dictionary.TryGetValue(key, out string existing))
            {
                dictionary[key] = value;

                CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace,
                    new KeyValuePair<string, string>(key, value),
                    new KeyValuePair<string, string>(key, existing)));
                PropertyChanged(this, new PropertyChangedEventArgs(nameof(Values)));
            }
            else
            {
                AddWithNotification(key, value);
            }
        }

        #endregion
    }
}
