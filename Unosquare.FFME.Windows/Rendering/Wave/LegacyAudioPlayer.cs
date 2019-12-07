namespace Unosquare.FFME.Rendering.Wave
{
    using Diagnostics;
    using Primitives;
    using System;
    using System.Collections.Generic;
    using System.Threading;

    /// <summary>
    /// A wave player that opens an audio device and continuously feeds it
    /// with audio samples using a wave provider.
    /// </summary>
    internal sealed class LegacyAudioPlayer : IntervalWorkerBase, IWavePlayer, ILoggingSource
    {
        #region State Variables

        private static readonly object DevicesEnumLock = new object();
        private readonly AutoResetEvent DriverCallbackEvent = new AutoResetEvent(false);

        private IntPtr DeviceHandle;
        private WaveOutBuffer[] Buffers;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="LegacyAudioPlayer" /> class.
        /// </summary>
        /// <param name="renderer">The renderer.</param>
        /// <param name="deviceNumber">The device number.</param>
        public LegacyAudioPlayer(AudioRenderer renderer, int deviceNumber)
            : base(nameof(LegacyAudioPlayer), Constants.DefaultTimingPeriod, IntervalWorkerMode.SystemDefault)
        {
            // Initialize the default values
            var deviceId = deviceNumber < -1 ? -1 : deviceNumber;

            Renderer = renderer;
            DeviceNumber = deviceId;
            DesiredLatency = 200;
            NumberOfBuffers = 2;
            Capabilities = WaveInterop.RetrieveAudioDeviceInfo(DeviceNumber);
        }

        #endregion

        #region Properties

        /// <inheritdoc />
        ILoggingHandler ILoggingSource.LoggingHandler => Renderer?.MediaCore;

        /// <inheritdoc />
        public AudioRenderer Renderer { get; }

        /// <inheritdoc />
        public int DesiredLatency { get; }

        /// <summary>
        /// Gets or sets the number of buffers used
        /// Should be set before a call to Init.
        /// </summary>
        public int NumberOfBuffers { get; }

        /// <summary>
        /// Gets the device number
        /// Should be set before a call to Init
        /// This must be between -1 and <see>DeviceCount</see> - 1.
        /// -1 means stick to default device even default device is changed.
        /// </summary>
        public int DeviceNumber { get; }

        /// <inheritdoc />
        public PlaybackState PlaybackState { get; private set; } = PlaybackState.Stopped;

        /// <inheritdoc />
        public bool IsRunning => WorkerState == WorkerState.Running;

        /// <summary>
        /// Gets the capabilities.
        /// </summary>
        public LegacyAudioDeviceData Capabilities { get; }

        #endregion

        #region Public API

        /// <summary>
        /// Gets the Windows Multimedia Extensions (MME) devices in the system.
        /// </summary>
        /// <returns>The available MME devices.</returns>
        public static List<LegacyAudioDeviceData> EnumerateDevices()
        {
            lock (DevicesEnumLock)
            {
                var devices = new List<LegacyAudioDeviceData>(32);
                var count = WaveInterop.RetrieveAudioDeviceCount();
                for (var i = -1; i < count; i++)
                    devices.Add(WaveInterop.RetrieveAudioDeviceInfo(i));

                return devices;
            }
        }

        /// <inheritdoc />
        public void Start()
        {
            if (DeviceHandle != IntPtr.Zero || IsDisposed)
                throw new InvalidOperationException($"{nameof(LegacyAudioPlayer)} was already started");

            var bufferSize = Renderer.WaveFormat.ConvertMillisToByteSize((DesiredLatency + NumberOfBuffers - 1) / NumberOfBuffers);

            // Acquire a device handle
            DeviceHandle = WaveInterop.OpenAudioDevice(
                DeviceNumber,
                Renderer.WaveFormat,
                DriverCallbackEvent.SafeWaitHandle,
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
            PlaybackState = PlaybackState.Playing;
            StartAsync();
        }

        /// <inheritdoc />
        public void Clear()
        {
            if (IsDisposed) return;
            foreach (var buffer in Buffers)
                buffer.Clear();
        }

        #endregion

        #region Worker Methods

        /// <inheritdoc />
        protected override void ExecuteCycleLogic(CancellationToken ct)
        {
            if (DriverCallbackEvent.WaitOne(DesiredLatency) == false)
            {
                this.LogWarning(Aspects.AudioRenderer,
                    $"{nameof(LegacyAudioPlayer)}:{nameof(DriverCallbackEvent)} timed out. Desired Latency: {DesiredLatency}ms");

                return;
            }

            foreach (var buffer in Buffers)
            {
                if (!buffer.IsQueued)
                    buffer.ReadWaveStream();
            }
        }

        /// <inheritdoc />
        protected override void OnCycleException(Exception ex)
        {
            this.LogError(Aspects.AudioRenderer, $"{nameof(LegacyAudioPlayer)} faulted.", ex);
        }

        /// <inheritdoc />
        protected override void OnDisposing()
        {
            DriverCallbackEvent.Set();

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
        }

        /// <inheritdoc />
        protected override void Dispose(bool alsoManaged)
        {
            base.Dispose(alsoManaged);
            DriverCallbackEvent.Dispose();
        }

        #endregion
    }
}