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
        /// The reportable position synchronization lock
        /// </summary>
        private readonly object ReportablePositionLock = new object();

        /// <summary>
        /// The property updates worker timer
        /// </summary>
        private GuiTimer PropertyUpdatesWorker = null;

        /// <summary>
        /// The backing member of the Reportable position
        /// </summary>
        private TimeSpan? m_ReportablePosition = default;

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
                    m_ReportablePosition = TimeSpan.Zero;
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
            var isSeeking = MediaCore?.State?.IsSeeking ?? false;

            // Write the media engine state property state to the dependency properties
            foreach (var kvp in dependencyProperties)
            {
                // Do not upstream the Source porperty
                // This causes unintended Open/Close commands to be run
                if (kvp.Key == SourceProperty)
                    continue;

                // Do not upstream the Position porperty
                // This causes unintended Seek commands to be run
                if (kvp.Key == PositionProperty)
                    continue;

                SetValue(kvp.Key, kvp.Value);
            }

            // Check if we need to report a new position as commanded by the MediaEngine
            // After we update the position dependency property, clear the reportable position
            // to make way for new updates.
            var notifiedPositionChanged = false;
            if (isSeeking == false)
            {
                lock (ReportablePositionLock)
                {
                    if (m_ReportablePosition != null)
                    {
                        // Upstream the final state
                        Position = m_ReportablePosition.Value;

                        // reset the reportable position to null it picks up the next change
                        m_ReportablePosition = default;

                        notifiedPositionChanged = true;
                    }
                }

                if (notifiedPositionChanged)
                {
                    // Do notify the ramianing duration has changed
                    NotifyPropertyChangedEvent(nameof(RemainingDuration));
                }
            }
        }
    }
}
