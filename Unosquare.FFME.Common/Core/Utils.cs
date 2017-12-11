namespace Unosquare.FFME.Core
{
    using Decoding;
    using FFmpeg.AutoGen;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading;

    /// <summary>
    /// Provides a set of utilities to perfrom logging, text formatting, 
    /// conversion and other handy calculations.
    /// </summary>
    internal static class Utils
    {
        #region Private Declarations

        private static readonly object FFmpegRegisterLock = new object();
        private static readonly unsafe av_log_set_callback_callback FFmpegLogCallback = FFmpegLog;

        private static readonly IDispatcherTimer LogOutputter = null;
        private static readonly object LogSyncLock = new object();
        private static readonly List<string> FFmpegLogBuffer = new List<string>();
        private static readonly ConcurrentQueue<MediaLogMessagEventArgs> LogQueue = new ConcurrentQueue<MediaLogMessagEventArgs>();

        private static readonly unsafe av_lockmgr_register_cb FFmpegLockManagerCallback = FFmpegManageLocking;
        private static readonly Dictionary<IntPtr, ManualResetEvent> FFmpegOpDone = new Dictionary<IntPtr, ManualResetEvent>();

        private static bool? m_IsInDebugMode;
        private static bool HasFFmpegRegistered = false;
        private static string FFmpegRegisterPath = null;

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes static members of the <see cref="Utils"/> class.
        /// </summary>
        static Utils()
        {
            LogOutputter = Platform.CreateTimer(CoreDispatcherPriority.Background);

            LogOutputter.Tick += LogOutputter_Tick;
            LogOutputter.Start();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets a value indicating whether this instance is in debug mode.
        /// </summary>
        public static bool IsInDebugMode
        {
            get
            {
                if (!m_IsInDebugMode.HasValue)
                    m_IsInDebugMode = Debugger.IsAttached;

                return m_IsInDebugMode.Value;
            }
        }

        /// <summary>
        /// Gets the assembly location.
        /// </summary>
        private static string AssemblyLocation => Path.GetFullPath(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location));

        #endregion

        #region Interop

        /// <summary>
        /// Converts a byte pointer to a string
        /// </summary>
        /// <param name="bytePtr">The byte PTR.</param>
        /// <returns>The string</returns>
        public static unsafe string PtrToString(byte* bytePtr)
        {
            return Marshal.PtrToStringAnsi(new IntPtr(bytePtr));
        }

        /// <summary>
        /// Converts a byte pointer to a UTF8 encoded string.
        /// </summary>
        /// <param name="bytePtr">The byte PTR.</param>
        /// <returns>The string</returns>
        public static unsafe string PtrToStringUTF8(byte* bytePtr)
        {
            if (bytePtr == null) return null;
            if (*bytePtr == 0) return string.Empty;

            var byteBuffer = new List<byte>(1024);
            var currentByte = default(byte);

            while (true)
            {
                currentByte = *bytePtr;
                if (currentByte == 0)
                    break;

                byteBuffer.Add(currentByte);
                bytePtr++;
            }

            return Encoding.UTF8.GetString(byteBuffer.ToArray());
        }

        #endregion

        #region Math 

        /// <summary>
        /// Converts the given value to a value that is of the given multiple. 
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="multiple">The multiple.</param>
        /// <returns>The value</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ToMultipleOf(this double value, double multiple)
        {
            var factor = (int)(value / multiple);
            return factor * multiple;
        }

        /// <summary>
        /// Gets a timespan given a timestamp and a timebase.
        /// </summary>
        /// <param name="pts">The PTS.</param>
        /// <param name="timeBase">The time base.</param>
        /// <returns>The TimeSpan</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TimeSpan ToTimeSpan(this double pts, AVRational timeBase)
        {
            if (double.IsNaN(pts) || pts == FFmpegEx.AV_NOPTS)
                return TimeSpan.MinValue;

            if (timeBase.den == 0)
                return TimeSpan.FromTicks((long)Math.Round(TimeSpan.TicksPerMillisecond * 1000 * pts / ffmpeg.AV_TIME_BASE, 0));

            return TimeSpan.FromTicks((long)Math.Round(TimeSpan.TicksPerMillisecond * 1000 * pts * timeBase.num / timeBase.den, 0));
        }

        /// <summary>
        /// Gets a timespan given a timestamp and a timebase.
        /// </summary>
        /// <param name="pts">The PTS.</param>
        /// <param name="timeBase">The time base.</param>
        /// <returns>The TimeSpan</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TimeSpan ToTimeSpan(this long pts, AVRational timeBase)
        {
            return ((double)pts).ToTimeSpan(timeBase);
        }

        /// <summary>
        /// Gets a timespan given a timestamp and a timebase.
        /// </summary>
        /// <param name="pts">The PTS in seconds.</param>
        /// <param name="timeBase">The time base.</param>
        /// <returns>The TimeSpan</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TimeSpan ToTimeSpan(this double pts, double timeBase)
        {
            if (double.IsNaN(pts) || pts == FFmpegEx.AV_NOPTS)
                return TimeSpan.MinValue;

            return TimeSpan.FromTicks((long)Math.Round(TimeSpan.TicksPerMillisecond * 1000 * pts / timeBase, 0));
        }

        /// <summary>
        /// Gets a timespan given a timestamp and a timebase.
        /// </summary>
        /// <param name="pts">The PTS.</param>
        /// <param name="timeBase">The time base.</param>
        /// <returns>The TimeSpan</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TimeSpan ToTimeSpan(this long pts, double timeBase)
        {
            return ((double)pts).ToTimeSpan(timeBase);
        }

        /// <summary>
        /// Gets a timespan given a timestamp (in AV_TIME_BASE units)
        /// </summary>
        /// <param name="pts">The PTS.</param>
        /// <returns>The TimeSpan</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TimeSpan ToTimeSpan(this double pts)
        {
            return ToTimeSpan(pts, ffmpeg.AV_TIME_BASE);
        }

        /// <summary>
        /// Gets a timespan given a timestamp (in AV_TIME_BASE units)
        /// </summary>
        /// <param name="pts">The PTS.</param>
        /// <returns>The TimeSpan</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TimeSpan ToTimeSpan(this long pts)
        {
            return ((double)pts).ToTimeSpan();
        }

        /// <summary>
        /// Converts a fraction to a double
        /// </summary>
        /// <param name="rational">The rational.</param>
        /// <returns>The value</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ToDouble(this AVRational rational)
        {
            return (double)rational.num / rational.den;
        }

        #endregion

        #region FFmpeg Registration

        /// <summary>
        /// Registers FFmpeg library and initializes its components.
        /// It only needs to be called once but calling it more than once
        /// has no effect. Returns the path that FFmpeg was registered from.
        /// </summary>
        /// <param name="overridePath">The override path.</param>
        /// <returns>Returns the path that FFmpeg was registered from.</returns>
        /// <exception cref="System.IO.FileNotFoundException">When the folder is not found</exception>
        public static unsafe string RegisterFFmpeg(string overridePath)
        {
            lock (FFmpegRegisterLock)
            {
                if (HasFFmpegRegistered)
                    return FFmpegRegisterPath;

                // Define the minimum set of ffmpeg binaries.
                string[] minimumFFmpegSet = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? new[] { Constants.DllAVCodec, Constants.DllAVFormat, Constants.DllAVUtil, Constants.DllSWResample }
                    : new[] { Constants.DllAVCodec_macOS, Constants.DllAVFormat_macOS, Constants.DllAVUtil_macOS, Constants.DllSWResample_macOS };

                var architecture = IntPtr.Size == 4 ? ProcessorArchitecture.X86 : ProcessorArchitecture.Amd64;
                var ffmpegFolderName = architecture == ProcessorArchitecture.X86 ? "ffmpeg32" : "ffmpeg64";
                var ffmpegPath = string.IsNullOrWhiteSpace(overridePath) == false ?
                    overridePath : Path.GetFullPath(Path.Combine(AssemblyLocation, ffmpegFolderName));

                // Ensure all files exist
                foreach (var fileName in minimumFFmpegSet)
                {
                    if (File.Exists(Path.Combine(ffmpegPath, fileName)) == false)
                        throw new FileNotFoundException($"Unable to load minimum set of FFmpeg binaries from folder '{ffmpegPath}'. File '{fileName}' is missing");
                }

                Platform.SetDllDirectory(ffmpegPath);

                if ((RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && File.Exists(Path.Combine(ffmpegPath, Constants.DllAVDevice))) ||
                    (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && File.Exists(Path.Combine(ffmpegPath, Constants.DllAVDevice_macOS))))
                    ffmpeg.avdevice_register_all();

                if ((RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && File.Exists(Path.Combine(ffmpegPath, Constants.DllAVFilter))) ||
                    (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && File.Exists(Path.Combine(ffmpegPath, Constants.DllAVFilter_macOS))))
                    ffmpeg.avfilter_register_all();

                ffmpeg.av_register_all();
                ffmpeg.avcodec_register_all();
                ffmpeg.avformat_network_init();

                ffmpeg.av_log_set_flags(ffmpeg.AV_LOG_SKIP_REPEATED);
                ffmpeg.av_log_set_level(IsInDebugMode ? ffmpeg.AV_LOG_VERBOSE : ffmpeg.AV_LOG_WARNING);
                ffmpeg.av_log_set_callback(FFmpegLogCallback);

                // because Zeranoe FFmpeg Builds don't have --enable-pthreads,
                // https://ffmpeg.zeranoe.com/builds/readme/win64/static/ffmpeg-20170620-ae6f6d4-win64-static-readme.txt
                // and because by default FFmpeg is not thread-safe,
                // https://stackoverflow.com/questions/13888915/thread-safety-of-libav-ffmpeg
                // we need to register a lock manager with av_lockmgr_register
                // Just like in https://raw.githubusercontent.com/FFmpeg/FFmpeg/release/3.4/ffplay.c
                if (Constants.EnableFFmpegLockManager)
                    ffmpeg.av_lockmgr_register(FFmpegLockManagerCallback);

                HasFFmpegRegistered = true;
                FFmpegRegisterPath = ffmpegPath;
                return FFmpegRegisterPath;
            }
        }

        #endregion

        #region Logging

        /// <summary>
        /// Logs the specified message.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="messageType">Type of the message.</param>
        /// <param name="message">The message.</param>
        /// <exception cref="System.ArgumentNullException">sender</exception>
        internal static void Log(object sender, MediaLogMessageType messageType, string message)
        {
            if (sender == null) throw new ArgumentNullException(nameof(sender));
            var eventArgs = new MediaLogMessagEventArgs(sender as MediaElementCore, messageType, message);
            LogQueue.Enqueue(eventArgs);
        }

        /// <summary>
        /// Logs a block rendering operation as a Trace Message
        /// if the debugger is attached.
        /// </summary>
        /// <param name="element">The media element.</param>
        /// <param name="block">The block.</param>
        /// <param name="clockPosition">The clock position.</param>
        /// <param name="renderIndex">Index of the render.</param>
        internal static void LogRenderBlock(this MediaElementCore element, MediaBlock block, TimeSpan clockPosition, int renderIndex)
        {
            if (IsInDebugMode == false) return;

            try
            {
                var drift = TimeSpan.FromTicks(clockPosition.Ticks - block.StartTime.Ticks);
                element?.Logger.Log(MediaLogMessageType.Trace,
                $"{block.MediaType.ToString().Substring(0, 1)} "
                    + $"BLK: {block.StartTime.Format()} | "
                    + $"CLK: {clockPosition.Format()} | "
                    + $"DFT: {drift.TotalMilliseconds,4:0} | "
                    + $"IX: {renderIndex,3} | "
                    + $"PQ: {element.Container?.Components[block.MediaType]?.PacketBufferLength / 1024d,7:0.0}k | "
                    + $"TQ: {element.Container?.Components.PacketBufferLength / 1024d,7:0.0}k");
            }
            catch
            {
                // swallow
            }
        }

        #endregion

        #region Output Formatting

        /// <summary>
        /// Returns a formatted timestamp string in Seconds
        /// </summary>
        /// <param name="ts">The ts.</param>
        /// <returns>The formatted string</returns>
        internal static string Format(this TimeSpan ts)
        {
            if (ts == TimeSpan.MinValue)
                return $"{"N/A",10}";
            else
                return $"{ts.TotalSeconds,10:0.000}";
        }

        /// <summary>
        /// Returns a formatted string with elapsed milliseconds between now and
        /// the specified date.
        /// </summary>
        /// <param name="dt">The dt.</param>
        /// <returns>The formatted string</returns>
        internal static string FormatElapsed(this DateTime dt)
        {
            return $"{DateTime.UtcNow.Subtract(dt).TotalMilliseconds,6:0}";
        }

        /// <summary>
        /// Returns a fromatted string, dividing by the specified
        /// factor. Useful for debugging longs with byte positions or sizes.
        /// </summary>
        /// <param name="ts">The ts.</param>
        /// <param name="divideBy">The divide by.</param>
        /// <returns>The formatted string</returns>
        internal static string Format(this long ts, double divideBy = 1)
        {
            return divideBy == 1 ? $"{ts,10:#,##0}" : $"{ts / divideBy,10:#,##0.000}";
        }

        /// <summary>
        /// Strips the SRT format and returns plain text.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <returns>The formatted string</returns>
        internal static string StripSrtFormat(this string input)
        {
            var output = new StringBuilder(input.Length);
            var isInTag = false;
            var currentChar = default(char);

            for (var i = 0; i < input.Length; i++)
            {
                currentChar = input[i];
                if (currentChar == '<' && isInTag == false)
                {
                    isInTag = true;
                    continue;
                }

                if (currentChar == '>' && isInTag == true)
                {
                    isInTag = false;
                    continue;
                }

                output.Append(currentChar);
            }

            return output.ToString();
        }

        /// <summary>
        /// Strips a line of text from the ASS format.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <returns>The formatted string</returns>
        internal static string StripAssFormat(this string input)
        {
            const string DialoguePrefix = "dialogue:";

            if (input.Substring(0, DialoguePrefix.Length).ToLowerInvariant().Equals(DialoguePrefix) == false)
                return string.Empty;

            var inputParts = input.Split(new char[] { ',' }, 10);
            if (inputParts.Length != 10)
                return string.Empty;

            input = inputParts[inputParts.Length - 1].Replace("\\n", " ").Replace("\\N", "\r\n");
            var builder = new StringBuilder(input.Length);
            var isInStyle = false;
            var currentChar = default(char);

            for (var i = 0; i < input.Length; i++)
            {
                currentChar = input[i];
                if (currentChar == '{' && isInStyle == false)
                {
                    isInStyle = true;
                    continue;
                }

                if (currentChar == '}' && isInStyle == true)
                {
                    isInStyle = false;
                    continue;
                }

                builder.Append(currentChar);
            }

            return builder.ToString().Trim();
        }

        #endregion

        #region Private Methods and Callbacks

        /// <summary>
        /// Handles the Tick event of the LogOutputter timer.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private static void LogOutputter_Tick(object sender, EventArgs e)
        {
            while (LogQueue.TryDequeue(out MediaLogMessagEventArgs eventArgs))
            {
                if (eventArgs.Source != null)
                    eventArgs.Source.RaiseMessageLogged(eventArgs);
                else
                    MediaElementCore.RaiseFFmpegMessageLogged(eventArgs);
            }
        }

        /// <summary>
        /// Manages FFmpeg Multithreaded locking
        /// </summary>
        /// <param name="mutex">The mutex.</param>
        /// <param name="lockingOperation">The op.</param>
        /// <returns>
        /// 0 for success, 1 for error
        /// </returns>
        private static unsafe int FFmpegManageLocking(void** mutex, AVLockOp lockingOperation)
        {
            switch (lockingOperation)
            {
                case AVLockOp.AV_LOCK_CREATE:
                    {
                        var m = new ManualResetEvent(true);
                        var mutexPointer = m.SafeWaitHandle.DangerousGetHandle();
                        *mutex = (void*)mutexPointer;
                        FFmpegOpDone[mutexPointer] = m;
                        return 0;
                    }

                case AVLockOp.AV_LOCK_OBTAIN:
                    {
                        var mutexPointer = new IntPtr(*mutex);
                        FFmpegOpDone[mutexPointer].WaitOne();
                        FFmpegOpDone[mutexPointer].Reset();
                        return 0;
                    }

                case AVLockOp.AV_LOCK_RELEASE:
                    {
                        var mutexPointer = new IntPtr(*mutex);
                        FFmpegOpDone[mutexPointer].Set();
                        return 0;
                    }

                case AVLockOp.AV_LOCK_DESTROY:
                    {
                        var mutexPointer = new IntPtr(*mutex);
                        var m = FFmpegOpDone[mutexPointer];
                        FFmpegOpDone.Remove(mutexPointer);
                        m.Set();
                        m.Dispose();
                        return 0;
                    }
            }

            return 1;
        }

        /// <summary>
        /// Log message callback from ffmpeg library.
        /// </summary>
        /// <param name="p0">The p0.</param>
        /// <param name="level">The level.</param>
        /// <param name="format">The format.</param>
        /// <param name="vl">The vl.</param>
        private static unsafe void FFmpegLog(void* p0, int level, string format, byte* vl)
        {
            const int lineSize = 1024;

            lock (LogSyncLock)
            {
                if (level > ffmpeg.av_log_get_level()) return;
                var lineBuffer = stackalloc byte[lineSize];
                var printPrefix = 1;
                ffmpeg.av_log_format_line(p0, level, format, vl, lineBuffer, lineSize, &printPrefix);
                var line = Utils.PtrToString(lineBuffer);
                FFmpegLogBuffer.Add(line);

                var messageType = MediaLogMessageType.Debug;
                if (Constants.FFmpegLogLevels.ContainsKey(level))
                    messageType = Constants.FFmpegLogLevels[level];

                if (line.EndsWith("\n"))
                {
                    line = string.Join(string.Empty, FFmpegLogBuffer);
                    line = line.TrimEnd();
                    FFmpegLogBuffer.Clear();
                    Utils.Log(typeof(MediaElementCore), messageType, line);
                }
            }
        }

        #endregion
    }
}
