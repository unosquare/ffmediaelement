namespace Unosquare.FFmpegMediaElement
{
    using NAudio.Wave;
    using System;

    /// <summary>
    /// An interface that defines a callback method providing audio data
    /// </summary>
    internal interface IAudioDataProvider
    {
        /// <summary>
        /// When called in its implementation, this method fills the buffer with audio data and sets the amount of bytes written
        /// </summary>
        /// <param name="bufferToFill">The buffer to fill.</param>
        /// <param name="bytesWritten">The bytes written.</param>
        /// <returns>True if the audio device should request more samples. False if the audio device needs to be shutdown</returns>
        bool RenderAudioBuffer(byte[] bufferToFill, ref int bytesWritten);
    };

    /// <summary>
    /// Represents a calss that renders audio data to speakers
    /// via callbacks
    /// </summary>
    internal sealed class AudioRenderer : IDisposable
    {
        /// <summary>
        /// A thread-safe callback provider
        /// </summary>
        private class CallbackWaveProvider16 : IWaveProvider
        {
            public delegate bool RenderAudioBufferDelegate(byte[] bufferToFill, ref int bytesWritten);

            private RenderAudioBufferDelegate RenderCallback = null;
            private WaveFormat m_Format = null;
            private byte[] SilenceBuffer = null;
            private object SyncLock = null;

            public CallbackWaveProvider16(WaveFormat format, RenderAudioBufferDelegate renderCallback, object syncLock)
            {
                SyncLock = syncLock;
                m_Format = format;
                SilenceBuffer = new byte[m_Format.BitsPerSample / 8 * m_Format.Channels * 2];
                RenderCallback = renderCallback;
            }

            public int Read(byte[] buffer, int offset, int count)
            {
                lock (SyncLock)
                {
                    var fillBuffer = new byte[count];
                    var bytesWritten = count;

                    if (RenderCallback != null)
                        RenderCallback(fillBuffer, ref bytesWritten);
                    else
                        return 0;

                    if (bytesWritten == 0)
                    {
                        Array.Copy(SilenceBuffer, 0, buffer, offset, SilenceBuffer.Length);
                        return SilenceBuffer.Length;
                    }
                    else
                    {
                        Array.Copy(fillBuffer, 0, buffer, offset, bytesWritten);
                        return bytesWritten;
                    }
                }
            }

            public WaveFormat WaveFormat
            {
                get { return m_Format; }
            }

        }


        private IWavePlayer m_Device = null;
        private VolumeWaveProvider16 m_WaveProvider = null;
        private readonly object SyncLock = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioRenderer"/> class.
        /// </summary>
        public AudioRenderer()
        {
            m_Device = new DirectSoundOut(DirectSoundOut.DSDEVID_DefaultPlayback);
        }

        /// <summary>
        /// Initializes the specified provider.
        /// </summary>
        /// <param name="provider">The provider.</param>
        /// <param name="sampleRate">The sample rate.</param>
        /// <param name="channels">The channels.</param>
        /// <param name="bitsPerSample">The bits per sample.</param>
        /// <returns></returns>
        /// <exception cref="System.InvalidOperationException">Wave device already started</exception>
        public bool Initialize(IAudioDataProvider provider, int sampleRate, int channels, int bitsPerSample)
        {
            lock (SyncLock)
            {
                if (this.HasInitialized)
                    throw new InvalidOperationException("Wave device already initialized");

                var format = new WaveFormat(sampleRate, bitsPerSample, channels);
                var callbackWaveProvider = new CallbackWaveProvider16(format, provider.RenderAudioBuffer, SyncLock);
                var volumeWaveProvider = new VolumeWaveProvider16(callbackWaveProvider);
                m_WaveProvider = volumeWaveProvider;
                m_Device.Init(m_WaveProvider);
                m_Device.Play();

                return true;
            }
        }

        /// <summary>
        /// Stops the underlying audio device
        /// </summary>
        /// <exception cref="System.InvalidOperationException">Wave device not started</exception>
        public void Stop()
        {
            lock (SyncLock)
            {
                if (this.HasInitialized == false)
                    throw new InvalidOperationException("Wave device not started");

                m_Device.Stop();
                // System.Diagnostics.Debug.WriteLine("STOP called on audio renderer.");
            }
        }

        /// <summary>
        /// Plays the underlying audio device
        /// </summary>
        /// <exception cref="System.InvalidOperationException">Wave device not started</exception>
        public void Play()
        {
            lock (SyncLock)
            {
                if (this.HasInitialized == false)
                    throw new InvalidOperationException("Wave device not started");

                m_Device.Play();
                System.Diagnostics.Debug.WriteLine("PLAY called on audio renderer.");
            }
        }

        /// <summary>
        /// Pauses the underlying audio device
        /// </summary>
        /// <exception cref="System.InvalidOperationException">Wave device not started</exception>
        public void Pause()
        {
            lock (SyncLock)
            {
                if (this.HasInitialized == false)
                    throw new InvalidOperationException("Wave device not started");

                m_Device.Pause();
                System.Diagnostics.Debug.WriteLine("PAUSE called on audio renderer.");
            }
        }

        /// <summary>
        /// Gets a value indicating whether this instance has initialized.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance has initialized; otherwise, <c>false</c>.
        /// </value>
        public bool HasInitialized
        {
            get { return m_WaveProvider != null; }
        }

        /// <summary>
        /// Gets or sets the volume (amplitude) of the data samples.
        /// Valid range is from 0.0 to 1.0
        /// </summary>
        public decimal Volume
        {
            get
            {
                lock (SyncLock)
                {
                    return Convert.ToDecimal(m_WaveProvider.Volume);
                }

            }
            set
            {
                lock (SyncLock)
                {
                    m_WaveProvider.Volume = Convert.ToSingle(value);
                }
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (m_Device == null) return;

            m_Device.Stop();
            m_Device.Dispose();
            m_Device = null;

            if (m_WaveProvider == null) return;
            m_WaveProvider = null;

        }
    }
}
