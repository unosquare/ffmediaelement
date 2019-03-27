namespace Unosquare.FFME.Windows.Sample.ViewModels
{
    using Foundation;
    using System;
    using System.Windows;
    using System.Windows.Media;

    /// <summary>
    /// Represents a VM for the Controller Control
    /// </summary>
    /// <seealso cref="AttachedViewModel" />
    public sealed class ControllerViewModel : AttachedViewModel
    {
        private Visibility m_IsMediaOpenVisibility = Visibility.Visible;
        private bool m_IsAudioControlEnabled = true;
        private bool m_IsSpeedRatioEnabled = true;
        private Visibility m_ClosedCaptionsVisibility = Visibility.Visible;
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
        /// Gets or sets the CC channel button control visibility.
        /// </summary>
        public Visibility ClosedCaptionsVisibility
        {
            get => m_ClosedCaptionsVisibility;
            set => SetProperty(ref m_ClosedCaptionsVisibility, value);
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

        /// <summary>
        /// Gets or sets the media element zoom.
        /// </summary>
        public double MediaElementZoom
        {
            get
            {
                var m = App.ViewModel.MediaElement;
                if (m == null) return 1d;

                var transform = m.RenderTransform as ScaleTransform;
                return transform?.ScaleX ?? 1d;
            }
            set
            {
                var m = App.ViewModel.MediaElement;
                if (m == null) return;

                // ReSharper disable once UseNegatedPatternMatching
                var transform = m.RenderTransform as ScaleTransform;
                if (transform == null)
                {
                    transform = new ScaleTransform(1, 1);
                    m.RenderTransformOrigin = new Point(0.5, 0.5);
                    m.RenderTransform = transform;
                }

                transform.ScaleX = value;
                transform.ScaleY = value;

                if (transform.ScaleX < 0.1d || transform.ScaleY < 0.1)
                {
                    transform.ScaleX = 0.1d;
                    transform.ScaleY = 0.1d;
                }
                else if (transform.ScaleX > 5d || transform.ScaleY > 5)
                {
                    transform.ScaleX = 5;
                    transform.ScaleY = 5;
                }

                NotifyPropertyChanged();
            }
        }

        /// <inheritdoc />
        internal override void OnApplicationLoaded()
        {
            base.OnApplicationLoaded();
            var m = App.ViewModel.MediaElement;

            new Action(() => { IsMediaOpenVisibility = m.IsOpen ? Visibility.Visible : Visibility.Hidden; })
                .WhenChanged(m, nameof(m.IsOpen));

            new Action(() => { ClosedCaptionsVisibility = m.HasClosedCaptions ? Visibility.Visible : Visibility.Hidden; })
                .WhenChanged(m, nameof(m.HasClosedCaptions));

            new Action(() =>
            {
                AudioControlVisibility = m.HasAudio ? Visibility.Visible : Visibility.Hidden;
                IsAudioControlEnabled = m.HasAudio;
            }).WhenChanged(m, nameof(m.HasAudio));

            new Action(() => { PauseButtonVisibility = m.CanPause && m.IsPlaying ? Visibility.Visible : Visibility.Collapsed; })
                .WhenChanged(m, nameof(m.CanPause), nameof(m.IsPlaying));

            new Action(() =>
            {
                PlayButtonVisibility =
                    m.IsOpen && m.IsPlaying == false && m.HasMediaEnded == false && m.IsSeeking == false && m.IsChanging == false ?
                    Visibility.Visible : Visibility.Collapsed;
            })
            .WhenChanged(m, nameof(m.IsOpen), nameof(m.IsPlaying), nameof(m.HasMediaEnded), nameof(m.IsSeeking), nameof(m.IsChanging));

            new Action(() =>
            {
                StopButtonVisibility =
                    m.IsOpen && m.IsChanging == false && m.IsSeeking == false && (m.HasMediaEnded || (m.IsSeekable && m.MediaState != MediaPlaybackState.Stop)) ?
                    Visibility.Visible : Visibility.Hidden;
            })
            .WhenChanged(m, nameof(m.IsOpen), nameof(m.HasMediaEnded), nameof(m.IsSeekable), nameof(m.MediaState), nameof(m.IsChanging), nameof(m.IsSeeking));

            new Action(() => { CloseButtonVisibility = m.IsOpen && m.IsChanging == false ? Visibility.Visible : Visibility.Hidden; })
                .WhenChanged(m, nameof(m.IsOpen), nameof(m.IsChanging));

            new Action(() => { SeekBarVisibility = m.IsSeekable ? Visibility.Visible : Visibility.Hidden; })
                .WhenChanged(m, nameof(m.IsSeekable));

            new Action(() => { BufferingProgressVisibility = m.IsOpening || (m.IsBuffering && m.BufferingProgress < 0.95) ? Visibility.Visible : Visibility.Hidden; })
                .WhenChanged(m, nameof(m.IsOpening), nameof(m.IsBuffering), nameof(m.BufferingProgress), nameof(m.Position));

            new Action(() => { DownloadProgressVisibility = m.IsOpen && m.HasMediaEnded == false && ((m.DownloadProgress > 0d && m.DownloadProgress < 0.95) || m.IsLiveStream) ? Visibility.Visible : Visibility.Hidden; })
                .WhenChanged(m, nameof(m.IsOpen), nameof(m.HasMediaEnded), nameof(m.DownloadProgress), nameof(m.IsLiveStream));

            new Action(() => { OpenButtonVisibility = m.IsOpening == false ? Visibility.Visible : Visibility.Hidden; })
                .WhenChanged(m, nameof(m.IsOpening));

            new Action(() => { IsSpeedRatioEnabled = m.IsOpening == false; })
                .WhenChanged(m, nameof(m.IsOpen), nameof(m.IsSeekable));
        }
    }
}
