namespace Unosquare.FFME
{
    using Core;
    using Shared;
    using System;

    public partial class MediaEngine
    {
        private Uri source = null;
        private MediaEngineState loadedBehavior = MediaEngineState.Play;
        private double speedRatio = Defaults.DefaultSpeedRatio;
        private MediaEngineState unloadedBehavior = MediaEngineState.Close;
        private double volume = Defaults.DefaultVolume;
        private double balance = Defaults.DefaultBalance;
        private bool isMuted = false;
        private bool scrubbingEnabled = true;
        private TimeSpan position = TimeSpan.Zero;

        #region Dependency Property CLR Accessors

        /// <summary>
        /// Gets/Sets the Source on this MediaElement. 
        /// The Source property is the Uri of the media to be played.
        /// </summary> 
        public Uri Source
        {
            get => source;
            set => SetProperty(ref source, value);
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
            get => loadedBehavior;
            set => SetProperty(ref loadedBehavior, value);
        }

        /// <summary>
        /// Gets/Sets the SpeedRatio property on the MediaElement. 
        /// </summary> 
        public double SpeedRatio
        {
            get => speedRatio;
            set => SetProperty(ref speedRatio, value);
        }

        /// <summary>
        /// Specifies how the underlying media should behave when 
        /// it has ended. The default behavior is to Close the media.
        /// </summary> 
        public MediaEngineState UnloadedBehavior
        {
            get => unloadedBehavior;
            set => SetProperty(ref unloadedBehavior, value);
        }

        /// <summary>
        /// Gets/Sets the Volume property on the MediaElement.
        /// Note: Valid values are from 0 to 1
        /// </summary>
        public double Volume
        {
            get => volume;
            set => SetProperty(ref volume, value);
        }

        /// <summary>
        /// Gets/Sets the Balance property on the MediaElement. 
        /// </summary> 
        public double Balance
        {
            get => balance;
            set => SetProperty(ref balance, value);
        }

        /// <summary>
        /// Gets/Sets the IsMuted property on the MediaElement.
        /// </summary> 
        public bool IsMuted
        {
            get => isMuted;
            set => SetProperty(ref isMuted, value);
        }

        /// <summary>
        /// Gets or sets a value that indicates whether the MediaElement will update frames 
        /// for seek operations while paused. This is a dependency property.
        /// </summary>
        public bool ScrubbingEnabled
        {
            get => scrubbingEnabled;
            set => SetProperty(ref scrubbingEnabled, value);
        }

        /// <summary>
        /// Gets/Sets the Position property on the MediaElement. 
        /// </summary> 
        public TimeSpan Position
        {
            get => position;
            set => SetProperty(ref position, value);
        }

        #endregion

    }
}
