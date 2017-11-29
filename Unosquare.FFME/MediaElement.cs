﻿namespace Unosquare.FFME
{
    using Commands;
    using Core;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Runtime.CompilerServices;
    using System.Threading;
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
        internal static AtomicBoolean IsFFmpegLoaded = new AtomicBoolean();

        /// <summary>
        /// The logger
        /// </summary>
        internal readonly GenericMediaLogger<MediaElement> Logger;

        /// <summary>
        /// This is the image that will display the video from a Writeable Bitmap
        /// </summary>
        internal readonly Image ViewBox = new Image();

        /// <summary>
        /// To detect redundant calls
        /// </summary>
        internal bool IsDisposed = false;

        /// <summary>
        /// The ffmpeg directory
        /// </summary>
        private static string m_FFmpegDirectory = null;

        /// <summary>
        /// IUriContext BaseUri backing
        /// </summary>
        private Uri m_BaseUri = null;

        /// <summary>
        /// The position update timer
        /// </summary>
        private DispatcherTimer UIPropertyUpdateTimer = null;

        /// <summary>
        /// When position is being set from within this control, this field will
        /// be set to true. This is useful to detect if the user is setting the position
        /// or if the Position property is being driven from within
        /// </summary>
        private AtomicBoolean m_IsPositionUpdating = new AtomicBoolean();

        /// <summary>
        /// Flag when disposing process start but not finished yet
        /// </summary>
        private AtomicBoolean m_IsDisposing = new AtomicBoolean();

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
            Commands = new MediaCommandManager(this);

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
                // The UI Property update timer is responsible for timely updates to properties outside of the worker threads
                // We use the loaded priority because it is the priority right below the Render one.
                UIPropertyUpdateTimer = new DispatcherTimer(DispatcherPriority.Loaded)
                {
                    Interval = Constants.UIPropertyUpdateInterval,
                    IsEnabled = true
                };

                // The tick callback performs the updates
                UIPropertyUpdateTimer.Tick += (s, e) =>
                {
                    UpdatePosition(IsOpen ? Clock?.Position ?? TimeSpan.Zero : TimeSpan.Zero);

                    if (HasMediaEnded == false && CanReadMorePackets && (IsOpening || IsOpen))
                    {
                        var bufferedLength = Container?.Components?.PacketBufferLength ?? 0d;
                        BufferingProgress = Math.Min(1d, bufferedLength / BufferCacheLength);
                        var oldIsBugffering = IsBuffering;
                        var newIsBuffering = bufferedLength < BufferCacheLength;

                        if (oldIsBugffering == false && newIsBuffering == true)
                            RaiseBufferingStartedEvent();
                        else if (oldIsBugffering == true && newIsBuffering == false)
                            RaiseBufferingEndedEvent();

                        IsBuffering = HasMediaEnded == false && newIsBuffering;
                    }
                    else
                    {
                        BufferingProgress = 0;
                        IsBuffering = false;
                    }

                    var downloadProgress = Math.Min(1d, Math.Round((Container?.Components.PacketBufferLength ?? 0d) / DownloadCacheLength, 3));
                    if (double.IsNaN(downloadProgress)) downloadProgress = 0;
                    DownloadProgress = downloadProgress;
                };

                // Go ahead and fire up the continuous updates
                UIPropertyUpdateTimer.Start();

                // for now forward stuff to underlying implementation
                mediaElementCore.MessageLogged += (o, e) => MessageLogged?.Invoke(o, e);
            }

            m_MetadataBase = new ObservableCollection<KeyValuePair<string, string>>();
            m_Metadata = CollectionViewSource.GetDefaultView(m_MetadataBase) as ICollectionView;
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

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the FFmpeg path from which to load the FFmpeg binaries.
        /// You must set this path before setting the Source property for the first time on any instance of this control.
        /// Settng this property when FFmpeg binaries have been registered will throw an exception.
        /// </summary>
        public static string FFmpegDirectory
        {
            get
            {
                return m_FFmpegDirectory;
            }
            set
            {
                if (IsFFmpegLoaded.Value == false)
                {
                    m_FFmpegDirectory = value;
                    return;
                }

                if ((value?.Equals(m_FFmpegDirectory) ?? false) == false)
                    throw new InvalidOperationException($"Unable to set a new FFmpeg registration path: {value}. FFmpeg binaries have already been registered.");
            }
        }

        /// <summary>
        /// Gets or sets the horizontal alignment characteristics applied to this element when it is 
        /// composed within a parent element, such as a panel or items control.
        /// </summary>
        public new HorizontalAlignment HorizontalAlignment
        {
            get
            {
                return base.HorizontalAlignment;
            }
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
            get
            {
                return m_BaseUri;
            }
            set
            {
                m_BaseUri = value;
            }
        }

        /// <summary>
        /// When position is being set from within this control, this field will
        /// be set to true. This is useful to detect if the user is setting the position
        /// or if the Position property is being driven from within
        /// </summary>
        internal bool IsPositionUpdating
        {
            get { return m_IsPositionUpdating.Value; }
            set { m_IsPositionUpdating.Value = value; }
        }

        /// <summary>
        /// Gets the grid control holding the rest of the controls.
        /// </summary>
        internal Grid ContentGrid { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Updates the position property signaling the update is
        /// coming internally. This is to distinguish between user/binding 
        /// written value to the Position Porperty and value set by this control's
        /// internal clock.
        /// </summary>
        /// <param name="value">The current position.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void UpdatePosition(TimeSpan value)
        {
            if (IsPositionUpdating || IsSeeking)
                return;

            IsPositionUpdating = true;
            Runner.UIEnqueueInvoke(
                DispatcherPriority.DataBind,
                (Action<TimeSpan>)((v) =>
                {
                    if (Position != v)
                        SetValue(PositionProperty, v);

                    IsPositionUpdating = false;
                }),
                value);
        }

        #endregion

        #region INotifyPropertyChanged Implementation

        /// <summary>
        /// Checks if a property already matches a desired value.  Sets the property and
        /// notifies listeners only when necessary.
        /// </summary>
        /// <typeparam name="T">Type of the property.</typeparam>
        /// <param name="storage">Reference to a property with both getter and setter.</param>
        /// <param name="value">Desired value for the property.</param>
        /// <param name="propertyName">Name of the property used to notify listeners.  This
        /// value is optional and can be provided automatically when invoked from compilers that
        /// support CallerMemberName.</param>
        /// <returns>True if the value was changed, false if the existing value matched the
        /// desired value.</returns>
        private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(storage, value))
                return false;

            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }

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

        #region IDisposable Implementation

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="alsoManaged"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        private void Dispose(bool alsoManaged)
        {
            if (IsDisposed) return;

            if (alsoManaged)
            {
                m_IsDisposing.Value = true;

                // free managed resources
                Commands.Close().Wait();

                if (Container != null)
                {
                    Container.Dispose();
                    Container = null;
                }

                if (UIPropertyUpdateTimer != null)
                {
                    UIPropertyUpdateTimer.Stop();
                    UIPropertyUpdateTimer.IsEnabled = false;
                    UIPropertyUpdateTimer = null;
                }

                m_PacketReadingCycle.Dispose();
                m_FrameDecodingCycle.Dispose();
                m_BlockRenderingCycle.Dispose();
                m_SeekingDone.Dispose();
            }

            IsDisposed = true;
        }

        #endregion

    }
}
