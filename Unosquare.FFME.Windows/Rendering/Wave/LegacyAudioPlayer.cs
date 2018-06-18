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
    internal sealed class LegacyAudioPlayer : IWavePlayer
    {
        #region State Variables

        private static readonly object DevicesEnumLock = new object();

        private readonly AtomicBoolean IsCancellationPending = new AtomicBoolean(false);
        private readonly IWaitEvent PlaybackFinished = WaitEventFactory.Create(isCompleted: true, useSlim: true);
        private readonly AutoResetEvent DriverCallbackEvent = new AutoResetEvent(false);
        private volatile bool IsDisposed = false;

        private IntPtr DeviceHandle;
        private WaveOutBuffer[] Buffers;
        private Thread AudioPlaybackThread = null;

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
            if (deviceNumber < -1) deviceNumber = -1;

            Renderer = renderer;
            DeviceNumber = deviceNumber;
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

        #endregion

        #region Public API

        /// <summary>
        /// Gets the Windows Multimedia Extensions (MME) devices in the system
        /// </summary>
        /// <returns>The available MME devices</returns>
        public static List<LegacyAudioDeviceInfo> EnumerateDevices()
        {
            lock (DevicesEnumLock)
            {
                var devices = new List<LegacyAudioDeviceInfo>(32);
                var count = WaveInterop.RetrieveAudioDeviceCount();
                for (var i = 0; i < count; i++)
                {
                    devices.Add(WaveInterop.RetrieveAudioDeviceInfo(i));
                }

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
            var bufferSize = Renderer.WaveFormat.ConvertLatencyToByteSize((DesiredLatency + NumberOfBuffers - 1) / NumberOfBuffers);

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

        public void Dispose()
        {
            Dispose(true);
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
                try { WaveInterop.PauseAudioDevice(DeviceHandle); } catch { }
                try { WaveInterop.ResetAudioDevice(DeviceHandle); } catch { }

                // Dispose of buffers
                foreach (var buffer in Buffers)
                    try { buffer.Dispose(); } catch { }

                // Close the device
                try { WaveInterop.CloseAudioDevice(DeviceHandle); } catch { }

                // Dispose of managed state
                DeviceHandle = IntPtr.Zero;
                PlaybackFinished.Complete();
            }
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="alsoManaged"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        private void Dispose(bool alsoManaged)
        {
            if (IsDisposed) return;

            if (alsoManaged)
            {
                IsCancellationPending.Value = true; // Causes the playback loop to exit
                DriverCallbackEvent.Set(); // causes the WaitOne to exit
                PlaybackFinished.Wait(); // waits for the playback loop to finish
                DriverCallbackEvent.Dispose();
                PlaybackFinished.Dispose();
            }

            IsDisposed = true;
        }

        #endregion
    }
}