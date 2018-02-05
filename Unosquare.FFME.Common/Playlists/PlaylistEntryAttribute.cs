namespace Unosquare.FFME.Playlists
{
    using System.ComponentModel;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// A Key-valu pair representing a playlist entry attribute
    /// </summary>
    /// <seealso cref="INotifyPropertyChanged" />
    public class PlaylistEntryAttribute : INotifyPropertyChanged
    {
        private string m_Key = null;
        private string m_Value = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="PlaylistEntryAttribute"/> class.
        /// </summary>
        public PlaylistEntryAttribute()
        {
            // placeholder
        }

        /// <summary>
        /// Occurs when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets or sets the key.
        /// </summary>
        public string Key
        {
            get => m_Key;
            set => SetProperty(ref m_Key, value);
        }

        /// <summary>
        /// Gets or sets the value.
        /// </summary>
        public string Value
        {
            get => m_Value;
            set => SetProperty(ref m_Value, value);
        }

        /// <summary>
        /// Checks if a property already matches a desired value.  Sets the property and
        /// notifies listeners only when necessary.
        /// </summary>
        /// <typeparam name="T">Type of the property.</typeparam>
        /// <param name="storage">Reference to a property with both getter and setter.</param>
        /// <param name="value">Desired value for the property.</param>
        /// <param name="propertyName">Name of the property used to notify listeners.  This
        /// value is optional and can be provided automatically when invoked from compilers that
        /// support CallerMemberName.</param>
        /// <returns>True if the value was changed, false if the existing value matched the
        /// desired value.</returns>
        protected virtual bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(storage, value)) return false;
            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        /// <summary>
        /// Notifies listeners that a property value has changed.
        /// </summary>
        /// <param name="propertyName">Name of the property used to notify listeners.  This
        /// value is optional and can be provided automatically when invoked from compilers
        /// that support <see cref="CallerMemberNameAttribute"/>.</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
