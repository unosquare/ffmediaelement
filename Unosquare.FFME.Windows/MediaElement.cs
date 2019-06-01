namespace Unosquare.FFME
{
    using Common;
    using Engine;
    using Platform;
    using Primitives;
    using Rendering;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Data;
    using System.Windows.Interop;
    using System.Windows.Markup;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using System.Windows.Threading;
    using Bitmap = System.Drawing.Bitmap;

    /// <summary>
    /// Represents a control that contains audio and/or video.
    /// In contrast with System.Windows.Controls.MediaElement, this version uses
    /// the FFmpeg library to perform reading and decoding of media streams.
    /// </summary>
    /// <seealso cref="UserControl" />
    /// <seealso cref="IUriContext" />
    [Localizability(LocalizationCategory.NeverLocalize)]
    [DefaultProperty(nameof(Source))]
    public sealed partial class MediaElement : UserControl, IUriContext
    {
        #region Fields and Property Backing

        /// <summary>
        /// The affects measure and render metadata options.
        /// </summary>
        internal const FrameworkPropertyMetadataOptions AffectsMeasureAndRender
            = FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender;

        /// <summary>
        /// The allow content change flag.
        /// </summary>
        private readonly bool AllowContentChange;

        private readonly ConcurrentBag<string> PropertyUpdates = new ConcurrentBag<string>();
        private readonly AtomicBoolean m_IsStateUpdating = new AtomicBoolean(false);

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes static members of the <see cref="MediaElement"/> class.
        /// </summary>
        static MediaElement()
        {
            MediaEngine.FFmpegMessageLogged += (s, message) =>
                FFmpegMessageLogged?.Invoke(typeof(MediaElement), new MediaLogMessageEventArgs(message));

            // A GUI context must be registered
            Library.RegisterGuiContext(GuiContext.Current);

            // Content property cannot be changed.
            ContentProperty.OverrideMetadata(typeof(MediaElement), new FrameworkPropertyMetadata(null, OnCoerceContentValue));

            var style = new Style(typeof(MediaElement), null);
            style.Setters.Add(new Setter(FlowDirectionProperty, FlowDirection.LeftToRight));
            style.Seal();
            StyleProperty.OverrideMetadata(typeof(MediaElement), new FrameworkPropertyMetadata(style));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaElement" /> class.
        /// </summary>
        public MediaElement()
        {
            try
            {
                AllowContentChange = true;

                if (!Library.IsInDesignMode)
                {
                    // Setup the media engine and property updates timer
                    MediaCore = new MediaEngine(this, new MediaConnector(this));
                    MediaCore.State.PropertyChanged += (s, e) => NotifyMediaCoreStateChanged(e.PropertyName);
                }

                InitializeComponent();

                // When the media element is removed from the visual tree
                // we want to close the current media to prevent memory leaks
                Unloaded += async (s, e) => await Close();
            }
            finally
            {
                AllowContentChange = false;
            }
        }

        #endregion

        #region Properties

        /// <inheritdoc />
        Uri IUriContext.BaseUri { get; set; }

        /// <summary>
        /// Provides access to various internal media renderer options.
        /// The default options are optimal to work for most media streams.
        /// This is an advanced feature and it is not recommended to change these
        /// options without careful consideration.
        /// </summary>
        public RendererOptions RendererOptions { get; } = new RendererOptions();

        /// <summary>
        /// This is the image that holds video bitmaps. It is a Hosted Image which means that in a WPF
        /// GUI context, it runs on its own dispatcher (multi-threaded UI).
        /// </summary>
        internal ImageHost VideoView { get; } = new ImageHost(GuiContext.Current.Type == GuiContextType.WPF && Library.EnableWpfMultiThreadedVideo)
        { Name = nameof(VideoView) };

        /// <summary>
        /// Gets the closed captions view control.
        /// </summary>
        internal ClosedCaptionsControl CaptionsView { get; } = new ClosedCaptionsControl { Name = nameof(CaptionsView) };

        /// <summary>
        /// A ViewBox holding the subtitle text blocks.
        /// </summary>
        internal SubtitlesControl SubtitlesView { get; } = new SubtitlesControl { Name = nameof(SubtitlesView) };

        /// <summary>
        /// Gets the grid control holding the rest of the controls.
        /// </summary>
        internal Grid ContentGrid { get; } = new Grid { Name = nameof(ContentGrid) };

        /// <summary>
        /// Determines whether the property values are being copied over from the
        /// <see cref="MediaCore"/> state.
        /// </summary>
        internal bool IsStateUpdating
        {
            get => m_IsStateUpdating.Value;
            set => m_IsStateUpdating.Value = value;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Captures the currently displayed video image and returns a GDI bitmap.
        /// </summary>
        /// <returns>The GDI bitmap copied from the video renderer.</returns>
        public ConfiguredTaskAwaitable<Bitmap> CaptureBitmapAsync() => Task.Run(async () =>
        {
            Bitmap retrievedBitmap = null;

            // Since VideoView might be hosted on a different dispatcher,
            // we use the custom InvokeAsync method
            await VideoView?.InvokeAsync(() =>
            {
                var source = VideoView?.Source?.Clone() as BitmapSource;
                if (source == null)
                    return;

                source.Freeze();
                var encoder = new BmpBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(source));
                var stream = new MemoryStream();
                encoder.Save(stream);
                stream.Position = 0;
                retrievedBitmap = new Bitmap(stream);
            });

            return retrievedBitmap;
        }).ConfigureAwait(true);

        #endregion

        #region Methods

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
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };

            BindingOperations.SetBinding(target, targetProperty, binding);
        }

        /// <summary>
        /// Called when [coerce content value].
        /// </summary>
        /// <param name="d">The d.</param>
        /// <param name="baseValue">The base value.</param>
        /// <returns>The content property value.</returns>
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
            // This is because the hosted element gets created after the MediaElement properties
            // might have been set.
            VideoView.ElementLoaded += (vs, ve) =>
            {
                VideoView.UseLayoutRounding = true;
                VideoView.SnapsToDevicePixels = true;
                VideoView.Focusable = false;
                VideoView.IsHitTestVisible = false;
                VideoView.Stretch = Stretch;
                VideoView.StretchDirection = StretchDirection;

                // Wire up the layout updates
                VideoView.LayoutUpdated += HandleVideoViewLayoutUpdates;
            };

            // Set some default layout properties.
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;

            // Setup the content grid and add it as part of the user control
            Content = ContentGrid;

            // Set some layout defaults
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
            SubtitlesView.HorizontalAlignment = HorizontalAlignment.Left;
            SubtitlesView.VerticalAlignment = VerticalAlignment.Top;

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

            // Position the Captions View
            CaptionsView.HorizontalAlignment = HorizontalAlignment.Left;
            CaptionsView.VerticalAlignment = VerticalAlignment.Top;

            // Compose the control by adding overlapping children
            ContentGrid.Children.Add(VideoView);
            ContentGrid.Children.Add(SubtitlesView);
            ContentGrid.Children.Add(CaptionsView);

            // Display the control (or not)
            if (Library.IsInDesignMode)
            {
                var bitmap = Properties.Resources.FFmpegMediaElementBackground;
                var bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(
                    bitmap.GetHbitmap(), IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                var controlBitmap = new WriteableBitmap(bitmapSource);
                VideoView.Source = controlBitmap;
            }
            else
            {
                // Check that all properties map back to the media state
                if (PropertyMapper.MissingPropertyMappings.Count > 0)
                {
                    throw new KeyNotFoundException($"{nameof(MediaElement)} is missing properties exposed by {nameof(IMediaEngineState)}. " +
                        $"Missing properties are: {string.Join(", ", PropertyMapper.MissingPropertyMappings)}. " +
                        $"Please add these properties to the {nameof(MediaElement)} class.");
                }
            }

            // Bind Content View Properties
            BindProperty(VideoView, HorizontalAlignmentProperty, this, nameof(HorizontalContentAlignment), BindingMode.OneWay);
            BindProperty(VideoView, VerticalAlignmentProperty, this, nameof(VerticalContentAlignment), BindingMode.OneWay);
        }

        /// <summary>
        /// Queues a property notification change from the state on to the UI thread.
        /// </summary>
        /// <param name="propertyName">The media state property that has changed.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void NotifyMediaCoreStateChanged(string propertyName)
        {
            PropertyUpdates.Add(propertyName);

            // The notifications occur in the Background priority
            // which is below the Input priority.
            Dispatcher.BeginInvoke((Action)(() =>
            {
                try
                {
                    if (PropertyUpdates.Count <= 0)
                        return;

                    IsStateUpdating = true;
                    while (PropertyUpdates.TryTake(out var p))
                    {
                        if (p == nameof(Position))
                        {
                            if (!IsSeeking)
                                Position = MediaCore.State.Position;

                            NotifyPropertyChangedEvent(nameof(RemainingDuration));
                        }
                        else if (p == nameof(Volume))
                        {
                            Volume = MediaCore.State.Volume;
                        }
                        else if (p == nameof(Balance))
                        {
                            Balance = MediaCore.State.Balance;
                        }
                        else if (p == nameof(IsMuted))
                        {
                            IsMuted = MediaCore.State.IsMuted;
                        }
                        else if (p == nameof(SpeedRatio))
                        {
                            SpeedRatio = MediaCore.State.SpeedRatio;
                        }
                        else if (p == nameof(Source))
                        {
                            Source = MediaCore.State.Source;
                        }

                        NotifyPropertyChangedEvent(p);
                    }
                }
                finally
                {
                    IsStateUpdating = false;
                }
            }), DispatcherPriority.Background);
        }

        /// <summary>
        /// Handles the video view layout updates.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void HandleVideoViewLayoutUpdates(object sender, EventArgs e)
        {
            if (ContentGrid.Children.IndexOf(VideoView) < 0 || VideoView.Element == null)
                return;

            // Compute the position offset of the video
            var videoPosition = VideoView.HasOwnDispatcher ?
                VideoView.TransformToAncestor(ContentGrid).Transform(new Point(0, 0)) :
                VideoView.Element.TransformToAncestor(ContentGrid).Transform(new Point(0, 0));

            // Compute the dimensions of the video
            var videoSize = VideoView.HasOwnDispatcher ?
                VideoView.RenderSize :
                VideoView.Element.DesiredSize;

            // Validate the dimensions; avoid layout operations with invalid values
            if (videoSize.Width <= 0 || double.IsNaN(videoSize.Width) ||
                videoSize.Height <= 0 || double.IsNaN(videoSize.Height))
            {
                return;
            }

            if (HasVideo || Library.IsInDesignMode)
            {
                // Position and Size the Captions View
                CaptionsView.Width = Math.Floor(videoSize.Width);
                CaptionsView.Height = Math.Floor(videoSize.Height * .80); // FCC Safe Caption Area Dimensions
                CaptionsView.Margin = new Thickness(
                    Math.Floor(videoPosition.X + ((videoSize.Width - CaptionsView.RenderSize.Width) / 2d)),
                    Math.Floor(videoPosition.Y + ((videoSize.Height - CaptionsView.RenderSize.Height) / 2d)),
                    0,
                    0);
                CaptionsView.Visibility = Visibility.Visible;

                // Position and Size the Subtitles View
                SubtitlesView.Width = Math.Floor(videoSize.Width * 0.9d);
                SubtitlesView.Height = Math.Floor(videoSize.Height / 8d);
                SubtitlesView.Margin = new Thickness(
                    Math.Floor(videoPosition.X + ((videoSize.Width - SubtitlesView.RenderSize.Width) / 2d)),
                    Math.Floor(videoPosition.Y + videoSize.Height - (1.8 * SubtitlesView.RenderSize.Height)),
                    0,
                    0);

                SubtitlesView.Visibility = Visibility.Visible;
            }
            else
            {
                CaptionsView.Width = 0;
                CaptionsView.Height = 0;
                CaptionsView.Visibility = Visibility.Collapsed;

                SubtitlesView.Width = 0;
                SubtitlesView.Height = 0;
                SubtitlesView.Visibility = Visibility.Collapsed;
            }
        }

        #endregion
    }
}