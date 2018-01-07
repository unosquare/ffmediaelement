namespace Unosquare.FFME.Rendering
{
    using Core;
    using Primitives;
    using Platform;
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
    /// <seealso cref="Unosquare.FFME.Shared.IMediaRenderer" />
    /// <seealso cref="Unosquare.FFME.Rendering.Wave.IWaveProvider" />
    /// <seealso cref="System.IDisposable" />
    internal sealed class AudioRenderer : IDisposable, IMediaRenderer, IWaveProvider
    {
        #region Private Members

        private readonly ManualResetEvent WaitForReadyEvent = new ManualResetEvent(false);
        private readonly object SyncLock = new object();

        private WavePlayer AudioDevice = null;
        private SoundTouch AudioProcessor = null;
        private short[] AudioProcessorBuffer = null;
        private CircularBuffer AudioBuffer = null;
        private bool IsDisposed = false;

        private byte[] ReadBuffer = null;
        private double LeftVolume = 1.0d;
        private double RightVolume = 1.0d;

        private WaveFormat m_Format = null;
        private AtomicDouble m_Volume = new AtomicDouble(Defaults.DefaultVolume);
        private AtomicDouble m_Balance = new AtomicDouble(Defaults.DefaultBalance);
        private AtomicBoolean m_IsMuted = new AtomicBoolean(false);

        private int BytesPerSample = 2;
        private double SyncThesholdMilliseconds = 0d;
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
                Defaults.AudioSampleRate,
                Defaults.AudioBitsPerSample,
                Defaults.AudioChannelCount);

            if (WaveFormat.BitsPerSample != 16 || WaveFormat.Channels != 2)
                throw new NotSupportedException("Wave Format has to be 16-bit and 2-channel.");

            if (MediaElement.HasAudio)
                Initialize();

            if (Application.Current != null)
            {
                WindowsPlatform.Instance.GuiInvoke((ActionPriority)DispatcherPriority.Normal, () =>
                {
                    Application.Current.Exit += OnApplicationExit;
                });
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the output format of the audio
        /// </summary>
        public WaveFormat WaveFormat => m_Format;

        /// <summary>
        /// Gets the parent media element (platform specific).
        /// </summary>
        public MediaElement MediaElement => MediaCore?.Parent as MediaElement;

        /// <summary>
        /// Gets the core platform independent player component.
        /// </summary>
        public MediaEngine MediaCore { get; }

        /// <summary>
        /// Gets or sets the volume.
        /// </summary>
        /// <value>
        /// The volume.
        /// </value>
        public double Volume
        {
            get => m_Volume.Value;
            set
            {
                if (value < 0) value = 0;
                if (value > 1) value = 1;

                var leftFactor = m_Balance.Value > 0 ? 1d - m_Balance.Value : 1d;
                var rightFactor = m_Balance.Value < 0 ? 1d + m_Balance.Value : 1d;

                LeftVolume = leftFactor * value;
                RightVolume = rightFactor * value;
                m_Volume.Value = value;
            }
        }

        /// <summary>
        /// Gets or sets the balance (-1.0 to 1.0).
        /// </summary>
        public double Balance
        {
            get => m_Balance.Value;
            set
            {
                if (value < -1.0) value = -1.0;
                if (value > 1.0) value = 1.0;
                m_Balance.Value = value;
                Volume = m_Volume.Value;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the wave output is muted.
        /// </summary>
        public bool IsMuted
        {
            get => m_IsMuted.Value;
            set => m_IsMuted.Value = value;
        }

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

        /// <summary>
        /// Gets the desired latency odf the audio device.
        /// Value is always positive and typically 200ms. This means audio gets rendered up to this late behind the wall clock.
        /// </summary>
        public TimeSpan DesiredLatency => TimeSpan.FromTicks((AudioDevice?.DesiredLatency ?? 1) * TimeSpan.TicksPerMillisecond);

        /// <summary>
        /// Gets the speed ratio.
        /// </summary>
        public double SpeedRatio { get; private set; }

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

            // Update the speedratio
            SpeedRatio = MediaCore?.RealTimeClockSpeedRatio ?? 0d;

            if (AudioBuffer == null) return;

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

                    // Render silence if we don't need to output anything
                    if (MediaElement.IsPlaying == false || SpeedRatio <= 0d || MediaElement.HasAudio == false || AudioBuffer.ReadableCount <= 0)
                    {
                        Array.Clear(targetBuffer, targetBufferOffset, requestedBytes);
                        return requestedBytes;
                    }

                    // Ensure a preallocated ReadBuffer
                    if (ReadBuffer == null || ReadBuffer.Length < (int)(requestedBytes * Defaults.MaxSpeedRatio))
                        ReadBuffer = new byte[(int)(requestedBytes * Defaults.MaxSpeedRatio)];

                    // Perform AV Synchronization if needed
                    if (MediaElement.HasVideo && Synchronize(targetBuffer, targetBufferOffset, requestedBytes) == false)
                        return requestedBytes;

                    // Perform DSP
                    if (SpeedRatio < 1.0)
                    {
                        ReadAndSlowDown(requestedBytes);
                    }
                    else if (SpeedRatio > 1.0)
                    {
                        if (AudioProcessor != null)
                            ReadAndUseAudioProcessor(requestedBytes);
                        else
                            ReadAndSpeedUp(requestedBytes, true);
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

            SyncThesholdMilliseconds = 0.05 * DesiredLatency.TotalMilliseconds; // ~5% sync threshold for audio samples 
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
            try
            {
                if (Application.Current != null)
                {
                    WindowsPlatform.Instance.GuiInvoke((ActionPriority)DispatcherPriority.Send, () =>
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

        #endregion

        #region DSP

        /// <summary>
        /// Synchronizes audio rendering to the wall clock.
        /// Returns true if additional samples need to be read.
        /// Returns false if silence has been written and no further reading is required.
        /// </summary>
        /// <param name="targetBuffer">The target buffer.</param>
        /// <param name="targetBufferOffset">The target buffer offset.</param>
        /// <param name="requestedBytes">The requested bytes.</param>
        /// <returns>True to continue processing. False to write silence.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool Synchronize(byte[] targetBuffer, int targetBufferOffset, int requestedBytes)
        {
            var audioLatency = Latency;

            if (audioLatency.TotalMilliseconds > 2d * SyncThesholdMilliseconds)
            {
                // a positive audio latency means we are rendering audio behind (after) the clock (skip some samples)
                // and therefore we need to advance the buffer before we read from it.
                MediaCore.Log(MediaLogMessageType.Warning,
                    $"SYNC AUDIO: LATENCY: {audioLatency.Format()} | SKIP (samples being rendered too late)");

                // skip some samples from the buffer.
                var audioLatencyBytes = WaveFormat.ConvertLatencyToByteSize((int)Math.Ceiling(audioLatency.TotalMilliseconds));
                AudioBuffer.Skip(Math.Min(audioLatencyBytes, AudioBuffer.ReadableCount));
            }
            else if (audioLatency.TotalMilliseconds < -2d * SyncThesholdMilliseconds)
            {
                // Compute the latency in bytes
                var audioLatencyBytes = WaveFormat.ConvertLatencyToByteSize((int)Math.Ceiling(Math.Abs(audioLatency.TotalMilliseconds)));
                audioLatencyBytes = requestedBytes; // TODO: Comment this line to enable rewinding.
                if (audioLatencyBytes > requestedBytes && audioLatencyBytes < AudioBuffer.RewindableCount)
                {
                    // This means we have the audio pointer a little too ahead of time and we need to
                    // rewind it the requested amount of bytes.
                    AudioBuffer.Rewind(Math.Min(audioLatencyBytes, AudioBuffer.RewindableCount));
                }
                else
                {
                    // a negative audio latency means we are rendering audio ahead (before) the clock
                    // and therefore we need to render some silence until the clock catches up
                    MediaCore.Log(MediaLogMessageType.Warning,
                        $"SYNC AUDIO: LATENCY: {audioLatency.Format()} | WAIT (samples being rendered too early)");

                    // render silence for the wait time and return
                    Array.Clear(targetBuffer, targetBufferOffset, requestedBytes);
                    return false;
                }
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ReadAndSlowDown(int requestedBytes)
        {
            var bytesToRead = Math.Min(
                AudioBuffer.ReadableCount,
                (int)(requestedBytes * SpeedRatio).ToMultipleOf(SampleBlockSize));
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ReadAndSpeedUp(int requestedBytes, bool computeAverage)
        {
            var bytesToRead = (int)(requestedBytes * SpeedRatio).ToMultipleOf(SampleBlockSize);
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ReadAndUseAudioProcessor(int requestedBytes)
        {
            if (AudioProcessorBuffer == null || AudioProcessorBuffer.Length < (int)(requestedBytes * Defaults.MaxSpeedRatio))
                AudioProcessorBuffer = new short[(int)(requestedBytes * Defaults.MaxSpeedRatio / BytesPerSample)];

            var speedRatio = SpeedRatio;
            var bytesToRead = (int)(requestedBytes * speedRatio).ToMultipleOf(SampleBlockSize);
            var samplesToRead = bytesToRead / SampleBlockSize;
            var samplesToRequest = requestedBytes / SampleBlockSize;
            AudioProcessor.Tempo = Convert.ToSingle(speedRatio);

            while (AudioProcessor.AvailableSampleCount < samplesToRequest && AudioBuffer != null)
            {
                var realBytesToRead = Math.Min(AudioBuffer.ReadableCount, bytesToRead);
                realBytesToRead = (int)(realBytesToRead * 1.0).ToMultipleOf(SampleBlockSize);
                AudioBuffer.Read(realBytesToRead, ReadBuffer, 0);
                Buffer.BlockCopy(ReadBuffer, 0, AudioProcessorBuffer, 0, realBytesToRead);
                AudioProcessor.PutSamplesI16(AudioProcessorBuffer, (uint)(realBytesToRead / SampleBlockSize));
            }

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
            // Samples are interleaved (left and right in 16-bit signed integers each)
            var isLeftSample = true;
            short sample = 0;

            if (IsMuted)
            {
                for (var sourceBufferOffset = 0; sourceBufferOffset < requestedBytes; sourceBufferOffset++)
                    targetBuffer[targetBufferOffset + sourceBufferOffset] = 0;

                return;
            }

            for (var sourceBufferOffset = 0; sourceBufferOffset < requestedBytes; sourceBufferOffset += BytesPerSample)
            {
                // TODO: Make architecture-agnostic sound processing
                // The sample has 2 bytes: at the base index is the LSB and at the baseIndex + 1 is the MSB
                // this obviously only holds true for Little Endian architectures, and thus, the current code is not portable.
                // This replaces BitConverter.ToInt16(ReadBuffer, baseIndex); which is obviously much slower.
                sample = (short)(ReadBuffer[sourceBufferOffset] | (ReadBuffer[sourceBufferOffset + 1] << 8));

                if (IsMuted)
                {
                    sample = 0;
                }
                else
                {
                    if (isLeftSample && LeftVolume != 1.0)
                        sample = (short)(sample * LeftVolume);
                    else if (isLeftSample == false && RightVolume != 1.0)
                        sample = (short)(sample * RightVolume);
                }

                targetBuffer[targetBufferOffset + sourceBufferOffset] = (byte)(sample & 0xff);
                targetBuffer[targetBufferOffset + sourceBufferOffset + 1] = (byte)(sample >> 8);
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
