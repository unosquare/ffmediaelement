namespace Unosquare.FFME
{
    using Events;
    using Platform;
    using Rendering;
    using System;
    using System.ComponentModel;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Interop;
    using System.Windows.Markup;
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

        internal const FrameworkPropertyMetadataOptions AffectsMeasureAndRender
            = FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender;

        /// <summary>
        /// IUriContext BaseUri backing
        /// </summary>
        private Uri m_BaseUri = null;

        /// <summary>
        /// Holds the Media Engine
        /// </summary>
        private MediaEngine m_MediaCore;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes static members of the <see cref="MediaElement"/> class.
        /// </summary>
        static MediaElement()
        {
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
            ContentGrid = new Grid { Name = nameof(ContentGrid) };
            Content = ContentGrid;
            ContentGrid.HorizontalAlignment = HorizontalAlignment.Stretch;
            ContentGrid.VerticalAlignment = VerticalAlignment.Stretch;
            Stretch = VideoView.Stretch;
            StretchDirection = VideoView.StretchDirection;

            // Add the child controls
            ContentGrid.Children.Add(VideoView);
            ContentGrid.Children.Add(SubtitleView);
            SubtitleView.Padding = new Thickness(5, 0, 5, 0);
            SubtitleView.FontSize = 60;
            SubtitleView.FontFamily = new System.Windows.Media.FontFamily("Arial Rounded MT Bold");
            SubtitleView.FontWeight = FontWeights.Normal;
            SubtitleView.TextOutlineWidth = new Thickness(4);
            SubtitleView.TextForeground = System.Windows.Media.Brushes.LightYellow;

            // Update as the VideoView updates but check if there are valid dimensions and it actually has video
            VideoView.LayoutUpdated += (s, e) =>
            {
                // When video dimensions are invalid, let's not do any layout.
                if (VideoView.ActualWidth <= 0 || VideoView.ActualHeight <= 0)
                    return;

                // Position the Subtitles
                var videoViewPosition = VideoView.TransformToAncestor(ContentGrid).Transform(new Point(0, 0));
                var targetHeight = VideoView.ActualHeight / 5d;
                var targetWidth = VideoView.ActualWidth;

                if (SubtitleView.Height != targetHeight)
                    SubtitleView.Height = targetHeight;

                if (SubtitleView.Width != targetWidth)
                    SubtitleView.Width = targetWidth;

                var verticalOffset = ContentGrid.ActualHeight - (videoViewPosition.Y + VideoView.ActualHeight);
                var verticalOffsetPadding = VideoView.ActualHeight / 20;
                var marginBottom = verticalOffset + verticalOffsetPadding;

                if (SubtitleView.Margin.Bottom != marginBottom)
                    SubtitleView.Margin = new Thickness(0, 0, 0, marginBottom);
            };

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
                // Setup the media engine
                MediaCore = new MediaEngine(this, new WindowsMediaConnector(this));
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
        /// Provides access to the underlying media engine driving this control.
        /// This property is intender for advance usages only.
        /// </summary>
        public MediaEngine MediaCore
        {
            get { return m_MediaCore; }
            private set { m_MediaCore = value; }
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
        /// This is the image that holds video bitmaps
        /// </summary>
        public Image VideoView { get; } = new Image();

        /// <summary>
        /// A viewbox holding the subtitle text blocks
        /// </summary>
        public SubtitleTextBlock SubtitleView { get; } = new SubtitleTextBlock();

        /// <summary>
        /// Gets the grid control holding the rest of the controls.
        /// </summary>
        internal Grid ContentGrid { get; }

        /// <summary>
        /// When position is being set from within this control, this field will
        /// be set to true. This is useful to detect if the user is setting the position
        /// or if the Position property is being driven from within
        /// </summary>
        internal bool IsPositionUpdating => MediaCore.IsPositionUpdating;

        #endregion

        #region Public API

        /// <summary>
        /// Begins or resumes playback of the currently loaded media.
        /// </summary>
        /// <returns>The awaitable command</returns>
        public async Task Play()
        {
            try { await MediaCore.Play(); }
            catch (TaskCanceledException) { }
            catch (Exception ex) { RaiseMediaFailedEvent(ex); }
        }

        /// <summary>
        /// Pauses playback of the currently loaded media.
        /// </summary>
        /// <returns>The awaitable command</returns>
        public async Task Pause()
        {
            try { await MediaCore.Pause(); }
            catch (TaskCanceledException) { }
            catch (Exception ex) { RaiseMediaFailedEvent(ex); }
        }

        /// <summary>
        /// Pauses and rewinds the currently loaded media.
        /// </summary>
        /// <returns>The awaitable command</returns>
        public async Task Stop()
        {
            try { await MediaCore.Stop(); }
            catch (TaskCanceledException) { }
            catch (Exception ex) { RaiseMediaFailedEvent(ex); }
        }

        /// <summary>
        /// Closes the currently loaded media.
        /// </summary>
        /// <returns>The awaitable command</returns>
        public async Task Close()
        {
            try { await MediaCore.Close(); }
            catch (TaskCanceledException) { }
            catch (Exception ex) { RaiseMediaFailedEvent(ex); }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            m_MediaCore.Dispose();
        }

        /// <summary>
        /// Invoked whenever the effective value of any dependency property on this <see cref="T:System.Windows.FrameworkElement" /> has been updated. The specific dependency property that changed is reported in the arguments parameter. Overrides <see cref="M:System.Windows.DependencyObject.OnPropertyChanged(System.Windows.DependencyPropertyChangedEventArgs)" />.
        /// </summary>
        /// <param name="e">The event data that describes the property that changed, as well as old and new values.</param>
        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            if (e.Property.Name == nameof(HorizontalAlignment))
                VideoView.HorizontalAlignment = (HorizontalAlignment)e.NewValue;
            else if (e.Property.Name == nameof(VerticalAlignment))
                VideoView.VerticalAlignment = (VerticalAlignment)e.NewValue;

            base.OnPropertyChanged(e);
        }

        #endregion

    }
}
