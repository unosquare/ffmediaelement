namespace Unosquare.FFME.Windows.Sample;

using ClosedCaptions;
using Common;
using Foundation;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ViewModels;
using ImageFormat = System.Drawing.Imaging.ImageFormat;

/// <summary>
/// Interaction logic for MainWindow.xaml.
/// </summary>
public partial class MainWindow : Window
{
    #region Fields

    private static readonly Key[] TogglePlayPauseKeys = [Key.Play, Key.MediaPlayPause, Key.Space];
    private readonly object ScreenshotSyncLock = new();
    private readonly object RecorderSyncLock = new();
    private TransportStreamRecorder StreamRecorder;
    private DateTime LastMouseMoveTime;
    private Point LastMousePosition;
    private DispatcherTimer MouseMoveTimer;
    private MediaType StreamCycleMediaType = MediaType.None;
    private bool m_IsCaptureInProgress;
    private bool IsControllerHideCompleted;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    public MainWindow()
    {
        // Set the ViewModel from the application resource
        ViewModel = App.ViewModel;

        // During runtime, let's hide the window. The loaded event handler will
        // compute the final placement of our window.
        if (!App.IsInDesignMode)
        {
            Left = int.MinValue;
            Top = int.MinValue;
        }

        // Load up WPF resources
        InitializeComponent();

        // Setup the UI
        InitializeMainWindow();
        InitializeMediaEvents();
    }

    #endregion

    #region Properties

    /// <summary>
    /// A proxy, strongly-typed property to the underlying DataContext.
    /// </summary>
    public RootViewModel ViewModel { get; }

    /// <summary>
    /// A flag indicating whether screenshot capture progress is currently active.
    /// </summary>
    private bool IsCaptureInProgress
    {
        get { lock (ScreenshotSyncLock) return m_IsCaptureInProgress; }
        set { lock (ScreenshotSyncLock) m_IsCaptureInProgress = value; }
    }

    private Storyboard HideControllerAnimation => FindResource("HideControlOpacity") as Storyboard;

    private Storyboard ShowControllerAnimation => FindResource("ShowControlOpacity") as Storyboard;

    #endregion

    #region Initialization Methods

    /// <summary>
    /// Initializes the main window.
    /// </summary>
    private void InitializeMainWindow()
    {
        Loaded += OnWindowLoaded;
        PreviewKeyDown += OnWindowKeyDown;
        MouseWheel += OnMouseWheelChange;

        #region Notification MEssages

        var notificationsStoryboard = FindResource("ShowNotification") as Storyboard;
        ViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName != nameof(ViewModel.NotificationMessage))
                return;

            Dispatcher.InvokeAsync(() =>
            {
                if (string.IsNullOrWhiteSpace(ViewModel.NotificationMessage))
                {
                    NotificationsGrid.Opacity = 0;
                    return;
                }

                Storyboard.SetTarget(notificationsStoryboard, NotificationsGrid);
                notificationsStoryboard.Begin();
            });
        };

        #endregion

        #region Mouse Move Detection for Hiding the Controller Panel

        LastMouseMoveTime = DateTime.UtcNow;

        Loaded += (s, e) =>
        {
            Storyboard.SetTarget(HideControllerAnimation, ControllerPanel);
            Storyboard.SetTarget(ShowControllerAnimation, ControllerPanel);

            HideControllerAnimation.Completed += (es, ee) =>
            {
                ControllerPanel.Visibility = Visibility.Hidden;
                IsControllerHideCompleted = true;
            };

            ShowControllerAnimation.Completed += (es, ee) =>
            {
                IsControllerHideCompleted = false;
            };
        };

        MouseMove += (s, e) =>
        {
            var currentPosition = e.GetPosition(window);
            if (Math.Abs(currentPosition.X - LastMousePosition.X) > double.Epsilon ||
                Math.Abs(currentPosition.Y - LastMousePosition.Y) > double.Epsilon)
                LastMouseMoveTime = DateTime.UtcNow;

            LastMousePosition = currentPosition;
        };

        MouseLeave += (s, e) =>
        {
            LastMouseMoveTime = DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(10));
        };

        MouseMoveTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(150),
            IsEnabled = true
        };

        MouseMoveTimer.Tick += (s, e) =>
        {
            var elapsedSinceMouseMove = DateTime.UtcNow.Subtract(LastMouseMoveTime);
            if (elapsedSinceMouseMove.TotalMilliseconds >= 3000 && Media.IsOpen && ControllerPanel.IsMouseOver == false
                && PropertiesPanel.Visibility != Visibility.Visible && ControllerPanel.SoundMenuPopup.IsOpen == false)
            {
                if (IsControllerHideCompleted) return;
                Cursor = Cursors.None;
                HideControllerAnimation?.Begin();
                IsControllerHideCompleted = false;
            }
            else
            {
                Cursor = Cursors.Arrow;
                ControllerPanel.Visibility = Visibility.Visible;
                ShowControllerAnimation?.Begin();
            }
        };

        MouseMoveTimer.Start();

        #endregion
    }

    /// <summary>
    /// Initializes the media events.
    /// </summary>
    private void InitializeMediaEvents()
    {
        // Global FFmpeg message handler
        FFME.MediaElement.FFmpegMessageLogged += OnMediaFFmpegMessageLogged;

        // MediaElement event bindings
        Media.PreviewMouseDoubleClick += OnMediaDoubleClick;
        Media.MediaInitializing += OnMediaInitializing;
        Media.MediaOpening += OnMediaOpening;
        Media.MediaOpened += OnMediaOpened;
        Media.MediaReady += OnMediaReady;
        Media.MediaClosed += OnMediaClosed;
        Media.MediaChanging += OnMediaChanging;
        Media.AudioDeviceStopped += OnAudioDeviceStopped;
        Media.MediaChanged += OnMediaChanged;
        Media.PositionChanged += OnMediaPositionChanged;
        Media.MediaFailed += OnMediaFailed;
        Media.MessageLogged += OnMediaMessageLogged;
        Media.MediaStateChanged += OnMediaStateChanged;
        Media.DataFrameReceived += OnDataFrameReceived;

        // Complex examples of Media Rendering Events
        BindMediaRenderingEvents();
    }

    #endregion

    #region Window Control and Input Event Handlers

    /// <summary>
    /// Handles the Loaded event of the MainWindow control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        // Remove the event handler reference
        Loaded -= OnWindowLoaded;

        // Compute and Apply Sizing Properties
        if (Content is UIElement contentElement &&
            VisualTreeHelper.GetParent(contentElement) is ContentPresenter presenter)
        {
            presenter.MinWidth = MinWidth;
            presenter.MinHeight = MinHeight;

            SizeToContent = SizeToContent.WidthAndHeight;
            MinWidth = ActualWidth;
            MinHeight = ActualHeight;
            SizeToContent = SizeToContent.Manual;
        }

        // Place on secondary screen by default if there is one
        {
            var screenOffsetX = 0d;
            var screenWidth = SystemParameters.PrimaryScreenWidth;
            var screenHeight = SystemParameters.PrimaryScreenHeight;

            if ((int)SystemParameters.VirtualScreenWidth != (int)SystemParameters.FullPrimaryScreenWidth &&
                (int)SystemParameters.VirtualScreenLeft == 0 && (int)SystemParameters.VirtualScreenTop == 0)
            {
                screenOffsetX = SystemParameters.PrimaryScreenWidth;
                screenWidth = SystemParameters.VirtualScreenWidth - SystemParameters.PrimaryScreenWidth;
            }

            Left = screenOffsetX + ((screenWidth - ActualWidth) / 2d);
            Top = (screenHeight - ActualHeight) / 2d;
        }

        // Open a file if it is specified in the arguments
        var args = Environment.GetCommandLineArgs();
        if (args.Length > 1)
        {
            App.ViewModel.Commands.OpenCommand.Execute(args[1].Trim());
        }
    }

    /// <summary>
    /// Handles the PreviewKeyDown event of the MainWindow control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="KeyEventArgs"/> instance containing the event data.</param>
    private async void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        // Debug.WriteLine($"KEY: {e.Key}, SRC: {e.OriginalSource?.GetType().Name}");
        if (e.OriginalSource is TextBox)
            return;

        // Keep the key focus on the main window
        FocusManager.SetIsFocusScope(this, true);
        FocusManager.SetFocusedElement(this, this);

        if (e.Key == Key.G)
        {
            // Example of toggling subtitle color
            Subtitles.SetForeground(Media,
                Subtitles.GetForeground(Media) == Brushes.LightYellow ? Brushes.Yellow : Brushes.LightYellow);
            return;
        }

        // Pause
        if (TogglePlayPauseKeys.Contains(e.Key) && Media.IsPlaying)
        {
            await App.ViewModel.Commands.PauseCommand.ExecuteAsync();
            return;
        }

        // Play
        if (TogglePlayPauseKeys.Contains(e.Key) && Media.IsPlaying == false)
        {
            await App.ViewModel.Commands.PlayCommand.ExecuteAsync();
            return;
        }

        // Seek to left
        if (e.Key == Key.Left)
        {
            await Media.StepBackward();
            return;
        }

        // Seek to right
        if (e.Key == Key.Right)
        {
            await Media.StepForward();
            return;
        }

        // Volume Up
        if (e.Key == Key.Add || e.Key == Key.VolumeUp)
        {
            Media.Volume += Media.Volume >= 1 ? 0 : 0.05;
            ViewModel.NotificationMessage = $"Volume: {Media.Volume:p0}";
            return;
        }

        // Volume Down
        if (e.Key == Key.Subtract || e.Key == Key.VolumeDown)
        {
            Media.Volume -= Media.Volume <= 0 ? 0 : 0.05;
            ViewModel.NotificationMessage = $"Volume: {Media.Volume:p0}";
            return;
        }

        // Mute/Unmute
        if (e.Key == Key.M || e.Key == Key.VolumeMute)
        {
            Media.IsMuted = !Media.IsMuted;
            ViewModel.NotificationMessage = Media.IsMuted ? "Muted." : "Unmuted.";
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

        // Cycle Through Audio Streams
        if (e.Key == Key.A)
        {
            StreamCycleMediaType = MediaType.Audio;
            await Media.ChangeMedia();
            return;
        }

        // Cycle Through Subtitle Streams
        if (e.Key == Key.S)
        {
            StreamCycleMediaType = MediaType.Subtitle;
            await Media.ChangeMedia();
            return;
        }

        // Cycle Through Video Streams
        if (e.Key == Key.Q)
        {
            StreamCycleMediaType = MediaType.Video;
            await Media.ChangeMedia();
            return;
        }

        // Cycle through closed captions
        if (e.Key == Key.C)
        {
            var currentCaptions = (int)Media.ClosedCaptionsChannel;
            var nextCaptions = currentCaptions >= (int)CaptionsChannel.CC4 ? CaptionsChannel.CCP : (CaptionsChannel)(currentCaptions + 1);
            Media.ClosedCaptionsChannel = nextCaptions;
            ViewModel.NotificationMessage = $"Closed-Captions: {nextCaptions}";
            return;
        }

        // Reset changes
        if (e.Key == Key.R)
        {
            Media.SpeedRatio = 1.0;
            Media.Volume = 1.0;
            Media.Balance = 0;
            Media.IsMuted = false;
            ViewModel.Controller.VideoContrast = 1;
            ViewModel.Controller.VideoBrightness = 0;
            ViewModel.Controller.VideoSaturation = 1;
            ViewModel.Controller.MediaElementZoom = 1.0;
            ViewModel.NotificationMessage = "Defaults applied.";
            return;
        }

        // Contrast Controls
        if (e.Key == Key.Y)
        {
            ViewModel.Controller.VideoContrast += 0.05;
            return;
        }

        if (e.Key == Key.H)
        {
            ViewModel.Controller.VideoContrast -= 0.05;
            return;
        }

        // Brightness Controls
        if (e.Key == Key.U)
        {
            ViewModel.Controller.VideoBrightness += 0.05;
            return;
        }

        if (e.Key == Key.J)
        {
            ViewModel.Controller.VideoBrightness -= 0.05;
            return;
        }

        // Saturation Controls
        if (e.Key == Key.I)
        {
            ViewModel.Controller.VideoSaturation += 0.05;
            return;
        }

        if (e.Key == Key.K)
        {
            ViewModel.Controller.VideoSaturation -= 0.05;
            return;
        }

        // Example of cycling through audio filters
        if (e.Key == Key.E)
        {
            var mediaOptions = ViewModel.CurrentMediaOptions;
            if (mediaOptions == null) return;

            if (string.IsNullOrWhiteSpace(mediaOptions.AudioFilter))
            {
                mediaOptions.AudioFilter = "aecho=0.8:0.9:1000:0.3";
                ViewModel.NotificationMessage = "Applied echo audio filter.";
            }
            else if (mediaOptions.AudioFilter == "aecho=0.8:0.9:1000:0.3")
            {
                mediaOptions.AudioFilter = "chorus=0.5:0.9:50|60|40:0.4|0.32|0.3:0.25|0.4|0.3:2|2.3|1.3";
                ViewModel.NotificationMessage = "Applied chorus audio filter.";
            }
            else
            {
                mediaOptions.AudioFilter = string.Empty;
                ViewModel.NotificationMessage = "Cleared audio filter.";
            }

            return;
        }

        // Capture Screenshot to desktop
        if (e.Key == Key.T)
        {
            // Don't run the capture operation as it is in progress
            // GDI requires exclusive access to files when writing
            // so we do this one at a time
            if (IsCaptureInProgress)
                return;

            // Immediately set the progress to true.
            IsCaptureInProgress = true;

            // Send the capture to the background so we don't have frames skipping
            // on the UI. This prvents frame jittering.
            var captureTask = Task.Run(() =>
            {
                try
                {
                    // Obtain the bitmap
                    var bmp = Media.CaptureBitmapAsync().GetAwaiter().GetResult();

                    // prevent firther processing if we did not get a bitmap.
                    bmp?.Save(App.GetCaptureFilePath("Screenshot", "png"), ImageFormat.Png);
                    ViewModel.NotificationMessage = "Captured screenshot.";
                }
                catch (Exception ex)
                {
                    var messageTask = Dispatcher.InvokeAsync(() =>
                    {
                        MessageBox.Show(
                            this,
                            $"Capturing Video Frame Failed: {ex.GetType()}\r\n{ex.Message}",
                            $"{nameof(MediaElement)} Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error,
                            MessageBoxResult.OK);
                    });
                }
                finally
                {
                    // unlock for further captures.
                    IsCaptureInProgress = false;
                }
            });

            return;
        }

        if (e.Key == Key.W)
        {
            // An example of recording packets (no transcoding) into a transport stream.
            lock (RecorderSyncLock)
            {
                if (StreamRecorder == null && Media.IsOpen)
                {
                    StreamRecorder = new TransportStreamRecorder(App.GetCaptureFilePath("Capture", "ts"), Media);
                    ViewModel.NotificationMessage = "Stream recording initiated.";
                }
                else
                {
                    if (StreamRecorder != null)
                        ViewModel.NotificationMessage = "Stream recording completed.";

                    StreamRecorder?.Close();
                    StreamRecorder = null;
                }
            }

            return;
        }

        // Exit fullscreen
        if (e.Key == Key.Escape && WindowStyle == WindowStyle.None)
        {
            await App.ViewModel.Commands.ToggleFullscreenCommand.ExecuteAsync();
        }
    }

    /// <summary>
    /// Handles the PreviewMouseDoubleClick event of the Media control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="MouseButtonEventArgs"/> instance containing the event data.</param>
    private async void OnMediaDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if ((sender?.Equals(Media) ?? false) == false)
            return;

        e.Handled = true;
        await App.ViewModel.Commands.ToggleFullscreenCommand.ExecuteAsync();
    }

    /// <summary>
    /// Handles the MouseWheel event of the MainWindow control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="MouseWheelEventArgs"/> instance containing the event data.</param>
    private void OnMouseWheelChange(object sender, MouseWheelEventArgs e)
    {
        if (Media.IsOpen == false || Media.IsOpening || Media.IsChanging)
            return;

        var delta = (e.Delta / 2000d).ToMultipleOf(0.05d);
        ViewModel.Controller.MediaElementZoom = Math.Round(App.ViewModel.Controller.MediaElementZoom + delta, 2);
    }

    #endregion
}
