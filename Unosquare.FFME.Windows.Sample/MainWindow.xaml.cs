namespace Unosquare.FFME.Windows.Sample
{
    using System;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Media;
    using System.Windows.Media.Animation;
    using System.Windows.Threading;
    using Foundation;
    using Shared;
    using ViewModels;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Fields

        private DateTime LastMouseMoveTime;
        private Point LastMousePosition;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindow"/> class.
        /// </summary>
        public MainWindow()
        {
            // Load up WPF resources
            InitializeComponent();

            // Change the default location of the ffmpeg binaries
            // You can get the binaries here: http://ffmpeg.zeranoe.com/builds/win32/shared/ffmpeg-3.4-win32-shared.zip
            FFME.MediaElement.FFmpegDirectory = PlaylistManager.FFmpegPath;

            // You can pick which FFmpeg binaries are loaded. See issue #28
            // Full Features is already the default.
            FFME.MediaElement.FFmpegLoadModeFlags = FFmpegLoadMode.FullFeatures;

            // Setup the UI
            // ConsoleManager.ShowConsole();
            InitializeMediaEvents();
            InitializeInputEvents();
            InitializeMainWindow();
        }

        #endregion

        #region Properties

        /// <summary>
        /// A proxy, strongly-typed property to the underlying DataContext
        /// </summary>
        public RootViewModel ViewModel => DataContext as RootViewModel;

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
            Media.MediaInitializing += Media_MediaInitializing;
            Media.MediaOpening += Media_MediaOpening;
            Media.MediaOpened += Media_MediaOpened;
            Media.PositionChanged += Media_PositionChanged;
            Media.MediaFailed += Media_MediaFailed;
            Media.MessageLogged += Media_MessageLogged;
            BindRenderingEvents();
        }

        /// <summary>
        /// Initializes the main window.
        /// </summary>
        private void InitializeMainWindow()
        {
            Loaded += MainWindow_Loaded;

            // Open a file if it is specified in the arguments
            var args = Environment.GetCommandLineArgs();
            if (args != null && args.Length > 1)
            {
                App.Current.Commands.OpenCommand.Execute(args[1].Trim());
            }
        }

        /// <summary>
        /// Initializes the mouse events for the window.
        /// </summary>
        private void InitializeInputEvents()
        {
            #region Keyboard Controls

            var togglePlayPauseKeys = new[] { Key.Play, Key.MediaPlayPause, Key.Space };

            window.PreviewKeyDown += async (s, e) =>
            {
                // Console.WriteLine($"KEY: {e.Key}, SRC: {e.OriginalSource?.GetType().Name}");
                if (e.OriginalSource is TextBox)
                    return;

                // Keep the key focus on the main window
                FocusManager.SetIsFocusScope(this, true);
                FocusManager.SetFocusedElement(this, this);

                if (e.Key == Key.G)
                {
                    Subtitles.SetForeground(Media, Brushes.Yellow);
                }

                // Pause
                if (togglePlayPauseKeys.Contains(e.Key) && Media.IsPlaying)
                {
                    await App.Current.Commands.PauseCommand.ExecuteAsync();
                    return;
                }

                // Play
                if (togglePlayPauseKeys.Contains(e.Key) && Media.IsPlaying == false)
                {
                    await App.Current.Commands.PlayCommand.ExecuteAsync();
                    return;
                }

                // Seek to left
                if (e.Key == Key.Left)
                {
                    if (Media.IsPlaying) await Media.Pause();
                    Media.Position -= TimeSpan.FromMilliseconds(
                        Media.FrameStepDuration.TotalMilliseconds * (Media.SpeedRatio >= 1 ? Media.SpeedRatio : 1));
                }

                // Seek to right
                if (e.Key == Key.Right)
                {
                    if (Media.IsPlaying) await Media.Pause();
                    Media.Position += TimeSpan.FromMilliseconds(
                        Media.FrameStepDuration.TotalMilliseconds * (Media.SpeedRatio >= 1 ? Media.SpeedRatio : 1));
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
                    ViewModel.Controller.MediaElementZoom = 1.0;
                }
            };

            #endregion

            #region Toggle Fullscreen with Double Click

            Media.PreviewMouseDoubleClick += async (s, e) =>
            {
                if (s != Media) return;
                e.Handled = true;
                await App.Current.Commands.ToggleFullscreenCommand.ExecuteAsync();
            };

            #endregion

            #region Exit fullscreen with Escape key

            PreviewKeyDown += async (s, e) =>
            {
                if (e.Key == Key.Escape && WindowStyle == WindowStyle.None)
                {
                    e.Handled = true;
                    await App.Current.Commands.ToggleFullscreenCommand.ExecuteAsync();
                }
            };

            #endregion

            #region Handle Zooming with Mouse Wheel

            MouseWheel += (s, e) =>
            {
                if (Media.IsOpen == false || Media.IsOpening)
                    return;

                var delta = (e.Delta / 2000d).ToMultipleOf(0.05d);
                ViewModel.Controller.MediaElementZoom = Math.Round(App.Current.ViewModel.Controller.MediaElementZoom + delta, 2);
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
                if (elapsedSinceMouseMove.TotalMilliseconds >= 3000 && Media.IsOpen && ControllerPanel.IsMouseOver == false
                    && PropertiesPanel.Visibility != Visibility.Visible && ControllerPanel.SoundMenuPopup.IsOpen == false)
                {
                    if (ControllerPanel.Opacity != 0d)
                    {
                        Cursor = Cursors.None;
                        var sb = FindResource("HideControlOpacity") as Storyboard;
                        Storyboard.SetTarget(sb, ControllerPanel);
                        sb.Begin();
                    }
                }
                else
                {
                    if (ControllerPanel.Opacity != 1d)
                    {
                        Cursor = Cursors.Arrow;
                        var sb = FindResource("ShowControlOpacity") as Storyboard;
                        Storyboard.SetTarget(sb, ControllerPanel);
                        sb.Begin();
                    }
                }
            };

            mouseMoveTimer.Start();

            #endregion
        }

        #endregion
    }
}
