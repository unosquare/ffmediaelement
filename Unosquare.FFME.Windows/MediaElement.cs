namespace Unosquare.FFME
{
    using Core;
    using Rendering;
    using System;
    using System.ComponentModel;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Data;
    using System.Windows.Interop;
    using System.Windows.Markup;
    using System.Windows.Media.Imaging;
    using System.Windows.Threading;

    /// <summary>
    /// Represents a control that contains audio and/or video.
    /// In contrast with System.Windows.Controls.MediaElement, this version uses
    /// the FFmpeg library to perform reading and decoding of media streams.
    /// </summary>
    /// <seealso cref="System.Windows.Controls.UserControl" />
    /// <seealso cref="System.IDisposable" />
    /// <seealso cref="System.ComponentModel.INotifyPropertyChanged" />
    /// <seealso cref="System.Windows.Markup.IUriContext" />
    [Localizability(LocalizationCategory.NeverLocalize)]
    [DefaultProperty(nameof(Source))]
    public sealed partial class MediaElement : UserControl, IDisposable, INotifyPropertyChanged, IUriContext
    {
        #region Fields and Property Backing

#pragma warning disable SA1401 // Fields must be private

        /// <summary>
        /// The logger
        /// </summary>
        internal readonly GenericMediaLogger<MediaElement> Logger;

        /// <summary>
        /// This is the image that will display the video from a Writeable Bitmap
        /// </summary>
        internal readonly Image ViewBox = new Image();

#pragma warning restore SA1401 // Fields must be private

        /// <summary>
        /// IUriContext BaseUri backing
        /// </summary>
        private Uri m_BaseUri = null;

        private MediaElementCore m_MediaCore;

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
            MediaElementCore.Initialize(WindowsPlatform.Default);
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
            ContentGrid.Children.Add(ViewBox);
            Stretch = ViewBox.Stretch;
            StretchDirection = ViewBox.StretchDirection;
            Logger = new GenericMediaLogger<MediaElement>(this);
            MediaCore = new MediaElementCore(this, WindowsGui.IsInDesignTime, new WindowsEventConnector(this));

            if (WindowsGui.IsInDesignTime)
            {
                // Shows an FFmpeg image if we are in design-time
                var bitmap = Properties.Resources.FFmpegMediaElementBackground;
                var bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(
                    bitmap.GetHbitmap(), IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                var controlBitmap = new WriteableBitmap(bitmapSource);
                ViewBox.Source = controlBitmap;
            }

            // TODO: Maybe make Metadata a little more accessible
            m_Metadata = CollectionViewSource.GetDefaultView(MediaCore.Metadata) as ICollectionView;
        }

        #endregion

        #region Events

        /// <summary>
        /// Occurs when a logging message from the FFmpeg library has been received.
        /// This is shared across all instances of Media Elements
        /// </summary>
        public static event EventHandler<MediaLogMessagEventArgs> FFmpegMessageLogged;

        /// <summary>
        /// Occurs when a logging message has been logged.
        /// This does not include FFmpeg messages.
        /// </summary>
        public event EventHandler<MediaLogMessagEventArgs> MessageLogged;

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
            get => MediaElementCore.FFmpegDirectory;
            set => MediaElementCore.FFmpegDirectory = value;
        }

        /// <summary>
        /// Gets or sets the horizontal alignment characteristics applied to this element when it is 
        /// composed within a parent element, such as a panel or items control.
        /// </summary>
        public new HorizontalAlignment HorizontalAlignment
        {
            get => base.HorizontalAlignment;
            set
            {
                ViewBox.HorizontalAlignment = value;
                base.HorizontalAlignment = value;
            }
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
        /// Common player part we are wrapping in this control.
        /// </summary>
        internal MediaElementCore MediaCore
        {
            get { return m_MediaCore; }
            private set { m_MediaCore = value; }
        }

        /// <summary>
        /// When position is being set from within this control, this field will
        /// be set to true. This is useful to detect if the user is setting the position
        /// or if the Position property is being driven from within
        /// </summary>
        internal bool IsPositionUpdating => MediaCore.IsPositionUpdating;

        /// <summary>
        /// Gets the grid control holding the rest of the controls.
        /// </summary>
        internal Grid ContentGrid { get; }

        #endregion

        #region Public API

        /// <summary>
        /// Begins or resumes playback of the currently loaded media.
        /// </summary>
        /// <returns>The awaitable command</returns>
        public async Task Play() => await MediaCore.Play();

        /// <summary>
        /// Pauses playback of the currently loaded media.
        /// </summary>
        /// <returns>The awaitable command</returns>
        public async Task Pause() => await MediaCore.Pause();

        /// <summary>
        /// Pauses and rewinds the currently loaded media.
        /// </summary>
        /// <returns>The awaitable command</returns>
        public async Task Stop() => await MediaCore.Stop();

        /// <summary>
        /// Closes the currently loaded media.
        /// </summary>
        /// <returns>The awaitable command</returns>
        public async Task Close() => await MediaCore.Close();

        #endregion

        #region Methods

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            m_MediaCore.Dispose();
        }

        #endregion

    }
}
