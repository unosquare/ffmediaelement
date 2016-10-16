namespace Unosquare.FFmpegMediaElement
{
    using System;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Threading;

    partial class MediaElement
    {
        #region Notification Property Backing
        #endregion

        #region Notification Properties

        /// <summary> 
        /// Returns whether the given media has audio. 
        /// Only valid after the MediaOpened event has fired.
        /// </summary> 
        public bool HasAudio { get { return Media == null ? false : Media.HasAudio; } private set { this.OnPropertyChanged(); } }

        /// <summary> 
        /// Returns whether the given media has video. Only valid after the
        /// MediaOpened event has fired.
        /// </summary>
        public bool HasVideo { get { return Media == null ? false : Media.HasVideo; } private set { this.OnPropertyChanged(); } }

        /// <summary>
        /// Gets the video codec.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public string VideoCodec { get { return Media == null ? null : Media.VideoCodec; } private set { this.OnPropertyChanged(); } }

        /// <summary>
        /// Gets the video bitrate.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public int VideoBitrate { get { return Media == null ? 0 : Media.VideoBitrate; } private set { this.OnPropertyChanged(); } }

        /// <summary>
        /// Returns the natural width of the media in the video.
        /// Only valid after the MediaOpened event has fired.
        /// </summary> 
        public int NaturalVideoWidth { get { return Media == null ? 0 : Media.VideoFrameWidth; } private set { this.OnPropertyChanged(); } }

        /// <summary> 
        /// Returns the natural height of the media in the video.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public int NaturalVideoHeight { get { return Media == null ? 0 : Media.VideoFrameHeight; } private set { this.OnPropertyChanged(); ; } }

        /// <summary>
        /// Gets the video frame rate.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public decimal VideoFrameRate { get { return Media == null ? 0M : Media.VideoFrameRate; } private set { this.OnPropertyChanged(); } }

        /// <summary>
        /// Gets the length of the video frame.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public decimal VideoFrameLength { get { return Media == null ? 0M : Media.VideoFrameLength; } private set { this.OnPropertyChanged(); } }

        /// <summary>
        /// Gets the audio codec.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public string AudioCodec { get { return Media == null ? null : Media.AudioCodec; } private set { this.OnPropertyChanged(); } }

        /// <summary>
        /// Gets the audio bitrate.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public int AudioBitrate { get { return Media == null ? 0 : Media.AudioBitrate; } private set { this.OnPropertyChanged(); } }

        /// <summary>
        /// Gets the audio channels count.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public int AudioChannels { get { return Media == null ? 0 : Media.AudioChannels; } private set { this.OnPropertyChanged(); } }

        /// <summary>
        /// Gets the audio output bits per sample.
        /// Only valid after the MediaOpened event has fired.
        /// This value will always have to be 16
        /// </summary>
        public int AudioOutputBitsPerSample { get { return Media == null ? 0 : Media.AudioOutputBitsPerSample; } private set { this.OnPropertyChanged(); } }

        /// <summary>
        /// Gets the audio sample rate.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public int AudioSampleRate { get { return Media == null ? 0 : Media.AudioSampleRate; } private set { this.OnPropertyChanged(); } }

        /// <summary>
        /// Gets the audio output sample rate.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public int AudioOutputSampleRate { get { return Media == null ? 0 : Media.AudioOutputSampleRate; } private set { this.OnPropertyChanged(); } }

        /// <summary>
        /// Gets the audio bytes per sample.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public int AudioBytesPerSample { get { return Media == null ? 0 : Media.AudioBytesPerSample; } private set { this.OnPropertyChanged(); } }

        /// <summary>
        /// Gets the Media's natural duration
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public double NaturalDuration { get { return Media == null ? 0d : Convert.ToDouble(Media.NaturalDuration); } private set { this.OnPropertyChanged(); } }

        /// <summary>
        /// Returns whether the given media can be paused. 
        /// This is only valid after the MediaOpened event has fired.
        /// Note: This property is computed based on wether the stream is detected to be a live stream.
        /// </summary>
        public bool CanPause { get { return Media != null ? Media.IsLiveStream == false : true; } }

        /// <summary>
        /// Gets a value indicating whether the media is playing.
        /// </summary>
        public bool IsPlaying { get { return Media == null ? false : Media.IsPlaying; } private set { this.OnPropertyChanged(); } }

        /// <summary>
        /// Gets a value indicating whether the media has reached its end.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance has media ended; otherwise, <c>false</c>.
        /// </value>
        public bool HasMediaEnded
        {
            get { return Media == null ? false : Media.HasMediaEnded; }
            private set
            {
                if (Media == null) return;

                if (Media.HasMediaEnded)
                {
                    RaiseEvent(new RoutedEventArgs(MediaEndedEvent, this));
                    if (UnloadedBehavior == MediaState.Stop)
                    {
                        this.Stop();
                    }

                    if (UnloadedBehavior == MediaState.Play)
                    {
                        this.Stop();
                        this.Play();
                    }

                    if (UnloadedBehavior == MediaState.Close)
                    {
                        this.CloseMedia(true);
                    }
                }
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Handles the PropertyChanged event of the underlying media.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.ComponentModel.PropertyChangedEventArgs"/> instance containing the event data.</param>
        private void HandleMediaPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName.Equals(PropertyNames.Volume))
            {
                Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() => { this.Volume = Media != null ? Convert.ToDouble(Media.Volume) : 1d; }));
                return;
            }

            if (e.PropertyName.Equals(PropertyNames.IsPlaying))
            {
                Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() => { this.IsPlaying = Media == null ? false : Media.IsPlaying; }));
                return;
            }

            if (e.PropertyName.Equals(PropertyNames.HasMediaEnded))
            {
                Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() => { this.HasMediaEnded = Media == null ? false : Media.HasMediaEnded; }));
                return;
            }

            if (e.PropertyName.Equals(PropertyNames.SpeedRatio))
            {
                Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() => { this.SpeedRatio = Media == null ? 1M : Media.SpeedRatio; }));
                return;
            }

            if (e.PropertyName.Equals(PropertyNames.Position))
            {
                Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() =>
                {
                    lock (PositionSyncLock)
                    {
                        if (this.SeekRequestedPosition != decimal.MinValue) return;
                        this.PositionUpdatingFromMediaDone = false;
                        this.Position = this.Media == null ? 0M :
                            this.Media.IsLiveStream ?
                                this.Media.Position : this.Media.Position - this.Media.StartTime;
                        this.PositionUpdatingFromMediaDone = true;
                    }

                }), DispatcherPriority.DataBind);
                return;
            }
        }

        /// <summary>
        /// Called when a media error occurs.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="ex">The ex.</param>
        private void OnMediaError(object sender, Exception ex)
        {
            Dispatcher.CurrentDispatcher.BeginInvoke(new Action<Exception>((mediaEx) =>
            {
                RaiseEvent(new MediaErrorRoutedEventArgs(MediaErroredEvent, this, mediaEx));
            }), ex);
        }

        /// <summary>
        /// Opens the media.
        /// </summary>
        /// <param name="sourceUri">The source URI.</param>
        private void OpenMedia(Uri sourceUri)
        {

            // TODO: Implement asynchronous loading of media with timeout

            try
            {
                if (this.Media != null)
                    this.CloseMedia(false);

                var inputPath = sourceUri.IsFile ? sourceUri.LocalPath : sourceUri.ToString();
                this.Media = new FFmpegMedia(inputPath, OnMediaError);
                this.Media.PropertyChanged += HandleMediaPropertyChanged;
                this.TargetBitmap = this.Media.VideoRenderer;
                this.ViewBox.Source = TargetBitmap;
                this.ViewBox.Visibility = this.Media.HasVideo ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
                RaiseEvent(new RoutedEventArgs(MediaOpenedEvent, this));
                PositionUpdatingFromMediaDone = false;

                if (this.LoadedBehavior == MediaState.Play)
                {
                    this.Play();
                }

            }
            catch (Exception ex)
            {
                this.CloseMedia(false);
                RaiseEvent(new MediaErrorRoutedEventArgs(MediaFailedEvent, this, ex));
            }
            finally
            {
                this.UpdateMediaProperties();
            }
        }

        /// <summary>
        /// Closes the media.
        /// </summary>
        /// <param name="updateProperties">if set to <c>true</c> [update properties].</param>
        private void CloseMedia(bool updateProperties)
        {
            try
            {
                if (this.Media != null)
                {
                    this.Media.PropertyChanged -= HandleMediaPropertyChanged;
                    this.Media.Dispose();
                }

            }
            catch
            {
                // placeholder
            }
            finally
            {
                this.Media = null;
                if (updateProperties)
                    this.UpdateMediaProperties();

                // Reset properties without backing media
                this.IsPlaying = default(bool);
                this.Volume = default(double);
                this.HasMediaEnded = default(bool);
                this.Position = default(decimal);
                this.SpeedRatio = default(decimal);
            }

        }

        /// <summary>
        /// Updates the media properties.
        /// </summary>
        private void UpdateMediaProperties()
        {
            if (this.Media == null)
            {
                this.HasAudio = false;
                this.HasVideo = false;
                this.VideoCodec = null;
                this.VideoBitrate = 0;
                this.NaturalVideoWidth = 0;
                this.NaturalVideoHeight = 0;
                this.VideoFrameRate = 0;
                this.VideoFrameLength = 0M;
                this.AudioCodec = null;
                this.AudioBitrate = 0;
                this.AudioChannels = 0;
                this.AudioOutputBitsPerSample = 0;
                this.AudioSampleRate = 0;
                this.AudioOutputSampleRate = 0;
                this.AudioBytesPerSample = 0;
                this.NaturalDuration = 0d;

                this.Volume = 0;
                this.Position = 0;
            }
            else
            {
                this.HasAudio = this.Media.HasAudio;
                this.HasVideo = this.Media.HasVideo;
                this.VideoCodec = this.Media.VideoCodec;
                this.VideoBitrate = this.Media.VideoBitrate;
                this.NaturalVideoWidth = this.Media.VideoFrameWidth;
                this.NaturalVideoHeight = this.Media.VideoFrameHeight;
                this.VideoFrameRate = this.Media.VideoFrameRate;
                this.VideoFrameLength = this.Media.VideoFrameLength;
                this.AudioCodec = this.Media.AudioCodec;
                this.AudioBitrate = this.Media.AudioBitrate;
                this.AudioChannels = this.Media.AudioChannels;
                this.AudioOutputBitsPerSample = this.Media.AudioOutputBitsPerSample;
                this.AudioSampleRate = this.Media.AudioSampleRate;
                this.AudioOutputSampleRate = this.Media.AudioOutputSampleRate;
                this.AudioBytesPerSample = this.Media.AudioBytesPerSample;
                this.NaturalDuration = System.Convert.ToDouble(this.Media.NaturalDuration);

                this.Volume = System.Convert.ToDouble(this.Media.Volume);
                this.Position = this.Media.Position;
            }

            this.IsMuted = false;
            this.HasMediaEnded = false;
            this.SpeedRatio = Constants.DefaultSpeedRatio;

        }

        #endregion
    }
}
