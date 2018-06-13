namespace Unosquare.FFME.Rendering.Wave
{
    using Shared;
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Threading;

    /// <summary>
    /// A wave player that opens an audio device and continuously feeds it
    /// with audio samples using a wave provider.
    /// </summary>
    internal sealed class LegacyWavePlayer : IWavePlayer
    {
        #region State Variables

        private static readonly object DevicesEnumLock = new object();
        private readonly object WaveOutLock = new object();
        private IntPtr DeviceHandle;
        private WaveOutBuffer[] Buffers;
        private IWaveProvider WaveStream;
        private AutoResetEvent CallbackEvent;
        private Thread AudioPlaybackTask = null;

        private volatile PlaybackState m_PlaybackState;
        private int m_DeviceNumber = -1;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="LegacyWavePlayer" /> class.
        /// </summary>
        /// <param name="renderer">The renderer.</param>
        /// <param name="deviceNumber">The device number.</param>
        public LegacyWavePlayer(AudioRenderer renderer, int deviceNumber)
        {
            // Initialize the default values
            Renderer = renderer;
            DeviceNumber = deviceNumber;
            DesiredLatency = 200;
            NumberOfBuffers = 2;
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="LegacyWavePlayer"/> class.
        /// </summary>
        ~LegacyWavePlayer()
        {
            Dispose(false);
            Renderer?.MediaCore?.Log(MediaLogMessageType.Error,
                $"{nameof(LegacyWavePlayer)}.{nameof(Dispose)} was not called. Please ensure you dispose when finished using this object.");
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the renderer that owns this wave player.
        /// </summary>
        public AudioRenderer Renderer { get; }

        /// <summary>
        /// Gets or sets the desired latency in milliseconds
        /// Should be set before a call to Init
        /// </summary>
        public int DesiredLatency { get; set; }

        /// <summary>
        /// Gets or sets the number of buffers used
        /// Should be set before a call to Init
        /// </summary>
        public int NumberOfBuffers { get; set; }

        /// <summary>
        /// Gets or sets the device number
        /// Should be set before a call to Init
        /// This must be between -1 and <see>DeviceCount</see> - 1.
        /// -1 means stick to default device even default device is changed
        /// </summary>
        public int DeviceNumber
        {
            get
            {
                return m_DeviceNumber;
            }
            set
            {
                m_DeviceNumber = value;
                lock (WaveOutLock)
                {
                    WaveInterop.NativeMethods.waveOutGetDevCaps((IntPtr)m_DeviceNumber, out LegacyWaveDeviceInfo caps, Marshal.SizeOf(typeof(LegacyWaveDeviceInfo)));
                    Capabilities = caps;
                }
            }
        }

        /// <summary>
        /// Gets a <see cref="WaveFormat"/> instance indicating the format the hardware is using.
        /// </summary>
        public WaveFormat OutputWaveFormat => WaveStream.WaveFormat;

        /// <summary>
        /// Playback State
        /// </summary>
        public PlaybackState PlaybackState => m_PlaybackState;

        /// <summary>
        /// Gets the capabilities.
        /// </summary>
        public LegacyWaveDeviceInfo Capabilities { get; private set; }

        #endregion

        #region Public API

        /// <summary>
        /// Gets the Windows Multimedia Extensions (MME) devices in the system
        /// </summary>
        /// <returns>The available MME devices</returns>
        public static IEnumerable<LegacyWaveDeviceInfo> EnumerateDevices()
        {
            lock (DevicesEnumLock)
            {
                var devices = new List<LegacyWaveDeviceInfo>(32);
                var count = WaveInterop.NativeMethods.waveOutGetNumDevs();
                for (var i = 0; i < count; i++)
                {
                    WaveInterop.NativeMethods.waveOutGetDevCaps((IntPtr)i, out var device, Marshal.SizeOf(typeof(LegacyWaveDeviceInfo)));
                    devices.Add(device);
                }

                return devices;
            }
        }

        /// <summary>
        /// Initializes the specified wave provider.
        /// </summary>
        /// <param name="waveProvider">The wave provider.</param>
        /// <exception cref="InvalidOperationException">Can't re-initialize during playback</exception>
        public void Init(IWaveProvider waveProvider)
        {
            if (m_PlaybackState != PlaybackState.Stopped)
                throw new InvalidOperationException("Can't re-initialize during playback");

            if (DeviceHandle != IntPtr.Zero)
            {
                // normally we don't allow calling Init twice, but as experiment, see if we can clean up and go again
                // try to allow reuse of this waveOut device
                // n.b. risky if Playback thread has not exited
                DisposeBuffers();
                CloseWaveOut();
            }

            CallbackEvent = new AutoResetEvent(false);

            WaveStream = waveProvider;
            var bufferSize = waveProvider.WaveFormat.ConvertLatencyToByteSize((DesiredLatency + NumberOfBuffers - 1) / NumberOfBuffers);

            MmResult result;
            lock (WaveOutLock)
            {
                result = WaveInterop.NativeMethods.waveOutOpenWindow(
                    out DeviceHandle,
                    DeviceNumber,
                    WaveStream.WaveFormat,
                    CallbackEvent.SafeWaitHandle.DangerousGetHandle(),
                    IntPtr.Zero,
                    WaveInterop.WaveInOutOpenFlags.CallbackEvent);
            }

            MmException.Try(result, nameof(WaveInterop.NativeMethods.waveOutOpen));

            Buffers = new WaveOutBuffer[NumberOfBuffers];
            m_PlaybackState = PlaybackState.Stopped;
            for (var n = 0; n < NumberOfBuffers; n++)
            {
                Buffers[n] = new WaveOutBuffer(DeviceHandle, bufferSize, WaveStream, WaveOutLock);
            }
        }

        /// <summary>
        /// Start playing the audio from the WaveStream
        /// </summary>
        public void Play()
        {
            if (Buffers == null || WaveStream == null)
                throw new InvalidOperationException($"Must call {nameof(Init)} first");

            if (m_PlaybackState == PlaybackState.Stopped)
            {
                m_PlaybackState = PlaybackState.Playing;
                CallbackEvent.Set(); // give the thread a kick
                AudioPlaybackTask = new Thread(StartPlaybackThread)
                {
                    IsBackground = true,
                    Name = nameof(AudioPlaybackTask),
                    Priority = ThreadPriority.AboveNormal
                };
                AudioPlaybackTask.Start();
            }
            else if (m_PlaybackState == PlaybackState.Paused)
            {
                Resume();
                CallbackEvent.Set(); // give the thread a kick
            }
        }

        /// <summary>
        /// Pause the audio
        /// </summary>
        public void Pause()
        {
            if (m_PlaybackState != PlaybackState.Playing) return;

            MmResult result;
            m_PlaybackState = PlaybackState.Paused; // set this here to avoid a deadlock problem with some drivers
            lock (WaveOutLock)
                result = WaveInterop.NativeMethods.waveOutPause(DeviceHandle);

            if (result != MmResult.NoError)
                throw new MmException(result, nameof(WaveInterop.NativeMethods.waveOutPause));
        }

        /// <summary>
        /// Stop and reset the WaveOut device
        /// </summary>
        public void Stop()
        {
            if (m_PlaybackState != PlaybackState.Stopped) return;

            // in the call to waveOutReset with function callbacks
            // some drivers will block here until OnDone is called
            // for every buffer
            m_PlaybackState = PlaybackState.Stopped; // set this here to avoid a problem with some drivers whereby
            CallbackEvent?.WaitOne(DesiredLatency);

            MmResult result = default;
            lock (WaveOutLock)
                result = WaveInterop.NativeMethods.waveOutReset(DeviceHandle);

            if (result != MmResult.NoError)
                throw new MmException(result, nameof(WaveInterop.NativeMethods.waveOutReset));

            CallbackEvent.Set(); // give the thread a kick, make sure we exit
        }

        /// <summary>
        /// Gets the current position in bytes from the wave output device.
        /// (n.b. this is not the same thing as the position within your reader
        /// stream - it calls directly into waveOutGetPosition)
        /// </summary>
        /// <returns>Position in bytes</returns>
        public long GetPosition()
        {
            lock (WaveOutLock)
            {
                var time = new MmTime()
                {
                    Type = MmTime.TIME_BYTES
                };

                MmException.Try(WaveInterop.NativeMethods.waveOutGetPosition(DeviceHandle, out time, Marshal.SizeOf(time)), nameof(WaveInterop.NativeMethods.waveOutGetPosition));

                if (time.Type != MmTime.TIME_BYTES)
                    throw new Exception(string.Format($"{nameof(WaveInterop.NativeMethods.waveOutGetPosition)}: wType -> Expected {0}, Received {1}", MmTime.TIME_BYTES, time.Type));

                return time.CB;
            }
        }

        /// <summary>
        /// Closes this WaveOut device
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Dispose(true);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Closes the WaveOut device and disposes of buffers
        /// </summary>
        /// <param name="alsoManaged">True if called from <see>Dispose</see></param>
        private void Dispose(bool alsoManaged)
        {
            try
            {
                Stop();
            }
            catch (Exception e)
            {
                // Dispose() and Finalize() methods should not throw exception
                // WaveInterop.NativeMethods.waveOutReset(DeviceHandle) throws MmException if DeviceHandle is invalid
                Renderer?.MediaCore?.Log(MediaLogMessageType.Error, $"{nameof(LegacyWavePlayer)} disposing. {e.Message}. Stack Trace:\r\n{e.StackTrace}");
            }

            if (alsoManaged)
                DisposeBuffers();

            CloseWaveOut();
        }

        /// <summary>
        /// Resume playing after a pause from the same position
        /// </summary>
        private void Resume()
        {
            if (m_PlaybackState != PlaybackState.Paused) return;

            MmResult result;
            lock (WaveOutLock)
                result = WaveInterop.NativeMethods.waveOutRestart(DeviceHandle);

            if (result != MmResult.NoError)
                throw new MmException(result, nameof(WaveInterop.NativeMethods.waveOutRestart));

            m_PlaybackState = PlaybackState.Playing;
        }

        /// <summary>
        /// Starts the playback thread.
        /// </summary>
        private void StartPlaybackThread()
        {
            try
            {
                PerformContinuousPlayback();
            }
            catch (MmException mex) when (mex.Result == MmResult.WaveHeaderUnprepared)
            {
                Renderer?.MediaCore?.Log(MediaLogMessageType.Debug, $"{nameof(AudioPlaybackTask)} forced exit requested. {mex.Message}.");
                return;
            }
            catch (Exception e)
            {
                Renderer?.MediaCore?.Log(MediaLogMessageType.Error, $"{nameof(AudioPlaybackTask)} exiting. {e.Message}. Stack Trace:\r\n{e.StackTrace}");
            }
            finally
            {
                try { CallbackEvent?.Set(); } catch { }
                m_PlaybackState = PlaybackState.Stopped;
            }
        }

        /// <summary>
        /// Performs the continuous playback.
        /// </summary>
        private void PerformContinuousPlayback()
        {
            var queued = 0;
            while (m_PlaybackState != PlaybackState.Stopped)
            {
                if (!(CallbackEvent?.WaitOne(DesiredLatency) ?? false) && m_PlaybackState == PlaybackState.Playing)
                    Renderer?.MediaCore?.Log(MediaLogMessageType.Warning, $"{nameof(AudioPlaybackTask)}:{nameof(CallbackEvent)} timed out. Desired Latency: {DesiredLatency}ms");

                if (m_PlaybackState != PlaybackState.Playing)
                    continue;

                queued = 0; // requeue any buffers returned to us
                if (Buffers != null)
                {
                    foreach (var buffer in Buffers)
                    {
                        if (buffer.InQueue || buffer.OnDone())
                            queued++;
                    }
                }

                if (queued == 0)
                {
                    // we got to the end
                    m_PlaybackState = PlaybackState.Stopped;
                    CallbackEvent?.Set();
                }
            }
        }

        /// <summary>
        /// Closes the wave device.
        /// </summary>
        private void CloseWaveOut()
        {
            if (CallbackEvent != null)
            {
                m_PlaybackState = PlaybackState.Stopped;
                CallbackEvent.Set();
                CallbackEvent.Close();
                CallbackEvent = null;
            }

            lock (WaveOutLock)
            {
                if (DeviceHandle != IntPtr.Zero)
                {
                    WaveInterop.NativeMethods.waveOutClose(DeviceHandle);
                    DeviceHandle = IntPtr.Zero;
                }
            }
        }

        /// <summary>
        /// Disposes the buffers.
        /// </summary>
        private void DisposeBuffers()
        {
            if (Buffers != null)
            {
                foreach (var buffer in Buffers)
                    buffer.Dispose();

                Buffers = null;
            }
        }

        #endregion
    }
}