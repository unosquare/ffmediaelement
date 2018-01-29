namespace Unosquare.FFME
{
    using Shared;
    using System;
    using System.Collections.Generic;
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
                #region 1. Cycle Setup

                // If there is overlapping (this code is already running), skip this cycle
                if (IsRunningPropertyUpdates) return;

                // Signal a begining of property updates
                IsRunningPropertyUpdates = true;

                // TODO: Maybe direct calls and detection of mediastechanges and position changes
                // must be more immediate and implement them in the Connector as a message

                // Capture the previous MediaStateChanged Event Control
                var raiseMediaStateChanged = false;
                var previousMediaState = default(System.Windows.Controls.MediaState);
                if (NotificationPropertyCache.ContainsKey(nameof(MediaState)))
                    previousMediaState = (System.Windows.Controls.MediaState)NotificationPropertyCache[nameof(MediaState)];

                // Detect Notification and Dependency property changes
                var raisePositionChanged = false;
                var notificationProperties = this.DetectNotificationPropertyChanges(NotificationPropertyCache);
                var dependencyProperties = this.DetectDependencyPropertyChanges();

                #endregion

                #region 2. Change Notification Triggers

                // Handling of Notification Properties
                foreach (var p in notificationProperties)
                {
                    // Notify the changed property
                    RaisePropertyChangedEvent(p);

                    // Determine if we need to raise the MediaStateChangedEvent
                    if (raiseMediaStateChanged == false && p.Equals(nameof(MediaState)))
                        raiseMediaStateChanged = true;
                }

                #endregion

                #region 3. Upstream (Write) Dependency Properties

                // Write the media engine state property state to the dependency properties
                foreach (var kvp in dependencyProperties)
                {
                    // Do not upstream the Source porperty
                    // This causes unintended Open/Close commands to be run
                    if (kvp.Key == SourceProperty)
                        continue;

                    // Process the Position property
                    if (kvp.Key == PositionProperty)
                    {
                        // Check if we need to raise the position changed event (i.e. when we are not seeking)
                        raisePositionChanged = IsSeeking == false;

                        // Check if we need to skip the writing of the Position Dependency Property
                        // Remember writing when not running property updates runs a seek
                        // Also we don't want to skip the updates when we have a change in media state.
                        if (raiseMediaStateChanged == false && (IsSeeking || IsPlaying == false))
                        {
                            continue;
                        }
                    }

                    SetValue(kvp.Key, kvp.Value);
                }

                // Force a position writeback if we did not notifiy it but the media state has changed.
                if (raiseMediaStateChanged)
                {
                    Position = MediaCore.State.Position;
                }

                #endregion

                #region 4. Raise Events and Finish the cycle

                try
                {
                    // Raise PositionChanged event
                    if (raisePositionChanged)
                        RaisePositionChangedEvent(MediaCore.State.Position);

                    // Raise MediaStateChanged Event
                    if (raiseMediaStateChanged)
                        RaiseMediaStateChangedEvent(previousMediaState, MediaState);
                }
                catch (Exception ex)
                {
                    // Log an exception. This should never really happen.
                    MediaCore.Log(MediaLogMessageType.Error, $"{nameof(PropertyUpdatesWorker)} callabck failed. {ex.GetType()}: {ex.Message}");
                }
                finally
                {
                    // Always signal that we are no longer running updates
                    IsRunningPropertyUpdates = false;
                }

                #endregion
            });
        }
    }
}
