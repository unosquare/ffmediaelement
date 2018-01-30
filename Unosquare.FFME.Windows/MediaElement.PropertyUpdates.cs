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
        /// The reportable position synchronization lock
        /// </summary>
        private readonly object ReportablePositionLock = new object();

        /// <summary>
        /// The property updates done event
        /// </summary>
        private ManualResetEvent PropertyUpdatesDone = new ManualResetEvent(true);

        /// <summary>
        /// The property updates worker timer
        /// </summary>
        private GuiTimer PropertyUpdatesWorker = null;

        /// <summary>
        /// The backing member of the Reportable position
        /// </summary>
        private TimeSpan? m_ReportablePosition = default(TimeSpan?);

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
        /// The media engine position to report.
        /// </summary>
        internal TimeSpan? ReportablePosition
        {
            set { lock (ReportablePositionLock) m_ReportablePosition = value; }
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

                // Detect Notification and Dependency property changes
                var notificationProperties = this.DetectNotificationPropertyChanges(NotificationPropertyCache);
                var dependencyProperties = this.DetectDependencyPropertyChanges();

                #endregion

                #region 2. Change Notification Triggers

                // Handling of Notification Properties
                foreach (var p in notificationProperties)
                {
                    // Notify the changed properties
                    RaisePropertyChangedEvent(p);
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

                    // Do not upstream the Source porperty
                    // This causes unintended Seek commands to be run
                    if (kvp.Key == PositionProperty)
                        continue;

                    SetValue(kvp.Key, kvp.Value);
                }

                #endregion

                // Check if we need to report a new position as commanded byt the MEdiaEngine
                // After we update the position dependency property, clear the reportable position
                // to make way for new updates.
                lock (ReportablePositionLock)
                {
                    if (m_ReportablePosition != null)
                    {
                        Position = m_ReportablePosition.Value;
                        m_ReportablePosition = default(TimeSpan?);
                    }
                }

                // Always signal that we are no longer running updates
                IsRunningPropertyUpdates = false;
            });
        }
    }
}
