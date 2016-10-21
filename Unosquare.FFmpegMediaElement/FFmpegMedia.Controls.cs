namespace Unosquare.FFmpegMediaElement
{
    using System;
    using System.ComponentModel;
    using System.Runtime.CompilerServices;

    unsafe partial class FFmpegMedia
    {
        #region Playback State Variables

        private decimal m_NaturalDuration = 0M;
        private decimal m_StartTime = 0M;
        private decimal m_EndTime = 0M;
        private bool m_IsLiveStream = false;
        private decimal m_Position = 0M;
        private bool m_IsAtEndOfStream = false;
        private decimal m_VolumeValue = 0M;
        private bool m_HasMediaEnded = false;

        #endregion

        #region Playback State Properties (Notification Properties)

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool InternalGetHasMediaEnded()
        {
            return IsAtEndOfStream && RealtimeClock.PositionSeconds > PrimaryFramesCache.LastFrameTime;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool InternalGetIsInFirstTimeSegment(decimal renderTime)
        {
            var isPositionInFisrtSegment = renderTime <= this.StartTime + this.PrimaryFramesCache.Duration && FirstLeadingFrameTime.HasValue;
            return isPositionInFisrtSegment && this.PrimaryFramesCache.Count > 0 && this.PrimaryFramesCache.FirstFrameTime == FirstLeadingFrameTime.Value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool InternalGetIsInLastTimeSegment(decimal renderTime)
        {
            return this.IsAtEndOfStream; //renderTime >= Duration - MediaFrameCache.HalfCapacityInSeconds;
        }

        /// <summary>
        /// Gets a value indicating whether the wall clock is in the first time segment.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is in first time segment; otherwise, <c>false</c>.
        /// </value>
        private bool IsInFirstTimeSegment
        {
            get
            {
                return InternalGetIsInFirstTimeSegment(RealtimeClock.PositionSeconds);
            }
        }

        /// <summary>
        /// Gets a value indicating whether the wall clock is in the last time segment.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is in last time segment; otherwise, <c>false</c>.
        /// </value>
        private bool IsInLastTimeSegment
        {
            get
            {
                return InternalGetIsInLastTimeSegment(RealtimeClock.PositionSeconds);
            }
        }

        /// <summary>
        /// Gets a value indicating whether frame bound should be checked.
        /// Typically, if the realtime clock is in the first time segment, we don't check for bounds
        /// </summary>
        /// <value>
        ///   <c>true</c> if [check frame bounds]; otherwise, <c>false</c>.
        /// </value>
        private bool CheckFrameBounds
        {
            get
            {
                return IsInFirstTimeSegment == false;
            }
        }

        /// <summary>
        /// Waits for the frame extractor to be ready for playback.
        /// Returns true if successful, false if it timed out.
        /// </summary>
        private bool WaitForPlaybackReadyState()
        {
            var renderTime = RealtimeClock.PositionSeconds;
            var startTime = DateTime.UtcNow;
            var cycleCount = -1;
            FFmpegMediaFrame playbackFrame = null;

            while (IsCancellationPending == false)
            {
                if (DateTime.UtcNow.Subtract(startTime) > Constants.WaitForPlaybackReadyStateTimeout)
                {
                    ErrorOccurredCallback(this, new MediaPlaybackException(MediaPlaybackErrorSources.WaitForPlaybackReadyState,
                        MediaPlaybackErrorCode.WaitForPlaybackTimedOut,
                        string.Format("Waiting for playback ready state @ {0:0.000} timed Out in {1} cycles", renderTime, cycleCount)));
                    return false;
                }

                cycleCount++;

                // Wait for a decoding cycle.
                MediaFramesExtractedDone.Wait(Constants.FrameExtractorWaitMs);
                renderTime = RealtimeClock.PositionSeconds;
                playbackFrame = PrimaryFramesCache.GetFrame(renderTime, CheckFrameBounds);

                if (IsLiveStream && PrimaryFramesCache.Count > 0)
                {
                    playbackFrame = PrimaryFramesCache.FirstFrame;
                    break;
                }

                if (playbackFrame != null)
                    break;

            }

            // Do some additional logging
            System.Diagnostics.Debug.WriteLineIf(
                cycleCount >= 0,
                string.Format("WaitForPlaybackReadyState @ {0:0.000} = {1} cycles. Leading Frames: {2}, Frame Index: {3}, Frame Start: {4}",
                    renderTime,
                    cycleCount,
                    PrimaryFramesCache.Count,
                    PrimaryFramesCache.IndexOf(playbackFrame),
                    (playbackFrame != null ? 
                        playbackFrame.StartTime.ToString("0.000") : "NULL")));

            return true;
        }

        /// <summary>
        /// Gets the estimated duration in number of seconds.
        /// This is not necessarily the real, precise duration in all media streams but it tends to be extremely close.
        /// </summary>
        /// <value>
        /// The duration.
        /// </value>
        public decimal NaturalDuration
        {
            get { return m_NaturalDuration; }
            private set
            {
                SetProperty(ref m_NaturalDuration, value);
            }
        }

        /// <summary>
        /// Gets the start time of the leading media stream.
        /// </summary>
        /// <value>
        /// The start time.
        /// </value>
        public decimal StartTime
        {
            get { return m_StartTime; }
            private set
            {
                SetProperty(ref m_StartTime, value);
            }
        }

        /// <summary>
        /// Gets the end time of the leading media stream.
        /// </summary>
        /// <value>
        /// The end time.
        /// </value>
        public decimal EndTime
        {
            get { return m_EndTime; }
            private set
            {
                SetProperty(ref m_EndTime, value);
            }
        }

        /// <summary>
        /// Determines if the input is a live stream.
        /// </summary>
        public bool IsLiveStream
        {
            get { return m_IsLiveStream; }
            private set { SetProperty(ref m_IsLiveStream, value); }
        }

        /// <summary>
        /// Gets the current position in number of seconds.
        /// Use the Seek method to move to a different position within the input.
        /// </summary>
        /// <value>
        /// The position.
        /// </value>
        public decimal Position
        {
            get { return m_Position; }
            private set
            {
                SetProperty(ref m_Position, value);
                var originalState = this.HasMediaEnded;
                this.HasMediaEnded = InternalGetHasMediaEnded();

                // Pause the audio when the media Ends
                if (originalState == false && this.HasMediaEnded && this.AudioRenderer != null && this.AudioRenderer.HasInitialized)
                    AudioRenderer.Pause();
            }
        }


        /// <summary>
        /// Queries if a leading frame for the given position is immediately available.
        /// </summary>
        /// <param name="position">The position.</param>
        /// <returns></returns>
        public bool QueryIsFrameAvailable(decimal position)
        {
            return PrimaryFramesCache.GetFrame(position, true) != null;
        }

        /// <summary>
        /// Gets or sets the volume of the current session.
        /// Valid ranges are anything from 0 to 1
        /// </summary>
        /// <value>
        /// The volume.
        /// </value>
        public decimal Volume
        {
            get
            {
                if (AudioRenderer == null || AudioRenderer.HasInitialized == false)
                    return 0M;
                return AudioRenderer.Volume;
            }
            set
            {
                if (AudioRenderer == null || AudioRenderer.HasInitialized == false)
                    return;

                if (SetProperty(ref m_VolumeValue, value))
                    AudioRenderer.Volume = value;
            }
        }

        /// <summary>
        /// Gets or sets the video playback speed ratio.
        /// By default the speed ratio is 1.0M
        /// Note: Audio will not be rendered when the speed ratio is not exactly 1.0M
        /// </summary>
        /// <value>
        /// The speed ratio.
        /// </value>
        public decimal SpeedRatio
        {
            get
            {
                return RealtimeClock.SpeedRatio;
            }
            set
            {
                //if (HasVideo == false)
                //    return;

                var currentValue = RealtimeClock.SpeedRatio;
                if (SetProperty(ref currentValue, value))
                {
                    PcmAudioProvider.Clear();
                    MediaFramesExtractedDone.Wait(Constants.FrameExtractorWaitMs);
                    RealtimeClock.SpeedRatio = value;
                    //AudioRenderer.PlaybackRate = value;
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether we have reached the end of stream.
        /// This does not necessarily means the media has reached an end. It simply means that all
        /// data within the media has been read (not necessarily that it all has been rendered).
        /// </summary>
        /// <value>
        /// <c>true</c> if this we are at end of stream; otherwise, <c>false</c>.
        /// </value>
        public bool IsAtEndOfStream
        {
            get
            {
                return m_IsAtEndOfStream;
            }
            private set
            {
                SetProperty(ref m_IsAtEndOfStream, value);
            }
        }

        /// <summary>
        /// Gets a value indicating whether the real-time clock is on the last media frame.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance has media ended; otherwise, <c>false</c>.
        /// </value>
        public bool HasMediaEnded
        {
            get
            {
                return m_HasMediaEnded;
            }
            private set
            {
                SetProperty(ref m_HasMediaEnded, value);
            }
        }

        /// <summary>
        /// Gets a value indicating whether the media is playing.
        /// </summary>
        /// <value>
        /// <c>true</c> if the media is playing; otherwise, <c>false</c>.
        /// </value>
        public bool IsPlaying
        {
            get
            {
                return this.RealtimeClock.IsPlaying;
            }
        }

        #endregion

        #region Playback Control Methods

        /// <summary>
        /// Sets the Volume to 0
        /// </summary>
        public void Mute()
        {
            this.Volume = 0M;
        }

        /// <summary>
        /// Starts or resumes media playback
        /// </summary>
        public void Play()
        {
            if (AudioRenderer.HasInitialized)
                AudioRenderer.Play();

            if (this.IsPlaying)
                return;
            if (this.HasMediaEnded)
                return;

            WaitForPlaybackReadyState();
            this.RealtimeClock.Play();
        }

        /// <summary>
        /// Pauses media playback
        /// </summary>
        public void Pause()
        {
            if (IsLiveStream) return;
            if (IsPlaying == false) return;

            if (AudioRenderer.HasInitialized)
                AudioRenderer.Stop();

            MediaFramesExtractedDone.Wait(Constants.FrameExtractorWaitMs);
            this.RealtimeClock.Pause();
        }

        /// <summary>
        /// Rewinds and pauses media playback
        /// </summary>
        public void Stop()
        {
            if (IsLiveStream) return;

            if (AudioRenderer.HasInitialized)
                AudioRenderer.Stop();

            MediaFramesExtractedDone.Wait(Constants.FrameExtractorWaitMs);
            this.HasMediaEnded = false;
            if (Position > StartTime)
                RealtimeClock.Seek(StartTime);
        }

        /// <summary>
        /// Seeks to the specified target second.
        /// </summary>
        /// <param name="targetSecond">The target second.</param>
        public void Seek(decimal targetSecond)
        {
            if (IsLiveStream) return;

            if (AudioRenderer.HasInitialized)
                AudioRenderer.Stop();

            MediaFramesExtractedDone.Wait(Constants.FrameExtractorWaitMs);
            RealtimeClock.Seek(targetSecond);
            this.m_Position = targetSecond;
            NotifyPlayStateChanged();
        }

        #endregion

        #region INotifyPropertyChanged Implementation

        private void NotifyPlayStateChanged()
        {
            OnPropertyChanged("Position");
            OnPropertyChanged("IsPlaying");
        }

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
    }
}
