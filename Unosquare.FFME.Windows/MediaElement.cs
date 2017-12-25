namespace Unosquare.FFME
{
    using Core;
    using Rendering;
    using System;
    using System.ComponentModel;
    using System.Runtime.CompilerServices;
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

        /// <summary>
        /// IUriContext BaseUri backing
        /// </summary>
        private Uri m_BaseUri = null;

#pragma warning restore SA1401 // Fields must be private
        #endregion

        #region Wrapped media control

        /// <summary>
        /// Common player part we are wrapping in this control.
        /// </summary>
        private MediaElementCore mediaElementCore;

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

            // Platform specific implementation
            Platform.SetDllDirectory = NativeMethods.SetDllDirectory;
            Platform.CopyMemory = NativeMethods.CopyMemory;
            Platform.FillMemory = NativeMethods.FillMemory;
            Platform.CreateTimer = (priority) =>
            {
                return new CustomDispatcherTimer((DispatcherPriority)priority);
            };
            Platform.UIInvoke = (priority, action) => Runner.UIInvoke((DispatcherPriority)priority, action);
            Platform.UIEnqueueInvoke = (priority, action, args) => Runner.UIEnqueueInvoke((DispatcherPriority)priority, action, args);
            Platform.CreateRenderer = (mediaType, m) =>
            {
                if (mediaType == MediaType.Audio) return new AudioRenderer(m);
                else if (mediaType == MediaType.Video) return new VideoRenderer(m);
                else if (mediaType == MediaType.Subtitle) return new SubtitleRenderer(m);

                throw new ArgumentException($"No suitable renderer for Media Type '{mediaType}'");
            };

            // Simply forward the calls
            MediaElementCore.FFmpegMessageLogged += (o, e) => FFmpegMessageLogged?.Invoke(o, e);
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

            mediaElementCore = new MediaElementCore(this, WPFUtils.IsInDesignTime);

            if (WPFUtils.IsInDesignTime)
            {
                // Shows an FFmpeg image if we are in design-time
                var bitmap = Properties.Resources.FFmpegMediaElementBackground;
                var bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(
                    bitmap.GetHbitmap(), IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                var controlBitmap = new WriteableBitmap(bitmapSource);
                ViewBox.Source = controlBitmap;
            }
            else
            {
                // Bind to RoutedEvent events
                mediaElementCore.MediaOpening += (s, e) => RaiseMediaOpeningEvent();
                mediaElementCore.MediaOpened += (s, e) => RaiseMediaOpenedEvent();
                mediaElementCore.MediaClosed += (s, e) => RaiseMediaClosedEvent();
                mediaElementCore.MediaFailed += (s, e) => RaiseMediaFailedEvent(e.Exception);
                mediaElementCore.MediaEnded += (s, e) => RaiseMediaEndedEvent();
                mediaElementCore.BufferingStarted += (s, e) => RaiseBufferingStartedEvent();
                mediaElementCore.BufferingEnded += (s, e) => RaiseBufferingEndedEvent();
                mediaElementCore.SeekingStarted += (s, e) => RaiseSeekingStartedEvent();
                mediaElementCore.SeekingEnded += (s, e) => RaiseSeekingEndedEvent();

                // Bind to non-RoutedEvent events
                mediaElementCore.MessageLogged += (o, e) => MessageLogged?.Invoke(this, e);
                mediaElementCore.PositionChanged += (s, e) => PositionChanged?.Invoke(this, e);

                // Bind to INotifyPropertyChanged event: PropertyChanged
                mediaElementCore.PropertyChanged += MediaElementCore_PropertyChanged;
            }

            m_Metadata = CollectionViewSource.GetDefaultView(mediaElementCore.Metadata) as ICollectionView;
        }

        #endregion

        #region Events

        /// <summary>
        /// Occurs when a logging message from the FFmpeg library has been received.
        /// This is shared across all instances of Media Elements
        /// </summary>
        public static event EventHandler<MediaLogMessagEventArgs> FFmpegMessageLogged;

        /// <summary>
        /// Multicast event for property change notifications.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Occurs when a logging message has been logged.
        /// This does not include FFmpeg messages.
        /// </summary>
        public event EventHandler<MediaLogMessagEventArgs> MessageLogged;

        /// <summary>
        /// Occurs when the underlying stream position is changed.
        /// This event is not fired when the position is written from user code but rather when the
        /// underlying media naturally drives the position.
        /// </summary>
        public event EventHandler<PositionChangedEventArgs> PositionChanged;

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
        /// When position is being set from within this control, this field will
        /// be set to true. This is useful to detect if the user is setting the position
        /// or if the Position property is being driven from within
        /// </summary>
        internal bool IsPositionUpdating => mediaElementCore.IsPositionUpdating;

        /// <summary>
        /// Gets the grid control holding the rest of the controls.
        /// </summary>
        internal Grid ContentGrid { get; }

        #endregion

        #region Public API

        /// <summary>
        /// Begins or resumes playback of the currently loaded media.
        /// </summary>
        public void Play() => mediaElementCore.Play();

        /// <summary>
        /// Pauses playback of the currently loaded media.
        /// </summary>
        public void Pause() => mediaElementCore.Pause();

        /// <summary>
        /// Pauses and rewinds the currently loaded media.
        /// </summary>
        public void Stop() => mediaElementCore.Stop();

        /// <summary>
        /// Closes the currently loaded media.
        /// </summary>
        public void Close() => mediaElementCore.Close();

        #endregion

        #region Methods

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            mediaElementCore.Dispose();
        }

        private void MediaElementCore_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                // forward internal changes to the dependency properties

                // dependency properties
                case nameof(MediaElementCore.Source):
                    this.Source = mediaElementCore.Source;
                    break;
                case nameof(MediaElementCore.LoadedBehavior):
                    this.LoadedBehavior = (MediaState)mediaElementCore.LoadedBehavior;
                    break;
                case nameof(MediaElementCore.SpeedRatio):
                    this.SpeedRatio = mediaElementCore.SpeedRatio;
                    break;
                case nameof(MediaElementCore.UnloadedBehavior):
                    this.UnloadedBehavior = (MediaState)mediaElementCore.UnloadedBehavior;
                    break;
                case nameof(MediaElementCore.Volume):
                    this.Volume = mediaElementCore.Volume;
                    break;
                case nameof(MediaElementCore.Balance):
                    this.Balance = mediaElementCore.Balance;
                    break;
                case nameof(MediaElementCore.IsMuted):
                    this.IsMuted = mediaElementCore.IsMuted;
                    break;
                case nameof(MediaElementCore.ScrubbingEnabled):
                    this.ScrubbingEnabled = mediaElementCore.ScrubbingEnabled;
                    break;
                case nameof(MediaElementCore.Position):
                    this.Position = mediaElementCore.Position;
                    break;

                // Simply forward notification of same-named properties
                case nameof(IsOpen):
                case nameof(IsOpening):
                case nameof(MediaFormat):
                case nameof(HasAudio):
                case nameof(HasVideo):
                case nameof(VideoCodec):
                case nameof(VideoBitrate):
                case nameof(NaturalVideoWidth):
                case nameof(NaturalVideoHeight):
                case nameof(VideoFrameRate):
                case nameof(VideoFrameLength):
                case nameof(AudioCodec):
                case nameof(AudioBitrate):
                case nameof(AudioChannels):
                case nameof(AudioSampleRate):
                case nameof(AudioBitsPerSample):
                case nameof(NaturalDuration):
                case nameof(CanPause):
                case nameof(IsLiveStream):
                case nameof(IsSeekable):
                case nameof(BufferCacheLength):
                case nameof(DownloadCacheLength):
                case nameof(FrameStepDuration):
                case nameof(MediaState):
                case nameof(IsBuffering):
                case nameof(BufferingProgress):
                case nameof(IsPlaying):
                case nameof(DownloadProgress):
                case nameof(HasMediaEnded):
                case nameof(IsSeeking):
                case nameof(IsPositionUpdating):
                case nameof(Metadata):
                    OnPropertyChanged(e.PropertyName);
                    break;
            }
        }

        #endregion

        #region INotifyPropertyChanged Implementation

        /// <summary>
        /// Notifies listeners that a property value has changed.
        /// </summary>
        /// <param name="propertyName">Name of the property used to notify listeners.  This
        /// value is optional and can be provided automatically when invoked from compilers
        /// that support <see cref="CallerMemberNameAttribute"/>.</param>
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            if (PropertyChanged == null) return;
            Runner.UIInvoke(DispatcherPriority.DataBind, () =>
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            });
        }

        #endregion

    }
}
