#pragma warning disable SA1201 // Elements must appear in the correct order
#pragma warning disable SA1117 // Parameters must be on same line or separate lines
namespace Unosquare.FFME
{
    using ClosedCaptions;
    using Shared;
    using System;
    using System.ComponentModel;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;

    public partial class MediaElement
    {
        #region Volume Dependency Property

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
        /// The DependencyProperty for the MediaElement.Volume property.
        /// </summary>
        public static readonly DependencyProperty VolumeProperty = DependencyProperty.Register(
            nameof(Volume), typeof(double), typeof(MediaElement),
            new FrameworkPropertyMetadata(
                Constants.Controller.DefaultVolume,
                FrameworkPropertyMetadataOptions.None,
                OnVolumePropertyChanged,
                OnVolumePropertyChanging));

        private static object OnVolumePropertyChanging(DependencyObject d, object value)
        {
            var element = d as MediaElement;
            if (element == null || element.MediaCore == null || element.MediaCore.IsDisposed) return Constants.Controller.DefaultVolume;
            if (element.PropertyUpdatesWorker.IsExecutingCycle) return value;
            if (element.HasAudio == false) return Constants.Controller.DefaultVolume;

            return ((double)value).Clamp(Constants.Controller.MinVolume, Constants.Controller.MaxVolume);
        }

        private static void OnVolumePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var element = d as MediaElement;
            element.MediaCore.State.Volume = (double)e.NewValue;
        }

        #endregion

        #region Balance Dependency Property

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
        /// The DependencyProperty for the MediaElement.Balance property.
        /// </summary>
        public static readonly DependencyProperty BalanceProperty = DependencyProperty.Register(
            nameof(Balance), typeof(double), typeof(MediaElement),
            new FrameworkPropertyMetadata(
                Constants.Controller.DefaultBalance,
                FrameworkPropertyMetadataOptions.None,
                OnBalancePropertyChanged,
                OnBalancePropertyChanging));

        private static object OnBalancePropertyChanging(DependencyObject d, object value)
        {
            var element = d as MediaElement;
            if (element == null || element.MediaCore == null || element.MediaCore.IsDisposed) return Constants.Controller.DefaultBalance;
            if (element.PropertyUpdatesWorker.IsExecutingCycle) return value;
            if (element.HasAudio == false) return Constants.Controller.DefaultBalance;

            return ((double)value).Clamp(Constants.Controller.MinBalance, Constants.Controller.MaxBalance);
        }

        private static void OnBalancePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            (d as MediaElement).MediaCore.State.Balance = (double)e.NewValue;
        }

        #endregion

        #region IsMuted Dependency Property

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
        /// The DependencyProperty for the MediaElement.IsMuted property.
        /// </summary>
        public static readonly DependencyProperty IsMutedProperty = DependencyProperty.Register(
            nameof(IsMuted), typeof(bool), typeof(MediaElement),
            new FrameworkPropertyMetadata(
                false,
                FrameworkPropertyMetadataOptions.None,
                OnIsMutedPropertyChanged,
                OnIsMutedPropertyChanging));

        private static object OnIsMutedPropertyChanging(DependencyObject d, object value)
        {
            var element = d as MediaElement;
            if (element == null || element.MediaCore == null || element.MediaCore.IsDisposed) return false;
            if (element.PropertyUpdatesWorker.IsExecutingCycle) return value;
            if (element.HasAudio == false) return false;

            return (bool)value;
        }

        private static void OnIsMutedPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            (d as MediaElement).MediaCore.State.IsMuted = (bool)e.NewValue;
        }

        #endregion

        #region SpeedRatio Dependency Property

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
        /// The DependencyProperty for the MediaElement.SpeedRatio property.
        /// </summary>
        public static readonly DependencyProperty SpeedRatioProperty = DependencyProperty.Register(
            nameof(SpeedRatio), typeof(double), typeof(MediaElement),
            new FrameworkPropertyMetadata(
                Constants.Controller.DefaultSpeedRatio,
                FrameworkPropertyMetadataOptions.None,
                OnSpeedRatioPropertyChanged,
                OnSpeedRatioPropertyChanging));

        private static object OnSpeedRatioPropertyChanging(DependencyObject d, object value)
        {
            var element = d as MediaElement;
            if (element == null || element.MediaCore == null || element.MediaCore.IsDisposed) return Constants.Controller.DefaultSpeedRatio;
            if (element.MediaCore.State.IsSeekable == false) return Constants.Controller.DefaultSpeedRatio;

            return ((double)value).Clamp(Constants.Controller.MinSpeedRatio, Constants.Controller.MaxSpeedRatio);
        }

        private static void OnSpeedRatioPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            (d as MediaElement).MediaCore?.RequestSpeedRatio((double)e.NewValue);
        }

        #endregion

        #region Position Dependency Property

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

        /// <summary>
        /// The DependencyProperty for the MediaElement.Position property.
        /// </summary>
        public static readonly DependencyProperty PositionProperty = DependencyProperty.Register(
            nameof(Position), typeof(TimeSpan), typeof(MediaElement),
            new FrameworkPropertyMetadata(
                TimeSpan.Zero,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnPositionPropertyChanged,
                OnPositionPropertyChanging));

        private static object OnPositionPropertyChanging(DependencyObject d, object value)
        {
            var element = d as MediaElement;
            if (element == null || element.MediaCore == null || element.MediaCore.IsDisposed) return TimeSpan.Zero;
            if (element.MediaCore.State.IsSeekable == false) return element.MediaCore.State.Position;

            if (element.PropertyUpdatesWorker.IsExecutingCycle)
            {
                lock (element.ReportablePositionLock)
                {
                    if (element.m_ReportablePosition != null)
                    {
                        // coming from underlying engine
                        return element.m_ReportablePosition.Value;
                    }
                }
            }

            // Clamp from 0 to duration
            var targetSeek = (TimeSpan)value;
            if ((element.MediaCore?.MediaInfo?.Duration ?? TimeSpan.Zero) != TimeSpan.Zero)
                targetSeek = ((TimeSpan)value).Clamp(TimeSpan.Zero, element.MediaCore.MediaInfo.Duration);

            // coming in as a seek from user
            element.MediaCore?.RequestSeek(targetSeek);
            return targetSeek;
        }

        private static void OnPositionPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // placeholder: nothing to do one we have changed the position.
        }

        #endregion

        #region Source Dependency Property

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
        /// DependencyProperty for FFmpegMediaElement Source property.
        /// </summary>
        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
            nameof(Source), typeof(Uri), typeof(MediaElement),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.None, OnSourcePropertyChanged, OnSourcePropertyChanging));

        private static object OnSourcePropertyChanging(DependencyObject d, object value)
        {
            var element = d as MediaElement;
            if (element == null || element.MediaCore == null || element.MediaCore.IsDisposed) return null;
            return value;
        }

        private static async void OnSourcePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var element = d as MediaElement;

            if (e.NewValue == null && e.OldValue != null && element.IsOpen)
            {
                await element.Close();
            }
            else if (e.NewValue != null && element.IsOpening == false && e.NewValue is Uri uri)
            {
                // Skip change actions if we are currently opening via the Open command
                if (element.IsOpeningViaCommand.Value == false)
                    await element?.MediaCore?.Open(e.NewValue as Uri);

                // Reset the opening via command.
                element.IsOpeningViaCommand.Value = false;
            }
        }

        #endregion

        #region Stretch Dependency Property

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
        /// DependencyProperty for Stretch property.
        /// </summary>
        public static readonly DependencyProperty StretchProperty = DependencyProperty.Register(
            nameof(Stretch), typeof(Stretch), typeof(MediaElement),
            new FrameworkPropertyMetadata(Stretch.Uniform, AffectsMeasureAndRender, OnStretchPropertyChanged));

        private static void OnStretchPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            var element = dependencyObject as MediaElement;
            if (element == null) return;

            element.VideoView.Stretch = (Stretch)e.NewValue;
        }

        #endregion

        #region StretchDirection Dependency Property

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
        /// DependencyProperty for StretchDirection property.
        /// </summary>
        public static readonly DependencyProperty StretchDirectionProperty = DependencyProperty.Register(
            nameof(StretchDirection), typeof(StretchDirection), typeof(MediaElement),
            new FrameworkPropertyMetadata(StretchDirection.Both, AffectsMeasureAndRender, OnStretchDirectionPropertyChanged));

        private static void OnStretchDirectionPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            var element = dependencyObject as MediaElement;
            if (element == null) return;

            element.VideoView.StretchDirection = (StretchDirection)e.NewValue;
        }

        #endregion

        #region ScrubbingEnabled Dependency Property

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
        /// The DependencyProperty for the MediaElement.ScrubbingEnabled property.
        /// </summary>
        public static readonly DependencyProperty ScrubbingEnabledProperty = DependencyProperty.Register(
            nameof(ScrubbingEnabled), typeof(bool), typeof(MediaElement),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.None));

        #endregion

        #region LoadedBahavior Dependency Property

        /// <summary>
        /// Specifies the action that the media element should execute when it
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
        /// The DependencyProperty for the MediaElement.LoadedBehavior property.
        /// </summary>
        public static readonly DependencyProperty LoadedBehaviorProperty = DependencyProperty.Register(
            nameof(LoadedBehavior), typeof(MediaState), typeof(MediaElement),
            new FrameworkPropertyMetadata(MediaState.Manual, FrameworkPropertyMetadataOptions.None));

        #endregion

        #region UnoadedBahavior Dependency Property

        /// <summary>
        /// Specifies how the underlying media should behave when
        /// it has ended. The default behavior is to Pause the media.
        /// </summary>
        [Category(nameof(MediaElement))]
        [Description("Specifies how the underlying media should behave when it has ended. The default behavior is to Close the media.")]
        public MediaState UnloadedBehavior
        {
            get { return (MediaState)GetValue(UnloadedBehaviorProperty); }
            set { SetValue(UnloadedBehaviorProperty, value); }
        }

        /// <summary>
        /// The DependencyProperty for the MediaElement.UnloadedBehavior property.
        /// </summary>
        public static readonly DependencyProperty UnloadedBehaviorProperty = DependencyProperty.Register(
            nameof(UnloadedBehavior), typeof(MediaState), typeof(MediaElement),
            new FrameworkPropertyMetadata(MediaState.Pause, FrameworkPropertyMetadataOptions.None));

        #endregion

        #region ClosedCaptionsChannel Dependency Property

        /// <summary>
        /// Gets/Sets the ClosedCaptionsChannel property on the MediaElement.
        /// Note: Valid values are from 0 to 1
        /// </summary>
        [Category(nameof(MediaElement))]
        [Description("The video CC Channel to render. Ranges from 0 to 4")]
        public CaptionsChannel ClosedCaptionsChannel
        {
            get { return (CaptionsChannel)GetValue(ClosedCaptionsChannelProperty); }
            set { SetValue(ClosedCaptionsChannelProperty, value); }
        }

        /// <summary>
        /// The DependencyProperty for the MediaElement.ClosedCaptionsChannel property.
        /// </summary>
        public static readonly DependencyProperty ClosedCaptionsChannelProperty = DependencyProperty.Register(
            nameof(ClosedCaptionsChannel), typeof(CaptionsChannel), typeof(MediaElement),
            new FrameworkPropertyMetadata(
                Constants.Controller.DefaultClosedCaptionsChannel,
                FrameworkPropertyMetadataOptions.None,
                OnClosedCaptionsChannelPropertyChanged));

        private static void OnClosedCaptionsChannelPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var element = d as MediaElement;
            element?.CaptionsView.Reset();
        }

        #endregion

    }
}
#pragma warning restore SA1117 // Parameters must be on same line or separate lines
#pragma warning restore SA1201 // Elements must appear in the correct order