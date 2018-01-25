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
        private readonly Dictionary<string, object> ControllerStatusCache = new Dictionary<string, object>();

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
            MediaCore.Media.TakeSnapshotInto(MediaStatusCache);
            MediaCore.Controller.TakeSnapshotInto(ControllerStatusCache);

            PropertyUpdatesWorker = new Timer((s) =>
            {
                if (IsRunningPropertyUpdates) return;

                IsRunningPropertyUpdates = true;
                MediaCore.Media.ContrastInto(MediaStatusCache);
                MediaCore.Controller.ContrastInto(ControllerStatusCache);

                try
                {
                    GuiContext.Current.Invoke(() =>
                    {
                        // Notify Media Properties
                        foreach (var p in MediaStatusCache)
                            RaisePropertyChangedEvent(p.Key);

                        // Notify Controller Properties
                        foreach (var p in ControllerStatusCache)
                        {
                            RaisePropertyChangedEvent(p.Key);
                            if (p.Key.Equals(nameof(Position)))
                                RaisePositionChangedEvent(MediaCore.Controller.Position);
                        }
                    });
                }
                catch (Exception ex)
                {
                    MediaCore.Log(MediaLogMessageType.Error, $"{nameof(PropertyUpdatesWorker)} callabck failed. {ex.GetType()}: {ex.Message}");
                }
                finally
                {
                    MediaCore.Media.TakeSnapshotInto(MediaStatusCache);
                    MediaCore.Controller.TakeSnapshotInto(ControllerStatusCache);
                    IsRunningPropertyUpdates = false;
                }
            },
            null,
            0,
            (int)Constants.Interval.MediumPriority.TotalMilliseconds);
        }
    }
}
