namespace Unosquare.FFME.Windows.Sample.Foundation
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;

    /// <summary>
    /// A base class for implementing models that fire notifications when their properties change.
    /// This class is ideal for implementing MVVM driven UIs.
    /// </summary>
    /// <seealso cref="INotifyPropertyChanged" />
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        private readonly ConcurrentDictionary<string, bool> QueuedNotifications = new ConcurrentDictionary<string, bool>();
        private readonly bool UseDeferredNotifications;

        /// <summary>
        /// Initializes a new instance of the <see cref="ViewModelBase"/> class.
        /// </summary>
        protected ViewModelBase()
            : this(false)
        {
            // placeholder
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ViewModelBase"/> class.
        /// </summary>
        /// <param name="useDeferredNotifications">Set to <c>true</c> to use deferred notifications in the background.</param>
        protected ViewModelBase(bool useDeferredNotifications)
        {
            UseDeferredNotifications = useDeferredNotifications;
        }

        /// <summary>
        /// Occurs when a property value changes.
        /// </summary>
        /// <returns>The property changed notification handler</returns>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>Checks if a property already matches a desired value.  Sets the property and
        /// notifies listeners only when necessary.</summary>
        /// <typeparam name="T">Type of the property.</typeparam>
        /// <param name="storage">Reference to a property with both getter and setter.</param>
        /// <param name="value">Desired value for the property.</param>
        /// <param name="propertyName">Name of the property used to notify listeners.  This
        /// value is optional and can be provided automatically when invoked from compilers that
        /// support CallerMemberName.</param>
        /// <param name="notifyAlso">An rray of property names to notify in addition to notifying the changes on the current property name.</param>
        /// <returns>True if the value was changed, false if the existing value matched the
        /// desired value.</returns>
        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = "", string[] notifyAlso = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value))
                return false;

            storage = value;
            NotifyPropertyChanged(propertyName, notifyAlso);
            return true;
        }

        /// <summary>
        /// Notifies one or more properties changed.
        /// </summary>
        /// <param name="propertyNames">The property names.</param>
        protected void NotifyPropertyChanged(params string[] propertyNames) => NotifyPropertyChanged(null, propertyNames);

        /// <summary>
        /// Notifies one or more properties changed.
        /// </summary>
        /// <param name="mainProperty">The main property.</param>
        /// <param name="auxiliaryProperties">The auxiliary properties.</param>
        private void NotifyPropertyChanged(string mainProperty, string[] auxiliaryProperties)
        {
            // Queue property notification
            if (string.IsNullOrWhiteSpace(mainProperty) == false)
                QueuedNotifications[mainProperty] = true;

            // Set the state for notification properties
            if (auxiliaryProperties != null)
            {
                foreach (var property in auxiliaryProperties)
                {
                    if (string.IsNullOrWhiteSpace(property) == false)
                        QueuedNotifications[property] = true;
                }
            }

            // Depending on operation mode, either fire the notifications in the background
            // or fire them immediately
            if (UseDeferredNotifications)
                Task.Run(NotifyQueuedProperties);
            else
                NotifyQueuedProperties();
        }

        /// <summary>
        /// Notifies the queued properties and resets the property name to a non-queued stated.
        /// </summary>
        private void NotifyQueuedProperties()
        {
            // get a snapshot of property names.
            var propertyNames = QueuedNotifications.Keys.ToArray();

            // Iterate through the properties
            foreach (var property in propertyNames)
            {
                // don't notify if we don't have a change
                if (!QueuedNotifications[property]) continue;

                // notify and reset queued state to false
                try { OnPropertyChanged(property); }
                finally { QueuedNotifications[property] = false; }
            }
        }

        /// <summary>
        /// Called when a property changes its backing value.
        /// </summary>
        /// <param name="propertyName">Name of the property.</param>
        private void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName ?? string.Empty));
    }
}
