namespace Unosquare.FFME.Windows.Sample.ViewModels
{
    using Foundation;
    using Platform;
    using System;

    /// <summary>
    /// Represents the application-wide view model
    /// </summary>
    /// <seealso cref="ViewModelBase" />
    public class RootViewModel : ViewModelBase
    {
        private readonly string AssemblyVersion = typeof(RootViewModel).Assembly.GetName().Version.ToString();
        private string m_WindowTitle = string.Empty;
        private bool m_IsPlaylistPanelOpen = GuiContext.Current.IsInDesignTime;
        private bool m_IsPropertiesPanelOpen = GuiContext.Current.IsInDesignTime;
        private bool m_IsApplicationLoaded = GuiContext.Current.IsInDesignTime;

        /// <summary>
        /// Initializes a new instance of the <see cref="RootViewModel"/> class.
        /// </summary>
        public RootViewModel()
        {
            // Attached ViewModel Inistialization
            Playlist = new PlaylistViewModel(this);
            Controller = new ControllerViewModel(this);
            WindowTitle = "Application Loading . . .";
        }

        /// <summary>
        /// Provides access to the application object owning this View-Model.
        /// </summary>
        public App App => App.Current;

        /// <summary>
        /// Gets the playlist ViewModel.
        /// </summary>
        public PlaylistViewModel Playlist { get; }

        /// <summary>
        /// Gets the controller.
        /// </summary>
        public ControllerViewModel Controller { get; }

        /// <summary>
        /// Gets the window title.
        /// </summary>
        public string WindowTitle
        {
            get => m_WindowTitle;
            private set => SetProperty(ref m_WindowTitle, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is playlist panel open.
        /// </summary>
        public bool IsPlaylistPanelOpen
        {
            get => m_IsPlaylistPanelOpen;
            set => SetProperty(ref m_IsPlaylistPanelOpen, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is properties panel open.
        /// </summary>
        public bool IsPropertiesPanelOpen
        {
            get => m_IsPropertiesPanelOpen;
            set => SetProperty(ref m_IsPropertiesPanelOpen, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is application loaded.
        /// </summary>
        public bool IsApplicationLoaded
        {
            get => m_IsApplicationLoaded;
            set => SetProperty(ref m_IsApplicationLoaded, value);
        }

        /// <summary>
        /// Called when application has finished loading
        /// </summary>
        internal void OnApplicationLoaded()
        {
            Playlist.OnApplicationLoaded();
            Controller.OnApplicationLoaded();

            var m = App.MediaElement;
            new Action(UpdateWindowTitle).WhenChanged(m,
                nameof(m.IsOpen),
                nameof(m.IsOpening),
                nameof(m.MediaState),
                nameof(m.Source));

            IsPlaylistPanelOpen = true;
            IsApplicationLoaded = true;
        }

        /// <summary>
        /// Updates the window title according to the current state.
        /// </summary>
        private void UpdateWindowTitle()
        {
            var title = App.MediaElement?.Source?.ToString() ?? "(No media loaded)";
            var state = App.MediaElement?.MediaState.ToString();

            if (App.MediaElement?.IsOpen ?? false)
            {
                foreach (var kvp in App.Current.MediaElement.Metadata)
                {
                    if (kvp.Key.ToLowerInvariant().Equals("title"))
                    {
                        title = kvp.Value;
                        break;
                    }
                }
            }
            else if (App.MediaElement?.IsOpening ?? false)
            {
                state = "Opening . . .";
            }
            else
            {
                title = "(No media loaded)";
                state = "Ready";
            }

            WindowTitle = $"{title} - {state} - Unosquare FFME Play v{AssemblyVersion}";
        }
    }
}
