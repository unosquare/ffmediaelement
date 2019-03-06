namespace Unosquare.FFME.Rendering
{
    using Platform;
    using Primitives;
    using Shared;
    using System;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Windows;
    using System.Windows.Threading;
    using Wave;

    /// <summary>
    /// Provides Audio Output capabilities by writing samples to the default audio output device.
    /// </summary>
    /// <seealso cref="IMediaRenderer" />
    /// <seealso cref="IWaveProvider" />
    /// <seealso cref="IDisposable" />
    internal sealed class AudioRenderer : IDisposable, IMediaRenderer, IWaveProvider, ILoggingSource
    {
        #region Private Members

        private const double SyncThresholdPerfect = 10;
        private const double SyncThresholdLagging = 100;
        private const double SyncThresholdLeading = -25;
        private const int SyncThresholdMaxStep = 25;
        private const int SyncLockTimeout = 100;

        private readonly IWaitEvent WaitForReadyEvent = WaitEventFactory.Create(isCompleted: false, useSlim: true);
        private readonly object SyncLock = new object();
        private readonly AtomicBoolean PlaySyncGaveUp = new AtomicBoolean(false);

        private IWavePlayer AudioDevice;
        private SoundTouch AudioProcessor;
        private short[] AudioProcessorBuffer;
        private CircularBuffer AudioBuffer;
        private bool IsDisposed;
        private bool m_HasFiredAudioDeviceStopped;

        private byte[] ReadBuffer;
        private int SampleBlockSize;

        private DateTime? PlaySyncStartTime;
        private int PlaySyncCount;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioRenderer"/> class.
        /// </summary>
        /// <param name="mediaCore">The core media engine.</param>
        public AudioRenderer(MediaEngine mediaCore)
        {
            MediaCore = mediaCore ?? throw new ArgumentNullException(nameof(mediaCore));

            WaveFormat = new WaveFormat(
                Constants.Audio.SampleRate,
                Constants.Audio.BitsPerSample,
                Constants.Audio.ChannelCount);

            if (WaveFormat.BitsPerSample != 16 || WaveFormat.Channels != 2)
                throw new NotSupportedException("Wave Format has to be 16-bit and 2-channel.");

            if (MediaCore.State.HasAudio)
                Initialize();
        }

        #endregion

        #region Properties

        /// <inheritdoc />
        ILoggingHandler ILoggingSource.LoggingHandler => MediaCore;

        /// <summary>
        /// Gets the parent media element (platform specific).
        /// </summary>
        public MediaElement MediaElement => MediaCore.Parent as MediaElement;

        /// <inheritdoc />
        public MediaEngine MediaCore { get; }

        /// <inheritdoc />
        public WaveFormat WaveFormat { get; }

        /// <summary>
        /// Gets the realtime latency of the audio relative to the internal wall clock.
        /// A negative value means audio is ahead of the wall clock.
        /// A positive value means audio is behind of the wall clock.
        /// </summary>
        public TimeSpan Latency
        {
            get
            {
                // The delay is the playback position minus the current audio buffer position
                lock (SyncLock)
                {
                    return TimeSpan.FromTicks(
                        MediaCore.PlaybackClock().Ticks -
                        Position.Ticks);
                }
            }
        }

        /// <summary>
        /// Gets current realtime audio position.
        /// </summary>
        public TimeSpan Position
        {
            get
            {
                lock (SyncLock)
                {
                    // if we don't have a valid write tag it's just whatever has been read from the audio buffer
                    if (AudioBuffer.WriteTag == TimeSpan.MinValue)
                    {
                        return TimeSpan.FromMilliseconds(Convert.ToDouble(
                            TimeSpan.TicksPerMillisecond * 1000d * (AudioBuffer.Length - AudioBuffer.ReadableCount) / WaveFormat.AverageBytesPerSecond));
                    }

                    // the pending audio length is the amount of audio samples time that has not been yet read by the audio device.
                    var pendingAudioLength = TimeSpan.FromTicks(Convert.ToInt64(
                        TimeSpan.TicksPerMillisecond * 1000d * AudioBuffer.ReadableCount / WaveFormat.AverageBytesPerSecond));

                    // the current position is the Write tag minus the pending length
                    return TimeSpan.FromTicks(AudioBuffer.WriteTag.Ticks - pendingAudioLength.Ticks);
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this instance has fired the audio device stopped event.
        /// </summary>
        private bool HasFiredAudioDeviceStopped
        {
            get { lock (SyncLock) return m_HasFiredAudioDeviceStopped; }
            set { lock (SyncLock) m_HasFiredAudioDeviceStopped = value; }
        }

        #endregion

        #region Public API

        /// <inheritdoc />
        public void Render(MediaBlock mediaBlock, TimeSpan clockPosition)
        {
            // We don't need to render anything while we are seeking. Simply drop the blocks.
            if (MediaCore.State.IsSeeking || HasFiredAudioDeviceStopped) return;

            var lockTaken = false;
            Monitor.TryEnter(SyncLock, SyncLockTimeout, ref lockTaken);
            if (lockTaken == false) return;

            try
            {
                if ((AudioDevice?.IsRunning ?? false) == false)
                {
                    if (HasFiredAudioDeviceStopped) return;
                    MediaElement.RaiseAudioDeviceStoppedEvent();
                    HasFiredAudioDeviceStopped = true;

                    return;
                }

                if (AudioBuffer == null) return;

                // Capture Media Block Reference
                if (mediaBlock is AudioBlock == false) return;
                var audioBlock = (AudioBlock)mediaBlock;
                var audioBlocks = MediaCore.Blocks[MediaType.Audio];

                while (audioBlock != null)
                {
                    if (audioBlock.TryAcquireReaderLock(out var readLock) == false)
                        return;

                    using (readLock)
                    {
                        // Write the block if we have to, avoiding repeated blocks.
                        // TODO: Ideally we want to feed the blocks from the renderer itself
                        if (AudioBuffer.WriteTag < audioBlock.StartTime)
                        {
                            AudioBuffer.Write(audioBlock.Buffer, audioBlock.SamplesBufferLength, audioBlock.StartTime, true);
                        }

                        // Stop adding if we have too much in there.
                        if (AudioBuffer.CapacityPercent >= 0.5)
                            break;

                        // Retrieve the following block
                        audioBlock = audioBlocks.ContinuousNext(audioBlock) as AudioBlock;
                    }
                }
            }
            catch (Exception ex)
            {
                this.LogError(Aspects.AudioRenderer, $"{nameof(AudioRenderer)}.{nameof(Read)} has faulted.", ex);
            }
            finally
            {
                Monitor.Exit(SyncLock);
            }
        }

        /// <inheritdoc />
        public void Update(TimeSpan clockPosition)
        {
            // We don't need to keep track of syncs for seekable media
            if (MediaCore.State.IsSeekable)
                return;

            // Detect state changes to reset give up sync conditions.
            lock (SyncLock)
            {
                if (MediaCore.State.IsPlaying && PlaySyncStartTime == null)
                {
                    PlaySyncStartTime = DateTime.UtcNow;
                    PlaySyncCount = 0;
                    PlaySyncGaveUp.Value = false;
                    return;
                }

                if (!MediaCore.State.IsPaused || PlaySyncStartTime == null)
                    return;

                PlaySyncStartTime = null;
                PlaySyncCount = 0;
                PlaySyncGaveUp.Value = false;
            }
        }

        /// <inheritdoc />
        public void Play()
        {
            // placeholder
        }

        /// <inheritdoc />
        public void Pause()
        {
            // Placeholder
        }

        /// <inheritdoc />
        public void Stop()
        {
            Seek();
        }

        /// <inheritdoc />
        public void Close()
        {
            // Self-disconnect
            if (Application.Current != null)
            {
                GuiContext.Current.EnqueueInvoke(DispatcherPriority.Render, () =>
                    Application.Current.Exit -= OnApplicationExit);
            }

            // Yes, seek and destroy... coincidentally.
            lock (SyncLock)
            {
                Seek();
                Destroy();
            }
        }

        /// <inheritdoc />
        public void Seek()
        {
            lock (SyncLock)
            {
                AudioBuffer?.Clear();

                // AudioDevice?.Clear(); // TODO: This causes crashes
                if (ReadBuffer != null)
                    Array.Clear(ReadBuffer, 0, ReadBuffer.Length);
            }
        }

        /// <inheritdoc />
        public void WaitForReadyState() => WaitForReadyEvent?.Wait();

        /// <inheritdoc />
        public void Dispose()
        {
            lock (SyncLock)
            {
                if (IsDisposed) return;

                IsDisposed = true;
                Destroy();
                WaitForReadyEvent.Dispose();
            }
        }

        #endregion

        #region IWaveProvider Support

        /// <inheritdoc />
        public int Read(byte[] targetBuffer, int targetBufferOffset, int requestedBytes)
        {
            // We sync-lock the reads to avoid null reference exceptions as destroy might have been called
            var lockTaken = false;
            Monitor.TryEnter(SyncLock, SyncLockTimeout, ref lockTaken);

            if (lockTaken == false || HasFiredAudioDeviceStopped)
            {
                Array.Clear(targetBuffer, targetBufferOffset, requestedBytes);
                return requestedBytes;
            }

            try
            {
                WaitForReadyEvent.Complete();
                var speedRatio = MediaCore.State.SpeedRatio;

                // Render silence if we don't need to output samples
                if (MediaCore.State.IsPlaying == false || speedRatio <= 0d || MediaCore.State.HasAudio == false || AudioBuffer.ReadableCount <= 0)
                {
                    Array.Clear(targetBuffer, targetBufferOffset, requestedBytes);
                    return requestedBytes;
                }

                // Ensure a pre-allocated ReadBuffer
                if (ReadBuffer == null || ReadBuffer.Length < Convert.ToInt32(requestedBytes * Constants.Controller.MaxSpeedRatio))
                    ReadBuffer = new byte[Convert.ToInt32(requestedBytes * Constants.Controller.MaxSpeedRatio)];

                // First part of DSP: Perform AV Synchronization if needed
                if (MediaCore.State.HasVideo && Synchronize(targetBuffer, targetBufferOffset, requestedBytes, speedRatio) == false)
                    return requestedBytes;

                var startPosition = Position;

                // Perform DSP
                if (speedRatio < 1.0)
                {
                    if (AudioProcessor != null)
                        ReadAndUseAudioProcessor(requestedBytes, speedRatio);
                    else
                        ReadAndSlowDown(requestedBytes, speedRatio);
                }
                else if (speedRatio > 1.0)
                {
                    if (AudioProcessor != null)
                        ReadAndUseAudioProcessor(requestedBytes, speedRatio);
                    else
                        ReadAndSpeedUp(requestedBytes, true, speedRatio);
                }
                else
                {
                    if (requestedBytes > AudioBuffer.ReadableCount)
                    {
                        Array.Clear(targetBuffer, targetBufferOffset, requestedBytes);
                        return requestedBytes;
                    }

                    AudioBuffer.Read(requestedBytes, ReadBuffer, 0);
                }

                ApplyVolumeAndBalance(targetBuffer, targetBufferOffset, requestedBytes);
                MediaElement.RaiseRenderingAudioEvent(
                    targetBuffer, requestedBytes, startPosition, WaveFormat.ConvertByteSizeToDuration(requestedBytes));
            }
            catch (Exception ex)
            {
                this.LogError(Aspects.AudioRenderer, $"{nameof(AudioRenderer)}.{nameof(Read)} has faulted.", ex);
                Array.Clear(targetBuffer, targetBufferOffset, requestedBytes);
            }
            finally
            {
                Monitor.Exit(SyncLock);
            }

            return requestedBytes;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Called when [application exit].
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="ExitEventArgs"/> instance containing the event data.</param>
        private void OnApplicationExit(object sender, ExitEventArgs e)
        {
            try { Dispose(); }
            catch { /* Ignore exception and continue */ }
        }

        /// <summary>
        /// Initializes the audio renderer.
        /// Call the Play Method to start reading samples
        /// </summary>
        private void Initialize()
        {
            Destroy();

            // Release the audio device always upon exiting
            if (Application.Current != null)
            {
                GuiContext.Current.EnqueueInvoke(DispatcherPriority.Render, () =>
                    Application.Current.Exit += OnApplicationExit);
            }

            // Enumerate devices. The default device is the first one so we check
            // that we have more than 1 device (other than the default stub)
            var hasAudioDevices = MediaElement.RendererOptions.UseLegacyAudioOut ?
                LegacyAudioPlayer.EnumerateDevices().Count > 1 :
                DirectSoundPlayer.EnumerateDevices().Count > 1;

            // Check if we have an audio output device.
            if (hasAudioDevices == false)
            {
                WaitForReadyEvent.Complete();
                HasFiredAudioDeviceStopped = true;
                this.LogWarning(Aspects.AudioRenderer,
                    "No audio device found for output.");

                return;
            }

            // Initialize the SoundTouch Audio Processor (if available)
            AudioProcessor = (SoundTouch.IsAvailable == false) ? null : new SoundTouch();
            if (AudioProcessor != null)
            {
                AudioProcessor.SetChannels(Convert.ToUInt32(WaveFormat.Channels));
                AudioProcessor.SetSampleRate(Convert.ToUInt32(WaveFormat.SampleRate));
            }

            // Initialize the Audio Device
            AudioDevice = MediaElement.RendererOptions.UseLegacyAudioOut ?
                new LegacyAudioPlayer(this, MediaElement.RendererOptions.LegacyAudioDevice?.DeviceId ?? -1) as IWavePlayer :
                new DirectSoundPlayer(this, MediaElement.RendererOptions.DirectSoundDevice?.DeviceId ?? DirectSoundPlayer.DefaultPlaybackDeviceId);

            // Create the Audio Buffer
            SampleBlockSize = Constants.Audio.BytesPerSample * Constants.Audio.ChannelCount;
            var bufferLength = WaveFormat.ConvertMillisToByteSize(2000); // 2-second buffer
            AudioBuffer = new CircularBuffer(bufferLength);
            AudioDevice.Start();
        }

        /// <summary>
        /// Destroys the audio renderer.
        /// Makes it useless.
        /// </summary>
        private void Destroy()
        {
            lock (SyncLock)
            {
                if (AudioDevice != null)
                {
                    AudioDevice.Dispose();
                    AudioDevice = null;
                }

                if (AudioBuffer != null)
                {
                    AudioBuffer.Dispose();
                    AudioBuffer = null;
                }

                if (AudioProcessor == null)
                    return;

                AudioProcessor.Dispose();
                AudioProcessor = null;
            }
        }

        #endregion

        #region DSP (All called inside the Read method)

        /// <summary>
        /// Synchronizes audio rendering to the wall clock.
        /// Returns true if additional samples need to be read.
        /// Returns false if silence has been written and no further reading is required.
        /// </summary>
        /// <param name="targetBuffer">The target buffer.</param>
        /// <param name="targetBufferOffset">The target buffer offset.</param>
        /// <param name="requestedBytes">The requested bytes.</param>
        /// <param name="speedRatio">The speed ratio.</param>
        /// <returns>
        /// True to continue processing. False to write silence.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool Synchronize(byte[] targetBuffer, int targetBufferOffset, int requestedBytes, double speedRatio)
        {
            #region Documentation

            /*
             * Wikipedia says:
             * For television applications, audio should lead video by no more than 15 milliseconds and audio should
             * lag video by no more than 45 milliseconds. For film, acceptable lip sync is considered to be no more
             * than 22 milliseconds in either direction.
             *
             * The Media and Acoustics Perception Lab says:
             * The results of the experiment determined that the average audio leading threshold for a/v sync
             * detection was 185.19 ms, with a standard deviation of 42.32 ms
             *
             * The ATSC says:
             * At first glance it seems loose: +90 ms to -185 ms as a Window of Acceptability
             * - Undetectable from -100 ms to +25 ms
             * - Detectable at -125 ms & +45 ms
             * - Becomes unacceptable at -185 ms & +90 ms
             *
             * NOTE: (- Sound delayed, + Sound advanced)
             */

            #endregion

            #region Private State

            var audioLatencyMs = Latency.TotalMilliseconds;
            var isBeyondThreshold = false;
            var readableCount = AudioBuffer.ReadableCount;
            var rewindableCount = AudioBuffer.RewindableCount;

            #endregion

            #region Sync Give-up Conditions

            if (MediaCore.MediaOptions?.DropLateFrames ?? false)
                speedRatio = 0;

            // we don't want to perform AV sync if the latency is huge
            // or if we have simply disabled it
            if (MediaElement.RendererOptions.AudioDisableSync ||
                (MediaCore.MediaOptions?.IsTimeSyncDisabled ?? true) ||
                audioLatencyMs < int.MinValue / 2d ||
                audioLatencyMs > int.MaxValue / 2d)
                return true;

            // Determine if we should continue to perform syncs.
            // Some live, non-seekable streams will send out-of-sync audio packets
            // and after trying too many times we simply give up.
            // The Sync conditions are reset in the Update method.
            if (MediaCore.State.IsSeekable == false && PlaySyncGaveUp.Value == false)
            {
                // 1. Determine if a sync is required
                if (audioLatencyMs > SyncThresholdLagging ||
                    audioLatencyMs < SyncThresholdLeading ||
                    Math.Abs(audioLatencyMs) > SyncThresholdPerfect)
                {
                    PlaySyncCount++;
                }

                // 2. Compute the variables to determine give-up conditions
                var playbackElapsedSeconds = PlaySyncStartTime.HasValue == false ?
                    0 : DateTime.UtcNow.Subtract(PlaySyncStartTime.Value).TotalSeconds;
                var syncsPerSecond = PlaySyncCount / playbackElapsedSeconds;

                // 3. Determine if we need to give up
                if (playbackElapsedSeconds >= 3 && syncsPerSecond >= 3)
                {
                    this.LogWarning(Aspects.AudioRenderer,
                        $"SYNC AUDIO: GIVE UP | SECS: {playbackElapsedSeconds:0.00}; " +
                        $"SYN: {PlaySyncCount}; RATE: {syncsPerSecond:0.00} SYN/s; LAT: {audioLatencyMs} ms.");
                    PlaySyncGaveUp.Value = true;
                }
            }

            // Detect if we have given up
            if (PlaySyncGaveUp == true)
                return true;

            #endregion

            #region Large Latency Handling

            if (audioLatencyMs > SyncThresholdLagging)
            {
                isBeyondThreshold = true;

                // a positive audio latency means we are rendering audio behind (after) the clock (skip some samples)
                // and therefore we need to advance the buffer before we read from it.
                if (Math.Abs(speedRatio - 1.0) <= double.Epsilon)
                {
                    this.LogWarning(Aspects.AudioRenderer,
                        $"SYNC AUDIO: LATENCY: {audioLatencyMs} ms. | SKIP (samples being rendered too late)");
                }

                // skip some samples from the buffer.
                var audioLatencyBytes = WaveFormat.ConvertMillisToByteSize(Convert.ToInt32(Math.Ceiling(audioLatencyMs)));
                AudioBuffer.Skip(Math.Min(audioLatencyBytes, readableCount));
            }
            else if (audioLatencyMs < SyncThresholdLeading)
            {
                isBeyondThreshold = true;

                // Compute the latency in bytes
                var audioLatencyBytes = WaveFormat.ConvertMillisToByteSize(Convert.ToInt32(Math.Ceiling(Math.Abs(audioLatencyMs))));

                // audioLatencyBytes = requestedBytes; // uncomment this line to enable rewinding.
                if (audioLatencyBytes > requestedBytes && audioLatencyBytes < rewindableCount)
                {
                    // This means we have the audio pointer a little too ahead of time and we need to
                    // rewind it the requested amount of bytes.
                    AudioBuffer.Rewind(Math.Min(audioLatencyBytes, rewindableCount));
                }
                else
                {
                    // a negative audio latency means we are rendering audio ahead (before) the clock
                    // and therefore we need to render some silence until the clock catches up
                    if (Math.Abs(speedRatio - 1.0) <= double.Epsilon)
                    {
                        this.LogWarning(Aspects.AudioRenderer,
                            $"SYNC AUDIO: LATENCY: {audioLatencyMs} ms. | WAIT (samples being rendered too early)");
                    }

                    // render silence for the wait time and return
                    Array.Clear(targetBuffer, targetBufferOffset, requestedBytes);
                    return false;
                }
            }

            #endregion

            #region Small Latency Handling

            // Check if minor adjustment is necessary
            if (MediaCore.State.HasVideo == false || Math.Abs(speedRatio - 1.0) > double.Epsilon ||
                isBeyondThreshold || Math.Abs(audioLatencyMs) <= SyncThresholdPerfect)
                return true;

            // Perform minor adjustments until the delay is less than 10ms in either direction
            var stepDurationMillis = Convert.ToInt32(Math.Min(SyncThresholdMaxStep, Math.Abs(audioLatencyMs)));
            var stepDurationBytes = WaveFormat.ConvertMillisToByteSize(stepDurationMillis);

            if (audioLatencyMs > SyncThresholdPerfect)
                AudioBuffer.Skip(Math.Min(stepDurationBytes, readableCount));
            else if (audioLatencyMs < -SyncThresholdPerfect)
                AudioBuffer.Rewind(Math.Min(stepDurationBytes, rewindableCount));

            #endregion

            return true;
        }

        /// <summary>
        /// Reads from the Audio Buffer and stretches the samples to the required requested bytes.
        /// This will make audio samples sound stretched (low pitch).
        /// The result is put to the first requestedBytes count of the ReadBuffer.
        /// requested
        /// </summary>
        /// <param name="requestedBytes">The requested bytes.</param>
        /// <param name="speedRatio">The speed ratio.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ReadAndSlowDown(int requestedBytes, double speedRatio)
        {
            var bytesToRead = Math.Min(
                AudioBuffer.ReadableCount,
                Convert.ToInt32((requestedBytes * speedRatio).ToMultipleOf(SampleBlockSize)));
            var repeatFactor = Convert.ToDouble(requestedBytes) / bytesToRead;

            var sourceOffset = requestedBytes;
            AudioBuffer.Read(bytesToRead, ReadBuffer, sourceOffset);

            var targetOffset = 0;
            var repeatCount = 0d;

            while (targetOffset < requestedBytes)
            {
                // When we are done repeating, advance 1 block in the source position
                if (repeatCount >= repeatFactor)
                {
                    repeatCount = repeatCount % repeatFactor;
                    sourceOffset += SampleBlockSize;
                }

                // Copy data from read data to the final 0-offset data of the same read buffer.
                Buffer.BlockCopy(ReadBuffer, sourceOffset, ReadBuffer, targetOffset, SampleBlockSize);
                targetOffset += SampleBlockSize;
                repeatCount += 1d;
            }
        }

        /// <summary>
        /// Reads from the Audio Buffer and shrinks (averages) the samples to the required requested bytes.
        /// This will make audio samples sound shrunken (high pitch).
        /// The result is put to the first requestedBytes count of the ReadBuffer.
        /// </summary>
        /// <param name="requestedBytes">The requested number of bytes.</param>
        /// <param name="computeAverage">if set to <c>true</c> average samples per block. Otherwise, take the first sample per block only</param>
        /// <param name="speedRatio">The speed ratio.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ReadAndSpeedUp(int requestedBytes, bool computeAverage, double speedRatio)
        {
            var bytesToRead = Convert.ToInt32((requestedBytes * speedRatio).ToMultipleOf(SampleBlockSize));
            var sourceOffset = 0;

            if (bytesToRead > AudioBuffer.ReadableCount)
            {
                Seek();
                return;
            }

            AudioBuffer.Read(bytesToRead, ReadBuffer, sourceOffset);

            var groupSize = Convert.ToDouble(bytesToRead) / requestedBytes;
            var targetOffset = 0;
            var currentGroupSizeW = Convert.ToInt32(groupSize);
            var currentGroupSizeF = groupSize - currentGroupSizeW;
            double leftSamples;
            double rightSamples;
            var isLeftSample = true;
            short sample;
            int samplesToAverage;

            while (targetOffset < requestedBytes)
            {
                // Extract left and right samples
                leftSamples = 0;
                rightSamples = 0;
                samplesToAverage = 0;

                if (computeAverage)
                {
                    for (var i = sourceOffset;
                        i < sourceOffset + (currentGroupSizeW * SampleBlockSize);
                        i += Constants.Audio.BytesPerSample)
                    {
                        sample = ReadBuffer.GetAudioSample(i);
                        if (isLeftSample)
                        {
                            leftSamples += sample;
                            samplesToAverage += 1;
                        }
                        else
                        {
                            rightSamples += sample;
                        }

                        isLeftSample = !isLeftSample;
                    }

                    // compute an average of the samples
                    leftSamples = leftSamples / samplesToAverage;
                    rightSamples = rightSamples / samplesToAverage;
                }
                else
                {
                    // If I set samples to average to 1 here, it does not change the pitch but
                    // audio gaps are noticeable
                    // Another option: currentGroupSizeW * SampleBlockSize / BytesPerSample / 2
                    samplesToAverage = 1;
                    leftSamples = ReadBuffer.GetAudioSample(sourceOffset);
                    rightSamples = ReadBuffer.GetAudioSample(sourceOffset + Constants.Audio.BytesPerSample);
                }

                // Write the samples
                ReadBuffer.PutAudioSample(targetOffset, Convert.ToInt16(leftSamples));
                ReadBuffer.PutAudioSample(targetOffset + Constants.Audio.BytesPerSample, Convert.ToInt16(rightSamples));

                // advance the base source offset
                currentGroupSizeW = Convert.ToInt32(groupSize + currentGroupSizeF);
                currentGroupSizeF = (groupSize + currentGroupSizeF) - currentGroupSizeW;

                sourceOffset += samplesToAverage * SampleBlockSize;
                targetOffset += SampleBlockSize;
            }
        }

        /// <summary>
        /// Reads from the Audio Buffer and uses the SoundTouch audio processor to adjust tempo
        /// The result is put to the first requestedBytes count of the ReadBuffer.
        /// This feature is experimental
        /// </summary>
        /// <param name="requestedBytes">The requested bytes.</param>
        /// <param name="speedRatio">The speed ratio.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ReadAndUseAudioProcessor(int requestedBytes, double speedRatio)
        {
            if (AudioProcessorBuffer == null || AudioProcessorBuffer.Length < Convert.ToInt32(requestedBytes * Constants.Controller.MaxSpeedRatio))
                AudioProcessorBuffer = new short[Convert.ToInt32(requestedBytes * Constants.Controller.MaxSpeedRatio / Constants.Audio.BytesPerSample)];

            var bytesToRead = Convert.ToInt32((requestedBytes * speedRatio).ToMultipleOf(SampleBlockSize));
            var samplesToRequest = requestedBytes / SampleBlockSize;

            // Set the new tempo (without changing the pitch) according to the speed ratio
            AudioProcessor.SetTempo(Convert.ToSingle(speedRatio));

            // Sending Samples to the processor
            while (AudioProcessor.AvailableSampleCount < samplesToRequest && AudioBuffer != null)
            {
                var realBytesToRead = Math.Min(AudioBuffer.ReadableCount, bytesToRead);
                if (realBytesToRead == 0) break;

                realBytesToRead = Convert.ToInt32((realBytesToRead * 1.0).ToMultipleOf(SampleBlockSize));
                AudioBuffer.Read(realBytesToRead, ReadBuffer, 0);
                Buffer.BlockCopy(ReadBuffer, 0, AudioProcessorBuffer, 0, realBytesToRead);
                AudioProcessor.PutSamplesI16(AudioProcessorBuffer, Convert.ToUInt32(realBytesToRead / SampleBlockSize));
            }

            // Receiving samples from the processor
            var numSamples = AudioProcessor.ReceiveSamplesI16(AudioProcessorBuffer, Convert.ToUInt32(samplesToRequest));
            Array.Clear(ReadBuffer, 0, ReadBuffer.Length);
            Buffer.BlockCopy(AudioProcessorBuffer, 0, ReadBuffer, 0, Convert.ToInt32(numSamples * SampleBlockSize));
        }

        /// <summary>
        /// Applies volume and balance to the audio samples stored in RedBuffer and writes them
        /// to the specified target buffer.
        /// </summary>
        /// <param name="targetBuffer">The target buffer.</param>
        /// <param name="targetBufferOffset">The target buffer offset.</param>
        /// <param name="requestedBytes">The requested number of bytes.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ApplyVolumeAndBalance(byte[] targetBuffer, int targetBufferOffset, int requestedBytes)
        {
            // Check if we are muted. We don't need process volume and balance
            var isMuted = MediaCore.State.IsMuted;
            if (isMuted)
            {
                for (var sourceBufferOffset = 0; sourceBufferOffset < requestedBytes; sourceBufferOffset++)
                    targetBuffer[targetBufferOffset + sourceBufferOffset] = 0;

                return;
            }

            // Capture and adjust volume and balance
            var volume = MediaCore.State.Volume;
            var balance = MediaCore.State.Balance;

            volume = volume.Clamp(Constants.Controller.MinVolume, Constants.Controller.MaxVolume);
            balance = balance.Clamp(Constants.Controller.MinBalance, Constants.Controller.MaxBalance);

            var leftVolume = volume * (balance > 0 ? 1d - balance : 1d);
            var rightVolume = volume * (balance < 0 ? 1d + balance : 1d);

            // Initialize the samples counter
            // Samples are interleaved (left and right in 16-bit signed integers each)
            var isLeftSample = true;
            short currentSample;

            for (var sourceBufferOffset = 0;
                sourceBufferOffset < requestedBytes;
                sourceBufferOffset += Constants.Audio.BytesPerSample)
            {
                // The sample has 2 bytes: at the base index is the LSB and at the baseIndex + 1 is the MSB.
                // This holds true for little endian architecture
                currentSample = ReadBuffer.GetAudioSample(sourceBufferOffset);

                if (isLeftSample && Math.Abs(leftVolume - 1.0) > double.Epsilon)
                    currentSample = Convert.ToInt16(currentSample * leftVolume);
                else if (isLeftSample == false && Math.Abs(rightVolume - 1.0) > double.Epsilon)
                    currentSample = Convert.ToInt16(currentSample * rightVolume);

                targetBuffer.PutAudioSample(targetBufferOffset + sourceBufferOffset, currentSample);
                isLeftSample = !isLeftSample;
            }
        }

        #endregion
    }
}
