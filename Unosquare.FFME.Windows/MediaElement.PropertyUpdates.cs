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

        /// <summary>
        /// The property updates done event
        /// </summary>
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

        /// <summary>
        /// Starts the property updates worker.
        /// </summary>
        /// <exception cref="KeyNotFoundException">MediaElement does not have minimum set of MediaProperties</exception>
        private void StartPropertyUpdatesWorker()
        {
            if (PropertyMapper.MissingPropertyMappings.Count > 0)
            {
                throw new KeyNotFoundException($"{nameof(MediaElement)} is missing properties exposed by {nameof(MediaEngineState)}. " +
                    $"Missing properties are: {string.Join(", ", PropertyMapper.MissingPropertyMappings)}. Please add these properties to the {nameof(MediaElement)} class.");
            }

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

                    if (dependencyProperties.Count > 0)
                    {
                        // Write the media engine state property state to the dependency properties
                        foreach (var kvp in dependencyProperties)
                        {
                            // Do not set the position property if we are seeking
                            if (kvp.Key == PositionProperty
                                && IsOpen && HasMediaEnded == false
                                && MediaState != System.Windows.Controls.MediaState.Stop
                                && (IsSeeking || IsPlaying == false))
                            {
                                continue;
                            }

                            // Do not upstream the source porperty
                            if (kvp.Key == SourceProperty)
                                continue;

                            SetValue(kvp.Key, kvp.Value);
                        }

                        // Raise PositionChanged event
                        if (dependencyProperties.ContainsKey(PositionProperty) && IsSeeking == false)
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
