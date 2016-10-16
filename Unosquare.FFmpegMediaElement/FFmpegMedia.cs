namespace Unosquare.FFmpegMediaElement
{
    using FFmpeg.AutoGen;
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Threading;
    using System.Windows;
    using System.Windows.Media.Imaging;
    using System.Windows.Threading;

    /// <summary>
    /// Represents a multimedia source with its corresponding control methods.
    /// </summary>
    internal sealed unsafe partial class FFmpegMedia : IAudioDataProvider, INotifyPropertyChanged, IDisposable
    {
        #region Thread Synchronization Objects

        private Thread MediaFrameExtractorThread = null; // this thread will write the frames, locking
        private ManualResetEventSlim MediaFramesExtractedDone = new ManualResetEventSlim(false);

        private readonly DispatcherTimer VideoRenderTimer = new DispatcherTimer(DispatcherPriority.Render);
        private readonly MediaTimer RealtimeClock = new MediaTimer();

        private bool IsCancellationPending = false;

        #endregion

        #region Error Delegates and Callbacks

        public delegate void MediaErrorOccurredCallback(object sender, Exception ex);
        private MediaErrorOccurredCallback ErrorOccurredCallback = null;

        #endregion

        #region Control Variables

        private readonly FFmpegMediaFrameCache VideoFramesCache = null;
        private readonly FFmpegMediaFrameCache AudioFramesCache = null;

        private FFmpegMediaFrameCache PrimaryFramesCache = null;
        private FFmpegMediaFrameCache SecondaryFramesCache = null;

        private decimal? FirstLeadingFrameTime = null;
        FFmpegMediaFrame LastRenderedVideoFrame = null;
        AudioBufferProvider PcmAudioProvider = null;

#if DEBUG
        private List<int> InnerLoopCounts = new List<int>();
        private List<long> SeekTimes = new List<long>();
#endif

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="FFmpegMedia"/> class.
        /// </summary>
        /// <param name="filePath">The file path.</param>
        /// <param name="errorCallback">The error callback.</param>
        public FFmpegMedia(string filePath, MediaErrorOccurredCallback errorCallback)
            : this(filePath, errorCallback, null, null)
        {
            // placeholder
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FFmpegMedia" /> class.
        /// </summary>
        /// <param name="filePath">The file path.</param>
        /// <param name="errorCallback">The error callback.</param>
        /// <param name="referer">The referer. Leave null or emtpy to skip setting it.</param>
        /// <param name="userAgent">The user agent. Leave null or empty in order to skip setting a User Agent</param>
        /// <exception cref="ArgumentException">errorCallback cannot be null
        /// or
        /// filePath cannot be null or empty</exception>
        /// <exception cref="Exception"></exception>
        /// <exception cref="System.ArgumentException">errorCallback cannot be null
        /// or
        /// filePath cannot be null or empty</exception>
        /// <exception cref="System.Exception"></exception>
        public FFmpegMedia(string filePath, MediaErrorOccurredCallback errorCallback, string referer, string userAgent)
        {
            // Argument validation
            if (errorCallback == null)
                throw new ArgumentException("errorCallback cannot be null");

            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("filePath cannot be null or empty");

            // Error callback
            this.ErrorOccurredCallback = errorCallback;

            // Register the property state change handler
            this.RealtimeClock.PropertyChanged += (s, e) => { NotifyPlayStateChanged(); };

            // Make sure we registwered the library            
            Helper.RegisterFFmpeg();

            // Create the audio provider and audio renderer
            this.PcmAudioProvider = new AudioBufferProvider(this);
            this.AudioRenderer = new AudioRenderer();

            // load input, codec and output contexts
            this.InitializeMedia(filePath, referer, userAgent);

            // Setup the frames Cache
            this.VideoFramesCache = new FFmpegMediaFrameCache(this.VideoFrameRate, MediaFrameType.Video);
            this.AudioFramesCache = new FFmpegMediaFrameCache(this.AudioSampleRate / 1000M, MediaFrameType.Audio);

            // Setup the Leading and Lagging frames cache
            if (HasVideo && (HasAudio == false || InputAudioStream->index > InputVideoStream->index))
            {
                this.PrimaryFramesCache = VideoFramesCache;
                this.SecondaryFramesCache = AudioFramesCache;
                this.StartDts = InputVideoStream->start_time;

                LeadingStreamType = MediaFrameType.Video;
                LaggingStreamType = HasAudio ? MediaFrameType.Audio : MediaFrameType.Unknown;
            }
            else
            {
                this.PrimaryFramesCache = AudioFramesCache;
                this.SecondaryFramesCache = VideoFramesCache;
                this.StartDts = InputAudioStream->start_time;

                LeadingStreamType = MediaFrameType.Audio;
                LaggingStreamType = HasVideo ? MediaFrameType.Video : MediaFrameType.Unknown;
            }

            if (Helper.IsNoPtsValue(StartDts))
                StartDts = 0;

            // Setup Video Renderer and Video Frames Cache
            if (HasVideo)
                this.VideoRenderer = new WriteableBitmap(this.VideoFrameWidth, this.VideoFrameHeight, 96, 96, System.Windows.Media.PixelFormats.Bgr24, null);
            else
                this.VideoRenderer = new WriteableBitmap(1, 1, 96, 96, System.Windows.Media.PixelFormats.Bgr24, null);


            // Setup Audio Renderer and Audio Frames Cache
            if (HasAudio)
            {
                this.StartAudioRenderer();
            }

            // Start the continuous Decoder thread that fills up our queue.
            MediaFrameExtractorThread = new Thread(ExtractMediaFramesContinuously)
            {
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal
            };

            // Begin the media extractor
            MediaFrameExtractorThread.Start();
            MediaFramesExtractedDone.Reset();
            if (MediaFramesExtractedDone.Wait(Constants.WaitForPlaybackReadyStateTimeout) == false)
            {
                throw new Exception(string.Format("Could not load sream frames in a timely manner. Timed out in {0}", Constants.WaitForPlaybackReadyStateTimeout));
            }

            // Initialize the Speed Ratio to 1.0 (Default)
            this.SpeedRatio = Constants.DefaultSpeedRatio;

            // Start the render timer on the UI thread.
            this.VideoRenderTimer.Tick += RenderVideoImage;
            this.VideoRenderTimer.Interval = TimeSpan.FromMilliseconds(Constants.VideoRenderTimerIntervalMillis);
            this.VideoRenderTimer.IsEnabled = true;
            this.VideoRenderTimer.Start();
        }

        #endregion

        #region Internal Management -- Assumes proper locking

        private void StartAudioRenderer()
        {
            PcmAudioProvider.Clear();

            var audioStarted = this.AudioRenderer.Initialize(this, this.AudioOutputSampleRate, Constants.AudioOutputChannelCount, this.AudioOutputBitsPerSample);
            if (audioStarted == false)
                throw new Exception("Could not start audio device with given parameters.");
            else
                this.Volume = AudioRenderer.Volume;
        }

        private void InternalFillFramesCache(TimeSpan timeout)
        {
            var startTime = DateTime.UtcNow;
            while (PrimaryFramesCache.IsFull == false && IsAtEndOfStream == false)
            {
                if (DateTime.UtcNow.Subtract(startTime).Ticks > timeout.Ticks)
                {
                    ErrorOccurredCallback(this, new MediaPlaybackException(MediaPlaybackErrorSources.InternalFillFamesCache, MediaPlaybackErrorCode.FillFramesFailed,
                        string.Format("Fill Frames Cache = Failed to fill cache in {0}; Leading Frames: {1}; Lagging Frames: {2}",
                            timeout, PrimaryFramesCache.Count, SecondaryFramesCache.Count)));

                    return;
                }

                var frame = this.PullMediaFrame();
                if (frame != null)
                {
                    // reset the start time because we are in fact getting frames.
                    startTime = DateTime.UtcNow;

                    if (frame.Type == PrimaryFramesCache.Type)
                    {
                        PrimaryFramesCache.Add(frame);
                    }
                    else if (frame.Type == SecondaryFramesCache.Type)
                    {
                        if (SecondaryFramesCache.IsFull)
                            SecondaryFramesCache.RemoveFirst();

                        SecondaryFramesCache.Add(frame);
                    }

                }
            }
        }

        /// <summary>
        /// Internals the seek input.
        /// </summary>
        /// <param name="renderTime">The render time.</param>
        private void InternalSeekInput(decimal renderTime)
        {
            if (IsLiveStream)
            {
                if (PrimaryFramesCache.IsEmpty == false)
                    RealtimeClock.Seek(PrimaryFramesCache.FirstFrameTime);

                return;
            }

#if DEBUG
            var seekStopwatch = new System.Diagnostics.Stopwatch();
            seekStopwatch.Start();
#endif

            if (renderTime < StartTime)
                renderTime = StartTime;

            RealtimeClock.Seek(renderTime);

            var allowedThreshold = Constants.SeekThresholdSeconds;
            var seekOffsetLength = Constants.SeekOffsetSeconds;

            var seekTime = renderTime - seekOffsetLength;
            var maxSeekStartTime = seekTime - allowedThreshold;

            var bufferedLeadingFrames = new FFmpegMediaFrameCache(PrimaryFramesCache);
            var bufferedLaggingFrames = new FFmpegMediaFrameCache(SecondaryFramesCache);

            var outerLoopCount = 0;
            var innerLoopCount = 0;
            var frameReleaseCount = 0;
            var doSeekInStream = true;
            var doSeekByPullingFrames = true;
            var seekFlag = 0;
            var seekFrameResult = 0;
            var startTime = DateTime.UtcNow;
            var lastFailedTimestamp = long.MinValue;
            var seekToLastFrame = false;

            var seekTimeBase = PrimaryFramesCache.Type == MediaFrameType.Video ? InputVideoStream->time_base : InputAudioStream->time_base;
            var seekStreamIndex = PrimaryFramesCache.Type == MediaFrameType.Video ? InputVideoStream->index : InputAudioStream->index;
            var leadingFrameIndex = -1;

            try
            {
                while (doSeekInStream)
                {
                    outerLoopCount++;

                    if (seekTime < StartTime) seekTime = StartTime;

                    if (lastFailedTimestamp == StartDts)
                    {
                        if (PrimaryFramesCache.IsEmpty == false)
                            RealtimeClock.Seek(PrimaryFramesCache.FirstFrameTime);

                        ErrorOccurredCallback(this, new MediaPlaybackException(MediaPlaybackErrorSources.InternalSeekInput, MediaPlaybackErrorCode.SeekFailedCritical,
                            string.Format("Target Postion @ {0:0.000}s has already failed to seek. First DTS {1} also failed and will not retry.", seekTime, StartDts)));

                        return;
                    }

                    var targetTimestamp = Helper.SecondsToTimestamp(seekTime, seekTimeBase);
                    if (lastFailedTimestamp == targetTimestamp)
                    {
                        ErrorOccurredCallback(this, new MediaPlaybackException(MediaPlaybackErrorSources.InternalSeekInput, MediaPlaybackErrorCode.SeekFailedWillRetry,
                            string.Format("Target Postion @ {0:0.000}s has already failed to seek. Target timestamp will now be First DTS {1}.", seekTime, StartDts)));

                        targetTimestamp = StartDts;
                    }

                    seekFlag = (seekTime < renderTime || seekTime <= StartTime ? (int)ffmpeg.AVSEEK_FLAG_BACKWARD : 0) | 0; // FFmpegInvoke.AVSEEK_FLAG_ANY;
                    //seekFlag = FFmpegInvoke.AVSEEK_FLAG_BACKWARD; // | FFmpegInvoke.AVSEEK_FLAG_ANY;

                    seekFrameResult = ffmpeg.av_seek_frame(InputFormatContext, seekStreamIndex, targetTimestamp, seekFlag); // significantly faster than seek_file
                    //seekFrameResult = FFmpegInvoke.avformat_seek_file(InputFormatContext, streamIndex, long.MinValue, targetTimestamp, long.MaxValue, seekFlag);
                    if (seekFrameResult < Constants.SuccessCode)
                    {
                        if (PrimaryFramesCache.IsEmpty == false)
                            RealtimeClock.Seek(PrimaryFramesCache.FirstFrameTime);

                        var errorMessage = Helper.GetFFmpegErrorMessage(seekFrameResult);
                        ErrorOccurredCallback(this, new MediaPlaybackException(MediaPlaybackErrorSources.InternalSeekInput, MediaPlaybackErrorCode.SeekFailedFFmpeg,
                            string.Format("FFmpeg av_seek_frame @ {1:0.000}: Failed with error code {0}. {2}", seekFrameResult, seekTime, errorMessage)));

                        return;
                    }
                    else
                    {
                        if (VideoCodecContext != null)
                            ffmpeg.avcodec_flush_buffers(VideoCodecContext);

                        if (AudioCodecContext != null)
                            ffmpeg.avcodec_flush_buffers(AudioCodecContext);
                    }

                    leadingFrameIndex = -1;
                    bufferedLeadingFrames.Clear();
                    bufferedLaggingFrames.Clear();

                    doSeekInStream = false;
                    doSeekByPullingFrames = true;

                    while (doSeekByPullingFrames)
                    {
                        innerLoopCount++;

                        var frame = this.PullMediaFrame();
                        if (frame != null)
                        {
                            if (frame.StartTime < maxSeekStartTime)
                            {
                                frame.EnqueueRelease();
                                frameReleaseCount++;
                                continue;
                            }

                            if (frame.Type == bufferedLeadingFrames.Type)
                            {
                                leadingFrameIndex++;
                                if (leadingFrameIndex == 0 && frame.Type == bufferedLeadingFrames.Type && frame.StartTime - frame.Duration > maxSeekStartTime && maxSeekStartTime > 0M)
                                {
                                    seekTime -= seekOffsetLength;
                                    frame.EnqueueRelease();
                                    doSeekInStream = true;
                                    lastFailedTimestamp = targetTimestamp;
                                    break;
                                }

                                // We are Full minus 1 at this point. We'll stop buffering
                                if (bufferedLeadingFrames.Count >= bufferedLeadingFrames.Capacity - 1)
                                    doSeekByPullingFrames = false;

                                bufferedLeadingFrames.Add(frame);
                            }
                            else if (frame.Type == bufferedLaggingFrames.Type)
                            {
                                // add the lagging frame no matter what
                                if (bufferedLaggingFrames.IsFull)
                                    bufferedLaggingFrames.RemoveFirst();

                                bufferedLaggingFrames.Add(frame);
                            }

                            // Find out if we have the frame
                            var seekFrameIndex = bufferedLeadingFrames.IndexOf(renderTime, true);
                            var minimumFrameCount = (seekFrameIndex - 1) * 2;

                            // if we have more than enough frames in the buffer or we have reached a full or end condition, stop buffering frames
                            if (seekFrameIndex > 0)
                                if (bufferedLeadingFrames.Count >= minimumFrameCount || bufferedLeadingFrames.IsFull || IsAtEndOfStream)
                                    doSeekByPullingFrames = false;

                        }

                        // We're already padt the end of the stream. Natural duration was wron for the leading frames cache.
                        if (IsAtEndOfStream && bufferedLeadingFrames.Count <= 0)
                        {
                            doSeekInStream = true;
                            seekTime -= seekOffsetLength;
                            maxSeekStartTime = seekTime - allowedThreshold;
                            seekToLastFrame = true;
                        }

                        if (doSeekInStream)
                            break;

                        if (doSeekByPullingFrames == false || IsAtEndOfStream)
                        {
                            PrimaryFramesCache.Replace(bufferedLeadingFrames);
                            SecondaryFramesCache.Replace(bufferedLaggingFrames);

                            if (seekToLastFrame && PrimaryFramesCache.Count > 0)
                                RealtimeClock.Seek(PrimaryFramesCache.LastFrameTime);

                            return;
                        }
                    }
                }
            }
            finally
            {
#if DEBUG
                seekStopwatch.Stop();
                SeekTimes.Add(seekStopwatch.ElapsedMilliseconds);
                InnerLoopCounts.Add(innerLoopCount);
                System.Diagnostics.Debug.WriteLine("Seek @ {6:0.000} = Long: {0:00}\t Short: {1:000}\t Short (AVG): {2:0.000}\t Waste Count: {3:000}\t Elapsed: {4}\tElapsed (AVG): {5:0.000}",
                    outerLoopCount, innerLoopCount, InnerLoopCounts.Average(), frameReleaseCount, seekStopwatch.ElapsedMilliseconds, SeekTimes.Average(), renderTime);
#endif
            }

        }

        private void InternalLoadFrames(decimal renderTime)
        {
            if (renderTime < StartTime) renderTime = StartTime;
            if (IsAtEndOfStream && PrimaryFramesCache.Count > 0 && renderTime >= PrimaryFramesCache.EndTime)
                renderTime = PrimaryFramesCache.EndTime;

            // The very first thing we do is fill the buffer if it is empty
            if (this.PrimaryFramesCache.IsEmpty)
            {
                this.InternalFillFramesCache(Constants.FrameExtractorFillTimeout);

                if (PrimaryFramesCache.Count > 0)
                {
                    if (FirstLeadingFrameTime == null)
                        FirstLeadingFrameTime = PrimaryFramesCache.FirstFrameTime;

                    if (IsLiveStream)
                    {
                        RealtimeClock.Seek(PrimaryFramesCache.FirstFrameTime);
                        RealtimeClock.Play();
                        return;
                    }
                }
            }

            var renderFrame = PrimaryFramesCache.GetFrame(renderTime, CheckFrameBounds);
            var renderFrameIndex = PrimaryFramesCache.IndexOf(renderFrame);
            var renderFrameFound = renderFrameIndex >= 0;

            // if we can't find the frame . . .
            if (renderFrameFound == false)
            {
                // Perform the seek operation
                this.InternalSeekInput(renderTime);
                renderTime = RealtimeClock.PositionSeconds;

                // try to find the frame now that we have stuff
                var seekFrame = PrimaryFramesCache.GetFrame(renderTime, CheckFrameBounds);

                if (seekFrame != null)
                {
                    // seek is successful at this point
                    RealtimeClock.Seek(renderTime);
                    PcmAudioProvider.Clear();
                }
                else
                {
                    // got some frames but not the ones we asked for

                    if (this.PrimaryFramesCache.Count > 0)
                    {
                        if (IsAtEndOfStream)
                        {
                            RealtimeClock.Seek(this.PrimaryFramesCache.MiddleFrameTime);
                        }
                        else
                        {
                            if (InternalGetIsInFirstTimeSegment(renderTime))
                            {
                                RealtimeClock.Seek(renderTime);
                                ErrorOccurredCallback(this, new MediaPlaybackException(MediaPlaybackErrorSources.InternalLoadFrames, MediaPlaybackErrorCode.LoadFramesFailedInFirstSegment,
                                    string.Format("Could not find frames at {0:0.000} (On first time segment). First Leading Frame occurs at {1:0.000}",
                                        renderTime, PrimaryFramesCache.FirstFrameTime)));
                            }
                            else
                            {
                                RealtimeClock.Seek(this.PrimaryFramesCache.LastFrameTime);
                                ErrorOccurredCallback(this, new MediaPlaybackException(MediaPlaybackErrorSources.InternalLoadFrames, MediaPlaybackErrorCode.LoadFramesFailedForCurrentPosition,
                                    string.Format("Could not find frames at {0:0.000} (NOT on first segment). Last Leading Frame occurs at {1:0.000} - This should not have occurred.",
                                        renderTime, PrimaryFramesCache.LastFrameTime)));
                            }

                        }
                    }
                    else
                    {
                        ErrorOccurredCallback(this, new MediaPlaybackException(MediaPlaybackErrorSources.InternalLoadFrames, MediaPlaybackErrorCode.LoadFramesFailedCritical,
                            string.Format("Could not find frames at {0:0.000} and no Leading Frames exist in the cache - Critical Error.",
                                renderTime)));
                    }

                }

                return;
            }
            else
            {
                var isInLastTimeSegment = InternalGetIsInLastTimeSegment(renderTime);
                var isInFirstTimeSegment = InternalGetIsInFirstTimeSegment(renderTime);

                // frward lookup
                if (renderFrameIndex > PrimaryFramesCache.MiddleIndex)
                {
                    if (isInLastTimeSegment == false)
                    {
                        if (PrimaryFramesCache.IsFull)
                        {
                            var removalCount = 1;

                            if (SpeedRatio >= Constants.DefaultSpeedRatio)
                                removalCount = (int)Math.Ceiling(SpeedRatio);

                            removalCount = Math.Min(PrimaryFramesCache.Count / 4, removalCount);

                            for (var i = 1; i <= removalCount; i++)
                                PrimaryFramesCache.RemoveFirst();
                        }

                        this.InternalFillFramesCache(Constants.FrameExtractorFillTimeout);
                        return;
                    }
                    else
                    {
                        if (renderFrameIndex >= PrimaryFramesCache.Count - 1)
                        {
                            // All input has been processed up to the last frame now.
                            RealtimeClock.Seek(PrimaryFramesCache.EndTime);
                            return;
                        }
                    }
                }

                // backward lookup
                if (renderFrameIndex <= 1 && isInFirstTimeSegment == false && IsLiveStream == false)
                {
                    InternalSeekInput(PrimaryFramesCache.StartTime);
                    var frame = PrimaryFramesCache.GetFrame(renderTime, CheckFrameBounds);
                    if (frame != null)
                        RealtimeClock.Seek(renderTime);
                    else
                        RealtimeClock.Seek(PrimaryFramesCache.MiddleFrame != null ? PrimaryFramesCache.MiddleFrame.StartTime : 0M);

                    return;
                }
            }

        }

        /// <summary>
        /// Extracts the media frames continuously.
        /// </summary>
        private void ExtractMediaFramesContinuously()
        {
            decimal renderTime = StartTime;

            while (IsCancellationPending == false)
            {
                var wasPlaying = this.IsPlaying;

                // Lock up changes
                MediaFramesExtractedDone.Reset();

                // Extract state
                renderTime = RealtimeClock.PositionSeconds;

                // Load frames
                InternalLoadFrames(renderTime);

                // Unlock
                MediaFramesExtractedDone.Set();

                if (wasPlaying && this.IsPlaying == false && this.HasMediaEnded == false)
                {
                    // HasMediaEnded will most likely contain an "old value" for the current cycle. That's why we call the method to re-evaluate.
                    if (InternalGetHasMediaEnded() == false)
                    {
                        ErrorOccurredCallback(this, new MediaPlaybackException(MediaPlaybackErrorSources.ExtractMediaFramesContinuously, MediaPlaybackErrorCode.FrameExtractionLoopForcedPause,
                            string.Format("WARNING: Something did not go smoothly. Wall clock paused @ {0:0.000} Call the Play method to resume playback.",
                                renderTime)));
                    }
                }

                // give waiter methods a chance to execute before a new lock is set.
                Thread.Sleep(Constants.FrameExtractorSleepTime);
            }
        }

        #endregion

        #region Video and Audio Frame Rendering

        /// <summary>
        /// Renders the video image. This method is called on a Dispatcher timer.
        /// It is responsible for rendering the decoded video image continuously.
        /// It also avoids rendering the same image again.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void RenderVideoImage(object sender, EventArgs e)
        {
            MediaFramesExtractedDone.Wait(Constants.FrameExtractorWaitMs);
            var renderTime = RealtimeClock.PositionSeconds;
            try
            {
                var videoFrame = VideoFramesCache.GetFrame(renderTime, false);
                if (videoFrame == null || videoFrame == LastRenderedVideoFrame) return;
                if (videoFrame.PictureBufferPtr != IntPtr.Zero)
                {
                    VideoRenderer.Lock();
                    Helper.NativeMethods.RtlMoveMemory(VideoRenderer.BackBuffer, videoFrame.PictureBufferPtr, videoFrame.PictureBufferLength);
                    VideoRenderer.AddDirtyRect(new Int32Rect(0, 0, VideoRenderer.PixelWidth, VideoRenderer.PixelHeight));
                    VideoRenderer.Unlock();
                    LastRenderedVideoFrame = videoFrame;
                }
            }
            finally
            {
                this.Position = renderTime;
            }
        }

        /// <summary>
        /// Renders the audio buffer. This is the implementation of IAudioDataProvider.RenderAudioBufferMethod
        /// It basically gets the decoded PCM bytes from the audio buffer provider. This method is called by the audio device itself.
        /// </summary>
        /// <param name="bufferToFill">The buffer to fill.</param>
        /// <param name="bytesWritten">The bytes written.</param>
        /// <returns></returns>
        public bool RenderAudioBuffer(byte[] bufferToFill, ref int bytesWritten)
        {
            if (IsCancellationPending)
                return false;

            bytesWritten = PcmAudioProvider.ProvideNext(bufferToFill);
            return true;
        }

        #endregion
    }

}
