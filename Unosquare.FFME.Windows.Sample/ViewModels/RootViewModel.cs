namespace Unosquare.FFME.Windows.Sample.ViewModels
{
    using Foundation;

    /// <summary>
    /// Represents the application-wide view model
    /// </summary>
    /// <seealso cref="ViewModelBase" />
    public class RootViewModel : ViewModelBase
    {
        private readonly string AssemblyVersion = typeof(RootViewModel).Assembly.GetName().Version.ToString();

        /// <summary>
        /// Initializes a new instance of the <see cref="RootViewModel"/> class.
        /// </summary>
        public RootViewModel()
        {
            // Attached ViewModel Inistialization
            Playlist = new PlaylistViewModel(this);
            Controller = new ControllerViewModel(this);
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
        /// Gets or sets the window title.
        /// </summary>>
        public string WindowTitle { get; set; } = string.Empty;

        /// <summary>
        /// Updates the window title according to the current state.
        /// </summary>
        private void UpdateWindowTitle()
        {
            var title = App.Current.MediaElement.Source?.ToString() ?? "(No media loaded)";
            var state = App.Current.MediaElement?.MediaState.ToString();

            if (App.Current.MediaElement.IsOpen)
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
            else if (App.Current.MediaElement.IsOpening)
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
