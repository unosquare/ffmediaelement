namespace Unosquare.FFME.Windows.Sample
{
    using Kernel;
    using Shared;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Media.Animation;
    using System.Windows.Threading;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        #region Fields

        private readonly Dictionary<string, Action> PropertyUpdaters;
        private readonly Dictionary<string, string[]> PropertyTriggers;
        private readonly ObservableCollection<string> HistoryItems = new ObservableCollection<string>();
        private readonly WindowStatus PreviousWindowStatus = new WindowStatus();

        private ConfigRoot Config;
        private DateTime LastMouseMoveTime;
        private Point LastMousePosition;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindow"/> class.
        /// </summary>
        public MainWindow()
        {
            // Define conditions for visibility properties
            PropertyUpdaters = new Dictionary<string, Action>
            {
                { nameof(IsMediaOpenVisibility), () => { IsMediaOpenVisibility = Media.IsOpen ? Visibility.Visible : Visibility.Hidden; } },
                { nameof(AudioControlVisibility), () => { AudioControlVisibility = Media.HasAudio ? Visibility.Visible : Visibility.Hidden; } },
                { nameof(IsAudioControlEnabled), () => { IsAudioControlEnabled = Media.HasAudio; } },
                { nameof(PauseButtonVisibility), () => { PauseButtonVisibility = Media.CanPause && Media.IsPlaying ? Visibility.Visible : Visibility.Collapsed; } },
                { nameof(PlayButtonVisibility), () => { PlayButtonVisibility = Media.IsOpen && Media.IsPlaying == false && Media.HasMediaEnded == false ? Visibility.Visible : Visibility.Collapsed; } },
                { nameof(StopButtonVisibility), () => { StopButtonVisibility = Media.IsOpen && (Media.HasMediaEnded || (Media.IsSeekable && Media.MediaState != MediaState.Stop)) ? Visibility.Visible : Visibility.Hidden; } },
                { nameof(CloseButtonVisibility), () => { CloseButtonVisibility = Media.IsOpen ? Visibility.Visible : Visibility.Hidden; } },
                { nameof(SeekBarVisibility), () => { SeekBarVisibility = Media.IsSeekable ? Visibility.Visible : Visibility.Hidden; } },
                { nameof(BufferingProgressVisibility), () => { BufferingProgressVisibility = Media.IsBuffering ? Visibility.Visible : Visibility.Hidden; } },
                { nameof(DownloadProgressVisibility), () => { DownloadProgressVisibility = Media.IsOpen && Media.HasMediaEnded == false && ((Media.DownloadProgress > 0d && Media.DownloadProgress < 0.95) || Media.IsLiveStream) ? Visibility.Visible : Visibility.Hidden; } },
                { nameof(OpenButtonVisibility), () => { OpenButtonVisibility = Media.IsOpening == false ? Visibility.Visible : Visibility.Hidden; } },
                { nameof(IsSpeedRatioEnabled), () => { IsSpeedRatioEnabled = Media.IsOpen && Media.IsSeekable; } },
                { nameof(WindowTitle), () => { UpdateWindowTitle(); } }
            };

            // Define triggering properties for the updaters above.
            PropertyTriggers = new Dictionary<string, string[]>
            {
                { nameof(Media.IsOpen), PropertyUpdaters.Keys.ToArray() },
                { nameof(Media.IsOpening), PropertyUpdaters.Keys.ToArray() },
                { nameof(Media.MediaState), PropertyUpdaters.Keys.ToArray() },
                { nameof(Media.HasMediaEnded), PropertyUpdaters.Keys.ToArray() },
                { nameof(Media.DownloadProgress), new[] { nameof(DownloadProgressVisibility) } },
                { nameof(Media.IsBuffering), new[] { nameof(BufferingProgressVisibility) } },
            };

            // Load up WPF resources
            InitializeComponent();

            // Load simple config.
            Config = ConfigRoot.Load();
            RefreshHistoryItems();

            // Change the default location of the ffmpeg binaries
            // You can get the binaries here: http://ffmpeg.zeranoe.com/builds/win32/shared/ffmpeg-3.4-win32-shared.zip
            Unosquare.FFME.MediaElement.FFmpegDirectory = Config.FFmpegPath;

            // You can pick which FFmpeg binaries are loaded. See issue #28
            // Full Features is already the default.
            Unosquare.FFME.MediaElement.FFmpegLoadModeFlags = FFmpegLoadMode.FullFeatures;

            // Setup the UI
            // ConsoleManager.ShowConsole();
            InitializeMediaEvents();
            InitializeInputEvents();
            InitializeMainWindow();
            UpdateWindowTitle();
        }

        #endregion

        #region Events

        /// <summary>
        /// Occurs when a property changes its value.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        #region Methods

        /// <summary>
        /// Initializes the media events.
        /// </summary>
        private void InitializeMediaEvents()
        {
            // Global FFmpeg message handler
            FFME.MediaElement.FFmpegMessageLogged += MediaElement_FFmpegMessageLogged;

            // MediaElement event bindings
            Media.PositionChanged += Media_PositionChanged;
            Media.MediaOpened += Media_MediaOpened;
            Media.MediaOpening += Media_MediaOpening;
            Media.MediaFailed += Media_MediaFailed;
            Media.MessageLogged += Media_MessageLogged;
            Media.PropertyChanged += Media_PropertyChanged;
            BindRenderingEvents();
        }

        /// <summary>
        /// Initializes the main window.
        /// </summary>
        private void InitializeMainWindow()
        {
            Loaded += MainWindow_Loaded;
            UrlTextBox.Text = HistoryItems.Count > 0 ? HistoryItems.First() : string.Empty;

            /*
             * Media.ScrubbingEnabled = false;
             * Media.LoadedBehavior = MediaState.Pause;
             */

            var args = Environment.GetCommandLineArgs();
            if (args != null && args.Length > 1)
            {
                UrlTextBox.Text = args[1].Trim();
                OpenCommand.Execute();
            }

            OpenMediaPopup.Opened += (s, e) =>
            {
                if (UrlTextBox.ItemsSource == null)
                    UrlTextBox.ItemsSource = HistoryItems;

                if (HistoryItems.Count > 0)
                    UrlTextBox.Text = HistoryItems.First();

                UrlTextBox.Focus();
            };

            UrlTextBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    OpenCommand.Execute();
                    e.Handled = true;
                }
            };
        }

        /// <summary>
        /// Initializes the mouse events for the window.
        /// </summary>
        private void InitializeInputEvents()
        {
            #region Allow Keyboard Control

            var togglePlayPauseKeys = new[] { Key.Play, Key.MediaPlayPause, Key.Space };

            window.PreviewKeyDown += (s, e) =>
            {
                if (e.OriginalSource is TextBox) return;

                // Pause
                if (togglePlayPauseKeys.Contains(e.Key) && Media.IsPlaying)
                {
                    PauseCommand.Execute();
                    return;
                }

                // Play
                if (togglePlayPauseKeys.Contains(e.Key) && Media.IsPlaying == false)
                {
                    PlayCommand.Execute();
                    return;
                }

                // Seek to left
                if (e.Key == Key.Left)
                {
                    if (Media.IsPlaying) PauseCommand.Execute();
                    Media.Position -= Media.FrameStepDuration;
                }

                // Seek to right
                if (e.Key == Key.Right)
                {
                    if (Media.IsPlaying) PauseCommand.Execute();
                    Media.Position += Media.FrameStepDuration;
                }

                // Volume Up
                if (e.Key == Key.Add || e.Key == Key.VolumeUp)
                {
                    Media.Volume += 0.05;
                    return;
                }

                // Volume Down
                if (e.Key == Key.Subtract || e.Key == Key.VolumeDown)
                {
                    Media.Volume -= 0.05;
                    return;
                }

                // Mute/Unmute
                if (e.Key == Key.M || e.Key == Key.VolumeMute)
                {
                    Media.IsMuted = !Media.IsMuted;
                    return;
                }

                // Increase speed
                if (e.Key == Key.Up)
                {
                    Media.SpeedRatio += 0.05;
                    return;
                }

                // Decrease speed
                if (e.Key == Key.Down)
                {
                    Media.SpeedRatio -= 0.05;
                    return;
                }

                // Reset changes
                if (e.Key == Key.R)
                {
                    Media.SpeedRatio = 1.0;
                    Media.Volume = 1.0;
                    Media.Balance = 0;
                    Media.IsMuted = false;
                }
            };

            #endregion

            #region Toggle Fullscreen with Double Click

            Media.PreviewMouseDoubleClick += (s, e) =>
            {
                if (s != Media) return;
                e.Handled = true;
                ToggleFullscreenCommand.Execute();
            };

            #endregion

            #region Exit fullscreen with Escape key

            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape && WindowStyle == WindowStyle.None)
                {
                    e.Handled = true;
                    ToggleFullscreenCommand.Execute();
                }
            };

            #endregion

            #region Handle Zooming with Mouse Wheel

            MouseWheel += (s, e) =>
            {
                if (Media.IsOpen == false || Media.IsOpening)
                    return;

                var delta = (e.Delta / 2000d).ToMultipleOf(0.05d);
                MediaZoom = Math.Round(MediaZoom + delta, 2);
            };

            UrlTextBox.PreviewMouseWheel += (s, e) =>
            {
                e.Handled = true;
            };

            #endregion

            #region Handle Play Pause with Mouse Clicks

            /*
            Media.PreviewMouseDown += (s, e) =>
            {
                if (s != Media) return;
                if (Media.IsOpen == false || Media.CanPause == false) return;
                if (Media.IsPlaying)
                    PauseCommand.Execute();
                else
                    PlayCommand.Execute();
            };
            */

            #endregion

            #region Mouse Move Handling (Hide and Show Controls)

            LastMouseMoveTime = DateTime.UtcNow;

            MouseMove += (s, e) =>
            {
                var currentPosition = e.GetPosition(window);
                if (currentPosition.X != LastMousePosition.X || currentPosition.Y != LastMousePosition.Y)
                    LastMouseMoveTime = DateTime.UtcNow;

                LastMousePosition = currentPosition;
            };

            MouseLeave += (s, e) =>
            {
                LastMouseMoveTime = DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(10));
            };

            var mouseMoveTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(150),
                IsEnabled = true
            };

            mouseMoveTimer.Tick += (s, e) =>
            {
                var elapsedSinceMouseMove = DateTime.UtcNow.Subtract(LastMouseMoveTime);
                if (elapsedSinceMouseMove.TotalMilliseconds >= 3000 && Media.IsOpen && Controls.IsMouseOver == false
                    && OpenMediaPopup.IsOpen == false && DebugWindowPopup.IsOpen == false && SoundMenuPopup.IsOpen == false)
                {
                    if (Controls.Opacity != 0d)
                    {
                        Cursor = System.Windows.Input.Cursors.None;
                        var sb = Player.FindResource("HideControlOpacity") as Storyboard;
                        Storyboard.SetTarget(sb, Controls);
                        sb.Begin();
                    }
                }
                else
                {
                    if (Controls.Opacity != 1d)
                    {
                        Cursor = System.Windows.Input.Cursors.Arrow;
                        var sb = Player.FindResource("ShowControlOpacity") as Storyboard;
                        Storyboard.SetTarget(sb, Controls);
                        sb.Begin();
                    }
                }
            };

            mouseMoveTimer.Start();

            #endregion

        }

        /// <summary>
        /// Refreshes the history items.
        /// </summary>
        private void RefreshHistoryItems()
        {
            HistoryItems.Clear();
            for (var entryIndex = Config.HistoryEntries.Count - 1; entryIndex >= 0; entryIndex--)
                HistoryItems.Add(Config.HistoryEntries[entryIndex]);
        }

        /// <summary>
        /// Updates the window title according to the current state.
        /// </summary>
        private void UpdateWindowTitle()
        {
            var v = typeof(MainWindow).Assembly.GetName().Version;
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

            window.Title = $"{title} - {state} - Unosquare FFME Play v{v.Major}.{v.Minor}.{v.Build}.{v.Revision}";
        }

        #endregion
    }
}
