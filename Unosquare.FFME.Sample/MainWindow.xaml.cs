namespace Unosquare.FFME.Sample
{
    using System;
    using System.Diagnostics;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using System.Windows.Threading;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        #region State Variables

        private readonly WindowStatus PreviousWindowStatus = new WindowStatus();
        private DateTime LastMouseMove;
        private Point LastMousePosition;

        #endregion

        #region Window Status

        private class WindowStatus
        {
            public WindowState WindowState { get; set; }
            public double Top { get; set; }
            public double Left { get; set; }
            public WindowStyle WindowStyle { get; set; }
            public bool Topmost { get; set; }
            public ResizeMode ResizeMode { get; set; }

            public void Capture(Window w)
            {
                WindowState = w.WindowState;
                Top = w.Top;
                Left = w.Left;
                WindowStyle = w.WindowStyle;
                Topmost = w.Topmost;
                ResizeMode = w.ResizeMode;
            }

            public void Apply(Window w)
            {
                w.WindowState = WindowState;
                w.Top = Top;
                w.Left = Left;
                w.WindowStyle = WindowStyle;
                w.Topmost = Topmost;
                w.ResizeMode = ResizeMode;
            }

        }

        #endregion

        #region Commands

        private DelegateCommand m_OpenCommand = null;
        private DelegateCommand m_PauseCommand = null;
        private DelegateCommand m_PlayCommand = null;
        private DelegateCommand m_StopCommand = null;
        private DelegateCommand m_CloseCommand = null;
        private DelegateCommand m_ToggleFullscreenCommand = null;

        public DelegateCommand OpenCommand
        {
            get
            {
                if (m_OpenCommand == null)
                    m_OpenCommand = new DelegateCommand((a) =>
                    {
                        Media.Source = new Uri(UrlTextBox.Text);
                        window.Title = Media.Source.ToString();
                        OpenMediaPopup.IsOpen = false;
                    }, (o) => { return !Media.IsOpening; });

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

        public DelegateCommand ToggleFullscreenCommand
        {
            get
            {
                if (m_ToggleFullscreenCommand == null)
                    m_ToggleFullscreenCommand = new DelegateCommand((o) =>
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
                    }, null);

                return m_ToggleFullscreenCommand;
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
            InitializeMediaEvents();
            InitializeMouseEvents();

            Loaded += (s, e) =>
            {
                var presenter = VisualTreeHelper.GetParent(Content as UIElement) as ContentPresenter;
                presenter.MinWidth = 1280;
                presenter.MinHeight = 720;

                SizeToContent = SizeToContent.WidthAndHeight;
                MinWidth = ActualWidth;
                MinHeight = ActualHeight;
                SizeToContent = SizeToContent.Manual;
            };

            UrlTextBox.Text = TestInputs.MatroskaLocalFile;

            var args = Environment.GetCommandLineArgs();
            if (args != null && args.Length > 1)
            {
                UrlTextBox.Text = args[1].Trim();
                OpenCommand.Execute();
            }
        }

        private void InitializeMediaEvents()
        {
            Media.MediaOpening += Media_MediaOpening;
            Media.MediaFailed += Media_MediaFailed;
            Media.MessageLogged += Media_MessageLogged;
            Unosquare.FFME.MediaElement.FFmpegMessageLogged += MediaElement_FFmpegMessageLogged;
        }

        private void InitializeMouseEvents()
        {
            LastMouseMove = DateTime.UtcNow;
            MouseMove += (s, e) =>
            {
                var currentPosition = e.GetPosition(window);
                if (currentPosition.X != LastMousePosition.X || currentPosition.Y != LastMousePosition.Y)
                    LastMouseMove = DateTime.UtcNow;

                LastMousePosition = currentPosition;
            };

            var mouseMoveTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(150)
            };
            mouseMoveTimer.Tick += (s, e) =>
            {
                var elapsedSinceMouseMove = DateTime.UtcNow.Subtract(LastMouseMove);
                if (elapsedSinceMouseMove.TotalMilliseconds >= 5000 && Media.IsPlaying)
                    Controls.Visibility = Visibility.Hidden;
                else
                    Controls.Visibility = Visibility.Visible;
            };

            mouseMoveTimer.IsEnabled = true;
            mouseMoveTimer.Start();
        }

        private void Media_MessageLogged(object sender, MediaLogMessagEventArgs e)
        {
            if (e.MessageType == MediaLogMessageType.Trace) return;
            Debug.WriteLine($"{e.MessageType,10} - {e.Message}");
        }

        private void MediaElement_FFmpegMessageLogged(object sender, MediaLogMessagEventArgs e)
        {
            Debug.WriteLine($"{e.MessageType,10} - {e.Message}");
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
            Media.Pause();

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

        private void DebugWindowThumb_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            DebugWindowPopup.HorizontalOffset += e.HorizontalChange;
            DebugWindowPopup.VerticalOffset += e.VerticalChange;
        }

        private void DebugWindowPopup_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            DebugWindowThumb.RaiseEvent(e);
        }
    }
}
