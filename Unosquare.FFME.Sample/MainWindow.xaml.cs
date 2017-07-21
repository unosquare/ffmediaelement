namespace Unosquare.FFME.Sample
{
    using Config;
    using FFmpeg.AutoGen;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Media;
    using System.Windows.Media.Animation;
    using System.Windows.Threading;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {

        #region State Variables, Property Backing and Events

        private readonly Dictionary<string, Action> PropertyUpdaters;
        private readonly Dictionary<string, string[]> PropertyTriggers;
        private ConfigRoot Config;
        private readonly ObservableCollection<string> HistoryItems = new ObservableCollection<string>();

        /// <summary>
        /// Occurs when a property changes its value.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        private readonly WindowStatus PreviousWindowStatus = new WindowStatus();
        private DateTime LastMouseMoveTime;
        private Point LastMousePosition;
        private bool WasPlaying = false;

        private DelegateCommand m_OpenCommand = null;
        private DelegateCommand m_PauseCommand = null;
        private DelegateCommand m_PlayCommand = null;
        private DelegateCommand m_StopCommand = null;
        private DelegateCommand m_CloseCommand = null;
        private DelegateCommand m_ToggleFullscreenCommand = null;

        #endregion

        #region Commands

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
                    m_OpenCommand = new DelegateCommand((a) =>
                    {
                        Media.Source = new Uri(UrlTextBox.Text);
                        OpenMediaPopup.IsOpen = false;
                    }, null);

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
                    m_PauseCommand = new DelegateCommand((o) => { Media.Pause(); }, null);

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
                    m_PlayCommand = new DelegateCommand((o) => { Media.Play(); }, null);

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
                    m_StopCommand = new DelegateCommand((o) => { Media.Stop(); }, null);

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
                    m_CloseCommand = new DelegateCommand((o) => { Media.Close(); }, null);

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

        #region UI Notification Properties

        /// <summary>
        /// Gets or sets the window title.
        /// </summary>
        /// <value>
        /// The window title.
        /// </value>
        public string WindowTitle { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the is media open visibility.
        /// </summary>
        /// <value>
        /// The is media open visibility.
        /// </value>
        public Visibility IsMediaOpenVisibility { get; set; } = Visibility.Visible;

        /// <summary>
        /// Gets or sets a value indicating whether this instance is audio control enabled.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is audio control enabled; otherwise, <c>false</c>.
        /// </value>
        public bool IsAudioControlEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether this instance is speed ratio enabled.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is speed ratio enabled; otherwise, <c>false</c>.
        /// </value>
        public bool IsSpeedRatioEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the audio control visibility.
        /// </summary>
        /// <value>
        /// The audio control visibility.
        /// </value>
        public Visibility AudioControlVisibility { get; set; } = Visibility.Visible;

        /// <summary>
        /// Gets or sets the pause button visibility.
        /// </summary>
        /// <value>
        /// The pause button visibility.
        /// </value>
        public Visibility PauseButtonVisibility { get; set; } = Visibility.Visible;

        /// <summary>
        /// Gets or sets the play button visibility.
        /// </summary>
        /// <value>
        /// The play button visibility.
        /// </value>
        public Visibility PlayButtonVisibility { get; set; } = Visibility.Visible;

        /// <summary>
        /// Gets or sets the stop button visibility.
        /// </summary>
        /// <value>
        /// The stop button visibility.
        /// </value>
        public Visibility StopButtonVisibility { get; set; } = Visibility.Visible;

        /// <summary>
        /// Gets or sets the close button visibility.
        /// </summary>
        /// <value>
        /// The close button visibility.
        /// </value>
        public Visibility CloseButtonVisibility { get; set; } = Visibility.Visible;

        /// <summary>
        /// Gets or sets the open button visibility.
        /// </summary>
        /// <value>
        /// The open button visibility.
        /// </value>
        public Visibility OpenButtonVisibility { get; set; } = Visibility.Visible;

        /// <summary>
        /// Gets or sets the seek bar visibility.
        /// </summary>
        /// <value>
        /// The seek bar visibility.
        /// </value>
        public Visibility SeekBarVisibility { get; set; } = Visibility.Visible;

        /// <summary>
        /// Gets or sets the buffering progress visibility.
        /// </summary>
        /// <value>
        /// The buffering progress visibility.
        /// </value>
        public Visibility BufferingProgressVisibility { get; set; } = Visibility.Visible;

        /// <summary>
        /// Gets or sets the download progress visibility.
        /// </summary>
        /// <value>
        /// The download progress visibility.
        /// </value>
        public Visibility DownloadProgressVisibility { get; set; } = Visibility.Visible;

        #endregion

        #region Constructor and Initialization

        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindow"/> class.
        /// </summary>
        public MainWindow()
        {

            PropertyUpdaters = new Dictionary<string, Action>
            {
                { nameof(IsMediaOpenVisibility), () => { IsMediaOpenVisibility = Media.IsOpen ? Visibility.Visible : Visibility.Hidden; } },
                { nameof(AudioControlVisibility), () => { AudioControlVisibility = Media.HasAudio ? Visibility.Visible : Visibility.Hidden; } },
                { nameof(IsAudioControlEnabled), () => { IsAudioControlEnabled = Media.HasAudio; } },
                { nameof(PauseButtonVisibility), () => { PauseButtonVisibility = Media.CanPause && Media.IsPlaying ? Visibility.Visible : Visibility.Collapsed; } },
                { nameof(PlayButtonVisibility), () => { PlayButtonVisibility = Media.IsOpen && Media.IsPlaying == false && Media.HasMediaEnded == false ? Visibility.Visible : Visibility.Collapsed; } },
                { nameof(StopButtonVisibility), () => { StopButtonVisibility = Media.IsOpen && Media.IsSeekable && Media.MediaState != MediaState.Stop ? Visibility.Visible : Visibility.Hidden; } },
                { nameof(CloseButtonVisibility), () => { CloseButtonVisibility = Media.IsOpen ? Visibility.Visible : Visibility.Hidden; } },
                { nameof(SeekBarVisibility), () => { SeekBarVisibility = Media.IsSeekable ? Visibility.Visible : Visibility.Hidden; } },
                { nameof(BufferingProgressVisibility), () => { BufferingProgressVisibility = Media.IsBuffering ? Visibility.Visible : Visibility.Hidden; } },
                { nameof(DownloadProgressVisibility), () => { DownloadProgressVisibility = Media.IsOpen && Media.HasMediaEnded == false  && (Media.DownloadProgress < 0.95 || Media.IsLiveStream) ? Visibility.Visible : Visibility.Hidden; } },
                { nameof(OpenButtonVisibility), () => { OpenButtonVisibility = Media.IsOpening == false ? Visibility.Visible : Visibility.Hidden; } },
                { nameof(IsSpeedRatioEnabled), () => { IsSpeedRatioEnabled = Media.IsOpen && Media.IsSeekable; } },
                { nameof(WindowTitle), () => { UpdateWindowTitle(); } }
            };

            PropertyTriggers = new Dictionary<string, string[]>
            {
                { nameof(Media.IsOpen), PropertyUpdaters.Keys.ToArray() },
                { nameof(Media.IsOpening), PropertyUpdaters.Keys.ToArray() },
                { nameof(Media.MediaState), PropertyUpdaters.Keys.ToArray() },
                { nameof(Media.HasMediaEnded), PropertyUpdaters.Keys.ToArray() },
                { nameof(Media.DownloadProgress), new[] { nameof(DownloadProgressVisibility) } },
                { nameof(Media.IsBuffering), new[] { nameof(BufferingProgressVisibility) } },
            };

            Config = ConfigRoot.Load();
            RefreshHistoryItems();

            // Change the default location of the ffmpeg binaries
            // You can get the binaries here: http://ffmpeg.zeranoe.com/builds/win32/shared/ffmpeg-3.2.4-win32-shared.zip
            Unosquare.FFME.MediaElement.FFmpegDirectory = Config.FFmpegPath;

            //ConsoleManager.ShowConsole();
            InitializeComponent();
            InitializeMediaEvents();
            InitializeMouseEvents();
            InitializeMainWindow();

            UpdateWindowTitle();
        }

        /// <summary>
        /// Initializes the media events.
        /// </summary>
        private void InitializeMediaEvents()
        {
            Media.MediaOpened += Media_MediaOpened;
            Media.MediaOpening += Media_MediaOpening;
            Media.MediaFailed += Media_MediaFailed;
            Media.MessageLogged += Media_MessageLogged;
            Media.PropertyChanged += Media_PropertyChanged;
            Unosquare.FFME.MediaElement.FFmpegMessageLogged += MediaElement_FFmpegMessageLogged;
        }

        /// <summary>
        /// Initializes the mouse events for the window.
        /// </summary>
        private void InitializeMouseEvents()
        {

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

                var delta = SnapToMultiple(e.Delta / 2000d, 0.05d);
                MediaZoom = Math.Round(MediaZoom + delta, 2);
            };

            UrlTextBox.PreviewMouseWheel += (s, e) =>
            {
                e.Handled = true;
            };

            #endregion

            #region Handle Play Pause with Mouse Clicks

            //Media.PreviewMouseDown += (s, e) =>
            //{
            //    if (s != Media) return;
            //    if (Media.IsOpen == false || Media.CanPause == false) return;

            //    if (Media.IsPlaying)
            //        PauseCommand.Execute();
            //    else
            //        PlayCommand.Execute();
            //};

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

            var mouseMoveTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(150), IsEnabled = true };
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
        /// Initializes the main window.
        /// </summary>
        private void InitializeMainWindow()
        {
            Loaded += MainWindow_Loaded;
            UrlTextBox.Text = HistoryItems.Count > 0 ? HistoryItems.First() : string.Empty;

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

        #endregion

        #region Event Handlers

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
                var metadata = (Media.Metadata.SourceCollection as IEnumerable<KeyValuePair<string, string>>);
                if (metadata != null)
                {
                    foreach (var kvp in metadata)
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

        /// <summary>
        /// Handles the Loaded event of the MainWindow control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var presenter = VisualTreeHelper.GetParent(Content as UIElement) as ContentPresenter;
            presenter.MinWidth = MinWidth;
            presenter.MinHeight = MinHeight;

            SizeToContent = SizeToContent.WidthAndHeight;
            MinWidth = ActualWidth;
            MinHeight = ActualHeight;
            SizeToContent = SizeToContent.Manual;

            foreach (var kvp in PropertyUpdaters)
            {
                kvp.Value.Invoke();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(kvp.Key));
            }

            Loaded -= MainWindow_Loaded;
        }

        /// <summary>
        /// Handles the PropertyChanged event of the Media control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="PropertyChangedEventArgs"/> instance containing the event data.</param>
        private void Media_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (PropertyTriggers.ContainsKey(e.PropertyName) == false) return;
            foreach (var propertyName in PropertyTriggers[e.PropertyName])
            {
                if (PropertyUpdaters.ContainsKey(propertyName) == false)
                    continue;

                PropertyUpdaters[propertyName]?.Invoke();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        /// <summary>
        /// Handles the MessageLogged event of the Media control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="MediaLogMessagEventArgs"/> instance containing the event data.</param>
        private void Media_MessageLogged(object sender, MediaLogMessagEventArgs e)
        {
            if (e.MessageType == MediaLogMessageType.Trace) return;
            Debug.WriteLine($"{e.MessageType,10} - {e.Message}");
        }

        /// <summary>
        /// Handles the FFmpegMessageLogged event of the MediaElement control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="MediaLogMessagEventArgs"/> instance containing the event data.</param>
        private void MediaElement_FFmpegMessageLogged(object sender, MediaLogMessagEventArgs e)
        {
            Debug.WriteLine($"{e.MessageType,10} - {e.Message}");
        }

        /// <summary>
        /// Handles the MediaFailed event of the Media control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="ExceptionRoutedEventArgs"/> instance containing the event data.</param>
        private void Media_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            MessageBox.Show($"Media Failed: {e.ErrorException.GetType()}\r\n{e.ErrorException.Message}",
                "MediaElement Error", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
        }

        /// <summary>
        /// Handles the MediaOpened event of the Media control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void Media_MediaOpened(object sender, RoutedEventArgs e)
        {
            MediaZoom = 1d;
            var source = Media.Source.ToString();

            if (Config.HistoryEntries.Contains(source))
            {
                var oldIndex = Config.HistoryEntries.IndexOf(source);
                Config.HistoryEntries.RemoveAt(oldIndex);
            }

            Config.HistoryEntries.Add(Media.Source.ToString());
            Config.Save();
            RefreshHistoryItems();

        }

        /// <summary>
        /// Handles the MediaOpening event of the Media control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="MediaOpeningRoutedEventArgs"/> instance containing the event data.</param>
        private void Media_MediaOpening(object sender, MediaOpeningRoutedEventArgs e)
        {

            // An example of switching to a different stream
            if (e.Info.InputUrl.EndsWith("matroska.mkv2"))
            {
                var subtitleStreams = e.Info.Streams.Where(kvp => kvp.Value.CodecType == AVMediaType.AVMEDIA_TYPE_SUBTITLE).Select(kvp => kvp.Value);
                var englishSubtitleStream = subtitleStreams.FirstOrDefault(s => s.Language.StartsWith("en"));
                if (englishSubtitleStream != null)
                    e.Options.SubtitleStream = englishSubtitleStream;

                var audioStreams = e.Info.Streams.Where(kvp => kvp.Value.CodecType == AVMediaType.AVMEDIA_TYPE_AUDIO)
                    .Select(kvp => kvp.Value).ToArray();

                var commentaryStream = audioStreams.FirstOrDefault(s => s.StreamIndex != e.Options.AudioStream.StreamIndex);
                e.Options.AudioStream = commentaryStream;
            }


            // The yadif filter deinterlaces the video; we check the field order if we need
            // to deinterlace the video automatically
            if (e.Options.VideoStream != null
                && e.Options.VideoStream.FieldOrder != AVFieldOrder.AV_FIELD_PROGRESSIVE
                && e.Options.VideoStream.FieldOrder != AVFieldOrder.AV_FIELD_UNKNOWN)
            {
                e.Options.VideoFilter = "yadif";
            }

        }

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

        /// <summary>
        /// Handles the DragDelta event of the DebugWindowThumb control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.Controls.Primitives.DragDeltaEventArgs"/> instance containing the event data.</param>
        private void DebugWindowThumb_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            DebugWindowPopup.HorizontalOffset += e.HorizontalChange;
            DebugWindowPopup.VerticalOffset += e.VerticalChange;
        }

        /// <summary>
        /// Handles the MouseDown event of the DebugWindowPopup control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.Input.MouseButtonEventArgs"/> instance containing the event data.</param>
        private void DebugWindowPopup_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            DebugWindowThumb.RaiseEvent(e);
        }

        #endregion

        #region Helper Methods and PRoperties

        /// <summary>
        /// Gets or sets the media zoom.
        /// </summary>
        private double MediaZoom
        {
            get
            {
                var transform = Media.RenderTransform as ScaleTransform;
                if (transform == null) return 1d;
                return transform.ScaleX;
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


        /// <summary>
        /// Snaps to the given multiple multiple.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="multiple">The multiple.</param>
        /// <returns></returns>
        public static double SnapToMultiple(double value, double multiple)
        {
            var factor = (int)(value / multiple);
            return factor * multiple;
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

        #endregion

    }
}
