namespace Unosquare.FFME
{
    using Events;
    using Platform;
    using Primitives;
    using Rendering;
    using Shared;
    using System;
    using System.ComponentModel;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Data;
    using System.Windows.Interop;
    using System.Windows.Markup;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;

    /// <summary>
    /// Represents a control that contains audio and/or video.
    /// In contrast with System.Windows.Controls.MediaElement, this version uses
    /// the FFmpeg library to perform reading and decoding of media streams.
    /// </summary>
    /// <seealso cref="UserControl" />
    /// <seealso cref="IDisposable" />
    /// <seealso cref="INotifyPropertyChanged" />
    /// <seealso cref="IUriContext" />
    [Localizability(LocalizationCategory.NeverLocalize)]
    [DefaultProperty(nameof(Source))]
    public sealed partial class MediaElement : UserControl, IDisposable, INotifyPropertyChanged, IUriContext
    {
        #region Fields and Property Backing

        /// <summary>
        /// The affects measure and render metadata options
        /// </summary>
        internal const FrameworkPropertyMetadataOptions AffectsMeasureAndRender
            = FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender;

        /// <summary>
        /// The dispose lock -- Prevents running actions while disposing.
        /// </summary>
        private readonly object DisposeLock = new object();

        /// <summary>
        /// Signals whether the open task was called via the open command
        /// so that the source property changing handler does not re-run the open command.
        /// </summary>
        private readonly AtomicBoolean IsOpeningViaCommand = new AtomicBoolean(false);

        /// <summary>
        /// To detect redundant calls
        /// </summary>
        private readonly AtomicBoolean m_IsDisposed = new AtomicBoolean(false);

        /// <summary>
        /// The allow content change flag
        /// </summary>
        private readonly bool AllowContentChange;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes static members of the <see cref="MediaElement"/> class.
        /// </summary>
        static MediaElement()
        {
            // Content property cannot be changed.
            ContentProperty.OverrideMetadata(typeof(MediaElement), new FrameworkPropertyMetadata(null, OnCoerceContentValue));

            var style = new Style(typeof(MediaElement), null);
            style.Setters.Add(new Setter(FlowDirectionProperty, FlowDirection.LeftToRight));
            style.Seal();
            StyleProperty.OverrideMetadata(typeof(MediaElement), new FrameworkPropertyMetadata(style));

            // Initialize the core
            MediaEngine.Initialize(WindowsPlatform.Instance);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaElement" /> class.
        /// </summary>
        public MediaElement()
        {
            try
            {
                AllowContentChange = true;
                InitializeComponent();
            }
            catch { throw; }
            finally
            {
                AllowContentChange = false;
            }
        }

        #endregion

        #region Events

        /// <summary>
        /// Occurs when a logging message from the FFmpeg library has been received.
        /// This is shared across all instances of Media Elements.
        /// </summary>
        /// <remarks>
        /// This event is raised on a background thread.
        /// All interaction with UI elements requires calls on their corresponding dispatcher.
        /// </remarks>
        public static event EventHandler<MediaLogMessageEventArgs> FFmpegMessageLogged;

        /// <summary>
        /// Occurs when a logging message has been logged.
        /// This does not include FFmpeg messages.
        /// </summary>
        /// <remarks>
        /// This event is raised on a background thread.
        /// All interaction with UI elements requires calls on their corresponding dispatcher.
        /// </remarks>
        public event EventHandler<MediaLogMessageEventArgs> MessageLogged;

        /// <summary>
        /// Raised before the input stream of the media is initialized.
        /// Use this method to modify the input options.
        /// </summary>
        /// <remarks>
        /// This event is raised on a background thread.
        /// All interaction with UI elements requires calls on their corresponding dispatcher.
        /// </remarks>
        public event EventHandler<MediaInitializingEventArgs> MediaInitializing;

        /// <summary>
        /// Raised before the input stream of the media is opened.
        /// Use this method to modify the media options and select streams.
        /// </summary>
        /// <remarks>
        /// This event is raised on a background thread.
        /// All interaction with UI elements requires calls on their corresponding dispatcher.
        /// </remarks>
        public event EventHandler<MediaOpeningEventArgs> MediaOpening;

        /// <summary>
        /// Raised before a change in media options is applied.
        /// Use this method to modify the selected streams.
        /// </summary>
        /// <remarks>
        /// This event is raised on a background thread.
        /// All interaction with UI elements requires calls on their corresponding dispatcher.
        /// </remarks>
        public event EventHandler<MediaOpeningEventArgs> MediaChanging;

        /// <summary>
        /// Multicast event for property change notifications.
        /// This event runs on the UI thread.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the FFmpeg path from which to load the FFmpeg binaries.
        /// You must set this path before setting the Source property for the first time on any instance of this control.
        /// Setting this property when FFmpeg binaries have been registered will throw an exception.
        /// </summary>
        public static string FFmpegDirectory
        {
            get => MediaEngine.FFmpegDirectory;
            set => MediaEngine.FFmpegDirectory = value;
        }

        /// <summary>
        /// Specifies the bitwise flags that correspond to FFmpeg library identifiers.
        /// Please use the <see cref="FFmpegLoadMode"/> class for valid combinations.
        /// If FFmpeg is already loaded, the value cannot be changed.
        /// </summary>
        public static int FFmpegLoadModeFlags
        {
            get => MediaEngine.FFmpegLoadModeFlags;
            set => MediaEngine.FFmpegLoadModeFlags = value;
        }

        /// <summary>
        /// Gets the FFmpeg version information. Returns null
        /// when the libraries have not been loaded.
        /// </summary>
        public static string FFmpegVersionInfo => MediaEngine.FFmpegVersionInfo;

        /// <summary>
        /// Gets or sets a value indicating whether the video visualization control
        /// creates its own dispatcher thread to handle rendering of video frames.
        /// This is an experimental feature and it is useful when creating video walls.
        /// For example if you want to display multiple videos at a time and don't want to
        /// use time from the main UI thread. This feature is only valid if we are in
        /// a WPF context.
        /// </summary>
        public static bool EnableWpfMultithreadedVideo { get; set; }

        /// <inheritdoc />
        Uri IUriContext.BaseUri { get; set; }

        /// <summary>
        /// Gets a value indicating whether this instance is disposed.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is disposed; otherwise, <c>false</c>.
        /// </value>
        public bool IsDisposed
        {
            get => m_IsDisposed.Value;
            private set => m_IsDisposed.Value = value;
        }

        /// <summary>
        /// Provides access to the underlying media engine driving this control.
        /// This property is intended for advance usages only.
        /// </summary>
        internal MediaEngine MediaCore { get; private set; } = null;

        /// <summary>
        /// This is the image that holds video bitmaps. It is a Hosted Image which means that in a WPF
        /// GUI context, it runs on its own dispatcher (multi-threaded UI)
        /// </summary>
        internal ImageHost VideoView { get; } = new ImageHost(
            GuiContext.Current.Type == GuiContextType.WPF && EnableWpfMultithreadedVideo)
        { Name = nameof(VideoView) };

        /// <summary>
        /// Gets the closed captions view control.
        /// </summary>
        internal ClosedCaptionsControl CaptionsView { get; } = new ClosedCaptionsControl { Name = nameof(CaptionsView) };

        /// <summary>
        /// A viewbox holding the subtitle text blocks
        /// </summary>
        internal SubtitlesControl SubtitlesView { get; } = new SubtitlesControl { Name = nameof(SubtitlesView) };

        /// <summary>
        /// Gets the grid control holding the rest of the controls.
        /// </summary>
        internal Grid ContentGrid { get; } = new Grid { Name = nameof(ContentGrid) };

        #endregion

        #region Public API

        /// <summary>
        /// Forces the preloading of the FFmpeg libraries according to the values of the
        /// <see cref="FFmpegDirectory"/> and <see cref="FFmpegLoadModeFlags"/>
        /// Also, sets the <see cref="FFmpegVersionInfo"/> property. Thorws an exception
        /// if the libraries cannot be loaded.
        /// </summary>
        /// <returns>true if libraries were loaded, false if libraries were already loaded.</returns>
        public static bool LoadFFmpeg() => MediaEngine.LoadFFmpeg();

        /// <summary>
        /// Requests new media options to be applied, including stream component selection.
        /// Handle the <see cref="MediaChanging"/> event to set new <see cref="MediaOptions"/> based on
        /// <see cref="MediaInfo"/> properties.
        /// </summary>
        /// <returns>The awaitable command</returns>
        public async Task ChangeMedia()
        {
            try { await MediaCore.ChangeMedia(); }
            catch (Exception ex) { PostMediaFailedEvent(ex); }
        }

        /// <summary>
        /// Begins or resumes playback of the currently loaded media.
        /// </summary>
        /// <returns>The awaitable command</returns>
        public async Task Play()
        {
            try { await MediaCore.Play(); }
            catch (Exception ex) { PostMediaFailedEvent(ex); }
        }

        /// <summary>
        /// Pauses playback of the currently loaded media.
        /// </summary>
        /// <returns>The awaitable command</returns>
        public async Task Pause()
        {
            try { await MediaCore.Pause(); }
            catch (Exception ex) { PostMediaFailedEvent(ex); }
        }

        /// <summary>
        /// Pauses and rewinds the currently loaded media.
        /// </summary>
        /// <returns>The awaitable command</returns>
        public async Task Stop()
        {
            try { await MediaCore.Stop(); }
            catch (Exception ex) { PostMediaFailedEvent(ex); }
        }

        /// <summary>
        /// Closes the currently loaded media.
        /// </summary>
        /// <returns>The awaitable command</returns>
        public async Task Close()
        {
            try
            {
                await MediaCore.Close();
                Source = null;
            }
            catch (Exception ex) { PostMediaFailedEvent(ex); }
        }

        /// <summary>
        /// Opens the specified URI.
        /// This is an alternative method of opening media vs using the
        /// <see cref="Source"/> Dependency Property.
        /// </summary>
        /// <param name="uri">The URI.</param>
        /// <returns>The awaitable task.</returns>
        public async Task Open(Uri uri)
        {
            try
            {
                IsOpeningViaCommand.Value = true;
                await GuiContext.Current.InvokeAsync(() => Source = uri);
                await MediaCore.Open(uri);
            }
            catch (Exception ex)
            {
                await GuiContext.Current.InvokeAsync(() => Source = null);
                PostMediaFailedEvent(ex);
                IsOpeningViaCommand.Value = false;
            }
        }

        /// <summary>
        /// Opens the specified custom input stream.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <returns>The awaitable task</returns>
        public async Task Open(IMediaInputStream stream)
        {
            try
            {
                IsOpeningViaCommand.Value = true;
                await GuiContext.Current.InvokeAsync(() => Source = stream.StreamUri);
                await MediaCore.Open(stream);
            }
            catch (Exception ex)
            {
                await GuiContext.Current.InvokeAsync(() => Source = null);
                PostMediaFailedEvent(ex);
                IsOpeningViaCommand.Value = false;
            }
        }

        #endregion

        #region Methods

        /// <inheritdoc />
        public void Dispose()
        {
            lock (DisposeLock)
            {
                if (IsDisposed) return;
                IsDisposed = true;

                // Stop the property updates worker
                PropertyUpdatesWorker.Dispose();

                // Make sure we perform GUI operations on the GUI thread.
                GuiContext.Current?.EnqueueInvoke(() =>
                {
                    // Remove ebent handlers
                    try { VideoView.LayoutUpdated -= HandleVideoViewLayoutUpdates; }
                    catch { /* Ignore if VideoView is already null by now. */ }

                    // Remove all the controls
                    ContentGrid?.Children.Remove(VideoView);
                    ContentGrid?.Children.Remove(SubtitlesView);
                    ContentGrid?.Children.Remove(CaptionsView);

                    // Force Refresh
                    ContentGrid?.Dispatcher?.InvokeAsync(() => { },
                        System.Windows.Threading.DispatcherPriority.Render);
                });
            }
        }

        /// <summary>
        /// Binds the property.
        /// </summary>
        /// <param name="target">The target.</param>
        /// <param name="targetProperty">The target property.</param>
        /// <param name="source">The source.</param>
        /// <param name="sourcePath">The source path.</param>
        /// <param name="mode">The mode.</param>
        internal static void BindProperty(
            DependencyObject target, DependencyProperty targetProperty, DependencyObject source, string sourcePath, BindingMode mode)
        {
            var binding = new Binding
            {
                Source = source,
                Path = new PropertyPath(sourcePath),
                Mode = mode,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
            };

            BindingOperations.SetBinding(target, targetProperty, binding);
        }

        /// <summary>
        /// Called when [coerce content value].
        /// </summary>
        /// <param name="d">The d.</param>
        /// <param name="baseValue">The base value.</param>
        /// <returns>The content property value</returns>
        /// <exception cref="InvalidOperationException">When content has been locked.</exception>
        private static object OnCoerceContentValue(DependencyObject d, object baseValue)
        {
            if (d is MediaElement element && element.AllowContentChange == false)
                throw new InvalidOperationException($"The '{nameof(Content)}' property is not meant to be set.");

            return baseValue;
        }

        /// <summary>
        /// Initializes the component.
        /// </summary>
        private void InitializeComponent()
        {
            // Synchronize initial property values to the MediaElement properties.
            // This is because the hosted element gets greated after the MEdiaElement properties
            // might have been set.
            VideoView.ElementLoaded += (vs, ve) =>
            {
                VideoView.Stretch = Stretch;
                VideoView.StretchDirection = StretchDirection;
                VideoView.UseLayoutRounding = true;
                VideoView.SnapsToDevicePixels = true;
                VideoView.Focusable = false;
                VideoView.IsHitTestVisible = false;
            };

            // Set some default properties. Centering the content allows for video-aligned subtitles
            VerticalContentAlignment = VerticalAlignment.Center;
            HorizontalContentAlignment = HorizontalAlignment.Center;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;

            // Setup the content grid and add it as part of the user control
            Content = ContentGrid;
            ContentGrid.HorizontalAlignment = HorizontalAlignment.Stretch;
            ContentGrid.VerticalAlignment = VerticalAlignment.Stretch;
            ContentGrid.UseLayoutRounding = true;
            ContentGrid.SnapsToDevicePixels = true;
            ContentGrid.IsHitTestVisible = false;

            // Setup the Subtitle View
            SubtitlesView.FontSize = 98;
            SubtitlesView.Padding = new Thickness(0);
            SubtitlesView.FontFamily = new FontFamily("Microsoft Sans Serif, Lucida Console, Calibri");
            SubtitlesView.FontStretch = FontStretches.Condensed;
            SubtitlesView.FontWeight = FontWeights.Bold;
            SubtitlesView.TextOutlineWidth = new Thickness(4);
            SubtitlesView.TextForeground = Brushes.LightYellow;
            SubtitlesView.UseLayoutRounding = true;
            SubtitlesView.SnapsToDevicePixels = true;
            SubtitlesView.IsHitTestVisible = false;
            SubtitlesView.Focusable = false;

            // Add the subtitles control and bind the attached properties
            Subtitles.SetForeground(this, SubtitlesView.TextForeground);
            BindProperty(this, Subtitles.ForegroundProperty, SubtitlesView, nameof(SubtitlesView.TextForeground), BindingMode.TwoWay);
            BindProperty(this, Subtitles.OutlineBrushProperty, SubtitlesView, nameof(SubtitlesView.TextOutline), BindingMode.TwoWay);
            BindProperty(this, Subtitles.OutlineWidthProperty, SubtitlesView, nameof(SubtitlesView.TextOutlineWidth), BindingMode.TwoWay);
            BindProperty(this, Subtitles.EffectProperty, SubtitlesView, nameof(SubtitlesView.TextForegroundEffect), BindingMode.TwoWay);
            BindProperty(this, Subtitles.FontSizeProperty, SubtitlesView, nameof(SubtitlesView.FontSize), BindingMode.TwoWay);
            BindProperty(this, Subtitles.FontWeightProperty, SubtitlesView, nameof(SubtitlesView.FontWeight), BindingMode.TwoWay);
            BindProperty(this, Subtitles.FontFamilyProperty, SubtitlesView, nameof(SubtitlesView.FontFamily), BindingMode.TwoWay);
            BindProperty(this, Subtitles.TextProperty, SubtitlesView, nameof(SubtitlesView.Text), BindingMode.TwoWay);

            // Compose the control by adding overlapping children
            ContentGrid.Children.Add(VideoView);
            ContentGrid.Children.Add(SubtitlesView);
            ContentGrid.Children.Add(CaptionsView);

            // Display the control (or not)
            if (WindowsPlatform.Instance.IsInDesignTime == false)
            {
                // Setup the media engine and associated property updates worker
                MediaCore = new MediaEngine(this, new WindowsMediaConnector(this));
                StartPropertyUpdatesWorker();
            }
            else
            {
                var bitmap = Properties.Resources.FFmpegMediaElementBackground;
                var bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(
                    bitmap.GetHbitmap(), IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                var controlBitmap = new WriteableBitmap(bitmapSource);
                VideoView.Source = controlBitmap;
            }

            // Update as the VideoView updates but check if there are valid dimensions and it actually has video
            VideoView.LayoutUpdated += HandleVideoViewLayoutUpdates;
        }

        /// <summary>
        /// Handles the video view layout updates.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void HandleVideoViewLayoutUpdates(object sender, EventArgs e)
        {
            // Prevent running the code
            lock (DisposeLock)
            {
                if (IsDisposed || ContentGrid.Children.IndexOf(VideoView) < 0)
                    return;

                // When video dimensions are invalid, let's not do any layout.
                if (VideoView.ActualWidth <= 0 || VideoView.ActualHeight <= 0)
                    return;

                if (HasVideo || GuiContext.Current.IsInDesignTime)
                {
                    CaptionsView.Width = VideoView.ActualWidth;
                    CaptionsView.Height = VideoView.ActualHeight * .80; // FCC Safe Caption Area Dimensions
                    CaptionsView.Visibility = Visibility.Visible;
                }
                else
                {
                    CaptionsView.Width = 0;
                    CaptionsView.Height = 0;
                    CaptionsView.Visibility = Visibility.Collapsed;
                }

                // Compute the position of the subtitles view based on the Video View
                var videoViewPosition = VideoView.TransformToAncestor(ContentGrid).Transform(new Point(0, 0));
                var targetHeight = VideoView.ActualHeight / 9d;
                var targetWidth = VideoView.ActualWidth * 0.90;

                if (SubtitlesView.Height != targetHeight)
                    SubtitlesView.Height = targetHeight;

                if (SubtitlesView.Width != targetWidth)
                    SubtitlesView.Width = targetWidth;

                var verticalOffset = ContentGrid.ActualHeight - (videoViewPosition.Y + VideoView.ActualHeight);
                var verticalOffsetPadding = targetHeight * 0.75d;
                var marginBottom = verticalOffset + verticalOffsetPadding;

                if (SubtitlesView.Margin.Bottom != marginBottom)
                    SubtitlesView.Margin = new Thickness(0, 0, 0, marginBottom);
            }
        }

        #endregion
    }
}
