namespace Unosquare.FFME.Windows.Sample
{
    using Foundation;
    using System;
    using System.Windows;

    /// <summary>
    /// Represents the Application-Wide Commands
    /// </summary>
    public class AppCommands
    {
        #region Private State

        private readonly WindowStatus PreviousWindowStatus = new WindowStatus();

        private DelegateCommand m_OpenCommand;
        private DelegateCommand m_PauseCommand;
        private DelegateCommand m_PlayCommand;
        private DelegateCommand m_StopCommand;
        private DelegateCommand m_CloseCommand;
        private DelegateCommand m_ToggleFullscreenCommand;
        private DelegateCommand m_RemovePlaylistItemCommand;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="AppCommands"/> class.
        /// </summary>
        internal AppCommands()
        {
            // placeholder
        }

        #endregion

        /// <summary>
        /// Gets the open command.
        /// </summary>
        /// <value>
        /// The open command.
        /// </value>
        public DelegateCommand OpenCommand => m_OpenCommand ??
            (m_OpenCommand = new DelegateCommand(async a =>
            {
                try
                {
                    if (a is string == false) return;
                    var uriString = (string)a;
                    if (string.IsNullOrWhiteSpace(uriString))
                        return;

                    // Current.MediaElement.Source = new Uri(uriString); // you can also set the source to the Uri to open
                    var target = new Uri(uriString);
                    if (target.ToString().StartsWith(FileInputStream.Scheme, StringComparison.OrdinalIgnoreCase))
                        await App.Current.MediaElement.Open(new FileInputStream(target.LocalPath));
                    else
                        await App.Current.MediaElement.Open(target);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        App.Current.MainWindow,
                        $"Media Failed: {ex.GetType()}\r\n{ex.Message}",
                        $"{nameof(MediaElement)} Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error,
                        MessageBoxResult.OK);
                }
            }));

        /// <summary>
        /// Gets the close command.
        /// </summary>
        /// <value>
        /// The close command.
        /// </value>
        public DelegateCommand CloseCommand => m_CloseCommand ??
            (m_CloseCommand = new DelegateCommand(async o =>
            {
                // Current.MediaElement.Dispose(); // Test the Dispose method uncommenting this line
                // Current.MediaElement.Source = null; // You can also set the source to null to close.
                await App.Current.MediaElement.Close();
            }));

        /// <summary>
        /// Gets the pause command.
        /// </summary>
        /// <value>
        /// The pause command.
        /// </value>
        public DelegateCommand PauseCommand => m_PauseCommand ??
            (m_PauseCommand = new DelegateCommand(async o =>
            {
                await App.Current.MediaElement.Pause();
            }));

        /// <summary>
        /// Gets the play command.
        /// </summary>
        /// <value>
        /// The play command.
        /// </value>
        public DelegateCommand PlayCommand => m_PlayCommand ??
            (m_PlayCommand = new DelegateCommand(async o =>
            {
                // await Current.MediaElement.Seek(TimeSpan.Zero)
                await App.Current.MediaElement.Play();
            }));

        /// <summary>
        /// Gets the stop command.
        /// </summary>
        /// <value>
        /// The stop command.
        /// </value>
        public DelegateCommand StopCommand => m_StopCommand ??
            (m_StopCommand = new DelegateCommand(async o =>
            {
                await App.Current.MediaElement.Stop();
            }));

        /// <summary>
        /// Gets the toggle fullscreen command.
        /// </summary>
        /// <value>
        /// The toggle fullscreen command.
        /// </value>
        public DelegateCommand ToggleFullscreenCommand => m_ToggleFullscreenCommand ??
            (m_ToggleFullscreenCommand = new DelegateCommand(o =>
            {
                // If we are already in fullscreen, go back to normal
                if (App.Current.MainWindow.WindowStyle == WindowStyle.None)
                {
                    PreviousWindowStatus.Apply(App.Current.MainWindow);
                    WindowStatus.EnableDisplayTimeout();
                }
                else
                {
                    PreviousWindowStatus.Capture(App.Current.MainWindow);
                    App.Current.MainWindow.WindowStyle = WindowStyle.None;
                    App.Current.MainWindow.ResizeMode = ResizeMode.NoResize;
                    App.Current.MainWindow.Topmost = true;
                    App.Current.MainWindow.WindowState = WindowState.Normal;
                    App.Current.MainWindow.WindowState = WindowState.Maximized;
                    WindowStatus.DisableDisplayTimeout();
                }
            }));

        /// <summary>
        /// Gets the remove playlist item command.
        /// </summary>
        /// <value>
        /// The remove playlist item command.
        /// </value>
        public DelegateCommand RemovePlaylistItemCommand => m_RemovePlaylistItemCommand ??
            (m_RemovePlaylistItemCommand = new DelegateCommand(arg =>
            {
                if (arg is CustomPlaylistEntry == false) return;
                var entry = (CustomPlaylistEntry)arg;

                App.Current.ViewModel.Playlist.Entries.RemoveEntryByMediaSource(entry.MediaSource);
                App.Current.ViewModel.Playlist.Entries.SaveEntries();
            }));
    }
}
