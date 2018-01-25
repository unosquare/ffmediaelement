namespace Unosquare.FFME.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Reflection;

    /// <summary>
    /// Defines controller status properties
    /// </summary>
    public sealed class ControllerStatus
    {
        #region Property Backing and Private State

        private static PropertyInfo[] Properties = null;

        private readonly MediaEngine Parent = null;
        private readonly Dictionary<string, object> CurrentState = new Dictionary<string, object>(64);

        private readonly ReadOnlyDictionary<string, string> EmptyDictionary
            = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());

        #endregion

        /// <summary>
        /// Initializes static members of the <see cref="ControllerStatus" /> class.
        /// </summary>
        static ControllerStatus()
        {
            Properties = typeof(ControllerStatus).GetProperties(BindingFlags.Instance | BindingFlags.Public);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ControllerStatus" /> class.
        /// </summary>
        /// <param name="parent">The parent.</param>
        internal ControllerStatus(MediaEngine parent)
        {
            Parent = parent;
        }

        #region Properties

        /// <summary>
        /// Gets or Sets the Source on this MediaElement.
        /// The Source property is the Uri of the media to be played.
        /// </summary>
        public Uri Source { get; internal set; }

        /// <summary>
        /// Specifies the behavior that the media element should have when it
        /// is loaded. The default behavior is that it is under manual control
        /// (i.e. the caller should call methods such as Play in order to play
        /// the media). If a source is set, then the default behavior changes to
        /// to be playing the media. If a source is set and a loaded behavior is
        /// also set, then the loaded behavior takes control.
        /// </summary>
        public MediaEngineState LoadedBehavior { get; set; }

        /// <summary>
        /// Specifies how the underlying media should behave when
        /// it has ended. The default behavior is to Close the media.
        /// </summary>
        public MediaEngineState UnloadedBehavior { get; set; }

        /// <summary>
        /// Gets or Sets the SpeedRatio property of the media.
        /// </summary>
        public double SpeedRatio { get; set; }

        /// <summary>
        /// Gets/Sets the Volume property on the MediaElement.
        /// Note: Valid values are from 0 to 1
        /// </summary>
        public double Volume { get; set; }

        /// <summary>
        /// Gets/Sets the Balance property on the MediaElement.
        /// </summary>
        public double Balance { get; set; }

        /// <summary>
        /// Gets/Sets the IsMuted property on the MediaElement.
        /// </summary>
        public bool IsMuted { get; set; }

        /// <summary>
        /// Gets or sets a value that indicates whether the MediaElement will update frames
        /// for seek operations while paused. This is a dependency property.
        /// </summary>
        public bool ScrubbingEnabled { get; set; }

        /// <summary>
        /// Gets or Sets the Position property on the MediaElement.
        /// </summary>
        public TimeSpan Position { get; internal set; }

        #endregion

        /// <summary>
        /// Compiles the state into the target dictionary of property names and property values
        /// </summary>
        /// <param name="target">The target.</param>
        public void TakeSnapshotInto(Dictionary<string, object> target)
        {
            foreach (var p in Properties)
                target[p.Name] = p.GetValue(this);
        }

        /// <summary>
        /// Contrasts the specified target with the current state.
        /// It leaves the target with only the properties that are different from the current state.
        /// </summary>
        /// <param name="target">The target.</param>
        public void ContrastInto(Dictionary<string, object> target)
        {
            TakeSnapshotInto(CurrentState);
            if (CurrentState.Count != target.Count)
                throw new KeyNotFoundException($"{nameof(target)} must contain a complete set of keys.");

            foreach (var kvp in CurrentState)
            {
                if (target[kvp.Key] == kvp.Value)
                    target.Remove(kvp.Key);
            }
        }
    }
}
