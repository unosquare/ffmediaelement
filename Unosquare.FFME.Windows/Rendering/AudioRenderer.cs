namespace Unosquare.FFME.Rendering
{
    using Platform;
    using Primitives;
    using Rendering.Wave;
    using Shared;
    using System;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Windows;
    using System.Windows.Threading;

    /// <summary>
    /// Provides Audio Output capabilities by writing samples to the default audio output device.
    /// </summary>
    /// <seealso cref="IMediaRenderer" />
    /// <seealso cref="IWaveProvider" />
    /// <seealso cref="IDisposable" />
    internal sealed class AudioRenderer : IDisposable, IMediaRenderer, IWaveProvider
    {
        #region Private Members

        private const double SyncThresholdPerfect = 10;
        private const double SyncThresholdLagging = 100;
        private const double SyncThresholdLeading = -25;
        private const int SyncThresholdMaxStep = 25;

        private readonly ManualResetEvent WaitForReadyEvent = new ManualResetEvent(false);
        private readonly object SyncLock = new object();

        private WavePlayer AudioDevice = null;
        private SoundTouch AudioProcessor = null;
        private short[] AudioProcessorBuffer = null;
        private CircularBuffer AudioBuffer = null;
        private bool IsDisposed = false;

        private byte[] ReadBuffer = null;
        private WaveFormat m_Format = null;
        private int BytesPerSample = 2;
        private int SampleBlockSize = 0;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioRenderer"/> class.
        /// </summary>
        /// <param name="mediaEngine">The core media engine.</param>
        public AudioRenderer(MediaEngine mediaEngine)
        {
            MediaCore = mediaEngine;

            m_Format = new WaveFormat(
                Constants.Audio.SampleRate,
                Constants.Audio.BitsPerSample,
                Constants.Audio.ChannelCount);

            if (WaveFormat.BitsPerSample != 16 || WaveFormat.Channels != 2)
                throw new NotSupportedException("Wave Format has to be 16-bit and 2-channel.");

            if (MediaElement.HasAudio)
                Initialize();

            if (Application.Current != null)
            {
                WindowsPlatform.Instance.Gui?.Invoke(DispatcherPriority.Normal, () =>
                {
                    Application.Current.Exit += OnApplicationExit;
                });
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the parent media element (platform specific).
        /// </summary>
        public MediaElement MediaElement => MediaCore?.Parent as MediaElement;

        /// <summary>
        /// Gets the core platform independent player component.
        /// </summary>
        public MediaEngine MediaCore { get; }

        /// <summary>
        /// Gets the output format of the audio
        /// </summary>
        public WaveFormat WaveFormat => m_Format;

        /// <summary>
        /// Gets the realtime latency of the audio relative to the internal wall clock.
        /// A negative value means audio is ahead of the wall clock.
        /// A positive value means audio is behind of the wall clock.
        /// </summary>
        public TimeSpan Latency
        {
            get
            {
                // The delay is the clock position minus the current position
                lock (SyncLock)
                    return TimeSpan.FromTicks(MediaCore.RealTimeClockPosition.Ticks - Position.Ticks);
            }
        }

        /// <summary>
        /// Gets current audio the position.
        /// </summary>
        public TimeSpan Position
        {
            get
            {
                lock (SyncLock)
                {
                    // if we don't have a valid write tag it's just wahtever has been read from the audio buffer
                    if (AudioBuffer.WriteTag == TimeSpan.MinValue)
                    {
                        return TimeSpan.FromMilliseconds((long)Math.Round(
                            TimeSpan.TicksPerMillisecond * 1000d * (AudioBuffer.Length - AudioBuffer.ReadableCount) / WaveFormat.AverageBytesPerSecond, 0));
                    }

                    // the pending audio length is the amount of audio samples time that has not been yet read by the audio device.
                    var pendingAudioLength = TimeSpan.FromTicks(
                        (long)Math.Round(TimeSpan.TicksPerMillisecond * 1000d * AudioBuffer.ReadableCount / WaveFormat.AverageBytesPerSecond, 0));

                    // the current position is the Write tag minus the pending length
                    return TimeSpan.FromTicks(AudioBuffer.WriteTag.Ticks - pendingAudioLength.Ticks);
                }
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Renders the specified media block.
        /// </summary>
        /// <param name="mediaBlock">The media block.</param>
        /// <param name="clockPosition">The clock position.</param>
        public void Render(MediaBlock mediaBlock, TimeSpan clockPosition)
        {
            // We don't need to render anything while we are seeking. Simply drop the blocks.
            if (MediaElement.IsSeeking) return;
            if (AudioBuffer == null) return;

            // Capture Media Block Reference
            var block = mediaBlock as AudioBlock;
            if (block == null) return;

            var audioBlocks = MediaCore.Blocks[MediaType.Audio];
            var audioBlock = mediaBlock as AudioBlock;

            while (audioBlock != null)
            {
                // Write the block if we have to, avoiding repeated blocks.
                if (AudioBuffer.WriteTag < audioBlock.StartTime)
                {
                    MediaElement.RaiseRenderingAudioEvent(audioBlock, clockPosition);
                    AudioBuffer.Write(audioBlock.Buffer, audioBlock.BufferLength, audioBlock.StartTime, true);
                }

                // Stop adding if we have too much in there.
                if (AudioBuffer.CapacityPercent >= 0.8)
                    break;

                // Retrieve the following block
                audioBlock = audioBlocks.Next(audioBlock) as AudioBlock;
            }
        }

        /// <summary>
        /// Called on every block rendering clock cycle just in case some update operation needs to be performed.
        /// This needs to return immediately so the calling thread is not disturbed.
        /// </summary>
        /// <param name="clockPosition">The clock position.</param>
        public void Update(TimeSpan clockPosition)
        {
            // placeholder
        }

        /// <summary>
        /// Executed when the Play method is called on the parent MediaElement
        /// </summary>
        public void Play()
        {
            // placeholder
        }

        /// <summary>
        /// Executed when the Pause method is called on the parent MediaElement
        /// </summary>
        public void Pause()
        {
            // Placeholder
        }

        /// <summary>
        /// Executed when the Pause method is called on the parent MediaElement
        /// </summary>
        public void Stop()
        {
            Seek();
        }

        /// <summary>
        /// Executed when the Close method is called on the parent MediaElement
        /// </summary>
        public void Close()
        {
            // Yes, seek and destroy... coincidentally.
            lock (SyncLock)
            {
                Seek();
                Destroy();
            }
        }

        /// <summary>
        /// Executed after a Seek operation is performed on the parent MediaElement
        /// </summary>
        public void Seek()
        {
            lock (SyncLock)
            {
                AudioBuffer?.Clear();

                if (ReadBuffer != null)
                    Array.Clear(ReadBuffer, 0, ReadBuffer.Length);
            }
        }

        /// <summary>
        /// Waits for the renderer to be ready to render.
        /// </summary>
        public void WaitForReadyState()
        {
            WaitForReadyEvent?.WaitOne();
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        #endregion

        #region IWaveProvider Support

        /// <summary>
        /// Called whenever the audio driver requests samples.
        /// Do not call this method directly.
        /// </summary>
        /// <param name="targetBuffer">The render buffer.</param>
        /// <param name="targetBufferOffset">The render buffer offset.</param>
        /// <param name="requestedBytes">The requested bytes.</param>
        /// <returns>The number of bytes that were read.</returns>
        public int Read(byte[] targetBuffer, int targetBufferOffset, int requestedBytes)
        {
            lock (SyncLock)
            {
                try
                {
                    WaitForReadyEvent.Set();
                    var speedRatio = MediaCore?.SpeedRatio ?? 0;

                    // Render silence if we don't need to output samples
                    if (MediaElement.IsPlaying == false || speedRatio <= 0d || MediaElement.HasAudio == false || AudioBuffer.ReadableCount <= 0)
                    {
                        Array.Clear(targetBuffer, targetBufferOffset, requestedBytes);
                        return requestedBytes;
                    }

                    // Ensure a preallocated ReadBuffer
                    if (ReadBuffer == null || ReadBuffer.Length < (int)(requestedBytes * Constants.Controller.MaxSpeedRatio))
                        ReadBuffer = new byte[(int)(requestedBytes * Constants.Controller.MaxSpeedRatio)];

                    // First part of DSP: Perform AV Synchronization if needed
                    if (MediaElement.HasVideo && Synchronize(targetBuffer, targetBufferOffset, requestedBytes, speedRatio) == false)
                        return requestedBytes;

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
                }
                catch (Exception ex)
                {
                    MediaElement?.MediaCore?.Log(MediaLogMessageType.Error, $"{ex.GetType()} in {nameof(AudioRenderer)}.{nameof(Read)}: {ex.Message}. Stack Trace:\r\n{ex.StackTrace}");
                    Array.Clear(targetBuffer, targetBufferOffset, requestedBytes);
                }

                return requestedBytes;
            }
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
            catch { }
        }

        /// <summary>
        /// Initializes the audio renderer.
        /// Call the Play Method to start reading samples
        /// </summary>
        private void Initialize()
        {
            Destroy();

            if (SoundTouch.IsAvailable)
            {
                AudioProcessor = new SoundTouch
                {
                    Channels = (uint)WaveFormat.Channels,
                    SampleRate = (uint)WaveFormat.SampleRate
                };
            }

            AudioDevice = new WavePlayer(this)
            {
                DesiredLatency = 200,
                NumberOfBuffers = 2,
            };

            BytesPerSample = WaveFormat.BitsPerSample / 8;
            SampleBlockSize = BytesPerSample * WaveFormat.Channels;

            var bufferLength = WaveFormat.ConvertLatencyToByteSize(AudioDevice.DesiredLatency) * MediaCore.Blocks[MediaType.Audio].Capacity / 2;
            AudioBuffer = new CircularBuffer(bufferLength);
            AudioDevice.Init(this);
            AudioDevice.Play();
        }

        /// <summary>
        /// Destroys the audio renderer.
        /// Makes it useless.
        /// </summary>
        private void Destroy()
        {
            lock (SyncLock)
            {
                try
                {
                    if (Application.Current != null)
                    {
                        WindowsPlatform.Instance.Gui?.Invoke(DispatcherPriority.Send, () =>
                        {
                            Application.Current.Exit -= OnApplicationExit;
                        });
                    }
                }
                catch
                {
                    // ignored
                }

                if (AudioDevice != null)
                {
                    AudioDevice.Pause();
                    AudioDevice.Dispose();
                    AudioDevice = null;
                }

                if (AudioBuffer != null)
                {
                    AudioBuffer.Dispose();
                    AudioBuffer = null;
                }

                if (AudioProcessor != null)
                {
                    AudioProcessor.Dispose();
                    AudioProcessor = null;
                }
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

            var audioLatencyMs = Latency.TotalMilliseconds;
            var isBeyondThreshold = false;
            var readableCount = AudioBuffer.ReadableCount;
            var rewindableCount = AudioBuffer.RewindableCount;

            if (audioLatencyMs > SyncThresholdLagging)
            {
                isBeyondThreshold = true;

                // a positive audio latency means we are rendering audio behind (after) the clock (skip some samples)
                // and therefore we need to advance the buffer before we read from it.
                if (speedRatio == 1.0)
                {
                    MediaCore.Log(MediaLogMessageType.Warning,
                        $"SYNC AUDIO: LATENCY: {Latency.Format()} | SKIP (samples being rendered too late)");
                }

                // skip some samples from the buffer.
                var audioLatencyBytes = WaveFormat.ConvertLatencyToByteSize((int)Math.Ceiling(audioLatencyMs));
                AudioBuffer.Skip(Math.Min(audioLatencyBytes, readableCount));
            }
            else if (audioLatencyMs < SyncThresholdLeading)
            {
                isBeyondThreshold = true;

                // Compute the latency in bytes
                var audioLatencyBytes = WaveFormat.ConvertLatencyToByteSize((int)Math.Ceiling(Math.Abs(audioLatencyMs)));

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
                    if (speedRatio == 1.0)
                    {
                        MediaCore.Log(MediaLogMessageType.Warning,
                            $"SYNC AUDIO: LATENCY: {Latency.Format()} | WAIT (samples being rendered too early)");
                    }

                    // render silence for the wait time and return
                    Array.Clear(targetBuffer, targetBufferOffset, requestedBytes);
                    return false;
                }
            }

            // Perform minor adjustments until the delay is less than 10ms in either direction
            if (MediaCore.HasVideo &&
                speedRatio == 1.0 &&
                isBeyondThreshold == false &&
                Math.Abs(audioLatencyMs) > SyncThresholdPerfect)
            {
                var stepDurationMillis = (int)Math.Min(SyncThresholdMaxStep, Math.Abs(audioLatencyMs));
                var stepDurationBytes = WaveFormat.ConvertLatencyToByteSize(stepDurationMillis);

                if (audioLatencyMs > SyncThresholdPerfect)
                    AudioBuffer.Skip(Math.Min(stepDurationBytes, readableCount));
                else if (audioLatencyMs < -SyncThresholdPerfect)
                    AudioBuffer.Rewind(Math.Min(stepDurationBytes, rewindableCount));
            }

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
                (int)(requestedBytes * speedRatio).ToMultipleOf(SampleBlockSize));
            var repeatFactor = (double)requestedBytes / bytesToRead;

            var sourceOffset = requestedBytes;
            AudioBuffer.Read(bytesToRead, ReadBuffer, sourceOffset);

            var targetOffset = 0;
            var repeatAccum = 0d;

            while (targetOffset < requestedBytes)
            {
                // When we are done repeating, advance 1 block in the source position
                if (repeatAccum >= repeatFactor)
                {
                    repeatAccum = repeatAccum % repeatFactor;
                    sourceOffset += SampleBlockSize;
                }

                // Copy data from read data to the final 0-offset data of the same read buffer.
                Buffer.BlockCopy(ReadBuffer, sourceOffset, ReadBuffer, targetOffset, SampleBlockSize);
                targetOffset += SampleBlockSize;
                repeatAccum += 1d;
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
            var bytesToRead = (int)(requestedBytes * speedRatio).ToMultipleOf(SampleBlockSize);
            var sourceOffset = 0;

            if (bytesToRead > AudioBuffer.ReadableCount)
            {
                Seek();
                return;
            }

            AudioBuffer.Read(bytesToRead, ReadBuffer, sourceOffset);

            var groupSize = (double)bytesToRead / requestedBytes;
            var targetOffset = 0;
            var currentGroupSizeW = (int)groupSize;
            var currentGroupSizeF = groupSize - currentGroupSizeW;
            var leftSamples = 0d;
            var rightSamples = 0d;
            var isLeftSample = true;
            short sample = 0;
            var samplesToAverage = 0;

            while (targetOffset < requestedBytes)
            {
                // Extract left and right samples
                leftSamples = 0;
                rightSamples = 0;
                samplesToAverage = 0;

                if (computeAverage)
                {
                    for (var i = sourceOffset; i < sourceOffset + (currentGroupSizeW * SampleBlockSize); i += BytesPerSample)
                    {
                        sample = (short)(ReadBuffer[i] | (ReadBuffer[i + 1] << 8));
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
                    leftSamples = Math.Round(leftSamples / samplesToAverage, 0);
                    rightSamples = Math.Round(rightSamples / samplesToAverage, 0);
                }
                else
                {
                    // If I set samples to average to 1 here, it does not change the pitch but
                    // audio gaps are noticeable
                    samplesToAverage = 1; //  currentGroupSizeW * SampleBlockSize / BytesPerSample / 2;
                    leftSamples = (short)(ReadBuffer[sourceOffset] | (ReadBuffer[sourceOffset + 1] << 8));
                    rightSamples = (short)(ReadBuffer[sourceOffset + 2] | (ReadBuffer[sourceOffset + 3] << 8));
                }

                // Write the samples
                ReadBuffer[targetOffset + 0] = (byte)((short)leftSamples & 0xff);
                ReadBuffer[targetOffset + 1] = (byte)((short)leftSamples >> 8);
                ReadBuffer[targetOffset + 2] = (byte)((short)rightSamples & 0xff);
                ReadBuffer[targetOffset + 3] = (byte)((short)rightSamples >> 8);

                // advance the base source offset
                currentGroupSizeW = (int)(groupSize + currentGroupSizeF);
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
            if (AudioProcessorBuffer == null || AudioProcessorBuffer.Length < (int)(requestedBytes * Constants.Controller.MaxSpeedRatio))
                AudioProcessorBuffer = new short[(int)(requestedBytes * Constants.Controller.MaxSpeedRatio / BytesPerSample)];

            var bytesToRead = (int)(requestedBytes * speedRatio).ToMultipleOf(SampleBlockSize);
            var samplesToRead = bytesToRead / SampleBlockSize;
            var samplesToRequest = requestedBytes / SampleBlockSize;

            // Set the new tempo (without changing the pitch) according to the speed ratio
            AudioProcessor.Tempo = Convert.ToSingle(speedRatio);

            // Sending Samples to the processor
            while (AudioProcessor.AvailableSampleCount < samplesToRequest && AudioBuffer != null)
            {
                var realBytesToRead = Math.Min(AudioBuffer.ReadableCount, bytesToRead);
                if (realBytesToRead == 0) break;

                realBytesToRead = (int)(realBytesToRead * 1.0).ToMultipleOf(SampleBlockSize);
                AudioBuffer.Read(realBytesToRead, ReadBuffer, 0);
                Buffer.BlockCopy(ReadBuffer, 0, AudioProcessorBuffer, 0, realBytesToRead);
                AudioProcessor.PutSamplesI16(AudioProcessorBuffer, (uint)(realBytesToRead / SampleBlockSize));
            }

            // Receiving samples from the processor
            uint numSamples = AudioProcessor.ReceiveSamplesI16(AudioProcessorBuffer, (uint)samplesToRequest);
            Array.Clear(ReadBuffer, 0, ReadBuffer.Length);
            Buffer.BlockCopy(AudioProcessorBuffer, 0, ReadBuffer, 0, (int)(numSamples * SampleBlockSize));
        }

        /// <summary>
        /// Applies volume and balance to the audio samples storead in RedBuffer and writes them
        /// to the specified target buffer.
        /// </summary>
        /// <param name="targetBuffer">The target buffer.</param>
        /// <param name="targetBufferOffset">The target buffer offset.</param>
        /// <param name="requestedBytes">The requested number of bytes.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ApplyVolumeAndBalance(byte[] targetBuffer, int targetBufferOffset, int requestedBytes)
        {
            // Check if we are muted. We don't need process volume and balance
            var isMuted = MediaCore?.IsMuted ?? true;
            if (isMuted)
            {
                for (var sourceBufferOffset = 0; sourceBufferOffset < requestedBytes; sourceBufferOffset++)
                    targetBuffer[targetBufferOffset + sourceBufferOffset] = 0;

                return;
            }

            // Capture and adjust volume and balance
            var volume = MediaCore?.Volume ?? Constants.Controller.DefaultVolume;
            var balance = MediaCore?.Balance ?? Constants.Controller.DefaultBalance;

            volume = volume.Clamp(Constants.Controller.MinVolume, Constants.Controller.MaxVolume);
            balance = balance.Clamp(Constants.Controller.MinBalance, Constants.Controller.MaxBalance);

            var leftVolume = volume * (balance > 0 ? 1d - balance : 1d);
            var rightVolume = volume * (balance < 0 ? 1d + balance : 1d);

            // Initialize the samples counter
            // Samples are interleaved (left and right in 16-bit signed integers each)
            var isLeftSample = true;
            short currentSample = 0;

            for (var sourceBufferOffset = 0; sourceBufferOffset < requestedBytes; sourceBufferOffset += BytesPerSample)
            {
                // TODO: Make architecture-agnostic sound processing
                // The sample has 2 bytes: at the base index is the LSB and at the baseIndex + 1 is the MSB
                // this obviously only holds true for Little Endian architectures, and thus, the current code might not be portable.
                // This replaces BitConverter.ToInt16(ReadBuffer, baseIndex); which is obviously much slower.
                currentSample = (short)(ReadBuffer[sourceBufferOffset] | (ReadBuffer[sourceBufferOffset + 1] << 8));

                if (isMuted)
                {
                    currentSample = 0;
                }
                else
                {
                    if (isLeftSample && leftVolume != 1.0)
                        currentSample = (short)(currentSample * leftVolume);
                    else if (isLeftSample == false && rightVolume != 1.0)
                        currentSample = (short)(currentSample * rightVolume);
                }

                targetBuffer[targetBufferOffset + sourceBufferOffset] = (byte)(currentSample & 0x00ff); // set the LSB
                targetBuffer[targetBufferOffset + sourceBufferOffset + 1] = (byte)(currentSample >> 8); // set the MSB
                isLeftSample = !isLeftSample;
            }
        }

        #endregion

        #region IDisposable Support

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="alsoManaged">
        ///   <c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        private void Dispose(bool alsoManaged)
        {
            if (!IsDisposed)
            {
                if (alsoManaged)
                {
                    Destroy();
                    WaitForReadyEvent.Dispose();
                }

                IsDisposed = true;
            }
        }

        #endregion

    }
}
