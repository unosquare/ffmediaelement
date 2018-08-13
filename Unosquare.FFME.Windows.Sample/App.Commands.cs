namespace Unosquare.FFME.Windows.Sample
{
    using Foundation;
    using System;
    using System.Windows;

    public partial class App
    {
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

            #region Properties (Commands)

            /// <summary>
            /// Gets the open command.
            /// </summary>
            /// <value>
            /// The open command.
            /// </value>
            public DelegateCommand OpenCommand
            {
                get
                {
                    if (m_OpenCommand == null)
                    {
                        m_OpenCommand = new DelegateCommand(async a =>
                        {
                            try
                            {
                                var uriString = a as string;
                                if (string.IsNullOrWhiteSpace(uriString))
                                    return;

                                // Current.MediaElement.Source = new Uri(uriString); // you can also set the source to the Uri to open
                                var target = new Uri(uriString);
                                if (target.ToString().StartsWith(FileInputStream.Scheme))
                                {
                                    await Current.MediaElement.Open(new FileInputStream(target.LocalPath));
                                }
                                else
                                {
                                    await Current.MediaElement.Open(target);
                                }
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show(
                                    Current.MainWindow,
                                    $"Media Failed: {ex.GetType()}\r\n{ex.Message}",
                                    $"{nameof(MediaElement)} Error",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error,
                                    MessageBoxResult.OK);
                            }
                        });
                    }

                    return m_OpenCommand;
                }
            }

            /// <summary>
            /// Gets the close command.
            /// </summary>
            /// <value>
            /// The close command.
            /// </value>
            public DelegateCommand CloseCommand
            {
                get
                {
                    if (m_CloseCommand == null)
                    {
                        m_CloseCommand = new DelegateCommand(async (o) =>
                        {
                            // Current.MediaElement.Dispose(); // Test the Dispose method uncommenting this line
                            // Current.MediaElement.Source = null; // You can also set the source to null to close.
                            await Current.MediaElement.Close();
                        });
                    }

                    return m_CloseCommand;
                }
            }

            /// <summary>
            /// Gets the pause command.
            /// </summary>
            /// <value>
            /// The pause command.
            /// </value>
            public DelegateCommand PauseCommand
            {
                get
                {
                    if (m_PauseCommand == null)
                        m_PauseCommand = new DelegateCommand(async o => { await Current.MediaElement.Pause(); });

                    return m_PauseCommand;
                }
            }

            /// <summary>
            /// Gets the play command.
            /// </summary>
            /// <value>
            /// The play command.
            /// </value>
            public DelegateCommand PlayCommand
            {
                get
                {
                    if (m_PlayCommand == null)
                        m_PlayCommand = new DelegateCommand(async o => { await Current.MediaElement.Play(); });

                    return m_PlayCommand;
                }
            }

            /// <summary>
            /// Gets the stop command.
            /// </summary>
            /// <value>
            /// The stop command.
            /// </value>
            public DelegateCommand StopCommand
            {
                get
                {
                    if (m_StopCommand == null)
                        m_StopCommand = new DelegateCommand(async o => { await Current.MediaElement.Stop(); });

                    return m_StopCommand;
                }
            }

            /// <summary>
            /// Gets the toggle fullscreen command.
            /// </summary>
            /// <value>
            /// The toggle fullscreen command.
            /// </value>
            public DelegateCommand ToggleFullscreenCommand
            {
                get
                {
                    if (m_ToggleFullscreenCommand == null)
                    {
                        m_ToggleFullscreenCommand = new DelegateCommand(o =>
                        {
                            // If we are already in fullscreen, go back to normal
                            if (Current.MainWindow.WindowStyle == WindowStyle.None)
                            {
                                PreviousWindowStatus.Apply(Current.MainWindow);
                                WindowStatus.EnableDisplayTimeout();
                            }
                            else
                            {
                                PreviousWindowStatus.Capture(Current.MainWindow);
                                Current.MainWindow.WindowStyle = WindowStyle.None;
                                Current.MainWindow.ResizeMode = ResizeMode.NoResize;
                                Current.MainWindow.Topmost = true;
                                Current.MainWindow.WindowState = WindowState.Normal;
                                Current.MainWindow.WindowState = WindowState.Maximized;
                                WindowStatus.DisableDisplayTimeout();
                            }
                        });
                    }

                    return m_ToggleFullscreenCommand;
                }
            }

            /// <summary>
            /// Gets the remove playlist item command.
            /// </summary>
            /// <value>
            /// The remove playlist item command.
            /// </value>
            public DelegateCommand RemovePlaylistItemCommand
            {
                get
                {
                    if (m_RemovePlaylistItemCommand == null)
                    {
                        m_RemovePlaylistItemCommand = new DelegateCommand((arg) =>
                        {
                            if (arg is CustomPlaylistEntry == false) return;
                            var entry = arg as CustomPlaylistEntry;

                            Current.ViewModel.Playlist.Entries.RemoveEntryByMediaUrl(entry.MediaUrl);
                            Current.ViewModel.Playlist.Entries.SaveEntries();
                        });
                    }

                    return m_RemovePlaylistItemCommand;
                }
            }

            #endregion
        }
    }
}
