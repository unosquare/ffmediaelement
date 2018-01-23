namespace Unosquare.FFME
{
    using Rendering;
    using Shared;
    using System;
    using System.ComponentModel;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;

    public partial class MediaElement
    {
        #region Dependency Property Registrations

        /// <summary>
        /// DependencyProperty for FFmpegMediaElement Source property.
        /// </summary>
        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
            nameof(Source),
            typeof(Uri),
            typeof(MediaElement),
            new FrameworkPropertyMetadata(null, AffectsMeasureAndRender, OnSourcePropertyChanged, OnSourcePropertyCoerce));

        /// <summary>
        /// DependencyProperty for Stretch property.
        /// </summary>
        public static readonly DependencyProperty StretchProperty = DependencyProperty.Register(
            nameof(Stretch),
            typeof(Stretch),
            typeof(MediaElement),
            new FrameworkPropertyMetadata(Stretch.Uniform, AffectsMeasureAndRender, OnStretchPropertyChanged));

        /// <summary>
        /// DependencyProperty for StretchDirection property.
        /// </summary>
        public static readonly DependencyProperty StretchDirectionProperty = DependencyProperty.Register(
            nameof(StretchDirection),
            typeof(StretchDirection),
            typeof(MediaElement),
            new FrameworkPropertyMetadata(StretchDirection.Both, AffectsMeasureAndRender, OnStretchDirectionPropertyChanged));

        /// <summary>
        /// The DependencyProperty for the MediaElement.Balance property.
        /// </summary>
        public static readonly DependencyProperty BalanceProperty = DependencyProperty.Register(
            nameof(Balance),
            typeof(double),
            typeof(MediaElement),
            new FrameworkPropertyMetadata(Constants.Controller.DefaultBalance, FrameworkPropertyMetadataOptions.None, new PropertyChangedCallback(BalancePropertyChanged), new CoerceValueCallback(CoerceBalanceProperty)));

        /// <summary>
        /// The DependencyProperty for the MediaElement.IsMuted property.
        /// </summary>
        public static readonly DependencyProperty IsMutedProperty = DependencyProperty.Register(
            nameof(IsMuted),
            typeof(bool),
            typeof(MediaElement),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.None, new PropertyChangedCallback(IsMutedPropertyChanged), new CoerceValueCallback(CoerceIsMutedProperty)));

        /// <summary>
        /// The DependencyProperty for the MediaElement.SpeedRatio property.
        /// </summary>
        public static readonly DependencyProperty SpeedRatioProperty = DependencyProperty.Register(
            nameof(SpeedRatio),
            typeof(double),
            typeof(MediaElement),
            new FrameworkPropertyMetadata(Constants.Controller.DefaultSpeedRatio, AffectsMeasureAndRender, new PropertyChangedCallback(SpeedRatioPropertyChanged), new CoerceValueCallback(CoerceSpeedRatioProperty)));

        /// <summary>
        /// The DependencyProperty for the MediaElement.Volume property.
        /// </summary>
        public static readonly DependencyProperty VolumeProperty = DependencyProperty.Register(
            nameof(Volume),
            typeof(double),
            typeof(MediaElement),
            new FrameworkPropertyMetadata(Constants.Controller.DefaultVolume, FrameworkPropertyMetadataOptions.None, new PropertyChangedCallback(VolumePropertyChanged), new CoerceValueCallback(CoerceVolumeProperty)));

        /// <summary>
        /// The DependencyProperty for the MediaElement.ScrubbingEnabled property.
        /// </summary>
        public static readonly DependencyProperty ScrubbingEnabledProperty = DependencyProperty.Register(
            nameof(ScrubbingEnabled),
            typeof(bool),
            typeof(MediaElement),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.None, new PropertyChangedCallback(ScrubbingEnabledPropertyChanged)));

        /// <summary>
        /// The DependencyProperty for the MediaElement.UnloadedBehavior property.
        /// TODO: Currently this property has no effect. Needs implementation.
        /// </summary>
        public static readonly DependencyProperty UnloadedBehaviorProperty = DependencyProperty.Register(
            nameof(UnloadedBehavior),
            typeof(MediaState),
            typeof(MediaElement),
            new FrameworkPropertyMetadata(MediaState.Close, FrameworkPropertyMetadataOptions.None, new PropertyChangedCallback(UnloadedBehaviorPropertyChanged)));

        /// <summary>
        /// The DependencyProperty for the MediaElement.LoadedBehavior property.
        /// </summary>
        public static readonly DependencyProperty LoadedBehaviorProperty = DependencyProperty.Register(
            nameof(LoadedBehavior),
            typeof(MediaState),
            typeof(MediaElement),
            new FrameworkPropertyMetadata(MediaState.Play, FrameworkPropertyMetadataOptions.None, new PropertyChangedCallback(LoadedBehaviorPropertyChanged)));

        /// <summary>
        /// The DependencyProperty for the MediaElement.Position property.
        /// </summary>
        public static readonly DependencyProperty PositionProperty = DependencyProperty.Register(
            nameof(Position),
            typeof(TimeSpan),
            typeof(MediaElement),
            new FrameworkPropertyMetadata(TimeSpan.Zero, AffectsMeasureAndRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, new PropertyChangedCallback(PositionPropertyChanged), new CoerceValueCallback(CoercePositionProperty)));

        #endregion

        #region Dependency Property CLR Accessors

        /// <summary>
        /// Gets/Sets the Source on this MediaElement.
        /// The Source property is the Uri of the media to be played.
        /// </summary>
        [Category(nameof(MediaElement))]
        [Description("The URL to load the media from. Set it to null in order to close the currently open media.")]
        public Uri Source
        {
            get { return GetValue(SourceProperty) as Uri; }
            set { SetValue(SourceProperty, value); }
        }

        /// <summary>
        /// Gets/Sets the Stretch on this MediaElement.
        /// The Stretch property determines how large the MediaElement will be drawn.
        /// </summary>
        public Stretch Stretch
        {
            get { return (Stretch)GetValue(StretchProperty); }
            set { SetValue(StretchProperty, value); }
        }

        /// <summary>
        /// Gets/Sets the stretch direction of the Viewbox, which determines the restrictions on
        /// scaling that are applied to the content inside the Viewbox.  For instance, this property
        /// can be used to prevent the content from being smaller than its native size or larger than
        /// its native size.
        /// </summary>
        public StretchDirection StretchDirection
        {
            get { return (StretchDirection)GetValue(StretchDirectionProperty); }
            set { SetValue(StretchDirectionProperty, value); }
        }

        /// <summary>
        /// Specifies the behavior that the media element should have when it
        /// is loaded. The default behavior is that it is under manual control
        /// (i.e. the caller should call methods such as Play in order to play
        /// the media). If a source is set, then the default behavior changes to
        /// to be playing the media. If a source is set and a loaded behavior is
        /// also set, then the loaded behavior takes control.
        /// </summary>
        [Category(nameof(MediaElement))]
        [Description("Specifies how the underlying media should behave when it has loaded. The default behavior is to Play the media.")]
        public MediaState LoadedBehavior
        {
            get { return (MediaState)GetValue(LoadedBehaviorProperty); }
            set { SetValue(LoadedBehaviorProperty, value); }
        }

        /// <summary>
        /// Gets/Sets the SpeedRatio property on the MediaElement.
        /// </summary>
        [Category(nameof(MediaElement))]
        [Description("Specifies how quickly or how slowly the media should be rendered. 1.0 is normal speed. Value must be greater then or equal to 0.0")]
        public double SpeedRatio
        {
            get { return (double)GetValue(SpeedRatioProperty); }
            set { SetValue(SpeedRatioProperty, value); }
        }

        /// <summary>
        /// Specifies how the underlying media should behave when
        /// it has ended. The default behavior is to Close the media.
        /// </summary>
        [Category(nameof(MediaElement))]
        [Description("Specifies how the underlying media should behave when it has ended. The default behavior is to Close the media.")]
        public MediaState UnloadedBehavior
        {
            get { return (MediaState)GetValue(UnloadedBehaviorProperty); }
            set { SetValue(UnloadedBehaviorProperty, value); }
        }

        /// <summary>
        /// Gets/Sets the Volume property on the MediaElement.
        /// Note: Valid values are from 0 to 1
        /// </summary>
        [Category(nameof(MediaElement))]
        [Description("The playback volume. Ranges from 0.0 to 1.0")]
        public double Volume
        {
            get { return (double)GetValue(VolumeProperty); }
            set { SetValue(VolumeProperty, value); }
        }

        /// <summary>
        /// Gets/Sets the Balance property on the MediaElement.
        /// </summary>
        [Category(nameof(MediaElement))]
        [Description("The audio volume for left and right audio channels. Valid ranges are -1.0 to 1.0")]
        public double Balance
        {
            get { return (double)GetValue(BalanceProperty); }
            set { SetValue(BalanceProperty, value); }
        }

        /// <summary>
        /// Gets/Sets the IsMuted property on the MediaElement.
        /// </summary>
        [Category(nameof(MediaElement))]
        [Description("Gets or sets whether audio samples should be rendered.")]
        public bool IsMuted
        {
            get { return (bool)GetValue(IsMutedProperty); }
            set { SetValue(IsMutedProperty, value); }
        }

        /// <summary>
        /// Gets or sets a value that indicates whether the MediaElement will update frames
        /// for seek operations while paused. This is a dependency property.
        /// </summary>
        [Category(nameof(MediaElement))]
        [Description("Gets or sets a value that indicates whether the MediaElement will update frames for seek operations while paused.")]
        public bool ScrubbingEnabled
        {
            get { return (bool)GetValue(ScrubbingEnabledProperty); }
            set { SetValue(ScrubbingEnabledProperty, value); }
        }

        /// <summary>
        /// Gets/Sets the Position property on the MediaElement.
        /// </summary>
        [Category(nameof(MediaElement))]
        [Description("Specifies the position of the underlying media. Set this property to seek though the media stream.")]
        public TimeSpan Position
        {
            get { return (TimeSpan)GetValue(PositionProperty); }
            set { SetValue(PositionProperty, value); }
        }

        #endregion

        #region Value Change Handling Callbacks

        private static object OnSourcePropertyCoerce(DependencyObject dependencyObject, object baseValue)
        {
            // TODO: Not sure why there was coersion in previous version...
            var element = dependencyObject as MediaElement;
            if (element == null) return null;

            return baseValue;
        }

        private static async void OnSourcePropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            var element = dependencyObject as MediaElement;
            if (element == null) return;
            if (element.MediaCore == null || element.MediaCore.IsDisposed) return;

            var uri = e.NewValue as Uri;
            await element.MediaCore.Open(uri);
        }

        private static void OnStretchPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            var element = dependencyObject as MediaElement;
            if (element == null) return;

            element.VideoView.Stretch = (Stretch)e.NewValue;
        }

        private static void OnStretchDirectionPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            var element = dependencyObject as MediaElement;
            if (element == null) return;

            element.VideoView.StretchDirection = (StretchDirection)e.NewValue;
        }

        private static object CoerceVolumeProperty(DependencyObject d, object value)
        {
            var element = d as MediaElement;
            if (element == null) return Constants.Controller.DefaultVolume;
            if (element.HasAudio == false) return Constants.Controller.DefaultVolume;

            var targetValue = (double)value;
            if (targetValue < Constants.Controller.MinVolume) targetValue = Constants.Controller.MinVolume;
            if (targetValue > Constants.Controller.MaxVolume) targetValue = Constants.Controller.MaxVolume;

            var audioRenderer = element.MediaCore?.RetrieveRenderer(MediaType.Audio) as AudioRenderer;
            return audioRenderer == null ? Constants.Controller.DefaultVolume : targetValue;
        }

        private static void VolumePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var element = d as MediaElement;
            if (element == null) return;
            if (element.HasAudio == false) return;
            if (element.MediaCore == null || element.MediaCore.IsDisposed) return;

            if (element.MediaCore.RetrieveRenderer(MediaType.Audio) is AudioRenderer audioRenderer)
                audioRenderer.Volume = (double)e.NewValue;

            element.MediaCore.Volume = (double)e.NewValue;
        }

        private static object CoerceBalanceProperty(DependencyObject d, object value)
        {
            var element = d as MediaElement;
            if (element == null) return Constants.Controller.DefaultBalance;
            if (element.HasAudio == false) return Constants.Controller.DefaultBalance;
            if (element.MediaCore == null || element.MediaCore.IsDisposed) return Constants.Controller.DefaultBalance;

            var targetValue = (double)value;
            if (targetValue < Constants.Controller.MinBalance) targetValue = Constants.Controller.MinBalance;
            if (targetValue > Constants.Controller.MaxBalance) targetValue = Constants.Controller.MaxBalance;

            var audioRenderer = element.MediaCore.RetrieveRenderer(MediaType.Audio) as AudioRenderer;
            return audioRenderer == null ? Constants.Controller.DefaultBalance : targetValue;
        }

        private static void BalancePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var element = d as MediaElement;
            if (element == null) return;
            if (element.HasAudio == false) return;
            if (element.MediaCore == null || element.MediaCore.IsDisposed) return;

            if (element.MediaCore.RetrieveRenderer(MediaType.Audio) is AudioRenderer audioRenderer)
                audioRenderer.Balance = (double)e.NewValue;

            element.MediaCore.Balance = (double)e.NewValue;
        }

        private static object CoerceIsMutedProperty(DependencyObject d, object value)
        {
            var element = d as MediaElement;
            if (element == null) return false;
            if (element.HasAudio == false) return false;
            if (element.MediaCore == null || element.MediaCore.IsDisposed) return false;

            var audioRenderer = element.MediaCore.RetrieveRenderer(MediaType.Audio) as AudioRenderer;
            return audioRenderer == null ? false : (bool)value;
        }

        private static void IsMutedPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var element = d as MediaElement;
            if (element == null) return;
            if (element.HasAudio == false) return;
            if (element.MediaCore == null || element.MediaCore.IsDisposed) return;

            if (element.MediaCore.RetrieveRenderer(MediaType.Audio) is AudioRenderer audioRenderer)
                audioRenderer.IsMuted = (bool)e.NewValue;

            element.MediaCore.IsMuted = (bool)e.NewValue;
        }

        private static void ScrubbingEnabledPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var element = d as MediaElement;
            if (element == null) return;
            if (element.MediaCore == null || element.MediaCore.IsDisposed) return;

            element.MediaCore.ScrubbingEnabled = (bool)e.NewValue;
        }

        private static void UnloadedBehaviorPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var element = d as MediaElement;
            if (element == null) return;
            if (element.MediaCore == null || element.MediaCore.IsDisposed) return;

            element.MediaCore.UnloadedBehavior = (MediaEngineState)e.NewValue;
        }

        private static void LoadedBehaviorPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var element = d as MediaElement;
            if (element == null) return;
            if (element.MediaCore == null || element.MediaCore.IsDisposed) return;

            element.MediaCore.LoadedBehavior = (MediaEngineState)e.NewValue;
        }

        private static void PositionPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var element = d as MediaElement;
            if (element == null) return;
            if (element.MediaCore == null || element.MediaCore.IsDisposed) return;
            if (element.IsPositionUpdating || element.MediaCore.IsSeekable == false) return;

            element.MediaCore.Seek((TimeSpan)e.NewValue);
        }

        private static object CoercePositionProperty(DependencyObject d, object value)
        {
            var element = d as MediaElement;
            if (element == null) return TimeSpan.Zero;
            if (element.MediaCore == null || element.MediaCore.IsDisposed) return TimeSpan.Zero;
            if (element.MediaCore.IsSeekable == false) return element.MediaCore.Position;

            var minPosition = element.MediaCore?.MediaInfo?.StartTime ?? TimeSpan.Zero;
            var maxPosition = minPosition + (element.MediaCore?.MediaInfo?.Duration ?? TimeSpan.Zero);
            var val = (TimeSpan)value;

            if (val < minPosition) return minPosition;
            if (val > maxPosition) return maxPosition;
            return val;
        }

        private static object CoerceSpeedRatioProperty(DependencyObject d, object value)
        {
            var element = d as MediaElement;
            if (element == null) return Constants.Controller.DefaultSpeedRatio;
            if (element.MediaCore == null || element.MediaCore.IsDisposed) return Constants.Controller.DefaultSpeedRatio;
            if ((element.MediaCore?.IsSeekable ?? false) == false) return Constants.Controller.DefaultSpeedRatio;

            var targetValue = (double)value;
            if (targetValue < Constants.Controller.MinSpeedRatio) return Constants.Controller.MinSpeedRatio;
            if (targetValue > Constants.Controller.MaxSpeedRatio) return Constants.Controller.MaxSpeedRatio;

            return targetValue;
        }

        private static void SpeedRatioPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var element = d as MediaElement;
            if (element == null) return;
            if (element.MediaCore == null || element.MediaCore.IsDisposed) return;

            var targetSpeedRatio = (double)e.NewValue;
            element.MediaCore.SetSpeedRatio(targetSpeedRatio);
        }

        #endregion

    }
}
