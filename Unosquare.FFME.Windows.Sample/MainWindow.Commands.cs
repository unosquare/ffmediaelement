namespace Unosquare.FFME.Windows.Sample
{
    using Kernel;
    using System;
    using System.Windows;

    public partial class MainWindow
    {
        private DelegateCommand m_OpenCommand = null;
        private DelegateCommand m_PauseCommand = null;
        private DelegateCommand m_PlayCommand = null;
        private DelegateCommand m_StopCommand = null;
        private DelegateCommand m_CloseCommand = null;
        private DelegateCommand m_ToggleFullscreenCommand = null;
        private DelegateCommand m_RemovePlaylistItemCommand = null;

        #region Properties: Commands

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
                    m_OpenCommand = new DelegateCommand(a =>
                    {
                        try
                        {
                            var target = default(Uri);
                            if (a is string && a != null)
                                target = new Uri(a as string);
                            else
                                target = new Uri(OpenFileTextBox.Text);

                            Media.Source = target;
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(
                                $"Media Failed: {ex.GetType()}\r\n{ex.Message}",
                                "MediaElement Error",
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
                    m_PauseCommand = new DelegateCommand(async o => { await Media.Pause(); });

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
                    m_PlayCommand = new DelegateCommand(async o => { await Media.Play(); });

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
                    m_StopCommand = new DelegateCommand(async o => { await Media.Stop(); });

                return m_StopCommand;
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
                        // Media.Dispose(); // Test the Dispose method uncommenting this line
                        await Media.Close();
                    });
                }

                return m_CloseCommand;
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
                        if (window.WindowStyle == WindowStyle.None)
                        {
                            PreviousWindowStatus.Apply(this);
                        }
                        else
                        {
                            PreviousWindowStatus.Capture(this);
                            WindowStyle = WindowStyle.None;
                            ResizeMode = ResizeMode.NoResize;
                            Topmost = true;
                            WindowState = WindowState.Normal;
                            WindowState = WindowState.Maximized;
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
                        var entry = arg as CustomPlaylistEntry;
                        if (entry == null) return;

                        PlaylistManager.RemoveEntry(entry.MediaUrl);
                        PlaylistManager.SaveEntries();
                    });
                }

                return m_RemovePlaylistItemCommand;
            }
        }

        #endregion
    }
}
