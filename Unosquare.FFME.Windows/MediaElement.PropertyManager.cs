namespace Unosquare.FFME
{
    using Primitives;
    using Shared;
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using Unosquare.FFME.Platform;

    public partial class MediaElement
    {
        private readonly Dictionary<string, object> MediaStatusCache = new Dictionary<string, object>();

        /// <summary>
        /// When position is being set from within this control, this field will
        /// be set to true. This is useful to detect if the user is setting the position
        /// or if the Position property is being driven from within
        /// </summary>
        private AtomicBoolean m_IsRunningPropertyUdates = new AtomicBoolean(false);

        /// <summary>
        /// The property updates worker timer
        /// </summary>
        private Timer PropertyUpdatesWorker = null;

        /// <summary>
        /// Gets or sets a value indicating whether this instance is running property updates.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is running property updates; otherwise, <c>false</c>.
        /// </value>
        public bool IsRunningPropertyUpdates
        {
            get { return m_IsRunningPropertyUdates.Value; }
            set { m_IsRunningPropertyUdates.Value = value; }
        }

        private void StartPropertyUpdatesWorker()
        {
            // TODO: Maybe make the timer a DispatcherTimer otherwise a Windows Timer if available?
            MediaCore.State.TakeSnapshotInto(MediaStatusCache);

            PropertyUpdatesWorker = new Timer((s) =>
            {
                if (IsRunningPropertyUpdates) return;

                IsRunningPropertyUpdates = true;
                MediaCore.State.ContrastInto(MediaStatusCache);

                try
                {
                    GuiContext.Current.Invoke(() =>
                    {
                        // Notify Media Properties
                        foreach (var p in MediaStatusCache)
                        {
                            RaisePropertyChangedEvent(p.Key);
                            if (p.Key.Equals(nameof(Position)))
                                RaisePositionChangedEvent(MediaCore.State.Position);
                        }
                    });
                }
                catch (Exception ex)
                {
                    MediaCore.Log(MediaLogMessageType.Error, $"{nameof(PropertyUpdatesWorker)} callabck failed. {ex.GetType()}: {ex.Message}");
                }
                finally
                {
                    MediaCore.State.TakeSnapshotInto(MediaStatusCache);
                    IsRunningPropertyUpdates = false;
                }
            },
            null,
            0,
            (int)Constants.Interval.MediumPriority.TotalMilliseconds);
        }
    }
}
