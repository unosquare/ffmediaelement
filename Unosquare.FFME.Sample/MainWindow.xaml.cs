namespace Unosquare.FFME.Sample
{
    using System;
    using System.Diagnostics;
    using System.Windows;
    using System.Windows.Controls;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Commands

        private DelegateCommand m_OpenCommand = null;
        private DelegateCommand m_PauseCommand = null;
        private DelegateCommand m_PlayCommand = null;
        private DelegateCommand m_StopCommand = null;
        private DelegateCommand m_CloseCommand = null;

        public DelegateCommand OpenCommand
        {
            get
            {
                if (m_OpenCommand == null)
                    m_OpenCommand = new DelegateCommand((a) =>
                    {
                        Media.Source = new Uri(UrlTextBox.Text);
                        window.Title = Media.Source.ToString();
                    }, null);

                return m_OpenCommand;
            }
        }

        public DelegateCommand PauseCommand
        {
            get
            {
                if (m_PauseCommand == null)
                    m_PauseCommand = new DelegateCommand((o) => { Media.Pause(); }, (o) => { return Media.IsPlaying; });

                return m_PauseCommand;
            }
        }

        public DelegateCommand PlayCommand
        {
            get
            {
                if (m_PlayCommand == null)
                    m_PlayCommand = new DelegateCommand((o) => { Media.Play(); }, (o) => { return Media.IsPlaying == false; });

                return m_PlayCommand;
            }
        }

        public DelegateCommand StopCommand
        {
            get
            {
                if (m_StopCommand == null)
                    m_StopCommand = new DelegateCommand((o) => { Media.Stop(); }, (o) =>
                    {
                        return Media.MediaState != MediaState.Close
                            && Media.MediaState != MediaState.Manual;
                    });

                return m_StopCommand;
            }
        }

        public DelegateCommand CloseCommand
        {
            get
            {
                if (m_CloseCommand == null)
                    m_CloseCommand = new DelegateCommand((o) => { Media.Close(); }, (o) =>
                    {
                        return Media.IsOpen;
                    });

                return m_CloseCommand;
            }
        }

        #endregion

        public MainWindow()
        {
            // Change the default location of the ffmpeg binaries
            // You can get the binaries here: http://ffmpeg.zeranoe.com/builds/win32/shared/ffmpeg-3.2.4-win32-shared.zip
            Unosquare.FFME.MediaElement.FFmpegDirectory = @"C:\ffmpeg";
            //ConsoleManager.ShowConsole();
            InitializeComponent();
            UrlTextBox.Text = TestInputs.MatroskaLocalFile;

            Media.MediaOpening += Media_MediaOpening;
            Media.MediaFailed += Media_MediaFailed;

            //e.Options.IsAudioDisabled = true;
            Media.LogMessageCallback = new Action<MediaLogMessageType, string>((t, m) =>
            {
                if (t == MediaLogMessageType.Trace) return;

                Debug.WriteLine($"{t} - {m}");
                //Terminal.Log(m, nameof(MediaElement), (LogMessageType)t);
            });
        }

        private void Media_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            MessageBox.Show($"Media Failed: {e.ErrorException.GetType()}\r\n{e.ErrorException.Message}",
                "MediaElement Error", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
        }

        private void Media_MediaOpening(object sender, MediaOpeningRoutedEventArgs e)
        {

            // The yadif filter deinterlaces the video
            if (UrlTextBox.Text.StartsWith("udp://"))
            {
                e.Options.VideoFilter = "yadif";
                //e.Options.ProbeSize = 32;
            }
                

        }

        #region Seek and Resume Behavior

        private bool WasPlaying = false;

        /// <summary>
        /// Handles the MouseDown event of the PositionSlider control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.Input.MouseButtonEventArgs"/> instance containing the event data.</param>
        private void PositionSlider_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            WasPlaying = Media.IsPlaying;
        }

        /// <summary>
        /// Handles the MouseUp event of the PositionSlider control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.Input.MouseButtonEventArgs"/> instance containing the event data.</param>
        private void PositionSlider_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (WasPlaying) Media.Play();
        }

        #endregion
    }
}
