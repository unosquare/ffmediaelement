namespace Unosquare.FFME
{
    using Shared;
    using System;
    using System.Runtime.CompilerServices;

    public partial class MediaEngine
    {
        private Uri ControllerSource = null;
        private MediaEngineState ControllerLoadedBehavior = MediaEngineState.Play;
        private double ControllerSpeedRatio = Constants.Controller.DefaultSpeedRatio;
        private MediaEngineState ControllerUnloadedBehavior = MediaEngineState.Close;
        private double ControllerVolume = Constants.Controller.DefaultVolume;
        private double ControllerBalance = Constants.Controller.DefaultBalance;
        private bool ControllerIsMuted = false;
        private bool ControllerScrubbingEnabled = true;
        private TimeSpan ControllerPosition = TimeSpan.Zero;

        #region Property CLR Accessors

        /// <summary>
        /// Gets or Sets the Source on this MediaElement.
        /// The Source property is the Uri of the media to be played.
        /// </summary>
        public Uri Source
        {
            get => ControllerSource;
            set => SetProperty(ref ControllerSource, value);
        }

        /// <summary>
        /// Specifies the behavior that the media element should have when it
        /// is loaded. The default behavior is that it is under manual control
        /// (i.e. the caller should call methods such as Play in order to play
        /// the media). If a source is set, then the default behavior changes to
        /// to be playing the media. If a source is set and a loaded behavior is
        /// also set, then the loaded behavior takes control.
        /// </summary>
        public MediaEngineState LoadedBehavior
        {
            get => ControllerLoadedBehavior;
            set => SetProperty(ref ControllerLoadedBehavior, value);
        }

        /// <summary>
        /// Specifies how the underlying media should behave when
        /// it has ended. The default behavior is to Close the media.
        /// </summary>
        public MediaEngineState UnloadedBehavior
        {
            get => ControllerUnloadedBehavior;
            set => SetProperty(ref ControllerUnloadedBehavior, value);
        }

        /// <summary>
        /// Gets or Sets the SpeedRatio property of the media.
        /// </summary>
        public double SpeedRatio
        {
            get => ControllerSpeedRatio;
            set => SetProperty(ref ControllerSpeedRatio, value);
        }

        /// <summary>
        /// Gets/Sets the Volume property on the MediaElement.
        /// Note: Valid values are from 0 to 1
        /// </summary>
        public double Volume
        {
            get => ControllerVolume;
            set => SetProperty(ref ControllerVolume, value);
        }

        /// <summary>
        /// Gets/Sets the Balance property on the MediaElement.
        /// </summary>
        public double Balance
        {
            get => ControllerBalance;
            set => SetProperty(ref ControllerBalance, value);
        }

        /// <summary>
        /// Gets/Sets the IsMuted property on the MediaElement.
        /// </summary>
        public bool IsMuted
        {
            get => ControllerIsMuted;
            set => SetProperty(ref ControllerIsMuted, value);
        }

        /// <summary>
        /// Gets or sets a value that indicates whether the MediaElement will update frames
        /// for seek operations while paused. This is a dependency property.
        /// </summary>
        public bool ScrubbingEnabled
        {
            get => ControllerScrubbingEnabled;
            set => SetProperty(ref ControllerScrubbingEnabled, value);
        }

        /// <summary>
        /// Gets or Sets the Position property on the MediaElement.
        /// </summary>
        public TimeSpan Position
        {
            get => ControllerPosition;
            set => SetProperty(ref ControllerPosition, value);
        }

        /// <summary>
        /// Gets the internal real time clock position.
        /// This is different from the regular property as this is the immediate value
        /// (i.e. might not yet be applied)
        /// </summary>
        public TimeSpan RealTimeClockPosition => Clock.Position;

        #endregion

        /// <summary>
        /// Resets the controller properies.
        /// </summary>
        internal void ResetControllerProperties()
        {
            Volume = Constants.Controller.DefaultVolume;
            Balance = Constants.Controller.DefaultBalance;
            SpeedRatio = Constants.Controller.DefaultSpeedRatio;
            IsMuted = false;
            Position = TimeSpan.Zero;
            Status.VideoSmtpeTimecode = string.Empty;
            Status.VideoHardwareDecoder = string.Empty;
            Status.HasMediaEnded = false;
        }

        /// <summary>
        /// Checks if a property already matches a desired value.  Sets the property and
        /// enqueues a property notification only when necessary.
        /// </summary>
        /// <typeparam name="T">Type of the property.</typeparam>
        /// <param name="storage">Reference to a property with both getter and setter.</param>
        /// <param name="value">Desired value for the property.</param>
        /// <param name="propertyName">Name of the property used to notify listeners.  This
        /// value is optional and can be provided automatically when invoked from compilers that
        /// support CallerMemberName.</param>
        /// <returns>True if the value was changed, false if the existing value matched the
        /// desired value.</returns>
        private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(storage, value))
                return false;

            storage = value;
            Connector?.OnPropertiesChanged(this, new[] { propertyName });
            return true;
        }
    }
}
