namespace Unosquare.FFME.Rendering
{
    using Core;
    using Decoding;
    using Rendering.Wave;
    using System;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Windows;
    using System.Windows.Threading;

    /// <summary>
    /// Provides Audio Output capabilities by writing samples to the default audio output device.
    /// </summary>
    /// <seealso cref="Unosquare.FFME.Rendering.Wave.IWaveProvider" />
    /// <seealso cref="Unosquare.FFME.Rendering.IRenderer" />
    /// <seealso cref="System.IDisposable" />
    internal sealed class AudioRenderer : IDisposable, IRenderer, IWaveProvider
    {
        #region Private Members

        private readonly ManualResetEvent WaitForReadyEvent = new ManualResetEvent(false);
        private readonly object SyncLock = new object();

        private WavePlayer AudioDevice;
        private CircularBuffer AudioBuffer;
        private bool IsDisposed = false;

        private byte[] ReadBuffer = null;
        private double LeftVolume = 1.0d;
        private double RightVolume = 1.0d;

        private WaveFormat m_Format = null;
        private double m_Volume = 1.0d;
        private double m_Balance = 0.0d;
        private volatile bool m_IsMuted = false;

        private int BytesPerSample = 2;
        private double SyncThesholdMilliseconds = 0d;
        private int SampleBlockSize = 0;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioRenderer"/> class.
        /// </summary>
        /// <param name="mediaElement">The media element.</param>
        public AudioRenderer(MediaElement mediaElement)
        {
            MediaElement = mediaElement;

            m_Format = new WaveFormat(AudioParams.Output.SampleRate, AudioParams.OutputBitsPerSample, AudioParams.Output.ChannelCount);
            if (WaveFormat.BitsPerSample != 16 || WaveFormat.Channels != 2)
                throw new NotSupportedException("Wave Format has to be 16-bit and 2-channel.");

            if (MediaElement.HasAudio)
                Initialize();

            if (Application.Current != null)
            {
                Runner.UIInvoke(DispatcherPriority.Normal, () =>
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
        public WaveFormat WaveFormat
        {
            get { return m_Format; }
        }

        /// <summary>
        /// Gets the parent media element.
        /// </summary>
        public MediaElement MediaElement { get; private set; }

        /// <summary>
        /// Gets or sets the volume.
        /// </summary>
        /// <value>
        /// The volume.
        /// </value>
        public double Volume
        {
            get
            {
                return Thread.VolatileRead(ref m_Volume);
            }
            set
            {
                if (value < 0) value = 0;
                if (value > 1) value = 1;

                var leftFactor = m_Balance > 0 ? 1d - m_Balance : 1d;
                var rightFactor = m_Balance < 0 ? 1d + m_Balance : 1d;

                LeftVolume = leftFactor * value;
                RightVolume = rightFactor * value;
                Thread.VolatileWrite(ref m_Volume, value);
            }
        }

        /// <summary>
        /// Gets or sets the balance (-1.0 to 1.0).
        /// </summary>
        public double Balance
        {
            get
            {
                return Thread.VolatileRead(ref m_Balance);
            }
            set
            {
                if (value < -1.0) value = -1.0;
                if (value > 1.0) value = 1.0;
                Thread.VolatileWrite(ref m_Balance, value);
                Volume = Thread.VolatileRead(ref m_Volume);
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the wave output is muted.
        /// </summary>
        public bool IsMuted
        {
            get { return m_IsMuted; }
            set { m_IsMuted = value; }
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
                    return TimeSpan.FromTicks(MediaElement.Clock.Position.Ticks - Position.Ticks);
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
        public TimeSpan DesiredLatency
        {
            get { return TimeSpan.FromTicks((AudioDevice?.DesiredLatency ?? 1) * TimeSpan.TicksPerMillisecond); }
        }

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
            SpeedRatio = MediaElement?.Clock?.SpeedRatio ?? 0d;

            if (AudioBuffer == null) return;

            var block = mediaBlock as AudioBlock;
            if (block == null) return;

            var audioBlocks = MediaElement.Blocks[MediaType.Audio];
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
            WaitForReadyEvent.WaitOne();
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
                    if (ReadBuffer == null || ReadBuffer.Length < (int)(requestedBytes * Constants.MaxSpeedRatio))
                        ReadBuffer = new byte[(int)(requestedBytes * Constants.MaxSpeedRatio)];

                    // Perform AV Synchronization if needed
                    if (MediaElement.HasVideo && Synchronize(targetBuffer, targetBufferOffset, requestedBytes) == false)
                        return requestedBytes;

                    // Perform DSP
                    if (SpeedRatio < 1.0)
                    {
                        ReadAndStretch(requestedBytes);
                    }
                    else if (SpeedRatio > 1.0)
                    {
                        ReadAndShrink(requestedBytes);
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
                    MediaElement.Logger.Log(MediaLogMessageType.Error, $"{ex.GetType()} in {nameof(AudioRenderer)}.{nameof(Read)}: {ex.Message}. Stack Trace:\r\n{ex.StackTrace}");
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

            AudioDevice = new WavePlayer(this)
            {
                DesiredLatency = 200,
                NumberOfBuffers = 2,
            };

            SyncThesholdMilliseconds = 0.05 * DesiredLatency.TotalMilliseconds; // ~5% sync threshold for audio samples 
            BytesPerSample = WaveFormat.BitsPerSample / 8;
            SampleBlockSize = BytesPerSample * WaveFormat.Channels;

            var bufferLength = WaveFormat.ConvertLatencyToByteSize(AudioDevice.DesiredLatency) * MediaElement.Blocks[MediaType.Audio].Capacity / 2;
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
                    Application.Current.Exit -= OnApplicationExit;
            }
            catch { }

            if (AudioDevice != null)
            {
                AudioDevice.Stop();
                AudioDevice.Dispose();
                AudioDevice = null;
            }

            if (AudioBuffer != null)
            {
                AudioBuffer.Dispose();
                AudioBuffer = null;
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

            if (audioLatency.TotalMilliseconds > SyncThesholdMilliseconds / 2d)
            {
                // a positive audio latency means we are rendering audio behind (after) the clock (skip some samples)
                // and therefore we need to advance the buffer before we read from it.
                MediaElement.Container?.Logger?.Log(MediaLogMessageType.Warning,
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
                    MediaElement.Container?.Logger?.Log(MediaLogMessageType.Warning,
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
        private void ReadAndStretch(int requestedBytes)
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
        /// <param name="requestedBytes">The requested bytes.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ReadAndShrink(int requestedBytes)
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
            var currentGroupSize = groupSize;
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
                for (var i = sourceOffset; i < sourceOffset + ((int)currentGroupSize * SampleBlockSize); i += BytesPerSample)
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

                // Write the samples
                ReadBuffer[targetOffset + 0] = (byte)((short)leftSamples & 0xff);
                ReadBuffer[targetOffset + 1] = (byte)((short)leftSamples >> 8);
                ReadBuffer[targetOffset + 2] = (byte)((short)rightSamples & 0xff);
                ReadBuffer[targetOffset + 3] = (byte)((short)rightSamples >> 8);

                // advance the base source offset
                currentGroupSize += samplesToAverage;
                if (currentGroupSize > groupSize) currentGroupSize = groupSize + (currentGroupSize % groupSize);
                sourceOffset += samplesToAverage * SampleBlockSize;
                targetOffset += SampleBlockSize;
            }
        }

        /// <summary>
        /// Applies volume and balance to the audio samples storead in RedBuffer and writes them
        /// to the specified target buffer.
        /// </summary>
        /// <param name="targetBuffer">The target buffer.</param>
        /// <param name="targetBufferOffset">The target buffer offset.</param>
        /// <param name="requestedBytes">The requested bytes.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ApplyVolumeAndBalance(byte[] targetBuffer, int targetBufferOffset, int requestedBytes)
        {
            // Samples are interleaved (left and right in 16-bit signed integers each)
            var isLeftSample = true;
            short sample;

            for (var sourceBufferOffset = 0; sourceBufferOffset < requestedBytes; sourceBufferOffset += BytesPerSample)
            {
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
