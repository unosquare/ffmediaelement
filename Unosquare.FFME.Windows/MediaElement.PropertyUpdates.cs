namespace Unosquare.FFME
{
    using Shared;
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using Unosquare.FFME.Platform;

    /*
     * This file contains the Property Updates Worker.
     * It is a Dispatcher Timer that fires every few milliseconds and copies the
     * MediaEngine State properties into the MediaElement's properties.
     */

    public partial class MediaElement
    {
        /// <summary>
        /// Holds the state of the notification properties
        /// </summary>
        private readonly Dictionary<string, object> NotificationPropertyCache
            = new Dictionary<string, object>(PropertyMapper.PropertyMaxCount);

        /// <summary>
        /// The property updates worker timer
        /// </summary>
        private GuiTimer PropertyUpdatesWorker = null;

        /// <summary>
        /// Starts the property updates worker.
        /// </summary>
        /// <exception cref="KeyNotFoundException">MediaElement does not have minimum set of MediaProperties</exception>
        private void StartPropertyUpdatesWorker()
        {
            // Check that we are not already started
            if (PropertyUpdatesWorker != null)
            {
                throw new InvalidOperationException($"{nameof(PropertyUpdatesWorker)} has to be null " +
                    $"before calling {nameof(StartPropertyUpdatesWorker)}");
            }

            // Check that all properties map back to the media state
            if (PropertyMapper.MissingPropertyMappings.Count > 0)
            {
                throw new KeyNotFoundException($"{nameof(MediaElement)} is missing properties exposed by {nameof(MediaEngineState)}. " +
                    $"Missing properties are: {string.Join(", ", PropertyMapper.MissingPropertyMappings)}. " +
                    $"Please add these properties to the {nameof(MediaElement)} class.");
            }

            // Properties Worker Logic
            PropertyUpdatesWorker = new GuiTimer(() =>
            {
                UpdateNotificationProperties();
                UpdateDependencyProperties();
            }, HandledAsynchronousDispose);
        }

        /// <summary>
        /// Handles the asynchronous dispose of the underlying Media Engine.
        /// </summary>
        private void HandledAsynchronousDispose()
        {
            // Dispose outside of the current thread to avoid deadlocks
            ThreadPool.QueueUserWorkItem((s) =>
            {
                MediaCore.Dispose();

                // Notify the one last state
                GuiContext.Current.EnqueueInvoke(() =>
                {
                    UpdateNotificationProperties();
                    UpdateDependencyProperties();
                });
            });
        }

        /// <summary>
        /// Updates the notification properties.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateNotificationProperties()
        {
            // Detect changes
            var notificationProperties = this.DetectNotificationPropertyChanges(NotificationPropertyCache);

            // Handling of Notification Properties
            foreach (var p in notificationProperties)
            {
                // Notify the changed properties
                NotifyPropertyChangedEvent(p);
            }
        }

        /// <summary>
        /// Updates the dependency properties.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateDependencyProperties()
        {
            // Detect Notification and Dependency property changes
            var dependencyProperties = this.DetectDependencyPropertyChanges();

            // Write the media engine state property state to the dependency properties
            foreach (var kvp in dependencyProperties)
            {
                // Do not upstream the Source porperty
                // This causes unintended Open/Close commands to be run
                if (kvp.Key == SourceProperty)
                    continue;

                SetValue(kvp.Key, kvp.Value);
            }

            if (dependencyProperties.ContainsKey(PositionProperty))
                NotifyPropertyChangedEvent(nameof(RemainingDuration));
        }
    }
}
