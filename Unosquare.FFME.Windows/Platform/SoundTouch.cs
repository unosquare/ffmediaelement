﻿namespace Unosquare.FFME.Platform
{
    using FFmpeg.AutoGen.Native;
    using System;
    using System.IO;
    using System.Runtime.InteropServices;

    /// <inheritdoc />
    /// <summary>
    /// SoundTouch audio processing library wrapper (SoundTouch.cs)
    /// Original code by
    /// Copyright (c) Olli Parviainen
    /// http://www.surina.net/soundtouch
    /// LGPL License
    /// Modified Code by:
    /// Mario Di Vece
    /// Changes:
    /// Set-prefixed methods to property setters
    /// Native wrappers to NativeMethods class name
    /// Adding enum with settings as defined in the header file
    /// Settings getters and setters as indexers
    /// Implemented Dispose pattern correctly.
    /// </summary>
    internal sealed class SoundTouch : IDisposable
    {
        #region Private Members

        private const string SoundTouchLibrary = "SoundTouch.dll";
        private readonly object SyncRoot = new object();
        private bool IsDisposed;
        private IntPtr handle;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes static members of the <see cref="SoundTouch"/> class.
        /// </summary>
        static SoundTouch()
        {
            try
            {
                // Include the ffmpeg directory in the search path
                var loadResult = LibraryLoader.LoadNativeLibrary(
                    Path.Combine(Library.FFmpegDirectory, SoundTouchLibrary));

                if (loadResult == IntPtr.Zero)
                {
                    IsAvailable = false;
                    return;
                }

                var versionId = NativeMethods.GetVersionId();
                IsAvailable = versionId != 0;
            }
            catch
            {
                IsAvailable = false;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SoundTouch"/> class.
        /// </summary>
        public SoundTouch()
        {
            handle = NativeMethods.CreateInstance();
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="SoundTouch"/> class.
        /// </summary>
        ~SoundTouch()
        {
            DisposeInternal();
        }

        /// <summary>
        /// Settings as defined in SoundTouch.h.
        /// </summary>
        public enum Setting
        {
            /// <summary>
            /// Enable/disable anti-alias filter in pitch transposer (0 = disable)
            /// </summary>
            UseAntiAliasFilter = 0,

            /// <summary>
            /// Pitch transposer anti-alias filter length (8 .. 128 taps, default = 32)
            /// </summary>
            AntiAliasFilterLength = 1,

            /// <summary>
            /// Enable/disable quick seeking algorithm in tempo changer routine
            /// (enabling quick seeking lowers CPU utilization but causes a minor sound
            ///  quality compromising)
            /// </summary>
            UseQuickSeek = 2,

            /// <summary>
            /// Time-stretch algorithm single processing sequence length in milliseconds. This determines
            /// to how long sequences the original sound is chopped in the time-stretch algorithm.
            /// See "STTypes.h" or README for more information.
            /// </summary>
            SequenceMilliseconds = 3,

            /// <summary>
            /// Time-stretch algorithm seeking window length in milliseconds for algorithm that finds the
            /// best possible overlapping location. This determines from how wide window the algorithm
            /// may look for an optimal joining location when mixing the sound sequences back together.
            /// See "STTypes.h" or README for more information.
            /// </summary>
            SeekWindowMilliseconds = 4,

            /// <summary>
            /// Time-stretch algorithm overlap length in milliseconds. When the chopped sound sequences
            /// are mixed back together, to form a continuous sound stream, this parameter defines over
            /// how long period the two consecutive sequences are let to overlap each other.
            /// See "STTypes.h" or README for more information.
            /// </summary>
            OverlapMilliseconds = 5,

            /// <summary>
            /// Call "getSetting" with this ID to query processing sequence size in samples.
            /// This value gives approximate value of how many input samples you'll need to
            /// feed into SoundTouch after initial buffering to get out a new batch of
            /// output samples.
            ///
            /// This value does not include initial buffering at beginning of a new processing
            /// stream, use SETTING_INITIAL_LATENCY to get the initial buffering size.
            ///
            /// Notices:
            /// - This is read-only parameter, i.e. setSetting ignores this parameter
            /// - This parameter value is not constant but change depending on
            ///   tempo/pitch/rate/sample rate settings.
            /// </summary>
            NominalInputSequence = 6,

            /// <summary>
            /// Call "getSetting" with this ID to query nominal average processing output
            /// size in samples. This value tells approximate value how many output samples
            /// SoundTouch outputs once it does DSP processing run for a batch of input samples.
            ///
            /// Notices:
            /// - This is read-only parameter, i.e. setSetting ignores this parameter
            /// - This parameter value is not constant but change depending on
            ///   tempo/pitch/rate/sample rate settings.
            /// </summary>
            NominalOutputSequence = 7,

            /// <summary>
            /// Call "getSetting" with this ID to query initial processing latency, i.e.
            /// approx. how many samples you'll need to enter to SoundTouch pipeline before
            /// you can expect to get first batch of ready output samples out.
            ///
            /// After the first output batch, you can then expect to get approx.
            /// SETTING_NOMINAL_OUTPUT_SEQUENCE ready samples out for every
            /// SETTING_NOMINAL_INPUT_SEQUENCE samples that you enter into SoundTouch.
            ///
            /// Example:
            ///     processing with parameter -tempo=5
            ///     => initial latency = 5509 samples
            ///        input sequence  = 4167 samples
            ///        output sequence = 3969 samples
            ///
            /// Accordingly, you can expect to feed in approx. 5509 samples at beginning of
            /// the stream, and then you'll get out the first 3969 samples. After that, for
            /// every approx. 4167 samples that you'll put in, you'll receive again approx.
            /// 3969 samples out.
            ///
            /// This also means that average latency during stream processing is
            /// INITIAL_LATENCY-OUTPUT_SEQUENCE/2, in the above example case 5509-3969/2
            /// = 3524 samples
            ///
            /// Notices:
            /// - This is read-only parameter, i.e. setSetting ignores this parameter
            /// - This parameter value is not constant but change depending on
            ///   tempo/pitch/rate/sample rate settings.
            /// </summary>
            InitialLatency = 8
        }

        #endregion

        #region Properties

        /// <summary>
        /// Get SoundTouch version string.
        /// </summary>
        public static string Version => Marshal.PtrToStringAnsi(NativeMethods.GetVersionString());

        /// <summary>
        /// Gets a value indicating whether the SoundTouch Library (dll) is available.
        /// </summary>
        public static bool IsAvailable { get; }

        /// <summary>
        /// Returns number of processed samples currently available in SoundTouch for immediate output.
        /// </summary>
        public uint AvailableSampleCount
        {
            get { lock (SyncRoot) { return NativeMethods.NumSamples(handle); } }
        }

        /// <summary>
        /// Returns number of samples currently unprocessed in SoundTouch internal buffer.
        /// </summary>
        /// <returns>Number of sample frames.</returns>
        public uint UnprocessedSampleCount
        {
            get { lock (SyncRoot) { return NativeMethods.NumUnprocessedSamples(handle); } }
        }

        /// <summary>
        /// Check if there aren't any samples available for outputting.
        /// </summary>
        /// <returns>nonzero if there aren't any samples available for outputting.</returns>
        public int IsEmpty
        {
            get { lock (SyncRoot) { return NativeMethods.IsEmpty(handle); } }
        }

        /// <summary>
        /// Changes or gets a setting controlling the processing system behaviour. See the
        /// 'SETTING_...' defines for available setting ID's.
        /// </summary>
        /// <value>
        /// The <see cref="int"/>.
        /// </value>
        /// <param name="settingId">The setting identifier.</param>
        /// <returns>The value of the setting.</returns>
        public int this[Setting settingId]
        {
            get
            {
                lock (SyncRoot) { return NativeMethods.GetSetting(handle, (int)settingId); }
            }
            set
            {
                lock (SyncRoot)
                {
                    var result = NativeMethods.SetSetting(handle, (int)settingId, value);
                    if (result == 0)
                        return;

                    throw new ArgumentException($"Unable to set {settingId} with value {value}. Code: {result}.");
                }
            }
        }

        #endregion

        #region Sample Stream Methods

        /// <summary>
        /// Sets sample rate.
        /// Value: Sample rate, e.g. 44100.
        /// </summary>
        /// <param name="value">The sample rate value.</param>
        public void SetSampleRate(uint value)
        {
            lock (SyncRoot)
                NativeMethods.SetSampleRate(handle, value);
        }

        /// <summary>
        /// Sets the number of channels
        /// Value: 1 = mono, 2 = stereo, n = multichannel.
        /// </summary>
        /// <param name="value">The value.</param>
        public void SetChannels(uint value)
        {
            lock (SyncRoot)
                NativeMethods.SetChannels(handle, value);
        }

        /// <summary>
        /// Sets new tempo control value.
        /// Value: Tempo setting. Normal tempo = 1.0, smaller values
        /// represent slower tempo, larger faster tempo.
        /// </summary>
        /// <param name="value">The value.</param>
        public void SetTempo(float value)
        {
            lock (SyncRoot)
                NativeMethods.SetTempo(handle, value);
        }

        /// <summary>
        /// Sets new tempo control value as a difference in percents compared
        /// to the original tempo (-50 .. +100 %);.
        /// </summary>
        /// <param name="value">The value.</param>
        public void SetTempoChange(float value)
        {
            lock (SyncRoot)
                NativeMethods.SetTempoChange(handle, value);
        }

        /// <summary>
        /// Sets new rate control value.
        /// Rate setting. Normal rate = 1.0, smaller values
        /// represent slower rate, larger faster rate.
        /// </summary>
        /// <param name="value">The value.</param>
        public void SetRate(float value)
        {
            lock (SyncRoot)
                NativeMethods.SetRate(handle, value);
        }

        /// <summary>
        /// Sets new rate control value as a difference in percents compared
        /// to the original rate (-50 .. +100 %);
        /// Value: Rate setting is in %.
        /// </summary>
        /// <param name="value">The value.</param>
        public void SetRateChange(float value)
        {
            lock (SyncRoot)
                NativeMethods.SetRateChange(handle, value);
        }

        /// <summary>
        /// Sets new pitch control value.
        /// Value: Pitch setting. Original pitch = 1.0, smaller values
        /// represent lower pitches, larger values higher pitch.
        /// </summary>
        /// <param name="value">The value.</param>
        public void SetPitch(float value)
        {
            lock (SyncRoot)
                NativeMethods.SetPitch(handle, value);
        }

        /// <summary>
        /// Sets pitch change in octaves compared to the original pitch
        /// (-1.00 .. +1.00 for +- one octave);
        /// Value: Pitch setting in octaves.
        /// </summary>
        /// <param name="value">The value.</param>
        public void SetPitchOctaves(float value)
        {
            lock (SyncRoot)
                NativeMethods.SetPitchOctaves(handle, value);
        }

        /// <summary>
        /// Sets pitch change in semi-tones compared to the original pitch
        /// (-12 .. +12 for +- one octave);
        /// Value: Pitch setting in semitones.
        /// </summary>
        /// <param name="value">The value.</param>
        public void SetPitchSemiTones(float value)
        {
            lock (SyncRoot)
                NativeMethods.SetPitchSemiTones(handle, value);
        }

        /// <summary>
        /// Flushes the last samples from the processing pipeline to the output.
        /// Clears also the internal processing buffers.
        /// Note: This function is meant for extracting the last samples of a sound
        /// stream. This function may introduce additional blank samples in the end
        /// of the sound stream, and thus it's not recommended to call this function
        /// in the middle of a sound stream.
        /// </summary>
        public void Flush()
        {
            lock (SyncRoot) { NativeMethods.Flush(handle); }
        }

        /// <summary>
        /// Clears all the samples in the object's output and internal processing
        /// buffers.
        /// </summary>
        public void Clear()
        {
            lock (SyncRoot) { NativeMethods.Clear(handle); }
        }

        /// <summary>
        /// Adds 'numSamples' pcs of samples from the 'samples' memory position into
        /// the input of the object. Notice that sample rate _has_to_ be set before
        /// calling this function, otherwise throws a runtime_error exception.
        /// </summary>
        /// <param name="samples">Sample buffer to input.</param>
        /// <param name="numSamples">Number of sample frames in buffer. Notice
        /// that in case of multi-channel sound a single sample frame contains
        /// data for all channels.</param>
        public void PutSamples(float[] samples, uint numSamples)
        {
            lock (SyncRoot) { NativeMethods.PutSamples(handle, samples, numSamples); }
        }

        /// <summary>
        /// int16 version of putSamples(): This accept int16 (i.e. short) sample data
        /// and internally converts it to float format before processing.
        /// </summary>
        /// <param name="samples">Sample input buffer.</param>
        /// <param name="numSamples">Number of sample frames in buffer. Notice
        /// that in case of multi-channel sound a single
        /// sample frame contains data for all channels.</param>
        public void PutSamplesI16(short[] samples, uint numSamples)
        {
            lock (SyncRoot) { NativeMethods.PutSamples_i16(handle, samples, numSamples); }
        }

        /// <summary>
        /// Receive processed samples from the processor.
        /// </summary>
        /// <param name="outBuffer">Buffer where to copy output samples.</param>
        /// <param name="maxSamples">Max number of sample frames to receive.</param>
        /// <returns>The number of samples received.</returns>
        public uint ReceiveSamples(float[] outBuffer, uint maxSamples)
        {
            lock (SyncRoot) { return NativeMethods.ReceiveSamples(handle, outBuffer, maxSamples); }
        }

        /// <summary>
        /// int16 version of receiveSamples(): This converts internal float samples
        /// into int16 (i.e. short) return data type.
        /// </summary>
        /// <param name="outBuffer">Buffer where to copy output samples.</param>
        /// <param name="maxSamples">How many samples to receive at max.</param>
        /// <returns>Number of received sample frames.</returns>
        public uint ReceiveSamplesI16(short[] outBuffer, uint maxSamples)
        {
            lock (SyncRoot) { return NativeMethods.ReceiveSamples_i16(handle, outBuffer, maxSamples); }
        }

        #endregion

        #region IDisposable Support

        /// <inheritdoc />
        public void Dispose()
        {
            DisposeInternal();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        private void DisposeInternal()
        {
            lock (SyncRoot)
            {
                if (IsDisposed) return;

                if (handle != IntPtr.Zero)
                    NativeMethods.DestroyInstance(handle);

                handle = IntPtr.Zero;
                IsDisposed = true;
            }
        }

        #endregion

        #region Native Methods

        /// <summary>
        /// Provides direct access to mapped DLL methods.
        /// </summary>
        private static class NativeMethods
        {
            [DllImport(SoundTouchLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "soundtouch_getVersionId")]
            public static extern int GetVersionId();

            [DllImport(SoundTouchLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "soundtouch_createInstance")]
            public static extern IntPtr CreateInstance();

            [DllImport(SoundTouchLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "soundtouch_destroyInstance")]
            public static extern void DestroyInstance(IntPtr h);

            [DllImport(SoundTouchLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "soundtouch_getVersionString")]
            public static extern IntPtr GetVersionString();

            [DllImport(SoundTouchLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "soundtouch_setRate")]
            public static extern void SetRate(IntPtr h, float newRate);

            [DllImport(SoundTouchLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "soundtouch_setTempo")]
            public static extern void SetTempo(IntPtr h, float newTempo);

            [DllImport(SoundTouchLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "soundtouch_setRateChange")]
            public static extern void SetRateChange(IntPtr h, float newRate);

            [DllImport(SoundTouchLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "soundtouch_setTempoChange")]
            public static extern void SetTempoChange(IntPtr h, float newTempo);

            [DllImport(SoundTouchLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "soundtouch_setPitch")]
            public static extern void SetPitch(IntPtr h, float newPitch);

            [DllImport(SoundTouchLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "soundtouch_setPitchOctaves")]
            public static extern void SetPitchOctaves(IntPtr h, float newPitch);

            [DllImport(SoundTouchLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "soundtouch_setPitchSemiTones")]
            public static extern void SetPitchSemiTones(IntPtr h, float newPitch);

            [DllImport(SoundTouchLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "soundtouch_setChannels")]
            public static extern void SetChannels(IntPtr h, uint numChannels);

            [DllImport(SoundTouchLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "soundtouch_setSampleRate")]
            public static extern void SetSampleRate(IntPtr h, uint sampleRate);

            [DllImport(SoundTouchLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "soundtouch_flush")]
            public static extern void Flush(IntPtr h);

            [DllImport(SoundTouchLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "soundtouch_putSamples")]
            public static extern void PutSamples(IntPtr h, float[] samples, uint numSamples);

            [DllImport(SoundTouchLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "soundtouch_putSamples_i16")]
            public static extern void PutSamples_i16(IntPtr h, short[] samples, uint numSamples);

            [DllImport(SoundTouchLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "soundtouch_clear")]
            public static extern void Clear(IntPtr h);

            [DllImport(SoundTouchLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "soundtouch_setSetting")]
            public static extern int SetSetting(IntPtr h, int settingId, int value);

            [DllImport(SoundTouchLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "soundtouch_getSetting")]
            public static extern int GetSetting(IntPtr h, int settingId);

            [DllImport(SoundTouchLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "soundtouch_numUnprocessedSamples")]
            public static extern uint NumUnprocessedSamples(IntPtr h);

            [DllImport(SoundTouchLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "soundtouch_receiveSamples")]
            public static extern uint ReceiveSamples(IntPtr h, float[] outBuffer, uint maxSamples);

            [DllImport(SoundTouchLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "soundtouch_receiveSamples_i16")]
            public static extern uint ReceiveSamples_i16(IntPtr h, short[] outBuffer, uint maxSamples);

            [DllImport(SoundTouchLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "soundtouch_numSamples")]
            public static extern uint NumSamples(IntPtr h);

            [DllImport(SoundTouchLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "soundtouch_isEmpty")]
            public static extern int IsEmpty(IntPtr h);
        }

        #endregion
    }
}
