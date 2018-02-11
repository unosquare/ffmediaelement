namespace Unosquare.FFME.Windows.Sample.ViewModels
{
    using System.Windows;

    /// <summary>
    /// Represents a VM for the Controller Control
    /// </summary>
    /// <seealso cref="AttachedViewModel" />
    public class ControllerViewModel : AttachedViewModel
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ControllerViewModel"/> class.
        /// </summary>
        /// <param name="root">The root.</param>
        internal ControllerViewModel(RootViewModel root)
            : base(root)
        {
            // placeholder
        }

        /// <summary>
        /// Gets or sets the is media open visibility.
        /// </summary>
        public Visibility IsMediaOpenVisibility { get; set; } = Visibility.Visible;

        /// <summary>
        /// Gets or sets a value indicating whether this instance is audio control enabled.
        /// </summary>
        public bool IsAudioControlEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether this instance is speed ratio enabled.
        /// </summary>
        public bool IsSpeedRatioEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the audio control visibility.
        /// </summary>
        public Visibility AudioControlVisibility { get; set; } = Visibility.Visible;

        /// <summary>
        /// Gets or sets the pause button visibility.
        /// </summary>
        public Visibility PauseButtonVisibility { get; set; } = Visibility.Visible;

        /// <summary>
        /// Gets or sets the play button visibility.
        /// </summary>
        public Visibility PlayButtonVisibility { get; set; } = Visibility.Visible;

        /// <summary>
        /// Gets or sets the stop button visibility.
        /// </summary>
        public Visibility StopButtonVisibility { get; set; } = Visibility.Visible;

        /// <summary>
        /// Gets or sets the close button visibility.
        /// </summary>
        public Visibility CloseButtonVisibility { get; set; } = Visibility.Visible;

        /// <summary>
        /// Gets or sets the open button visibility.
        /// </summary>
        public Visibility OpenButtonVisibility { get; set; } = Visibility.Visible;

        /// <summary>
        /// Gets or sets the seek bar visibility.
        /// </summary>
        public Visibility SeekBarVisibility { get; set; } = Visibility.Visible;

        /// <summary>
        /// Gets or sets the buffering progress visibility.
        /// </summary>
        public Visibility BufferingProgressVisibility { get; set; } = Visibility.Visible;

        /// <summary>
        /// Gets or sets the download progress visibility.
        /// </summary>
        public Visibility DownloadProgressVisibility { get; set; } = Visibility.Visible;
    }
}
