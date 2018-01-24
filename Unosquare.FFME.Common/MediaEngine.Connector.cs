namespace Unosquare.FFME
{
    using Primitives;
    using Shared;
    using System;
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Runtime.CompilerServices;

    public partial class MediaEngine
    {
        /// <summary>
        /// The property change queue
        /// </summary>
        private readonly ConcurrentDictionary<string, bool> PropertyChangeQueue = new ConcurrentDictionary<string, bool>();

        /// <summary>
        /// When position is being set from within this control, this field will
        /// be set to true. This is useful to detect if the user is setting the position
        /// or if the Position property is being driven from within
        /// </summary>
        private AtomicBoolean m_IsRunningPropertyUdates = new AtomicBoolean(false);

        /// <summary>
        /// Gets or sets a value indicating whether this instance is running property updates.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is running property updates; otherwise, <c>false</c>.
        /// </value>
        public bool IsRunningPropertyUpdates
        {
            get { return m_IsRunningPropertyUdates.Value; }
            set { m_IsRunningPropertyUdates.Value = value; }
        }

        #region Connector Signals

        /// <summary>
        /// Raises the MessageLogged event
        /// </summary>
        /// <param name="message">The <see cref="MediaLogMessage" /> instance containing the message.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SendOnMessageLogged(MediaLogMessage message)
        {
            Connector?.OnMessageLogged(this, message);
        }

        /// <summary>
        /// Raises the media failed event.
        /// </summary>
        /// <param name="ex">The ex.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SendOnMediaFailed(Exception ex)
        {
            Log(MediaLogMessageType.Error, $"Media Failure - {ex?.GetType()}: {ex?.Message}");
            Connector?.OnMediaFailed(this, ex);
        }

        /// <summary>
        /// Raises the media closed event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SendOnMediaClosed()
        {
            Connector?.OnMediaClosed(this);
        }

        /// <summary>
        /// Raises the media opened event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SendOnMediaOpened()
        {
            Connector?.OnMediaOpened(this);
        }

        /// <summary>
        /// Raises the media opening event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SendOnMediaOpening()
        {
            Connector?.OnMediaOpening(this, Container.MediaOptions, Container.MediaInfo);
        }

        /// <summary>
        /// Raises the buffering started event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SendOnBufferingStarted()
        {
            Connector?.OnBufferingStarted(this);
        }

        /// <summary>
        /// Raises the buffering ended event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SendOnBufferingEnded()
        {
            Connector?.OnBufferingEnded(this);
        }

        /// <summary>
        /// Raises the Seeking started event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SendOnSeekingStarted()
        {
            Connector?.OnSeekingStarted(this);
        }

        /// <summary>
        /// Raises the Seeking ended event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SendOnSeekingEnded()
        {
            Connector?.OnSeekingEnded(this);
        }

        /// <summary>
        /// Raises the media ended event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SendOnMediaEnded()
        {
            Connector?.OnMediaEnded(this);
        }

        /// <summary>
        /// Raises the Position Changed event
        /// </summary>
        /// <param name="position">The position.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SendOnPositionChanged(TimeSpan position)
        {
            Connector?.OnPositionChanged(this, position);
        }

        #endregion

        #region Property Change Management

        /// <summary>
        /// Updates the media properties notifying that there are new values to be read from all of them.
        /// Call this method only when necessary because it creates a lot of events.
        /// </summary>
        internal void EnqueueAllPropertiesChanged()
        {
            EnqueuePropertyChange(nameof(IsOpen));
            EnqueuePropertyChange(nameof(MediaFormat));
            EnqueuePropertyChange(nameof(HasAudio));
            EnqueuePropertyChange(nameof(HasVideo));
            EnqueuePropertyChange(nameof(VideoCodec));
            EnqueuePropertyChange(nameof(VideoBitrate));
            EnqueuePropertyChange(nameof(NaturalVideoWidth));
            EnqueuePropertyChange(nameof(NaturalVideoHeight));
            EnqueuePropertyChange(nameof(VideoFrameRate));
            EnqueuePropertyChange(nameof(VideoFrameLength));
            EnqueuePropertyChange(nameof(VideoHardwareDecoder));
            EnqueuePropertyChange(nameof(AudioCodec));
            EnqueuePropertyChange(nameof(AudioBitrate));
            EnqueuePropertyChange(nameof(AudioChannels));
            EnqueuePropertyChange(nameof(AudioSampleRate));
            EnqueuePropertyChange(nameof(AudioBitsPerSample));
            EnqueuePropertyChange(nameof(NaturalDuration));
            EnqueuePropertyChange(nameof(CanPause));
            EnqueuePropertyChange(nameof(IsLiveStream));
            EnqueuePropertyChange(nameof(IsSeekable));
            EnqueuePropertyChange(nameof(GuessedByteRate));
            EnqueuePropertyChange(nameof(BufferCacheLength));
            EnqueuePropertyChange(nameof(DownloadCacheLength));
            EnqueuePropertyChange(nameof(FrameStepDuration));
            EnqueuePropertyChange(nameof(Metadata));
        }

        /// <summary>
        /// Resets the controller properies.
        /// </summary>
        internal void ResetControllerProperties()
        {
            Volume = Constants.Controller.DefaultVolume;
            Balance = Constants.Controller.DefaultBalance;
            SpeedRatio = Constants.Controller.DefaultSpeedRatio;
            IsMuted = false;
            VideoSmtpeTimecode = string.Empty;
            VideoHardwareDecoder = string.Empty;
            IsMuted = false;
            HasMediaEnded = false;
            Position = TimeSpan.Zero;
        }

        internal void ClearPropertyChangeQueue()
        {
            PropertyChangeQueue.Clear();
        }

        /// <summary>
        /// Checks if a property already matches a desired value.  Sets the property and
        /// enqueues a property notification only when necessary.
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
            EnqueuePropertyChange(propertyName);
            return true;
        }

        private void EnqueuePropertyChange(string propertyName)
        {
            PropertyChangeQueue[propertyName] = true;
        }

        private void NotifyEnqueuedPropertyChanges()
        {
            try
            {
                Connector?.OnPropertiesChanged(this, PropertyChangeQueue.Keys.ToArray());
            }
            catch
            {
                throw;
            }
            finally
            {
                ClearPropertyChangeQueue();
            }
        }

        #endregion
    }
}