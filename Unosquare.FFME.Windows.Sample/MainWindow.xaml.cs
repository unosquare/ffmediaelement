namespace Unosquare.FFME.Windows.Sample
{
    using ClosedCaptions;
    using Platform;
    using Shared;
    using System;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Media;
    using System.Windows.Media.Animation;
    using System.Windows.Threading;
    using ViewModels;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Fields

        private static readonly Key[] TogglePlayPauseKeys = new[] { Key.Play, Key.MediaPlayPause, Key.Space };
        private DateTime LastMouseMoveTime;
        private Point LastMousePosition;
        private DispatcherTimer MouseMoveTimer = null;
        private MediaType StreamCycleMediaType = MediaType.None;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindow"/> class.
        /// </summary>
        public MainWindow()
        {
            // During runtime, let's hide the window. The loaded event handler will
            // compute the final placement of our window.
            if (GuiContext.Current.IsInDesignTime == false)
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
        /// A proxy, strongly-typed property to the underlying DataContext
        /// </summary>
        public RootViewModel ViewModel => DataContext as RootViewModel;

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

            #region Mouse Move Detection for Hiding the Controller Panel

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
            Media.MediaChanging += OnMediaChanging;
            Media.MediaChanged += OnMediaChanged;
            Media.PositionChanged += OnMediaPositionChanged;
            Media.MediaFailed += OnMediaFailed;
            Media.MessageLogged += OnMediaMessageLogged;

            // Complex examples of MEdia Rendering Events
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
            {
                var presenter = VisualTreeHelper.GetParent(Content as UIElement) as ContentPresenter;
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

                if (SystemParameters.VirtualScreenWidth != SystemParameters.FullPrimaryScreenWidth &&
                    SystemParameters.VirtualScreenLeft == 0 && SystemParameters.VirtualScreenTop == 0)
                {
                    screenOffsetX = SystemParameters.PrimaryScreenWidth;
                    screenWidth = SystemParameters.VirtualScreenWidth - SystemParameters.PrimaryScreenWidth;
                }

                Left = screenOffsetX + ((screenWidth - ActualWidth) / 2d);
                Top = (screenHeight - ActualHeight) / 2d;
            }

            // Open a file if it is specified in the arguments
            var args = Environment.GetCommandLineArgs();
            if (args != null && args.Length > 1)
            {
                App.Current.Commands.OpenCommand.Execute(args[1].Trim());
            }
        }

        /// <summary>
        /// Handles the PreviewKeyDown event of the MainWindow control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="KeyEventArgs"/> instance containing the event data.</param>
        private async void OnWindowKeyDown(object sender, KeyEventArgs e)
        {
            // Console.WriteLine($"KEY: {e.Key}, SRC: {e.OriginalSource?.GetType().Name}");
            if (e.OriginalSource is TextBox)
                return;

            // Keep the key focus on the main window
            FocusManager.SetIsFocusScope(this, true);
            FocusManager.SetFocusedElement(this, this);

            if (e.Key == Key.G)
            {
                // Example of toggling subtitle color
                if (Subtitles.GetForeground(Media) == Brushes.LightYellow)
                    Subtitles.SetForeground(Media, Brushes.Yellow);
                else
                    Subtitles.SetForeground(Media, Brushes.LightYellow);

                return;
            }

            // Pause
            if (TogglePlayPauseKeys.Contains(e.Key) && Media.IsPlaying)
            {
                await App.Current.Commands.PauseCommand.ExecuteAsync();
                return;
            }

            // Play
            if (TogglePlayPauseKeys.Contains(e.Key) && Media.IsPlaying == false)
            {
                await App.Current.Commands.PlayCommand.ExecuteAsync();
                return;
            }

            // Seek to left
            if (e.Key == Key.Left)
            {
                if (Media.IsPlaying) await Media.Pause();
                Media.Position = Media.PositionPrevious;

                return;
            }

            // Seek to right
            if (e.Key == Key.Right)
            {
                if (Media.IsPlaying) await Media.Pause();
                Media.Position = Media.PositionNext;

                return;
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
                return;
            }

            // Exit fullscreen
            if (e.Key == Key.Escape && WindowStyle == WindowStyle.None)
            {
                await App.Current.Commands.ToggleFullscreenCommand.ExecuteAsync();
                return;
            }
        }

        /// <summary>
        /// Handles the PreviewMouseDoubleClick event of the Media control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="MouseButtonEventArgs"/> instance containing the event data.</param>
        private async void OnMediaDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender != Media) return;
            e.Handled = true;
            await App.Current.Commands.ToggleFullscreenCommand.ExecuteAsync();
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
            ViewModel.Controller.MediaElementZoom = Math.Round(App.Current.ViewModel.Controller.MediaElementZoom + delta, 2);
        }

        #endregion
    }
}
