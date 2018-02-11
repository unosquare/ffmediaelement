namespace Unosquare.FFME.Windows.Sample.ViewModels
{
    using Foundation;
    using System;
    using System.Windows;
    using System.Windows.Media;
    using Kernel;

    /// <summary>
    /// Represents the application-wide view model
    /// </summary>
    /// <seealso cref="ViewModelBase" />
    public class RootViewModel : ViewModelBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RootViewModel"/> class.
        /// </summary>
        public RootViewModel()
        {
            // placeholder
            AssemblyVersion = typeof(RootViewModel).Assembly.GetName().Version.ToString();
        }

        /// <summary>
        /// Gets the media.
        /// </summary>
        public MediaElement Media { get; private set; }

        #region Properties: Notification

        /// <summary>
        /// Gets the assembly version string.
        /// </summary>
        public string AssemblyVersion { get; }

        /// <summary>
        /// Gets or sets the playlist search string.
        /// </summary>
        public string PlaylistSearchString
        {
            get => PlaylistManager.SearchString;
            set => PlaylistManager.SearchString = value;
        }

        /// <summary>
        /// Gets or sets the window title.
        /// </summary>>
        public string WindowTitle { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether this instance is playlist enabled.
        /// </summary>
        public bool IsPlaylistEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether this instance has taken thumbnail.
        /// </summary>
        public bool HasTakenThumbnail { get; set; } = false;

        /// <summary>
        /// Gets or sets the media zoom.
        /// </summary>
        private double MediaZoom
        {
            get
            {
                var transform = Media.RenderTransform as ScaleTransform;
                return transform?.ScaleX ?? 1d;
            }
            set
            {
                var transform = Media.RenderTransform as ScaleTransform;
                if (transform == null)
                {
                    transform = new ScaleTransform(1, 1);
                    Media.RenderTransformOrigin = new Point(0.5, 0.5);
                    Media.RenderTransform = transform;
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
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Binds to media element.
        /// </summary>
        /// <param name="media">The media.</param>
        public void BindToMediaElement(MediaElement media)
        {
            if (Media != null)
                throw new InvalidOperationException("Cannot bind to a new Media object.");

            Media = media;

            this.Watch(Media, nameof(Media.MediaState))
                .OnChange((s, e) => UpdateWindowTitle())
                .Notify(nameof(WindowTitle));
        }

        /// <summary>
        /// Updates the window title according to the current state.
        /// </summary>
        private void UpdateWindowTitle()
        {
            var title = Media.Source?.ToString() ?? "(No media loaded)";
            var state = Media?.MediaState.ToString();

            if (Media.IsOpen)
            {
                foreach (var kvp in Media.Metadata)
                {
                    if (kvp.Key.ToLowerInvariant().Equals("title"))
                    {
                        title = kvp.Value;
                        break;
                    }
                }
            }
            else if (Media.IsOpening)
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

        #endregion
    }
}
