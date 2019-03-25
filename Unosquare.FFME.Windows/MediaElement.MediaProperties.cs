namespace Unosquare.FFME
{
    using Engine;
    using Rendering;
    using System.Windows.Controls;

    public partial class MediaElement
    {
        /// <summary>
        /// Provides access to various internal media renderer options.
        /// The default options are optimal to work for most media streams.
        /// This is an advanced feature and it is not recommended to change these
        /// options without careful consideration.
        /// </summary>
        public RendererOptions RendererOptions { get; } = new RendererOptions();

        /// <summary>
        /// Gets the current playback state.
        /// </summary>
        public MediaState MediaState => (MediaState)(MediaCore?.State.MediaState ?? PlaybackStatus.Close);
    }
}
