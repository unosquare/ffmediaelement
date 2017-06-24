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

        // This is the image that will display the video from a Writeable Bitmap
        internal readonly Image ViewBox = new Image();

        private Action<MediaLogMessageType, string> m_LogMessageCallback = null;

        /// <summary>
        /// Gets or sets the horizontal alignment characteristics applied to this element when it is composed within a parent element, such as a panel or items control.
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
        /// Initializes a new instance of the <see cref="MediaElement"/> class.
        /// </summary>
        public MediaElement()
            : base()
        {
            Content = ViewBox;
            Stretch = ViewBox.Stretch;
            StretchDirection = ViewBox.StretchDirection;
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

            m_MetadataBase = new ObservableCollection<KeyValuePair<string, string>>();
            m_Metadata = CollectionViewSource.GetDefaultView(m_MetadataBase) as ICollectionView;
        }

        #endregion

        #region Logging

        /// <summary>
        /// Gets or sets the log message callback.
        /// All logging messages will be passed to this method if set.
        /// </summary>
        public Action<MediaLogMessageType, string> LogMessageCallback
        {
            get { return m_LogMessageCallback; }
            set
            {
                m_LogMessageCallback = value;
                try
                {
                    if (Container != null)
                        Container.MediaOptions.LogMessageCallback = value;
                }
                catch
                {
                    // swallow
                }

            }
        }

        #endregion

        #region FFmpeg Registration

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

            }
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
            InvokeOnUI(DispatcherPriority.DataBind, () =>
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            });
        }

        #endregion

        #region IUriContext Implementation

        // IUriContext BaseUri backing
        private Uri m_BaseUri = null;

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

        #endregion

        #region Debugging

        /// <summary>
        /// Dumps the state into a string dictionary.
        /// Optionally, it prints the output to the debugging console
        /// </summary>
        public Dictionary<string, string> DumpState(bool printToDebuggingConsole)
        {
            //Pause();

            var dict = new Dictionary<string, string>();

            dict["MediaElement/NotificationProperties/HasAudio"] = string.Format("{0}", HasAudio);
            dict["MediaElement/NotificationProperties/HasVideo"] = string.Format("{0}", HasVideo);
            dict["MediaElement/NotificationProperties/VideoCodec"] = string.Format("{0}", VideoCodec);
            dict["MediaElement/NotificationProperties/VideoBitrate"] = string.Format("{0}", VideoBitrate);
            dict["MediaElement/NotificationProperties/NaturalVideoWidth"] = string.Format("{0}", NaturalVideoWidth);
            dict["MediaElement/NotificationProperties/NaturalVideoHeight"] = string.Format("{0}", NaturalVideoHeight);
            dict["MediaElement/NotificationProperties/VideoFrameRate"] = string.Format("{0}", VideoFrameRate);
            dict["MediaElement/NotificationProperties/VideoFrameLength"] = string.Format("{0}", VideoFrameLength);
            dict["MediaElement/NotificationProperties/AudioCodec"] = string.Format("{0}", AudioCodec);
            dict["MediaElement/NotificationProperties/AudioBitrate"] = string.Format("{0}", AudioBitrate);
            dict["MediaElement/NotificationProperties/AudioChannels"] = string.Format("{0}", AudioChannels);
            dict["MediaElement/NotificationProperties/AudioSampleRate"] = string.Format("{0}", AudioSampleRate);
            dict["MediaElement/NotificationProperties/AudioBitsPerSample"] = string.Format("{0}", AudioBitsPerSample);
            dict["MediaElement/NotificationProperties/NaturalDuration"] = string.Format("{0}", NaturalDuration);
            dict["MediaElement/NotificationProperties/IsPlaying"] = string.Format("{0}", IsPlaying);
            dict["MediaElement/NotificationProperties/HasMediaEnded"] = string.Format("{0}", HasMediaEnded);

            dict["MediaElement/DependencyProperties/Source"] = string.Format("{0}", Source); // TODO: minor work required (prevent concurrent opening/closing)
            dict["MediaElement/DependencyProperties/Stretch"] = string.Format("{0}", Stretch);
            dict["MediaElement/DependencyProperties/StretchDirection"] = string.Format("{0}", StretchDirection);
            dict["MediaElement/DependencyProperties/Volume"] = string.Format("{0}", Volume);
            dict["MediaElement/DependencyProperties/Balance"] = string.Format("{0}", Balance);
            dict["MediaElement/DependencyProperties/ScrubbingEnabled"] = string.Format("{0}", ScrubbingEnabled); // TODO: not yet implemented
            dict["MediaElement/DependencyProperties/UnloadedBehavior"] = string.Format("{0}", UnloadedBehavior); // TODO: not yet implemented
            dict["MediaElement/DependencyProperties/LoadedBehavior"] = string.Format("{0}", LoadedBehavior);
            dict["MediaElement/DependencyProperties/IsMuted"] = string.Format("{0}", IsMuted);
            dict["MediaElement/DependencyProperties/Position"] = string.Format("{0}", Position); // TODO: needs work with seeking
            dict["MediaElement/DependencyProperties/SpeedRatio"] = string.Format("{0}", SpeedRatio); //TODO: needs implementation

            const int keyStringLength = 80;
            if (printToDebuggingConsole)
            {
                foreach (var kvp in dict)
                {
                    var paddingLength = keyStringLength - kvp.Key.Length;
                    if (paddingLength <= 0) paddingLength = 1;
                    var paddingString = new string('.', paddingLength);
                    this.Log(MediaLogMessageType.Info, $"{kvp.Key}{paddingString}{kvp.Value}");
                }
            }

            return dict;

        }

        #endregion
    }
}
