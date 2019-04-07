namespace Unosquare.FFME
{
    using Common;
    using Platform;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;

    /*
     * This file contains the Property Updates Worker.
     * It is a Dispatcher Timer that fires every few milliseconds and copies the
     * MediaEngine State properties into the MediaElement's properties.
     */

    public partial class MediaElement
    {
        private readonly object PropertyUpdatesLock = new object();

        /// <summary>
        /// Holds the state of the notification properties.
        /// </summary>
        private readonly Dictionary<string, object> NotificationPropertyCache
            = new Dictionary<string, object>(64);

        /// <summary>
        /// The property updates worker timer.
        /// </summary>
        private IGuiTimer PropertyUpdatesWorker;

        /// <summary>
        /// Starts the property updates worker. You will need to call this method in the constructor of
        /// the platform-specific MediaElement implementation to continuously pull values from the media state.
        /// </summary>
        /// <exception cref="KeyNotFoundException">MediaElement does not have minimum set of MediaProperties.</exception>
        private void StartPropertyUpdatesWorker()
        {
            lock (PropertyUpdatesLock)
            {
                // Check that we are not already started
                if (PropertyUpdatesWorker != null)
                    return;

                // Check that all properties map back to the media state
                if (PropertyMapper.MissingPropertyMappings.Count > 0)
                {
                    throw new KeyNotFoundException($"{nameof(MediaElement)} is missing properties exposed by {nameof(IMediaEngineState)}. " +
                        $"Missing properties are: {string.Join(", ", PropertyMapper.MissingPropertyMappings)}. " +
                        $"Please add these properties to the {nameof(MediaElement)} class.");
                }

                // Properties Worker Logic
                PropertyUpdatesWorker = Library.GuiContext.CreateTimer(Constants.PropertyUpdatesInterval, UpdateStateProperties);
            }
        }

        /// <summary>
        /// Stops the property updates worker. You will need to call this method when the platform-specific
        /// implementation of the control is disposed or removed.
        /// </summary>
        private void StopPropertyUpdatesWorker()
        {
            lock (PropertyUpdatesLock)
            {
                PropertyUpdatesWorker?.Stop();
                PropertyUpdatesWorker = null;
            }

            // Ensure one last update
            UpdateStateProperties();
        }

        /// <summary>
        /// Updates the read-only properties properties coming from the MediaEngine. Call <see cref="UpdateStateProperties"/> instead.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateReadOnlyProperties()
        {
            // Detect changes
            var changedProperties = this.DetectReadOnlyChanges(NotificationPropertyCache);
            var notifyRemainingDuration = false;

            // Handling of Notification Properties
            foreach (var propertyName in changedProperties)
            {
                // Notify the changed properties
                NotifyPropertyChangedEvent(propertyName);

                // Check if we need to notify the remaining duration
                if (propertyName == nameof(NaturalDuration) || propertyName == nameof(IsSeekable))
                    notifyRemainingDuration = true;
            }

            // Always notify a change in remaining duration if natural duration changes
            if (notifyRemainingDuration)
                NotifyPropertyChangedEvent(nameof(RemainingDuration));
        }

        /// <summary>
        /// Updates the properties that are both readable and writable. Call <see cref="UpdateStateProperties"/> instead.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateReadWriteProperties()
        {
            // Detect Notification and Dependency property changes
            var changes = this.DetectReadWriteChanges();

            // Remove the position property updates if we are
            // not allowed to report changes from the engine
            if ((MediaCore?.State.IsSeeking ?? false) && changes.ContainsKey(nameof(Position)))
            {
                changes.Remove(nameof(Position));

                // Only notify remaining duration if we have seekable media.
                if (IsSeekable)
                    NotifyPropertyChangedEvent(nameof(RemainingDuration));
            }

            // Write the media engine state property state to the dependency properties
            foreach (var property in changes.Keys)
            {
                // Do not upstream the Source property
                // This causes unintended Open/Close commands to be run
                if (property == nameof(Source))
                    continue;

                // Update the writeable property value
                PropertyMapper.SetValue(this, property, changes[property]);

                // Send a notification that the property has changed
                NotifyPropertyChangedEvent(property);

                // Update the remaining duration if we have seekable media
                if (property == nameof(Position) && IsSeekable)
                    NotifyPropertyChangedEvent(nameof(RemainingDuration));
            }
        }

        /// <summary>
        /// Updates all of the media state properties.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateStateProperties()
        {
            lock (PropertyUpdatesLock)
            {
                UpdateReadOnlyProperties();
                UpdateReadWriteProperties();
            }
        }
    }
}
