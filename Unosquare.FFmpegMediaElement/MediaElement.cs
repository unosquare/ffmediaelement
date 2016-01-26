namespace Unosquare.FFmpegMediaElement
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Runtime.CompilerServices;
    using System.Windows;
    using System.Windows.Controls;
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
    public partial class MediaElement : UserControl, IDisposable, INotifyPropertyChanged, IUriContext
    {
        // TODO: Implement network buffering events that the standard MediaElement provides.

        #region Property Backing

        // This is the image that will display the video from a Writeable Bitmap
        private readonly Image ViewBox = new Image();

        // The target bitmap is where the video image will be held
        private WriteableBitmap TargetBitmap = null;

        // Our main character is the Media object.
        private FFmpegMedia Media = null;

        #endregion

        #region FFmpeg Paths

        /// <summary>
        /// Provides access to the paths where FFmpeg binaries are extracted to
        /// </summary>
        static public class FFmpegPaths
        {
            /// <summary>
            /// Initializes the <see cref="FFmpegPaths"/> class.
            /// </summary>
            static FFmpegPaths()
            {
                Helper.RegisterFFmpeg();
            }

            /// <summary>
            /// Gets the path to where the FFmpeg binaries are stored
            /// </summary>
            public static string BasePath{ get; internal set; }

            /// <summary>
            /// Gets the full path to ffmpeg.exe
            /// </summary>
            public static string FFmpeg { get; internal set; }
            /// <summary>
            /// Gets the full path to ffprobe.exe
            /// </summary>
            public static string FFprobe { get; internal set; }

            /// <summary>
            /// Gets the full path to ffplay.exe
            /// </summary>
            public static string FFplay { get; internal set; }
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
            this.Content = this.ViewBox;
            this.Stretch = this.ViewBox.Stretch;
            this.StretchDirection = this.ViewBox.StretchDirection;

            if (Helper.IsInDesignTime)
            {
                // Shows a nice FFmpeg image if we are in design-time
                var bitmap = Properties.Resources.FFmpegMediaElementBackground;
                var bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(bitmap.GetHbitmap(), IntPtr.Zero,
                    Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                this.TargetBitmap = new WriteableBitmap(bitmapSource);
                this.ViewBox.Source = TargetBitmap;
            }
            else
            {
                InitializeSeekPositionTimer();
            }
        }

        /// <summary>
        /// Initializes the seek position timer.
        /// </summary>
        private void InitializeSeekPositionTimer()
        {
            if (SeekPositionUpdateTimer != null) return;

            SeekPositionUpdateTimer = new DispatcherTimer(DispatcherPriority.Input);
            SeekPositionUpdateTimer.Tick += SeekPositionUpdateTimerTick;
            SeekPositionUpdateTimer.Interval = TimeSpan.FromMilliseconds(Constants.SeekPositionUpdateTimerIntervalMillis);
            SeekPositionUpdateTimer.IsEnabled = true;
            SeekPositionUpdateTimer.Start();
        }

        #endregion

        #region IDisposable Implementation

        ~MediaElement()
        {
            this.Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool alsoManaged)
        {
            if (alsoManaged)
            {
                // free managed resources
                if (this.Media != null)
                {
                    this.Media.Dispose();
                    this.Media = null;
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
            if (object.Equals(storage, value))
                return false;

            storage = value;
            this.OnPropertyChanged(propertyName);
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
            var eventHandler = this.PropertyChanged;
            if (eventHandler != null)
                eventHandler(this, new PropertyChangedEventArgs(propertyName));
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
                return this.m_BaseUri;
            }
            set
            {
                this.m_BaseUri = value;
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
            this.Pause();

            var dict = new Dictionary<string, string>();

            dict["MediaElement/NotificationProperties/HasAudio"] = string.Format("{0}", this.HasAudio);
            dict["MediaElement/NotificationProperties/HasVideo"] = string.Format("{0}", this.HasVideo);
            dict["MediaElement/NotificationProperties/VideoCodec"] = string.Format("{0}", this.VideoCodec);
            dict["MediaElement/NotificationProperties/VideoBitrate"] = string.Format("{0}", this.VideoBitrate);
            dict["MediaElement/NotificationProperties/NaturalVideoWidth"] = string.Format("{0}", this.NaturalVideoWidth);
            dict["MediaElement/NotificationProperties/NaturalVideoHeight"] = string.Format("{0}", this.NaturalVideoHeight);
            dict["MediaElement/NotificationProperties/VideoFrameRate"] = string.Format("{0}", this.VideoFrameRate);
            dict["MediaElement/NotificationProperties/VideoFrameLength"] = string.Format("{0}", this.VideoFrameLength);
            dict["MediaElement/NotificationProperties/AudioCodec"] = string.Format("{0}", this.AudioCodec);
            dict["MediaElement/NotificationProperties/AudioBitrate"] = string.Format("{0}", this.AudioBitrate);
            dict["MediaElement/NotificationProperties/AudioChannels"] = string.Format("{0}", this.AudioChannels);
            dict["MediaElement/NotificationProperties/AudioOutputBitsPerSample"] = string.Format("{0}", this.AudioOutputBitsPerSample);
            dict["MediaElement/NotificationProperties/AudioSampleRate"] = string.Format("{0}", this.AudioSampleRate);
            dict["MediaElement/NotificationProperties/AudioOutputSampleRate"] = string.Format("{0}", this.AudioOutputSampleRate);
            dict["MediaElement/NotificationProperties/AudioBytesPerSample"] = string.Format("{0}", this.AudioBytesPerSample);
            dict["MediaElement/NotificationProperties/NaturalDuration"] = string.Format("{0}", this.NaturalDuration);
            dict["MediaElement/NotificationProperties/IsPlaying"] = string.Format("{0}", this.IsPlaying);
            dict["MediaElement/NotificationProperties/HasMediaEnded"] = string.Format("{0}", this.HasMediaEnded);

            dict["MediaElement/DependencyProperties/Source"] = string.Format("{0}", this.Source);
            dict["MediaElement/DependencyProperties/Stretch"] = string.Format("{0}", this.Stretch);
            dict["MediaElement/DependencyProperties/StretchDirection"] = string.Format("{0}", this.StretchDirection);
            dict["MediaElement/DependencyProperties/Volume"] = string.Format("{0}", this.Volume);
            dict["MediaElement/DependencyProperties/Balance"] = string.Format("{0}", this.Balance);
            dict["MediaElement/DependencyProperties/ScrubbingEnabled"] = string.Format("{0}", this.ScrubbingEnabled);
            dict["MediaElement/DependencyProperties/UnloadedBehavior"] = string.Format("{0}", this.UnloadedBehavior);
            dict["MediaElement/DependencyProperties/LoadedBehavior"] = string.Format("{0}", this.LoadedBehavior);
            dict["MediaElement/DependencyProperties/IsMuted"] = string.Format("{0}", this.IsMuted);
            dict["MediaElement/DependencyProperties/Position"] = string.Format("{0}", this.Position);
            dict["MediaElement/DependencyProperties/SpeedRatio"] = string.Format("{0}", this.SpeedRatio);

            const int keyStringLength = 80;
            if (printToDebuggingConsole)
            {
                foreach (var kvp in dict)
                {
                    var paddingLength = keyStringLength - kvp.Key.Length;
                    if (paddingLength <= 0) paddingLength = 1;
                    var paddingString = new string('.', paddingLength);
                    System.Diagnostics.Debug.WriteLine("{0}{1}{2}", kvp.Key, paddingString, kvp.Value);
                }
            }



            return dict;

        }

        #endregion
    }
}
