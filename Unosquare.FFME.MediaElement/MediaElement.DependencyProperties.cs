#pragma warning disable SA1201 // Elements must appear in the correct order
#pragma warning disable SA1117 // Parameters must be on same line or separate lines
namespace Unosquare.FFME
{
    using Engine;
    using System.ComponentModel;
#if WINDOWS_UWP
    using Windows.UI.Xaml;
#else
    using System.Windows;
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
#if WINDOWS_UWP
            new PropertyMetadata(Constants.DefaultVolume, OnVolumePropertyChanged));
#else
            new FrameworkPropertyMetadata(
                Constants.DefaultVolume,
                FrameworkPropertyMetadataOptions.None,
                OnVolumePropertyChanged,
                OnVolumePropertyChanging));
#endif

        private static object OnVolumePropertyChanging(DependencyObject d, object value) =>
            ((double)value).Clamp(Constants.MinVolume, Constants.MaxVolume);

        private static void OnVolumePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MediaElement m && m.MediaCore != null && e.NewValue is double v)
                m.MediaCore.State.Volume = v;
        }

#endregion
    }
}
#pragma warning restore SA1117 // Parameters must be on same line or separate lines
#pragma warning restore SA1201 // Elements must appear in the correct order