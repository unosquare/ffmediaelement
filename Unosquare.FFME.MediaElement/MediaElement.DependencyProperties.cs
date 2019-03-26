#pragma warning disable SA1201 // Elements must appear in the correct order
#pragma warning disable SA1117 // Parameters must be on same line or separate lines
namespace Unosquare.FFME
{
    using Engine;
    using System;
    using System.ComponentModel;
#if WINDOWS_UWP
    using Windows.UI.Xaml;
    using FrameworkPropertyMetadata = Windows.UI.Xaml.PropertyMetadata;
    using MediaState = Engine.PlaybackStatus;
#else
    using System.Windows;
    using System.Windows.Controls;
#endif

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
            get => (double)GetValue(VolumeProperty);
            set => SetValue(VolumeProperty, value);
        }

        /// <summary>
        /// The DependencyProperty for the MediaElement.Volume property.
        /// </summary>
        public static readonly DependencyProperty VolumeProperty = DependencyProperty.Register(
            nameof(Volume), typeof(double), typeof(MediaElement),
            new FrameworkPropertyMetadata(Constants.DefaultVolume, OnVolumePropertyChanged));

        private static void OnVolumePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MediaElement m && m.MediaCore != null && e.NewValue is double v)
                m.MediaCore.State.Volume = v;
        }

        #endregion

        #region Balance Dependency Property

        /// <summary>
        /// Gets/Sets the Balance property on the MediaElement.
        /// </summary>
        [Category(nameof(MediaElement))]
        [Description("The audio balance for left and right audio channels. Valid ranges are -1.0 to 1.0")]
        public double Balance
        {
            get => (double)GetValue(BalanceProperty);
            set => SetValue(BalanceProperty, value);
        }

        /// <summary>
        /// The DependencyProperty for the MediaElement.Balance property.
        /// </summary>
        public static readonly DependencyProperty BalanceProperty = DependencyProperty.Register(
            nameof(Balance), typeof(double), typeof(MediaElement),
            new FrameworkPropertyMetadata(Constants.DefaultBalance, OnBalancePropertyChanged));

        private static void OnBalancePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MediaElement m && m.MediaCore != null && e.NewValue is double v)
                m.MediaCore.State.Balance = v;
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
            get => (bool)GetValue(IsMutedProperty);
            set => SetValue(IsMutedProperty, value);
        }

        /// <summary>
        /// The DependencyProperty for the MediaElement.IsMuted property.
        /// </summary>
        public static readonly DependencyProperty IsMutedProperty = DependencyProperty.Register(
            nameof(IsMuted), typeof(bool), typeof(MediaElement),
            new FrameworkPropertyMetadata(false, OnIsMutedPropertyChanged));

        private static void OnIsMutedPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MediaElement m && m.MediaCore != null && e.NewValue is bool v)
                m.MediaCore.State.IsMuted = v;
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
            get => (double)GetValue(SpeedRatioProperty);
            set => SetValue(SpeedRatioProperty, value);
        }

        /// <summary>
        /// The DependencyProperty for the MediaElement.SpeedRatio property.
        /// </summary>
        public static readonly DependencyProperty SpeedRatioProperty = DependencyProperty.Register(
            nameof(SpeedRatio), typeof(double), typeof(MediaElement),
            new FrameworkPropertyMetadata(Constants.DefaultSpeedRatio, OnSpeedRatioPropertyChanged));

        private static void OnSpeedRatioPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MediaElement m && m.MediaCore != null && e.NewValue is double v)
                m.MediaCore.State.SpeedRatio = v;
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
            get => GetValue(SourceProperty) as Uri;
            set => SetValue(SourceProperty, value);
        }

        /// <summary>
        /// DependencyProperty for FFmpegMediaElement Source property.
        /// </summary>
        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
            nameof(Source), typeof(Uri), typeof(MediaElement),
            new FrameworkPropertyMetadata(null, OnSourcePropertyChanged));

        private static async void OnSourcePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d == null || d is MediaElement == false) return;

            var element = (MediaElement)d;
            if (element.MediaCore == null || element.MediaCore.IsDisposed) return;

            if (e.NewValue == null && e.OldValue != null && element.IsOpen)
            {
                await element.Close();
            }
            else if (e.NewValue != null && element.IsOpening == false && e.NewValue is Uri uri)
            {
                // Skip change actions if we are currently opening via the Open command
                // We configure the await to true because we need to continue on the captured context
                if (element.IsOpeningViaCommand == false && element.MediaCore != null)
                    await element.MediaCore.Open(uri).ConfigureAwait(true);
            }
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
            get => (TimeSpan)GetValue(PositionProperty);
            set => SetValue(PositionProperty, value);
        }

        /// <summary>
        /// The DependencyProperty for the MediaElement.Position property.
        /// </summary>
        public static readonly DependencyProperty PositionProperty = DependencyProperty.Register(
            nameof(Position), typeof(TimeSpan), typeof(MediaElement),
            new FrameworkPropertyMetadata(TimeSpan.Zero,
#if !WINDOWS_UWP
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
#endif
                OnPositionPropertyChanged));

        private static void OnPositionPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d == null || d is MediaElement == false) return;

            var element = (MediaElement)d;
            if (element.MediaCore == null || element.MediaCore.IsDisposed || element.MediaCore.MediaInfo == null)
                return;

            if (element.MediaCore.State.IsSeekable == false)
                return;

            var valueComingFromEngine = element.PropertyUpdatesWorker.IsExecutingCycle;

            if (valueComingFromEngine && element.MediaCore.State.IsSeeking == false)
                return;

            // Clamp from 0 to duration
            var targetSeek = (TimeSpan)e.NewValue;
            var minTarget = element.MediaCore.State.PlaybackStartTime ?? TimeSpan.Zero;
            var maxTarget = element.MediaCore.State.PlaybackEndTime ?? TimeSpan.Zero;
            var hasValidTaget = maxTarget > minTarget;

            if (hasValidTaget)
                targetSeek = targetSeek.Clamp(minTarget, maxTarget);

            if (valueComingFromEngine)
                return;

            // coming in as a seek from user
            if (hasValidTaget)
                element.MediaCore?.Seek(targetSeek);
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
            get => (bool)GetValue(ScrubbingEnabledProperty);
            set => SetValue(ScrubbingEnabledProperty, value);
        }

        /// <summary>
        /// The DependencyProperty for the MediaElement.ScrubbingEnabled property.
        /// </summary>
        public static readonly DependencyProperty ScrubbingEnabledProperty = DependencyProperty.Register(
            nameof(ScrubbingEnabled), typeof(bool), typeof(MediaElement),
            new FrameworkPropertyMetadata(true));

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
            get => (MediaState)GetValue(LoadedBehaviorProperty);
            set => SetValue(LoadedBehaviorProperty, value);
        }

        /// <summary>
        /// The DependencyProperty for the MediaElement.LoadedBehavior property.
        /// </summary>
        public static readonly DependencyProperty LoadedBehaviorProperty = DependencyProperty.Register(
            nameof(LoadedBehavior), typeof(MediaState), typeof(MediaElement),
            new FrameworkPropertyMetadata(MediaState.Manual));

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
            get => (MediaState)GetValue(UnloadedBehaviorProperty);
            set => SetValue(UnloadedBehaviorProperty, value);
        }

        /// <summary>
        /// The DependencyProperty for the MediaElement.UnloadedBehavior property.
        /// </summary>
        public static readonly DependencyProperty UnloadedBehaviorProperty = DependencyProperty.Register(
            nameof(UnloadedBehavior), typeof(MediaState), typeof(MediaElement),
            new FrameworkPropertyMetadata(MediaState.Pause));

        #endregion
    }
}
#pragma warning restore SA1117 // Parameters must be on same line or separate lines
#pragma warning restore SA1201 // Elements must appear in the correct order