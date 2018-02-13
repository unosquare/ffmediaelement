namespace Unosquare.FFME.Windows.Sample.ViewModels
{
    using Events;
    using Foundation;
    using System;

    /// <summary>
    /// Represents the Playlist
    /// </summary>
    /// <seealso cref="AttachedViewModel" />
    public class PlaylistViewModel : AttachedViewModel
    {
        private bool m_IsInOpenMode = false;
        private string m_OpenModeUrl = string.Empty;

        /// <summary>
        /// Initializes a new instance of the <see cref="PlaylistViewModel"/> class.
        /// </summary>
        /// <param name="root">The root.</param>
        public PlaylistViewModel(RootViewModel root)
            : base(root)
        {
            // placeholder
        }

        /// <summary>
        /// Gets or sets the playlist search string.
        /// </summary>
        public string PlaylistSearchString
        {
            get => PlaylistManager.SearchString;
            set => PlaylistManager.SearchString = value;
        }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is playlist enabled.
        /// </summary>
        public bool IsPlaylistEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether this instance has taken thumbnail.
        /// </summary>
        public bool HasTakenThumbnail { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether this instance is in open mode.
        /// </summary>
        public bool IsInOpenMode
        {
            get => m_IsInOpenMode;
            set => SetProperty(ref m_IsInOpenMode, value);
        }

        /// <summary>
        /// Gets or sets the open model URL.
        /// </summary>
        public string OpenModelUrl
        {
            get => m_OpenModeUrl;
            set => SetProperty(ref m_OpenModeUrl, value);
        }

        /// <summary>
        /// Called by the root ViewModel when the application is loaded and fully available
        /// </summary>
        internal override void OnApplicationLoaded()
        {
            base.OnApplicationLoaded();
            var m = Root.App.MediaElement;

            new Action(() => { IsPlaylistEnabled = m.IsOpening == false; })
                .WhenChanged(m, nameof(m.IsOpening));

            m.MediaOpened += MediaOpened;
            m.RenderingVideo += RenderingVideo;
        }

        /// <summary>
        /// Called when Media is opened
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="System.Windows.RoutedEventArgs"/> instance containing the event data.</param>
        private void MediaOpened(object sender, System.Windows.RoutedEventArgs e)
        {
            HasTakenThumbnail = false;
        }

        /// <summary>
        /// Handles the RenderingVideo event of the Media control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RenderingVideoEventArgs"/> instance containing the event data.</param>
        private void RenderingVideo(object sender, RenderingVideoEventArgs e)
        {
            if (HasTakenThumbnail) return;
            var m = Root.App.MediaElement;

            if (m.HasMediaEnded || m.Position.TotalSeconds >= 3 || (m.NaturalDuration.HasTimeSpan && m.NaturalDuration.TimeSpan.TotalSeconds <= 3))
            {
                HasTakenThumbnail = true;
                PlaylistManager.AddOrUpdateEntryThumbnail(m.Source.ToString(), e.Bitmap);
                PlaylistManager.SaveEntries();
            }
        }
    }
}
