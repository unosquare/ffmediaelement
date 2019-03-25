#pragma warning disable SA1201 // Elements must appear in the correct order
#pragma warning disable SA1117 // Parameters must be on same line or separate lines
namespace Unosquare.FFME
{
    using ClosedCaptions;
    using Engine;
    using System;
    using System.ComponentModel;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;

    public partial class MediaElement
    {
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
            new FrameworkPropertyMetadata(
                TimeSpan.Zero,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnPositionPropertyChanged,
                OnPositionPropertyChanging));

        private static object OnPositionPropertyChanging(DependencyObject d, object value)
        {
            if (d == null || d is MediaElement == false) return value;

            var element = (MediaElement)d;
            if (element.MediaCore == null || element.MediaCore.IsDisposed || element.MediaCore.MediaInfo == null)
                return TimeSpan.Zero;

            if (element.MediaCore.State.IsSeekable == false)
                return element.MediaCore.State.Position;

            var valueComingFromEngine = element.PropertyUpdatesWorker.IsExecutingCycle;

            if (valueComingFromEngine && element.MediaCore.State.IsSeeking == false)
                return value;

            // Clamp from 0 to duration
            var targetSeek = (TimeSpan)value;
            var minTarget = element.MediaCore.State.PlaybackStartTime ?? TimeSpan.Zero;
            var maxTarget = element.MediaCore.State.PlaybackEndTime ?? TimeSpan.Zero;
            var hasValidTaget = maxTarget > minTarget;

            if (hasValidTaget)
                targetSeek = ((TimeSpan)value).Clamp(minTarget, maxTarget);

            if (valueComingFromEngine)
                return targetSeek;

            // coming in as a seek from user
            if (hasValidTaget)
                element.MediaCore?.Seek(targetSeek);

            return targetSeek;
        }

        private static void OnPositionPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // placeholder: nothing to do once we have changed the position.
        }

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
            get => (CaptionsChannel)GetValue(ClosedCaptionsChannelProperty);
            set => SetValue(ClosedCaptionsChannelProperty, value);
        }

        /// <summary>
        /// The DependencyProperty for the MediaElement.ClosedCaptionsChannel property.
        /// </summary>
        public static readonly DependencyProperty ClosedCaptionsChannelProperty = DependencyProperty.Register(
            nameof(ClosedCaptionsChannel), typeof(CaptionsChannel), typeof(MediaElement),
            new FrameworkPropertyMetadata(
                Constants.DefaultClosedCaptionsChannel,
                FrameworkPropertyMetadataOptions.None,
                OnClosedCaptionsChannelPropertyChanged));

        private static void OnClosedCaptionsChannelPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MediaElement m) m.CaptionsView.Reset();
        }

        #endregion

        #region Stretch Dependency Property

        /// <summary>
        /// Gets/Sets the Stretch on this MediaElement.
        /// The Stretch property determines how large the MediaElement will be drawn.
        /// </summary>
        public Stretch Stretch
        {
            get => (Stretch)GetValue(StretchProperty);
            set => SetValue(StretchProperty, value);
        }

        /// <summary>
        /// DependencyProperty for Stretch property.
        /// </summary>
        public static readonly DependencyProperty StretchProperty = DependencyProperty.Register(
            nameof(Stretch), typeof(Stretch), typeof(MediaElement),
            new FrameworkPropertyMetadata(Stretch.Uniform, AffectsMeasureAndRender, OnStretchPropertyChanged));

        private static void OnStretchPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MediaElement m && m.VideoView != null && m.VideoView.IsLoaded && e.NewValue is Stretch v)
                m.VideoView.Stretch = v;
        }

        #endregion

        #region StretchDirection Dependency Property

        /// <summary>
        /// Gets/Sets the stretch direction of the ViewBox, which determines the restrictions on
        /// scaling that are applied to the content inside the ViewBox.  For instance, this property
        /// can be used to prevent the content from being smaller than its native size or larger than
        /// its native size.
        /// </summary>
        public StretchDirection StretchDirection
        {
            get => (StretchDirection)GetValue(StretchDirectionProperty);
            set => SetValue(StretchDirectionProperty, value);
        }

        /// <summary>
        /// DependencyProperty for StretchDirection property.
        /// </summary>
        public static readonly DependencyProperty StretchDirectionProperty = DependencyProperty.Register(
            nameof(StretchDirection), typeof(StretchDirection), typeof(MediaElement),
            new FrameworkPropertyMetadata(StretchDirection.Both, AffectsMeasureAndRender, OnStretchDirectionPropertyChanged));

        private static void OnStretchDirectionPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MediaElement m && m.VideoView != null && m.VideoView.IsLoaded && e.NewValue is StretchDirection v)
                m.VideoView.StretchDirection = v;
        }

        #endregion
    }
}
#pragma warning restore SA1117 // Parameters must be on same line or separate lines
#pragma warning restore SA1201 // Elements must appear in the correct order