namespace Unosquare.FFME.Windows.Sample
{
    using Config;
    using Events;
    using FFmpeg.AutoGen;
    using Shared;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Runtime.InteropServices;
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
        #region Fields

        private readonly Dictionary<string, Action> PropertyUpdaters;
        private readonly Dictionary<string, string[]> PropertyTriggers;
        private readonly ObservableCollection<string> HistoryItems = new ObservableCollection<string>();
        private readonly WindowStatus PreviousWindowStatus = new WindowStatus();

        private ConfigRoot Config;
        private DateTime LastMouseMoveTime;
        private Point LastMousePosition;

        private DelegateCommand m_OpenCommand = null;
        private DelegateCommand m_PauseCommand = null;
        private DelegateCommand m_PlayCommand = null;
        private DelegateCommand m_StopCommand = null;
        private DelegateCommand m_CloseCommand = null;
        private DelegateCommand m_ToggleFullscreenCommand = null;

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
                            OpenMediaPopup.IsOpen = false;
                            var target = new Uri(UrlTextBox.Text);
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
                    m_CloseCommand = new DelegateCommand(async (o) => { await Media.Close(); });

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

        #endregion

        #region Properties: Notification

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

        #region Methods: Helpers and Initialization

        /// <summary>
        /// Initializes the media events.
        /// </summary>
        private void InitializeMediaEvents()
        {
            Media.PositionChanged += (s, e) =>
            {
                // Debug.WriteLine($"{nameof(Media.Position)} = {e.Position}");
            };

            Media.MediaOpened += Media_MediaOpened;
            Media.MediaOpening += Media_MediaOpening;
            Media.MediaFailed += Media_MediaFailed;
            Media.MessageLogged += Media_MessageLogged;
            Media.PropertyChanged += Media_PropertyChanged;
            FFME.MediaElement.FFmpegMessageLogged += MediaElement_FFmpegMessageLogged;

#if HANDLE_RENDERING_EVENTS

            #region Audio and Video Frame Rendering Variables

            System.Drawing.Bitmap overlayBitmap = null;
            System.Drawing.Graphics overlayGraphics = null;
            var overlayTextFont = new System.Drawing.Font("Arial", 14, System.Drawing.FontStyle.Bold);
            var overlayTextFontBrush = System.Drawing.Brushes.WhiteSmoke;
            var overlayTextOffset = new System.Drawing.PointF(12, 8);
            var overlayBackBuffer = IntPtr.Zero;

            var drawVuMeterLeftPen = new System.Drawing.Pen(System.Drawing.Color.OrangeRed, 12);
            var drawVuMeterRightPen = new System.Drawing.Pen(System.Drawing.Color.GreenYellow, 12);
            var drawVuMeterRmsLock = new object();
            var drawVuMeterLeftRms = new SortedDictionary<TimeSpan, double>();
            var drawVuMeterRightRms = new SortedDictionary<TimeSpan, double>();

            var drawVuMeterLeftValue = 0d;
            var drawVuMeterRightValue = 0d;
            const float drawVuMeterLeftOffset = 16;
            const float drawVuMeterTopOffset = 50;
            const float drawVuMeterScaleFactor = 20; // RMS * pixel factor = the length of the VU meter lines

            #endregion

            #region Rendering Event Examples

            Media.RenderingVideo += (s, e) =>
            {
            #region Create the overlay buffer to work with

                if (overlayBackBuffer != e.Bitmap.BackBuffer)
                {
                    lock (drawVuMeterRmsLock)
                    {
                        drawVuMeterLeftRms.Clear();
                        drawVuMeterRightRms.Clear();
                    }

                    if (overlayGraphics != null) overlayGraphics.Dispose();
                    if (overlayBitmap != null) overlayBitmap.Dispose();

                    overlayBitmap = new System.Drawing.Bitmap(
                        e.Bitmap.PixelWidth,
                        e.Bitmap.PixelHeight,
                        e.Bitmap.BackBufferStride,
                        System.Drawing.Imaging.PixelFormat.Format32bppRgb,
                        e.Bitmap.BackBuffer);

                    overlayBackBuffer = e.Bitmap.BackBuffer;
                    overlayGraphics = System.Drawing.Graphics.FromImage(overlayBitmap);
                    overlayGraphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Default;
                }

            #endregion

            #region Read the instantaneous RMS of the audio

                lock (drawVuMeterRmsLock)
                {
                    drawVuMeterLeftValue = drawVuMeterLeftRms.Where(kvp => kvp.Key > Media.Position).Select(kvp => kvp.Value).FirstOrDefault();
                    drawVuMeterRightValue = drawVuMeterRightRms.Where(kvp => kvp.Key > Media.Position).Select(kvp => kvp.Value).FirstOrDefault();

                    // do some cleanup so the dictionary does not grow too big.
                    if (drawVuMeterLeftRms.Count > 256)
                    {
                        var keysToRemove = drawVuMeterLeftRms.Keys.Where(k => k < Media.Position).OrderBy(k => k).ToArray();
                        foreach (var k in keysToRemove)
                        {
                            drawVuMeterLeftRms.Remove(k);
                            drawVuMeterRightRms.Remove(k);

                            if (drawVuMeterLeftRms.Count < 256)
                                break;
                        }
                    }
                }

            #endregion

            #region Draw the text and the VU meter

                e.Bitmap.Lock();
                var differenceMillis = TimeSpan.FromTicks(e.Clock.Ticks - e.StartTime.Ticks).TotalMilliseconds;

                overlayGraphics.DrawString($"Clock: {e.StartTime.TotalSeconds:00.000} | Skew: {differenceMillis:00.000} | PN: {e.PictureNumber}",
                    overlayTextFont,
                    overlayTextFontBrush,
                    overlayTextOffset);

                // draw a simple VU meter
                overlayGraphics.DrawLine(drawVuMeterLeftPen,
                    drawVuMeterLeftOffset,
                    drawVuMeterTopOffset,
                    drawVuMeterLeftOffset + 5 + (Convert.ToSingle(drawVuMeterLeftValue) * drawVuMeterScaleFactor),
                    drawVuMeterTopOffset);

                overlayGraphics.DrawLine(drawVuMeterRightPen,
                    drawVuMeterLeftOffset,
                    drawVuMeterTopOffset + 20,
                    drawVuMeterLeftOffset + 5 + (Convert.ToSingle(drawVuMeterRightValue) * drawVuMeterScaleFactor),
                    drawVuMeterTopOffset + 20);

                e.Bitmap.AddDirtyRect(new Int32Rect(0, 0, e.Bitmap.PixelWidth, e.Bitmap.PixelHeight));
                e.Bitmap.Unlock();

            #endregion
            };

            Media.RenderingAudio += (s, e) =>
            {
                // The buffer contains all the samples
                var buffer = new byte[e.BufferLength];
                Marshal.Copy(e.Buffer, buffer, 0, e.BufferLength);

                // We need to split the samples into left and right samples
                var leftSamples = new double[e.SamplesPerChannel];
                var rightSamples = new double[e.SamplesPerChannel];

                // Iterate through the buffer
                var isLeftSample = true;
                var sampleIndex = 0;
                var samplePercent = default(double);

                for (var i = 0; i < e.BufferLength; i += e.BitsPerSample / 8)
                {
                    samplePercent = 100d * Math.Abs((double)((short)(buffer[i] | (buffer[i + 1] << 8)))) / (double)short.MaxValue;

                    if (isLeftSample)
                        leftSamples[sampleIndex] = samplePercent;
                    else
                        rightSamples[sampleIndex] = samplePercent;

                    sampleIndex += !isLeftSample ? 1 : 0;
                    isLeftSample = !isLeftSample;
                }

                // Compute the RMS of the samples and save it for the given point in time.
                lock (drawVuMeterRmsLock)
                {
                    // The VU meter should show the audio RMS, we compute it and save it in a dictionary.
                    drawVuMeterLeftRms[e.StartTime] = Math.Sqrt((1d / leftSamples.Length) * leftSamples.Sum(n => n));
                    drawVuMeterRightRms[e.StartTime] = Math.Sqrt((1d / rightSamples.Length) * rightSamples.Sum(n => n));
                }
            };

            Media.RenderingSubtitles += (s, e) =>
            {
                // a simple example of suffixing subtitles
                if (e.Text != null && e.Text.Count > 0)
                    e.Text[0] = $"{e.Text[0]}\r\n(subtitles)";
            };

            #endregion

#endif
        }

        /// <summary>
        /// Initializes the mouse events for the window.
        /// </summary>
        private void InitializeInputEvents()
        {
            var togglePlayPauseKeys = new[] { Key.Play, Key.MediaPlayPause, Key.Space };

            // Command keys
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

        #endregion

        #region Methods: Event Handlers

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
                if (Media.Metadata.SourceCollection is IEnumerable<KeyValuePair<string, string>> metadata)
                {
                    foreach (var kvp in metadata)
                    {
                        if (kvp.Key.ToLowerInvariant().Equals("title"))
                        {
                            title = kvp.Value;
                            break;
                        }
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
        /// <param name="e">The <see cref="MediaLogMessageEventArgs" /> instance containing the event data.</param>
        private void Media_MessageLogged(object sender, MediaLogMessageEventArgs e)
        {
            if (e.MessageType == MediaLogMessageType.Trace)
                return;

            Debug.WriteLine($"{e.MessageType,10} - {e.Message}");
        }

        /// <summary>
        /// Handles the FFmpegMessageLogged event of the MediaElement control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="MediaLogMessageEventArgs"/> instance containing the event data.</param>
        private void MediaElement_FFmpegMessageLogged(object sender, MediaLogMessageEventArgs e)
        {
            if (e.Message.Contains("] Reinit context to ")
                || e.Message.Contains("Using non-standard frame rate"))
            {
                return;
            }

            Debug.WriteLine($"{e.MessageType,10} - {e.Message}");
        }

        /// <summary>
        /// Handles the MediaFailed event of the Media control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="ExceptionRoutedEventArgs"/> instance containing the event data.</param>
        private void Media_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            MessageBox.Show(
                $"Media Failed: {e.ErrorException.GetType()}\r\n{e.ErrorException.Message}",
                "MediaElement Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error,
                MessageBoxResult.OK);
        }

        /// <summary>
        /// Handles the MediaOpened event of the Media control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void Media_MediaOpened(object sender, RoutedEventArgs e)
        {
            // Set a start position (see issue #66)
            /*
            Media.Position = TimeSpan.FromSeconds(5);
            Media.Play();
            */

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
            // An example of injecting format options for http/https streams
            if (e.Info.InputUrl.StartsWith("http://") || e.Info.InputUrl.StartsWith("https://"))
            {
                e.Options.InputOptions["usetoc"] = "1";
                e.Options.InputOptions["user_agent"] = $"{typeof(MediaOptions).Namespace}/{typeof(MediaOptions).Assembly.GetName().Version}";
                e.Options.InputOptions["headers"] = $"Referer:https://www.unosquare.com";
                e.Options.InputOptions["multiple_requests"] = "1";
                e.Options.InputOptions["reconnect"] = "1";
                e.Options.InputOptions["reconnect_at_eof"] = "1";
                e.Options.InputOptions["reconnect_streamed"] = "1";
                e.Options.InputOptions["reconnect_delay_max"] = "10"; // in seconds
            }

            // An example of switching to a different stream
            if (e.Info.InputUrl.EndsWith("matroska.mkv"))
            {
                var subtitleStreams = e.Info.Streams.Where(kvp => kvp.Value.CodecType == AVMediaType.AVMEDIA_TYPE_SUBTITLE).Select(kvp => kvp.Value);
                var englishSubtitleStream = subtitleStreams.FirstOrDefault(s => s.Language.StartsWith("en"));
                if (englishSubtitleStream != null)
                    e.Options.SubtitleStream = englishSubtitleStream;

                var audioStreams = e.Info.Streams.Where(kvp => kvp.Value.CodecType == AVMediaType.AVMEDIA_TYPE_AUDIO)
                    .Select(kvp => kvp.Value).ToArray();

                // var commentaryStream = audioStreams.FirstOrDefault(s => s.StreamIndex != e.Options.AudioStream.StreamIndex);
                // e.Options.AudioStream = commentaryStream;
            }

            // In realtime streams probesize can be reduced to reduce latency
            // e.Options.ProbeSize = 32; // 32 is the minimum

            // In realtime strams analyze duration can be reduced to reduce latency
            // e.Options.MaxAnalyzeDuration = TimeSpan.Zero;

            // The yadif filter deinterlaces the video; we check the field order if we need
            // to deinterlace the video automatically
            if (e.Options.VideoStream != null
                && e.Options.VideoStream.FieldOrder != AVFieldOrder.AV_FIELD_PROGRESSIVE
                && e.Options.VideoStream.FieldOrder != AVFieldOrder.AV_FIELD_UNKNOWN)
            {
                e.Options.VideoFilter = "yadif";

                // When enabling HW acceleration, the filtering does not seem to get applied for some reason.
                e.Options.EnableHardwareAcceleration = false;
            }

            // Experimetal HW acceleration support. Remove if not needed.
            /* e.Options.EnableHardwareAcceleration = Debugger.IsAttached; */

#if APPLY_AUDIO_FILTER

            // e.Options.AudioFilter = "aecho=0.8:0.9:1000:0.3";
            e.Options.AudioFilter = "chorus=0.5:0.9:50|60|40:0.4|0.32|0.3:0.25|0.4|0.3:2|2.3|1.3";
#endif
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

        #region Methods: Private Methods

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
