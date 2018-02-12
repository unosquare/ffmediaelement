namespace Unosquare.FFME.Windows.Sample.ViewModels
{
    using System.Windows;

    /// <summary>
    /// Represents a VM for the Controller Control
    /// </summary>
    /// <seealso cref="AttachedViewModel" />
    public sealed class ControllerViewModel : AttachedViewModel
    {
        private Visibility m_IsMediaOpenVisibility = Visibility.Visible;
        private bool m_IsAudioControlEnabled = true;
        private bool m_IsSpeedRatioEnabled = true;
        private Visibility m_AudioControlVisibility = Visibility.Visible;
        private Visibility m_PauseButtonVisibility = Visibility.Visible;
        private Visibility m_PlayButtonVisibility = Visibility.Visible;
        private Visibility m_StopButtonVisibility = Visibility.Visible;
        private Visibility m_CloseButtonVisibility = Visibility.Visible;
        private Visibility m_OpenButtonVisibility = Visibility.Visible;
        private Visibility m_SeekBarVisibility = Visibility.Visible;
        private Visibility m_BufferingProgressVisibility = Visibility.Visible;
        private Visibility m_DownloadProgressVisibility = Visibility.Visible;

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
        public Visibility IsMediaOpenVisibility
        {
            get => m_IsMediaOpenVisibility;
            set => SetProperty(ref m_IsMediaOpenVisibility, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is audio control enabled.
        /// </summary>
        public bool IsAudioControlEnabled
        {
            get => m_IsAudioControlEnabled;
            set => SetProperty(ref m_IsAudioControlEnabled, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is speed ratio enabled.
        /// </summary>
        public bool IsSpeedRatioEnabled
        {
            get => m_IsSpeedRatioEnabled;
            set => SetProperty(ref m_IsSpeedRatioEnabled, value);
        }

        /// <summary>
        /// Gets or sets the audio control visibility.
        /// </summary>
        public Visibility AudioControlVisibility
        {
            get => m_AudioControlVisibility;
            set => SetProperty(ref m_AudioControlVisibility, value);
        }

        /// <summary>
        /// Gets or sets the pause button visibility.
        /// </summary>
        public Visibility PauseButtonVisibility
        {
            get => m_PauseButtonVisibility;
            set => SetProperty(ref m_PauseButtonVisibility, value);
        }

        /// <summary>
        /// Gets or sets the play button visibility.
        /// </summary>
        public Visibility PlayButtonVisibility
        {
            get => m_PlayButtonVisibility;
            set => SetProperty(ref m_PlayButtonVisibility, value);
        }

        /// <summary>
        /// Gets or sets the stop button visibility.
        /// </summary>
        public Visibility StopButtonVisibility
        {
            get => m_StopButtonVisibility;
            set => SetProperty(ref m_StopButtonVisibility, value);
        }

        /// <summary>
        /// Gets or sets the close button visibility.
        /// </summary>
        public Visibility CloseButtonVisibility
        {
            get => m_CloseButtonVisibility;
            set => SetProperty(ref m_CloseButtonVisibility, value);
        }

        /// <summary>
        /// Gets or sets the open button visibility.
        /// </summary>
        public Visibility OpenButtonVisibility
        {
            get => m_OpenButtonVisibility;
            set => SetProperty(ref m_OpenButtonVisibility, value);
        }

        /// <summary>
        /// Gets or sets the seek bar visibility.
        /// </summary>
        public Visibility SeekBarVisibility
        {
            get => m_SeekBarVisibility;
            set => SetProperty(ref m_SeekBarVisibility, value);
        }

        /// <summary>
        /// Gets or sets the buffering progress visibility.
        /// </summary>
        public Visibility BufferingProgressVisibility
        {
            get => m_BufferingProgressVisibility;
            set => SetProperty(ref m_BufferingProgressVisibility, value);
        }

        /// <summary>
        /// Gets or sets the download progress visibility.
        /// </summary>
        public Visibility DownloadProgressVisibility
        {
            get => m_DownloadProgressVisibility;
            set => SetProperty(ref m_DownloadProgressVisibility, value);
        }
    }
}
