namespace Unosquare.FFME
{
    using Events;
    using Platform;
    using Primitives;
    using Rendering;
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
        /// Signals whether the open task was called via the open command
        /// so that the source property changing handler does not re-run the open command.
        /// </summary>
        private AtomicBoolean IsOpeningViaCommand = new AtomicBoolean(false);

        /// <summary>
        /// The allow content change flag
        /// </summary>
        private bool AllowContentChange = false;

        /// <summary>
        /// IUriContext BaseUri backing
        /// </summary>
        private Uri m_BaseUri = null;

        /// <summary>
        /// TO detect redundant calls
        /// </summary>
        private volatile bool IsDisposed = false;

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
            : base()
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
        /// This is shared across all instances of Media Elements
        /// </summary>
        public static event EventHandler<MediaLogMessageEventArgs> FFmpegMessageLogged;

        /// <summary>
        /// Occurs when a logging message has been logged.
        /// This does not include FFmpeg messages.
        /// </summary>
        public event EventHandler<MediaLogMessageEventArgs> MessageLogged;

        /// <summary>
        /// Multicast event for property change notifications.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the FFmpeg path from which to load the FFmpeg binaries.
        /// You must set this path before setting the Source property for the first time on any instance of this control.
        /// Settng this property when FFmpeg binaries have been registered will throw an exception.
        /// </summary>
        public static string FFmpegDirectory
        {
            get => MediaEngine.FFmpegDirectory;
            set => MediaEngine.FFmpegDirectory = value;
        }

        /// <summary>
        /// Specifies the bitwise flags that correspond to FFmpeg library identifiers.
        /// Please use the <see cref="Shared.FFmpegLoadMode"/> class for valid combinations.
        /// If FFmpeg is already loaded, the value cannot be changed.
        /// </summary>
        public static int FFmpegLoadModeFlags
        {
            get => MediaEngine.FFmpegLoadModeFlags;
            set => MediaEngine.FFmpegLoadModeFlags = value;
        }

        /// <summary>
        /// Gets or sets the base URI of the current application context.
        /// </summary>
        Uri IUriContext.BaseUri
        {
            get => m_BaseUri;
            set => m_BaseUri = value;
        }

        /// <summary>
        /// Provides access to the underlying media engine driving this control.
        /// This property is intender for advance usages only.
        /// </summary>
        internal MediaEngine MediaCore { get; private set; } = null;

        /// <summary>
        /// This is the image that holds video bitmaps
        /// </summary>
        internal Image VideoView { get; } = new Image { Name = nameof(VideoView) };

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
        /// Begins or resumes playback of the currently loaded media.
        /// </summary>
        /// <returns>The awaitable command</returns>
        public async Task Play()
        {
            try { await MediaCore.Play(); }
            catch (Exception ex) { var t = RaiseMediaFailedEvent(ex); }
        }

        /// <summary>
        /// Pauses playback of the currently loaded media.
        /// </summary>
        /// <returns>The awaitable command</returns>
        public async Task Pause()
        {
            try { await MediaCore.Pause(); }
            catch (Exception ex) { var t = RaiseMediaFailedEvent(ex); }
        }

        /// <summary>
        /// Pauses and rewinds the currently loaded media.
        /// </summary>
        /// <returns>The awaitable command</returns>
        public async Task Stop()
        {
            try { await MediaCore.Stop(); }
            catch (Exception ex) { var t = RaiseMediaFailedEvent(ex); }
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
            catch (Exception ex) { var t = RaiseMediaFailedEvent(ex); }
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
                GuiContext.Current.Invoke(() => Source = uri);
                await MediaCore.Open(uri);
            }
            catch (Exception ex)
            {
                GuiContext.Current.Invoke(() => Source = null);
                var t = RaiseMediaFailedEvent(ex);
                IsOpeningViaCommand.Value = false;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;
            PropertyUpdatesWorker.Dispose();
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
            // Setup the content grid and add it as part of the user control
            Content = ContentGrid;
            ContentGrid.HorizontalAlignment = HorizontalAlignment.Stretch;
            ContentGrid.VerticalAlignment = VerticalAlignment.Stretch;

            // Initialize dependency properties to those of the Video view
            Stretch = VideoView.Stretch;
            StretchDirection = VideoView.StretchDirection;

            // Add the child video view and bind the alignment properties
            BindProperty(VideoView, HorizontalAlignmentProperty, this, nameof(HorizontalAlignment), BindingMode.OneWay);
            BindProperty(VideoView, VerticalAlignmentProperty, this, nameof(VerticalAlignment), BindingMode.OneWay);

            // Setup the Subtitle View
            SubtitlesView.FontSize = 98;
            SubtitlesView.Padding = new Thickness(0);
            SubtitlesView.FontFamily = new FontFamily("Microsoft Sans Serif, Lucida Console, Calibri");
            SubtitlesView.FontStretch = FontStretches.Condensed;
            SubtitlesView.FontWeight = FontWeights.Bold;
            SubtitlesView.TextOutlineWidth = new Thickness(4);
            SubtitlesView.TextForeground = Brushes.LightYellow;

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

            // Update as the VideoView updates but check if there are valid dimensions and it actually has video
            VideoView.LayoutUpdated += (s, e) =>
            {
                // When video dimensions are invalid, let's not do any layout.
                if (VideoView.ActualWidth <= 0 || VideoView.ActualHeight <= 0)
                    return;

                if (HasVideo)
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
            };

            // Compose the control by adding overlapping children
            ContentGrid.Children.Add(VideoView);
            ContentGrid.Children.Add(SubtitlesView);
            ContentGrid.Children.Add(CaptionsView);

            // Display the control (or not)
            if (WindowsPlatform.Instance.IsInDesignTime)
            {
                // Shows an FFmpeg image if we are in design-time
                var bitmap = Properties.Resources.FFmpegMediaElementBackground;
                var bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(
                    bitmap.GetHbitmap(), IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                var controlBitmap = new WriteableBitmap(bitmapSource);
                VideoView.Source = controlBitmap;
            }
            else
            {
                // Setup the media engine and associated property updates worker
                MediaCore = new MediaEngine(this, new WindowsMediaConnector(this));
                StartPropertyUpdatesWorker();
            }
        }

        #endregion
    }
}
