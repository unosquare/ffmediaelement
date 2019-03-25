namespace Unosquare.FFME
{
    using Engine;
    using Windows.UI.Xaml.Media;

    public partial class MediaElement
    {
        /// <summary>
        /// Gets the current playback state.
        /// </summary>
        public MediaElementState MediaState => PlaybackStatusToMediaState(MediaCore?.State.MediaState ?? PlaybackStatus.Close);
    }
}
