namespace Unosquare.FFME
{
    using Platform;
    using Windows.UI.Xaml.Controls;

    public partial class MediaElement : UserControl
    {
        static MediaElement()
        {
            // Initialize the core
            MediaEngine.Initialize(UniversalPlatform.Instance);
        }

        public MediaElement()
        {
            ContentGrid.Children.Add(VideoView);

            // Display the control (or not)
            if (UniversalPlatform.Instance.IsInDesignTime == false)
            {
                // Setup the media engine and associated property updates worker
                MediaCore = new MediaEngine(this, new MediaConnector(this));
                StartPropertyUpdatesWorker();
            }
        }

        internal Grid ContentGrid { get; } = new Grid() { Name = nameof(ContentGrid) };

        internal Image VideoView { get; } = new Image() { Name = nameof(VideoView) };
    }
}
