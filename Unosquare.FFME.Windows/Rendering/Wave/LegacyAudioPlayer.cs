namespace Unosquare.FFME.Rendering.Wave
{
    using Primitives;
    using Shared;
    using System;
    using System.Collections.Generic;
    using System.Threading;

    /// <summary>
    /// A wave player that opens an audio device and continuously feeds it
    /// with audio samples using a wave provider.
    /// </summary>
    internal sealed class LegacyAudioPlayer : IWavePlayer, IDisposable
    {
        #region State Variables

        private static readonly object DevicesEnumLock = new object();

        private readonly AtomicBoolean IsCancellationPending = new AtomicBoolean(false);
        private readonly IWaitEvent PlaybackFinished = WaitEventFactory.Create(isCompleted: true, useSlim: true);
        private readonly AutoResetEvent DriverCallbackEvent = new AutoResetEvent(false);
        private readonly AtomicBoolean m_IsDisposed = new AtomicBoolean(false);

        private IntPtr DeviceHandle;
        private WaveOutBuffer[] Buffers;
        private Thread AudioPlaybackThread;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="LegacyAudioPlayer" /> class.
        /// </summary>
        /// <param name="renderer">The renderer.</param>
        /// <param name="deviceNumber">The device number.</param>
        public LegacyAudioPlayer(AudioRenderer renderer, int deviceNumber)
        {
            // Initialize the default values
            var deviceId = deviceNumber;
            if (deviceId < -1) deviceId = -1;

            Renderer = renderer;
            DeviceNumber = deviceId;
            DesiredLatency = 200;
            NumberOfBuffers = 2;
            Capabilities = WaveInterop.RetrieveAudioDeviceInfo(DeviceNumber);
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
        public int DesiredLatency { get; }

        /// <summary>
        /// Gets or sets the number of buffers used
        /// Should be set before a call to Init
        /// </summary>
        public int NumberOfBuffers { get; }

        /// <summary>
        /// Gets the device number
        /// Should be set before a call to Init
        /// This must be between -1 and <see>DeviceCount</see> - 1.
        /// -1 means stick to default device even default device is changed
        /// </summary>
        public int DeviceNumber { get; private set; }

        /// <summary>
        /// Playback State
        /// </summary>
        public PlaybackState PlaybackState { get; private set; } = PlaybackState.Stopped;

        /// <summary>
        /// Gets a value indicating whether the audio playback is running.
        /// </summary>
        public bool IsRunning => (IsDisposed || IsCancellationPending.Value || PlaybackFinished.IsCompleted) ? false : true;

        /// <summary>
        /// Gets the capabilities.
        /// </summary>
        public LegacyAudioDeviceInfo Capabilities { get; private set; }

        /// <summary>
        /// Gets a value indicating whether this instance is disposed.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is disposed; otherwise, <c>false</c>.
        /// </value>
        public bool IsDisposed
        {
            get => m_IsDisposed.Value;
            private set => m_IsDisposed.Value = value;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Gets the Windows Multimedia Extensions (MME) devices in the system.
        /// </summary>
        /// <returns>The available MME devices</returns>
        public static List<LegacyAudioDeviceInfo> EnumerateDevices()
        {
            lock (DevicesEnumLock)
            {
                var devices = new List<LegacyAudioDeviceInfo>(32);
                var count = WaveInterop.RetrieveAudioDeviceCount();
                for (var i = -1; i < count; i++)
                    devices.Add(WaveInterop.RetrieveAudioDeviceInfo(i));

                return devices;
            }
        }

        /// <summary>
        /// Begin playback
        /// </summary>
        public void Start()
        {
            if (DeviceHandle != IntPtr.Zero || IsDisposed)
                throw new InvalidOperationException($"{nameof(AudioPlaybackThread)} was already started");

            PlaybackFinished.Begin();
            var bufferSize = Renderer.WaveFormat.ConvertMillisToByteSize((DesiredLatency + NumberOfBuffers - 1) / NumberOfBuffers);

            // Acquire a device handle
            DeviceHandle = WaveInterop.OpenAudioDevice(
                DeviceNumber,
                Renderer.WaveFormat,
                DriverCallbackEvent.SafeWaitHandle.DangerousGetHandle(),
                IntPtr.Zero,
                WaveInterop.WaveInOutOpenFlags.CallbackEvent);

            // Create the buffers
            Buffers = new WaveOutBuffer[NumberOfBuffers];
            for (var n = 0; n < NumberOfBuffers; n++)
            {
                Buffers[n] = new WaveOutBuffer(DeviceHandle, bufferSize, Renderer);
            }

            // Start the playback thread
            DriverCallbackEvent.Set(); // give the thread an initial kick
            AudioPlaybackThread = new Thread(PerformContinuousPlayback)
            {
                IsBackground = true,
                Name = nameof(AudioPlaybackThread),
                Priority = ThreadPriority.AboveNormal
            };

            // Begin the thread
            AudioPlaybackThread.Start();
        }

        /// <summary>
        /// Clears the internal audio data with silence data.
        /// </summary>
        public void Clear()
        {
            if (IsDisposed) return;
            foreach (var buffer in Buffers)
                buffer.Clear();
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (IsDisposed) return;

            IsCancellationPending.Value = true; // Causes the playback loop to exit
            DriverCallbackEvent.Set(); // causes the WaitOne to exit
            PlaybackFinished.Wait(); // waits for the playback loop to finish
            DriverCallbackEvent.Dispose();
            PlaybackFinished.Dispose();
            IsDisposed = true;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Performs the continuous playback.
        /// </summary>
        private void PerformContinuousPlayback()
        {
            var queued = 0;
            PlaybackState = PlaybackState.Playing;

            try
            {
                while (IsCancellationPending == false)
                {
                    if (DriverCallbackEvent.WaitOne(DesiredLatency) == false)
                    {
                        if (IsCancellationPending == true) break;

                        Renderer?.MediaCore?.Log(MediaLogMessageType.Warning,
                            $"{nameof(AudioPlaybackThread)}:{nameof(DriverCallbackEvent)} timed out. Desired Latency: {DesiredLatency}ms");
                        continue;
                    }

                    // Reset the queue count
                    queued = 0;
                    if (IsCancellationPending == true)
                        break;

                    foreach (var buffer in Buffers)
                    {
                        if (buffer.IsQueued || buffer.ReadWaveStream())
                            queued++;
                    }

                    // Detect an end of playback
                    if (queued <= 0)
                        break;
                }
            }
            catch (Exception ex)
            {
                Renderer?.MediaCore?.Log(MediaLogMessageType.Error,
                    $"{nameof(LegacyAudioPlayer)} faulted. {ex.GetType().Name}: {ex.Message}");
                throw;
            }
            finally
            {
                // Update the state
                PlaybackState = PlaybackState.Stopped;

                // Immediately stop the audio driver. Pause it first to
                // avoid quirky repetitive samples
                try { WaveInterop.PauseAudioDevice(DeviceHandle); } catch { /* Ignore */ }
                try { WaveInterop.ResetAudioDevice(DeviceHandle); } catch { /* Ignore */ }

                // Dispose of buffers
                foreach (var buffer in Buffers)
                    try { buffer.Dispose(); } catch { /* Ignore */ }

                // Close the device
                try { WaveInterop.CloseAudioDevice(DeviceHandle); } catch { /* Ignore */ }

                // Dispose of managed state
                DeviceHandle = IntPtr.Zero;
                PlaybackFinished.Complete();
            }
        }

        #endregion
    }
}