namespace Unosquare.FFME.Windows.Sample.ViewModels
{
    using Events;
    using Foundation;
    using Platform;
    using System;
    using System.ComponentModel;
    using System.IO;
    using System.Windows.Data;

    /// <summary>
    /// Represents the Playlist
    /// </summary>
    /// <seealso cref="AttachedViewModel" />
    public class PlaylistViewModel : AttachedViewModel
    {
        #region Private State

        // Constants
        private const int MinimumSearchLength = 3;

        // Private state management
        private readonly TimeSpan SearchActionDelay = TimeSpan.FromSeconds(0.25);
        private bool HasTakenThumbnail = false;
        private DeferredAction SearchAction = null;
        private string FilterString = string.Empty;

        // Property Backing
        private bool m_IsInOpenMode = GuiContext.Current.IsInDesignTime;
        private bool m_IsPlaylistEnabled = true;
        private string m_OpenTargetUrl = string.Empty;
        private string m_PlaylistSearchString = string.Empty;

        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="PlaylistViewModel"/> class.
        /// </summary>
        /// <param name="root">The root.</param>
        public PlaylistViewModel(RootViewModel root)
            : base(root)
        {
            // Set and create a thumbnails directory
            ThumbsDirectory = Path.Combine(root.AppDataDirectory, "Thumbnails");
            if (Directory.Exists(ThumbsDirectory) == false)
                Directory.CreateDirectory(ThumbsDirectory);

            PlaylistFilePath = Path.Combine(root.AppDataDirectory, "ffme.m3u8");

            Entries = new CustomPlaylist(this);
            EntriesView = CollectionViewSource.GetDefaultView(Entries) as ICollectionView;
            EntriesView.Filter = (item) =>
            {
                var entry = item as CustomPlaylistEntry;
                if (entry == null) return false;
                if (string.IsNullOrWhiteSpace(PlaylistSearchString) || PlaylistSearchString.Trim().Length < MinimumSearchLength)
                    return true;

                if ((entry.Title?.ToLowerInvariant().Contains(PlaylistSearchString) ?? false) ||
                    (entry.MediaUrl?.ToLowerInvariant().Contains(PlaylistSearchString) ?? false))
                    return true;

                return false;
            };

            NotifyPropertyChanged(nameof(EntriesView));
        }

        /// <summary>
        /// Gets the custom playlist. Do not use for data-binding
        /// </summary>
        public CustomPlaylist Entries { get; }

        /// <summary>
        /// Gets the custom playlist entries as a view that can be uased in data binding scenarios.
        /// </summary>
        public ICollectionView EntriesView { get; private set; }

        /// <summary>
        /// Gets the full path wehre thumbnails are stored.
        /// </summary>
        public string ThumbsDirectory { get; }

        /// <summary>
        /// Gets the playlist file path.
        /// </summary>
        public string PlaylistFilePath { get; }

        /// <summary>
        /// Gets or sets the playlist search string.
        /// </summary>
        public string PlaylistSearchString
        {
            get => m_PlaylistSearchString;
            set
            {
                if (!SetProperty(ref m_PlaylistSearchString, value))
                    return;

                if (SearchAction == null)
                {
                    SearchAction = DeferredAction.Create(() =>
                    {
                        var futureSearch = PlaylistSearchString ?? string.Empty;
                        var currentSearch = FilterString ?? string.Empty;

                        if (currentSearch == futureSearch) return;
                        if (futureSearch.Length < MinimumSearchLength && currentSearch.Length < MinimumSearchLength) return;

                        EntriesView.Refresh();
                        FilterString = string.Copy(m_PlaylistSearchString) ?? string.Empty;
                    });
                }

                SearchAction.Defer(SearchActionDelay);
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is playlist enabled.
        /// </summary>
        public bool IsPlaylistEnabled
        {
            get => m_IsPlaylistEnabled;
            set => SetProperty(ref m_IsPlaylistEnabled, value);
        }

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
        public string OpenTargetUrl
        {
            get => m_OpenTargetUrl;
            set => SetProperty(ref m_OpenTargetUrl, value);
        }

        /// <summary>
        /// Called by the root ViewModel when the application is loaded and fully available
        /// </summary>
        internal override void OnApplicationLoaded()
        {
            base.OnApplicationLoaded();
            var m = Root.App.MediaElement;

            new Action(() =>
            {
                IsPlaylistEnabled = m.IsOpening == false;
            }).WhenChanged(m, nameof(m.IsOpening));

            m.MediaOpened += OnMediaOpened;
            m.RenderingVideo += OnRenderingVideo;
        }

        /// <summary>
        /// Called when Media is opened
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="System.Windows.RoutedEventArgs"/> instance containing the event data.</param>
        private void OnMediaOpened(object sender, System.Windows.RoutedEventArgs e)
        {
            HasTakenThumbnail = false;
            Entries.AddOrUpdateEntry(
                Root.App.MediaElement.Source?.ToString() ?? Root.App.MediaElement.MediaInfo.InputUrl,
                Root.App.MediaElement.MediaInfo);
            Entries.SaveEntries();
        }

        /// <summary>
        /// Handles the RenderingVideo event of the Media control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RenderingVideoEventArgs"/> instance containing the event data.</param>
        private void OnRenderingVideo(object sender, RenderingVideoEventArgs e)
        {
            var state = e.EngineState;
            if (HasTakenThumbnail || state.Source == null)
                return;

            var sourceUrl = state.Source.ToString();
            if (string.IsNullOrWhiteSpace(sourceUrl))
                return;

            if (state.HasMediaEnded
                || state.Position.TotalSeconds >= 3
                || (state.NaturalDuration.HasValue && state.NaturalDuration.Value.TotalSeconds <= 3))
            {
                HasTakenThumbnail = true;
                Entries.AddOrUpdateEntryThumbnail(sourceUrl, e.Bitmap);
                Entries.SaveEntries();
            }
        }
    }
}
