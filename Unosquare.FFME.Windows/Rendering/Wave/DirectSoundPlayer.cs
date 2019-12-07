namespace Unosquare.FFME.Rendering.Wave
{
    using Diagnostics;
    using Primitives;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using System.Security;
    using System.Threading;

    /// <summary>
    /// NativeDirectSoundOut using DirectSound COM interop.
    /// Contact author: Alexandre Mutel - alexandre_mutel at yahoo.fr
    /// Modified by: Graham "Gee" Plumb.
    /// </summary>
    internal sealed class DirectSoundPlayer : IntervalWorkerBase, IWavePlayer, ILoggingSource
    {
        #region Fields

        /// <summary>
        /// DirectSound default playback device GUID.
        /// </summary>
        public static readonly Guid DefaultPlaybackDeviceId = new Guid("DEF00000-9C6D-47ED-AAF1-4DDA8F2B5C03");

        // Device enumerations
        private static readonly object DevicesEnumLock = new object();
        private static List<DirectSoundDeviceData> EnumeratedDevices;

        // Instance fields
        private readonly EventWaitHandle CancelEvent = new EventWaitHandle(false, EventResetMode.ManualReset);

        private readonly WaveFormat WaveFormat;
        private int SamplesTotalSize;
        private int SamplesFrameSize;
        private int NextSamplesWriteIndex;
        private Guid DeviceId;
        private byte[] Samples;
        private DirectSound.IDirectSound DirectSoundDriver;
        private DirectSound.IDirectSoundBuffer AudioRenderBuffer;
        private DirectSound.IDirectSoundBuffer AudioBackBuffer;
        private EventWaitHandle FrameStartEventWaitHandle;
        private EventWaitHandle FrameEndEventWaitHandle;
        private EventWaitHandle PlaybackEndedEventWaitHandle;
        private WaitHandle[] PlaybackWaitHandles;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="DirectSoundPlayer" /> class.
        /// (40ms seems to work under Vista).
        /// </summary>
        /// <param name="renderer">The renderer.</param>
        /// <param name="deviceId">Selected device.</param>
        public DirectSoundPlayer(AudioRenderer renderer, Guid deviceId)
            : base(nameof(DirectSoundPlayer), Constants.DefaultTimingPeriod, IntervalWorkerMode.SystemDefault)
        {
            Renderer = renderer;
            DeviceId = deviceId == Guid.Empty ? DefaultPlaybackDeviceId : deviceId;
            WaveFormat = renderer.WaveFormat;
        }

        #endregion

        #region Properties

        /// <inheritdoc />
        ILoggingHandler ILoggingSource.LoggingHandler => Renderer?.MediaCore;

        /// <inheritdoc />
        public AudioRenderer Renderer { get; }

        /// <inheritdoc />
        public PlaybackState PlaybackState { get; private set; } = PlaybackState.Stopped;

        /// <inheritdoc />
        public bool IsRunning => WorkerState == WorkerState.Running;

        /// <inheritdoc />
        public int DesiredLatency { get; private set; } = 50;

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets the DirectSound output devices in the system.
        /// </summary>
        /// <returns>The available DirectSound devices.</returns>
        public static List<DirectSoundDeviceData> EnumerateDevices()
        {
            lock (DevicesEnumLock)
            {
                EnumeratedDevices = new List<DirectSoundDeviceData>(32);
                NativeMethods.DirectSoundEnumerateA(EnumerateDevicesCallback, IntPtr.Zero);
                return EnumeratedDevices;
            }
        }

        /// <inheritdoc />
        public void Start()
        {
            if (DirectSoundDriver != null || IsDisposed)
                throw new InvalidOperationException($"{nameof(DirectSoundPlayer)} was already started");

            InitializeDirectSound();
            AudioBackBuffer.SetCurrentPosition(0);
            NextSamplesWriteIndex = 0;

            // Give the buffer initial samples to work with
            if (FeedBackBuffer(SamplesTotalSize) <= 0)
                throw new InvalidOperationException($"Method {nameof(FeedBackBuffer)} could not write samples.");

            // Set the state to playing
            PlaybackState = PlaybackState.Playing;

            // Begin notifications on playback wait events
            AudioBackBuffer.Play(0, 0, DirectSound.DirectSoundPlayFlags.Looping);

            StartAsync();
        }

        /// <inheritdoc />
        public void Clear() => ClearBackBuffer();

        #endregion

        #region Worker Methods

        /// <inheritdoc />
        protected override void ExecuteCycleLogic(CancellationToken ct)
        {
            const int FrameStartHandle = 0;
            const int PlaybackEndHandle = 2;
            const int CancelHandle = 3;
            const int TimeoutHandle = WaitHandle.WaitTimeout;

            // Wait for signals on frameEventWaitHandle1 (Position 0), frameEventWaitHandle2 (Position 1/2)
            var handleIndex = WaitHandle.WaitAny(PlaybackWaitHandles, DesiredLatency * 3, false);

            // Not ready yet
            if (handleIndex == TimeoutHandle)
                return;

            // Handle cancel events
            if (handleIndex == CancelHandle || handleIndex == PlaybackEndHandle)
            {
                WantedWorkerState = WorkerState.Stopped;
                return;
            }

            NextSamplesWriteIndex = handleIndex == FrameStartHandle ? SamplesFrameSize : default;

            // Only carry on playing if we can read more samples
            if (FeedBackBuffer(SamplesFrameSize) <= 0)
                throw new InvalidOperationException($"Method {nameof(FeedBackBuffer)} could not write samples.");
        }

        /// <inheritdoc />
        protected override void OnCycleException(Exception ex)
        {
            this.LogError(Aspects.AudioRenderer, $"{nameof(DirectSoundPlayer)} faulted.", ex);
        }

        /// <inheritdoc />
        protected override void OnDisposing()
        {
            // Signal Completion
            PlaybackState = PlaybackState.Stopped;
            CancelEvent.Set(); // causes the WaitAny to exit

            try { AudioRenderBuffer.Stop(); } catch { /* Ignore exception and continue */ }

            try { ClearBackBuffer(); } catch { /* Ignore exception and continue */ }
            try { AudioBackBuffer.Stop(); } catch { /* Ignore exception and continue */ }
        }

        /// <inheritdoc />
        protected override void Dispose(bool alsoManaged)
        {
            base.Dispose(alsoManaged);

            if (alsoManaged)
            {
                // Dispose DirectSound buffer wait handles
                PlaybackEndedEventWaitHandle?.Dispose();
                FrameStartEventWaitHandle?.Dispose();
                FrameEndEventWaitHandle?.Dispose();
                CancelEvent.Dispose();
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Enumerates the devices.
        /// </summary>
        /// <param name="deviceGuidPtr">The device unique identifier pointer.</param>
        /// <param name="descriptionPtr">The description string pointer.</param>
        /// <param name="modulePtr">The module string pointer.</param>
        /// <param name="contextPtr">The context pointer.</param>
        /// <returns>The devices.</returns>
        private static bool EnumerateDevicesCallback(IntPtr deviceGuidPtr, IntPtr descriptionPtr, IntPtr modulePtr, IntPtr contextPtr)
        {
            var device = new DirectSoundDeviceData();
            if (deviceGuidPtr == IntPtr.Zero)
            {
                device.Guid = Guid.Empty;
            }
            else
            {
                var guidBytes = new byte[16];
                Marshal.Copy(deviceGuidPtr, guidBytes, 0, 16);
                device.Guid = new Guid(guidBytes);
            }

            device.Description = descriptionPtr != IntPtr.Zero ? Marshal.PtrToStringAnsi(descriptionPtr) : default;
            device.ModuleName = modulePtr != IntPtr.Zero ? Marshal.PtrToStringAnsi(modulePtr) : default;

            EnumeratedDevices.Add(device);
            return true;
        }

        /// <summary>
        /// Creates a DirectSound position notification.
        /// </summary>
        /// <param name="eventHandle">The event handle.</param>
        /// <param name="offset">The offset.</param>
        /// <returns>A DirectSound Position Notification.</returns>
        private static DirectSound.DirectSoundBufferPositionNotify CreatePositionNotification(WaitHandle eventHandle, uint offset) =>
            new DirectSound.DirectSoundBufferPositionNotify
            {
                Offset = offset,
                NotifyHandle = eventHandle.SafeWaitHandle.DangerousGetHandle()
            };

        /// <summary>
        /// Initializes the direct sound.
        /// </summary>
        private void InitializeDirectSound()
        {
            // We will have 2 buffers: one for immediate audio out rendering, and another where we will
            // feed the samples. We will copy audio data from the back buffer into the immediate render
            // buffer. We first open the DirectSound driver, create the buffers and start the playback!
            // Open DirectSound
            DirectSoundDriver = null;
            var createDriverResult = NativeMethods.DirectSoundCreate(ref DeviceId, out DirectSoundDriver, IntPtr.Zero);

            if (DirectSoundDriver == null || createDriverResult != 0)
                return;

            // Set Cooperative Level to PRIORITY (priority level can call the SetFormat and Compact methods)
            DirectSoundDriver.SetCooperativeLevel(NativeMethods.GetDesktopWindow(),
                DirectSound.DirectSoundCooperativeLevel.Priority);

            // Fill BufferDescription for immediate, rendering buffer
            var renderBuffer = new DirectSound.BufferDescription
            {
                Size = Marshal.SizeOf<DirectSound.BufferDescription>(),
                BufferBytes = 0,
                Flags = DirectSound.DirectSoundBufferCaps.PrimaryBuffer,
                Reserved = 0,
                FormatHandle = IntPtr.Zero,
                AlgorithmId = Guid.Empty
            };

            // Create the Render Buffer (Immediate audio out)
            DirectSoundDriver.CreateSoundBuffer(renderBuffer, out var audioRenderBuffer, IntPtr.Zero);
            AudioRenderBuffer = audioRenderBuffer as DirectSound.IDirectSoundBuffer;

            // Play & Loop on the render buffer
            AudioRenderBuffer?.Play(0, 0, DirectSound.DirectSoundPlayFlags.Looping);

            // A frame of samples equals to Desired Latency
            SamplesFrameSize = MillisToBytes(DesiredLatency);
            var waveFormatHandle = GCHandle.Alloc(WaveFormat, GCHandleType.Pinned);

            // Fill BufferDescription for sample-receiving back buffer
            var backBuffer = new DirectSound.BufferDescription
            {
                Size = Marshal.SizeOf<DirectSound.BufferDescription>(),
                BufferBytes = (uint)(SamplesFrameSize * 2),
                Flags = DirectSound.DirectSoundBufferCaps.GetCurrentPosition2
                        | DirectSound.DirectSoundBufferCaps.ControlNotifyPosition
                        | DirectSound.DirectSoundBufferCaps.GlobalFocus
                        | DirectSound.DirectSoundBufferCaps.ControlVolume
                        | DirectSound.DirectSoundBufferCaps.StickyFocus
                        | DirectSound.DirectSoundBufferCaps.GetCurrentPosition2,
                Reserved = 0,
                FormatHandle = waveFormatHandle.AddrOfPinnedObject(),
                AlgorithmId = Guid.Empty
            };

            // Create back buffer where samples will be fed
            DirectSoundDriver.CreateSoundBuffer(backBuffer, out audioRenderBuffer, IntPtr.Zero);
            AudioBackBuffer = audioRenderBuffer as DirectSound.IDirectSoundBuffer;
            waveFormatHandle.Free();

            // Get effective SecondaryBuffer size
            var bufferCapabilities = new DirectSound.BufferCaps { Size = Marshal.SizeOf<DirectSound.BufferCaps>() };
            AudioBackBuffer?.GetCaps(bufferCapabilities);

            NextSamplesWriteIndex = 0;
            SamplesTotalSize = bufferCapabilities.BufferBytes;
            Samples = new byte[SamplesTotalSize];
            Debug.Assert(SamplesTotalSize == (2 * SamplesFrameSize), "Invalid SamplesTotalSize vs SamplesFrameSize");

            // Create double buffering notifications.
            // Use DirectSoundNotify at Position [0, 1/2] and Stop Position (0xFFFFFFFF)
            var notifier = audioRenderBuffer as DirectSound.IDirectSoundNotify;

            FrameStartEventWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset);
            FrameEndEventWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset);
            PlaybackEndedEventWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset);
            PlaybackWaitHandles = new WaitHandle[] { FrameStartEventWaitHandle, FrameEndEventWaitHandle, PlaybackEndedEventWaitHandle, CancelEvent };

            var notificationEvents = new[]
            {
                CreatePositionNotification(FrameStartEventWaitHandle, 0),
                CreatePositionNotification(FrameEndEventWaitHandle, (uint)SamplesFrameSize),
                CreatePositionNotification(PlaybackEndedEventWaitHandle, 0xFFFFFFFF)
            };

            notifier?.SetNotificationPositions((uint)notificationEvents.Length, notificationEvents);
        }

        /// <summary>
        /// Determines whether the SecondaryBuffer is lost.
        /// </summary>
        /// <returns>
        /// <c>true</c> if [is buffer lost]; otherwise, <c>false</c>.
        /// </returns>
        private bool IsBufferLost() =>
            AudioBackBuffer.GetStatus().HasFlag(DirectSound.DirectSoundBufferStatus.BufferLost);

        /// <summary>
        /// Convert ms to bytes size according to WaveFormat.
        /// </summary>
        /// <param name="millis">The milliseconds.</param>
        /// <returns>number of bytes.</returns>
        private int MillisToBytes(int millis)
        {
            var bytes = millis * (WaveFormat.AverageBytesPerSecond / 1000);
            bytes -= bytes % WaveFormat.BlockAlign;
            return bytes;
        }

        /// <summary>
        /// Clean up the SecondaryBuffer.
        /// </summary>
        /// <remarks>
        /// <para>
        /// In DirectSound, when playback is started,
        /// the rest of the sound that was played last time is played back as noise.
        /// This happens even if the secondary buffer is completely silenced,
        /// so it seems that the buffer in the primary buffer or higher is not cleared.
        /// </para>
        /// <para>
        /// To solve this problem fill the secondary buffer with silence data when stop playback.
        /// </para>
        /// </remarks>
        private void ClearBackBuffer()
        {
            if (AudioBackBuffer == null)
                return;

            var silence = new byte[SamplesTotalSize];

            // Lock the SecondaryBuffer
            AudioBackBuffer.Lock(0,
                (uint)SamplesTotalSize,
                out var wavBuffer1,
                out var nbSamples1,
                out var wavBuffer2,
                out var nbSamples2,
                DirectSound.DirectSoundBufferLockFlag.None);

            // Copy silence data to the SecondaryBuffer
            if (wavBuffer1 != IntPtr.Zero)
            {
                Marshal.Copy(silence, 0, wavBuffer1, nbSamples1);
                if (wavBuffer2 != IntPtr.Zero)
                {
                    Marshal.Copy(silence, 0, wavBuffer1, nbSamples1);
                }
            }

            // Unlock the SecondaryBuffer
            AudioBackBuffer.Unlock(wavBuffer1, nbSamples1, wavBuffer2, nbSamples2);
        }

        /// <summary>
        /// Feeds the SecondaryBuffer with the WaveStream.
        /// </summary>
        /// <param name="bytesToCopy">number of bytes to feed.</param>
        /// <returns>The number of bytes that were read.</returns>
        private int FeedBackBuffer(int bytesToCopy)
        {
            // Restore the buffer if lost
            if (IsBufferLost())
                AudioBackBuffer.Restore();

            // Read data from stream (Should this be inserted between the lock / unlock?)
            var bytesRead = Renderer?.Read(Samples, 0, bytesToCopy) ?? 0;

            // Write silence
            if (bytesRead <= 0)
            {
                Array.Clear(Samples, 0, Samples.Length);
                return 0;
            }

            // Lock a portion of the SecondaryBuffer (starting from 0 or 1/2 the buffer)
            AudioBackBuffer.Lock(NextSamplesWriteIndex,
                (uint)bytesRead,  // (uint)bytesToCopy,
                out var wavBuffer1,
                out var nbSamples1,
                out var wavBuffer2,
                out var nbSamples2,
                DirectSound.DirectSoundBufferLockFlag.None);

            // Copy back to the SecondaryBuffer
            if (wavBuffer1 != IntPtr.Zero)
            {
                Marshal.Copy(Samples, 0, wavBuffer1, nbSamples1);
                if (wavBuffer2 != IntPtr.Zero)
                {
                    // TODO: Should this be wav buffer 2 and nbSamples2 ??
                    Marshal.Copy(Samples, 0, wavBuffer1, nbSamples1);
                }
            }

            // Unlock the SecondaryBuffer
            AudioBackBuffer.Unlock(wavBuffer1, nbSamples1, wavBuffer2, nbSamples2);

            return bytesRead;
        }

        #endregion

        #region Native DirectSound COM Interface

        private static class DirectSound
        {
            /// <summary>
            /// DirectSound default capture device GUID.
            /// </summary>
            public static readonly Guid DefaultCaptureDeviceId = new Guid("DEF00001-9C6D-47ED-AAF1-4DDA8F2B5C03");

            /// <summary>
            /// DirectSound default device for voice playback.
            /// </summary>
            public static readonly Guid DefaultVoicePlaybackDeviceId = new Guid("DEF00002-9C6D-47ED-AAF1-4DDA8F2B5C03");

            /// <summary>
            /// DirectSound default device for voice capture.
            /// </summary>
            public static readonly Guid DefaultVoiceCaptureDeviceId = new Guid("DEF00003-9C6D-47ED-AAF1-4DDA8F2B5C03");

            /// <summary>
            /// The DSEnumCallback function is an application-defined callback function that enumerates the DirectSound drivers.
            /// The system calls this function in response to the application's call to the DirectSoundEnumerate or DirectSoundCaptureEnumerate function.
            /// </summary>
            /// <param name="deviceGuidPtr">Address of the GUID that identifies the device being enumerated, or NULL for the primary device. This value can be passed to the DirectSoundCreate8 or DirectSoundCaptureCreate8 function to create a device object for that driver. </param>
            /// <param name="descriptionPtr">Address of a null-terminated string that provides a textual description of the DirectSound device. </param>
            /// <param name="modulePtr">Address of a null-terminated string that specifies the module name of the DirectSound driver corresponding to this device. </param>
            /// <param name="contextPtr">Address of application-defined data. This is the pointer passed to DirectSoundEnumerate or DirectSoundCaptureEnumerate as the lpContext parameter. </param>
            /// <returns>Returns TRUE to continue enumerating drivers, or FALSE to stop.</returns>
            public delegate bool EnumerateDevicesDelegate(IntPtr deviceGuidPtr, IntPtr descriptionPtr, IntPtr modulePtr, IntPtr contextPtr);

            public enum DirectSoundCooperativeLevel : uint
            {
                Normal = 0x00000001,
                Priority = 0x00000002,
                Exclusive = 0x00000003,
                WritePrimary = 0x00000004
            }

            [Flags]
            public enum DirectSoundPlayFlags : uint
            {
                Looping = 0x00000001,
                LocHardware = 0x00000002,
                LocSoftware = 0x00000004,
                TerminateByTime = 0x00000008,
                TerminateByDistance = 0x000000010,
                TerminateByPriority = 0x000000020
            }

            [Flags]
            public enum DirectSoundBufferLockFlag : uint
            {
                None = 0,
                FromWriteCursor = 0x00000001,
                EntireBuffer = 0x00000002
            }

            [Flags]
            public enum DirectSoundBufferStatus : uint
            {
                Playing = 0x00000001,
                BufferLost = 0x00000002,
                Looping = 0x00000004,
                LocHardware = 0x00000008,
                LocSoftware = 0x00000010,
                Terminated = 0x00000020
            }

            [Flags]
            public enum DirectSoundBufferCaps : uint
            {
                PrimaryBuffer = 0x00000001,
                StaticBuffer = 0x00000002,
                LocHardware = 0x00000004,
                LocSoftware = 0x00000008,
                Control3D = 0x00000010,
                ControlFrequency = 0x00000020,
                ControlPan = 0x00000040,
                ControlVolume = 0x00000080,
                ControlNotifyPosition = 0x00000100,
                ControlEffects = 0x00000200,
                StickyFocus = 0x00004000,
                GlobalFocus = 0x00008000,
                GetCurrentPosition2 = 0x00010000,
                Mute3dAtMaxDistance = 0x00020000,
                LocDefer = 0x00040000
            }

            /// <summary>
            /// IDirectSound interface.
            /// </summary>
            [ComImport]
            [Guid("279AFA83-4981-11CE-A521-0020AF0BE560")]
            [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            [SuppressUnmanagedCodeSecurity]
            public interface IDirectSound
            {
                void CreateSoundBuffer([In] BufferDescription desc, [Out, MarshalAs(UnmanagedType.Interface)] out object dsDSoundBuffer, IntPtr pUnkOuter);

                void GetCaps(IntPtr caps);

                void DuplicateSoundBuffer([In, MarshalAs(UnmanagedType.Interface)] IDirectSoundBuffer bufferOriginal, [In, MarshalAs(UnmanagedType.Interface)] IDirectSoundBuffer bufferDuplicate);

                void SetCooperativeLevel(IntPtr windowHandle, [In, MarshalAs(UnmanagedType.U4)] DirectSoundCooperativeLevel dwLevel);

                void Compact();

                void GetSpeakerConfig(IntPtr pdwSpeakerConfig);

                void SetSpeakerConfig(uint pdwSpeakerConfig);

                void Initialize([In, MarshalAs(UnmanagedType.LPStruct)] Guid guid);
            }

            /// <summary>
            /// IDirectSoundBuffer interface.
            /// </summary>
            [ComImport]
            [Guid("279AFA85-4981-11CE-A521-0020AF0BE560")]
            [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            [SuppressUnmanagedCodeSecurity]
            public interface IDirectSoundBuffer
            {
                void GetCaps([MarshalAs(UnmanagedType.LPStruct)] BufferCaps pBufferCaps);

                void GetCurrentPosition([Out] out uint currentPlayCursor, [Out] out uint currentWriteCursor);

                void GetFormat();

                [return: MarshalAs(UnmanagedType.I4)]
                int GetVolume();

                void GetPan([Out] out uint pan);

                [return: MarshalAs(UnmanagedType.I4)]
                int GetFrequency();

                [return: MarshalAs(UnmanagedType.U4)]
                DirectSoundBufferStatus GetStatus();

                void Initialize([In, MarshalAs(UnmanagedType.Interface)] IDirectSound directSound, [In] BufferDescription desc);

                void Lock(int dwOffset, uint dwBytes, [Out] out IntPtr audioPtr1, [Out] out int audioBytes1, [Out] out IntPtr audioPtr2, [Out] out int audioBytes2, [MarshalAs(UnmanagedType.U4)] DirectSoundBufferLockFlag dwFlags);

                void Play(uint dwReserved1, uint dwPriority, [In, MarshalAs(UnmanagedType.U4)] DirectSoundPlayFlags dwFlags);

                void SetCurrentPosition(uint dwNewPosition);

                void SetFormat([In] WaveFormat waveFormat);

                void SetVolume(int volume);

                void SetPan(uint pan);

                void SetFrequency(uint frequency);

                void Stop();

                void Unlock(IntPtr pvAudioPtr1, int dwAudioBytes1, IntPtr pvAudioPtr2, int dwAudioBytes2);

                void Restore();
            }

            /// <summary>
            /// IDirectSoundNotify interface.
            /// </summary>
            [ComImport]
            [Guid("b0210783-89cd-11d0-af08-00a0c925cd16")]
            [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            [SuppressUnmanagedCodeSecurity]
            public interface IDirectSoundNotify
            {
                void SetNotificationPositions(uint dwPositionNotifies, [In, MarshalAs(UnmanagedType.LPArray)] DirectSoundBufferPositionNotify[] pcPositionNotifies);
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct DirectSoundBufferPositionNotify : IEquatable<DirectSoundBufferPositionNotify>
            {
                public uint Offset;

                public IntPtr NotifyHandle;

                /// <inheritdoc />
                public bool Equals(DirectSoundBufferPositionNotify other) =>
                    NotifyHandle == other.NotifyHandle;

                /// <inheritdoc />
                public override bool Equals(object obj)
                {
                    if (obj is DirectSoundBufferPositionNotify other)
                        return Equals(other);

                    return false;
                }

                /// <inheritdoc />
                public override int GetHashCode() =>
                    throw new NotSupportedException($"{nameof(DirectSoundBufferPositionNotify)} does not support hashing.");
            }

#pragma warning disable SA1401 // Fields must be private
#pragma warning disable 649 // Field is never assigned

            [StructLayout(LayoutKind.Sequential, Pack = 2)]
            public class BufferDescription
            {
                public int Size;

                [MarshalAs(UnmanagedType.U4)]
                public DirectSoundBufferCaps Flags;

                public uint BufferBytes;

                public int Reserved;

                public IntPtr FormatHandle;

                public Guid AlgorithmId;
            }

            [StructLayout(LayoutKind.Sequential, Pack = 2)]
            public class BufferCaps
            {
                public int Size;
                public int Flags;
                public int BufferBytes;
                public int UnlockTransferRate;
                public int PlayCpuOverhead;
            }

#pragma warning restore 649 // Field is never assigned
#pragma warning restore SA1401 // Fields must be private
        }

        private static class NativeMethods
        {
            private const string DirectSoundLib = "dsound.dll";
            private const string User32Lib = "user32.dll";

            /// <summary>
            /// Instantiate DirectSound from the DLL.
            /// </summary>
            /// <param name="deviceGuid">The GUID.</param>
            /// <param name="directSound">The direct sound.</param>
            /// <param name="pUnkOuter">The p unk outer.</param>
            /// <returns>The result code.</returns>
            [DllImport(DirectSoundLib, EntryPoint = nameof(DirectSoundCreate), SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
            public static extern int DirectSoundCreate(ref Guid deviceGuid, [Out, MarshalAs(UnmanagedType.Interface)] out DirectSound.IDirectSound directSound, IntPtr pUnkOuter);

            /// <summary>
            /// The DirectSoundEnumerate function enumerates the DirectSound drivers installed in the system.
            /// </summary>
            /// <param name="lpDSEnumCallback">callback function.</param>
            /// <param name="lpContext">User context.</param>
            [DllImport(DirectSoundLib, EntryPoint = nameof(DirectSoundEnumerateA), SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
            public static extern void DirectSoundEnumerateA(DirectSound.EnumerateDevicesDelegate lpDSEnumCallback, IntPtr lpContext);

            /// <summary>
            /// Gets the HANDLE of the desktop window.
            /// </summary>
            /// <returns>HANDLE of the Desktop window.</returns>
            [DllImport(User32Lib)]
            public static extern IntPtr GetDesktopWindow();
        }

        #endregion
    }
}
