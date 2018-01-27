namespace Unosquare.FFME
{
    using Shared;
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using Unosquare.FFME.Platform;

    public partial class MediaElement
    {
        /// <summary>
        /// Holds the state of the notification properties
        /// </summary>
        private readonly Dictionary<string, object> NotificationPropertyCache
            = new Dictionary<string, object>(PropertyMapper.PropertyMaxCount);

        private ManualResetEvent PropertyUpdatesDone = new ManualResetEvent(true);

        /// <summary>
        /// The property updates worker timer
        /// </summary>
        private GuiTimer PropertyUpdatesWorker = null;

        /// <summary>
        /// Gets or sets a value indicating whether this instance is running property updates.
        /// </summary>
        internal bool IsRunningPropertyUpdates
        {
            get
            {
                return (PropertyUpdatesDone?.IsSet() ?? true) == false;
            }
            set
            {
                if (value)
                    PropertyUpdatesDone?.Reset();
                else
                    PropertyUpdatesDone?.Set();
            }
        }

        private void StartPropertyUpdatesWorker()
        {
            PropertyUpdatesWorker = new GuiTimer(() =>
            {
                if (IsRunningPropertyUpdates) return;

                IsRunningPropertyUpdates = true;
                var notificationProperties = this.DetectNotificationPropertyChanges(NotificationPropertyCache);
                var dependencyProperties = this.DetectDependencyPropertyChanges();

                try
                {
                    // Handling of Notification Properties
                    if (notificationProperties.Length > 0)
                    {
                        foreach (var p in notificationProperties)
                        {
                            RaisePropertyChangedEvent(p);
                        }
                    }

                    var isSeeking = IsSeeking;
                    if (dependencyProperties.Count > 0)
                    {
                        // Write the media engine state property state to the dependency properties
                        foreach (var kvp in dependencyProperties)
                        {
                            if (kvp.Key == PositionProperty && isSeeking)
                                continue;

                            SetValue(kvp.Key, kvp.Value);
                        }

                        // Raise PositionChanged event
                        if (dependencyProperties.ContainsKey(PositionProperty) && isSeeking == false)
                        {
                            RaisePositionChangedEvent((TimeSpan)dependencyProperties[PositionProperty]);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MediaCore.Log(MediaLogMessageType.Error, $"{nameof(PropertyUpdatesWorker)} callabck failed. {ex.GetType()}: {ex.Message}");
                }
                finally
                {
                    IsRunningPropertyUpdates = false;
                }
            });
        }
    }
}
