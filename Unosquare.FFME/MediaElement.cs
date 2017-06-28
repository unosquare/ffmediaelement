namespace Unosquare.FFME
{
    using Commands;
    using Core;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
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
    [Localizability(LocalizationCategory.NeverLocalize)]
    [DefaultProperty(nameof(Source))]
    public sealed partial class MediaElement : UserControl, IDisposable, INotifyPropertyChanged, IUriContext
    {
        #region Static Definitions

        private static string m_FFmpegDirectory = null;
        internal static bool IsFFmpegLoaded = false;

        #endregion

        #region Property Backing

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

        /// <summary>
        /// The position update timer
        /// </summary>
        private DispatcherTimer UIPropertyUpdateTimer = null;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes the <see cref="MediaElement"/> class.
        /// </summary>
        static MediaElement()
        {
            var style = new Style(typeof(MediaElement), null);
            style.Setters.Add(new Setter(FlowDirectionProperty, FlowDirection.LeftToRight));
            style.Seal();
            StyleProperty.OverrideMetadata(typeof(MediaElement), new FrameworkPropertyMetadata(style));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaElement" /> class.
        /// </summary>
        public MediaElement()
            : base()
        {
            Content = ViewBox;
            Stretch = ViewBox.Stretch;
            StretchDirection = ViewBox.StretchDirection;
            Logger = new GenericMediaLogger<MediaElement>(this);
            Commands = new MediaCommandManager(this);

            if (Utils.IsInDesignTime)
            {
                // Shows an FFmpeg image if we are in design-time
                var bitmap = Properties.Resources.FFmpegMediaElementBackground;
                var bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(bitmap.GetHbitmap(), IntPtr.Zero,
                    Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                var targetBitmap = new WriteableBitmap(bitmapSource);
                ViewBox.Source = targetBitmap;
            }
            else
            {
                // The UI Property update timer is responsible for timely updates to properties outside of the worker threads
                UIPropertyUpdateTimer = new DispatcherTimer(DispatcherPriority.DataBind)
                {
                    Interval = Constants.PositionUpdateInterval,
                    IsEnabled = true
                };

                // The tick callback performs the updates
                UIPropertyUpdateTimer.Tick += (s, e) =>
                {
                    UpdatePosition(IsOpen ? Clock?.Position ?? TimeSpan.Zero : TimeSpan.Zero);

                    var downloadProgress = Math.Min(1d, Math.Round((Container?.Components.PacketBufferLength ?? 0d) / DownloadCacheLength, 3));
                    if (double.IsNaN(downloadProgress)) downloadProgress = 0;
                    DownloadProgress = downloadProgress;
                };

                // Go ahead and fire up the continuous updates
                UIPropertyUpdateTimer.Start();
            }

            m_MetadataBase = new ObservableCollection<KeyValuePair<string, string>>();
            m_Metadata = CollectionViewSource.GetDefaultView(m_MetadataBase) as ICollectionView;
        }

        #endregion

        #region Properties

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
        /// Gets or sets the FFmpeg path from which to load the FFmpeg binaries.
        /// You must set this path before setting the Source property for the first time on any instance of this control.
        /// Settng this property when FFmpeg binaries have been registered will throw an exception.
        /// </summary>
        public static string FFmpegDirectory
        {
            get { return m_FFmpegDirectory; }
            set
            {
                if (IsFFmpegLoaded == false)
                    m_FFmpegDirectory = value;
                else
                    throw new InvalidOperationException($"Unable to set a new FFmpeg registration path: {value}. FFmpeg binaries have already been registered.");
            }
        }

        #endregion

        #region Logging Events

        /// <summary>
        /// Occurs when a logging message from the FFmpeg library has been received.
        /// This is shared across all instances of Media Elements
        /// </summary>
        public static event EventHandler<MediaLogMessagEventArgs> FFmpegMessageLogged;

        /// <summary>
        /// Raises the FFmpegMessageLogged event
        /// </summary>
        /// <param name="eventArgs">The <see cref="MediaLogMessagEventArgs" /> instance containing the event data.</param>
        internal static void RaiseFFmpegMessageLogged(MediaLogMessagEventArgs eventArgs)
        {
            FFmpegMessageLogged?.Invoke(typeof(MediaElement), eventArgs);
        }

        /// <summary>
        /// Occurs when a logging message has been logged.
        /// This does not include FFmpeg messages.
        /// </summary>
        public event EventHandler<MediaLogMessagEventArgs> MessageLogged;

        /// <summary>
        /// Raises the MessageLogged event
        /// </summary>
        /// <param name="eventArgs">The <see cref="MediaLogMessagEventArgs" /> instance containing the event data.</param>
        internal void RaiseMessageLogged(MediaLogMessagEventArgs eventArgs)
        {
            MessageLogged?.Invoke(this, eventArgs);
        }

        #endregion

        #region INotifyPropertyChanged Implementation

        /// <summary>
        /// Multicast event for property change notifications.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

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
            Utils.UIInvoke(DispatcherPriority.DataBind, () =>
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            });
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="alsoManaged"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        private void Dispose(bool alsoManaged)
        {
            if (alsoManaged)
            {
                // free managed resources
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

            }
        }

        #endregion

    }
}
