namespace Unosquare.FFME.Core
{
    using Decoding;
    using FFmpeg.AutoGen;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Threading;

    /// <summary>
    /// Provides a set of utilities to perfrom logging, text formatting, 
    /// conversion and other handy calculations.
    /// </summary>
    internal static class Utils
    {
        #region Private Declarations

        private static readonly object FFmpegRegisterLock = new object();
        private static unsafe readonly av_log_set_callback_callback FFmpegLogCallback = FFmpegLog;
        
        private static readonly DispatcherTimer LogOutputter = null;
        private static readonly object LogSyncLock = new object();
        private static readonly List<string> FFmpegLogBuffer = new List<string>();
        private static readonly ConcurrentQueue<MediaLogMessagEventArgs> LogQueue = new ConcurrentQueue<MediaLogMessagEventArgs>();

        private static unsafe readonly av_lockmgr_register_cb FFmpegLockManagerCallback = FFmpegManageLocking;
        private static readonly Dictionary<IntPtr, ManualResetEvent> FFmpegOpDone = new Dictionary<IntPtr, ManualResetEvent>();

        private static bool? m_IsInDesignTime;
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
            LogOutputter = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(50),
                IsEnabled = true,
            };

            LogOutputter.Tick += LogOutputter_Tick;
            LogOutputter.Start();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Determines if we are currently in Design Time
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is in design time; otherwise, <c>false</c>.
        /// </value>
        public static bool IsInDesignTime
        {
            get
            {
                if (!m_IsInDesignTime.HasValue)
                {
                    m_IsInDesignTime = (bool)DesignerProperties.IsInDesignModeProperty.GetMetadata(
                          typeof(DependencyObject)).DefaultValue;
                }
                return m_IsInDesignTime.Value;
            }
        }

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
        /// Gets the UI dispatcher.
        /// </summary>
        public static Dispatcher UIDispatcher
        {
            get { return Application.Current?.Dispatcher; }
        }

        /// <summary>
        /// Gets the assembly location.
        /// </summary>
        private static string AssemblyLocation
        {
            get
            {
                return Path.GetFullPath(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location));
            }
        }

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
        public static TimeSpan ToTimeSpan(this double pts, AVRational timeBase)
        {
            if (double.IsNaN(pts) || pts == Utils.FFmpeg.AV_NOPTS)
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
        public static TimeSpan ToTimeSpan(this double pts, double timeBase)
        {
            if (double.IsNaN(pts) || pts == Utils.FFmpeg.AV_NOPTS)
                return TimeSpan.MinValue;

            return TimeSpan.FromTicks((long)Math.Round(TimeSpan.TicksPerMillisecond * 1000 * pts / timeBase, 0));
        }

        /// <summary>
        /// Gets a timespan given a timestamp and a timebase.
        /// </summary>
        /// <param name="pts">The PTS.</param>
        /// <param name="timeBase">The time base.</param>
        /// <returns>The TimeSpan</returns>
        public static TimeSpan ToTimeSpan(this long pts, double timeBase)
        {
            return ((double)pts).ToTimeSpan(timeBase);
        }

        /// <summary>
        /// Gets a timespan given a timestamp (in AV_TIME_BASE units)
        /// </summary>
        /// <param name="pts">The PTS.</param>
        /// <returns>The TimeSpan</returns>
        public static TimeSpan ToTimeSpan(this double pts)
        {
            return ToTimeSpan(pts, ffmpeg.AV_TIME_BASE);
        }

        /// <summary>
        /// Gets a timespan given a timestamp (in AV_TIME_BASE units)
        /// </summary>
        /// <param name="pts">The PTS.</param>
        /// <returns>The TimeSpan</returns>
        public static TimeSpan ToTimeSpan(this long pts)
        {
            return ((double)pts).ToTimeSpan();
        }

        /// <summary>
        /// Converts a fraction to a double
        /// </summary>
        /// <param name="rational">The rational.</param>
        /// <returns>The value</returns>
        public static double ToDouble(this AVRational rational)
        {
            return (double)rational.num / rational.den;
        }

        /// <summary>
        /// Rounds the ticks.
        /// </summary>
        /// <param name="ticks">The ticks.</param>
        /// <returns>The ticks</returns>
        public static long RoundTicks(this long ticks)
        {
            return Convert.ToInt64(Convert.ToDouble(ticks) / 1000d) * 1000;
        }

        /// <summary>
        /// Rounds the seconds to 4 decimals.
        /// </summary>
        /// <param name="seconds">The seconds.</param>
        /// <returns>The seconds</returns>
        public static decimal RoundSeconds(this decimal seconds)
        {
            return Math.Round(seconds, 4);
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
                var minimumFFmpegSet = new[] { Constants.DllAVCodec, Constants.DllAVFormat, Constants.DllAVUtil, Constants.DllSWResample };

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

                NativeMethods.SetDllDirectory(ffmpegPath);

                if (File.Exists(Path.Combine(ffmpegPath, Constants.DllAVDevice)))
                    ffmpeg.avdevice_register_all();

                if (File.Exists(Path.Combine(ffmpegPath, Constants.DllAVFilter)))
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
                // Just like in https://raw.githubusercontent.com/FFmpeg/FFmpeg/release/3.2/ffplay.c
                ffmpeg.av_lockmgr_register(FFmpegLockManagerCallback);

                HasFFmpegRegistered = true;
                FFmpegRegisterPath = ffmpegPath;
                return FFmpegRegisterPath;
            }
        }

        /// <summary>
        /// Manages FFmpeg Multithreaded locking
        /// </summary>
        /// <param name="mutex">The mutex.</param>
        /// <param name="op">The op.</param>
        /// <returns>0 for success, 1 for error</returns>
        private static unsafe int FFmpegManageLocking(void** mutex, AVLockOp op)
        {
            switch (op)
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
        /// Log message callback fro ffmpeg library.
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
                    Utils.Log(typeof(MediaElement), messageType, line);
                }
            }
        }

        #endregion

        #region Dispatching

        /// <summary>
        /// Invokes the specified delegate on the specified dispatcher.
        /// </summary>
        /// <param name="dispatcher">The dispatcher.</param>
        /// <param name="priority">The priority.</param>
        /// <param name="action">The action.</param>
        /// <param name="args">The arguments.</param>
        /// <returns>The awaitable task</returns>
        public static async Task InvokeAsync(this Dispatcher dispatcher, DispatcherPriority priority, Delegate action, params object[] args)
        {
            // exit if we don't have a valid dispatcher
            if (dispatcher == null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished) return;

            // synchronously invoke if we are on the same context
            if (Dispatcher.CurrentDispatcher == dispatcher)
            {
                action.DynamicInvoke(args);
                return;
            }

            // Execute asynchronously
            try
            {
                await dispatcher.InvokeAsync(() => { action.DynamicInvoke(args); }, priority);
            }
            catch (TaskCanceledException)
            {
                // swallow
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// Synchronously invokes the given instructions on the main application dispatcher.
        /// </summary>
        /// <param name="priority">The priority. Set it to Normal by default.</param>
        /// <param name="action">The action.</param>
        public static void UIInvoke(DispatcherPriority priority, Action action)
        {
            UIDispatcher.InvokeAsync(priority, action, null).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Enqueues the given instructions with the given arguments on the main application dispatcher.
        /// This is a way to execute code in a fire-and-forget style
        /// </summary>
        /// <param name="priority">The priority.</param>
        /// <param name="action">The action.</param>
        /// <param name="args">The arguments.</param>
        public static void UIEnqueueInvoke(DispatcherPriority priority, Delegate action, params object[] args)
        {
            var task = UIDispatcher.InvokeAsync(priority, action, args);
        }

        #endregion

        #region Logging

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
                    MediaElement.RaiseFFmpegMessageLogged(eventArgs);
            }
        }

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
            var eventArgs = new MediaLogMessagEventArgs(sender as MediaElement, messageType, message);
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
        internal static void LogRenderBlock(this MediaElement element, MediaBlock block, TimeSpan clockPosition, int renderIndex)
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
            if (divideBy == 1)
                return $"{ts,10:#,##0}";
            else
                return $"{(ts / divideBy),10:#,##0.000}";
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
            char currentChar = default(char);

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

        /// <summary>
        /// Additions to FFmpeg.Autogen
        /// </summary>
        public static class FFmpeg
        {

            /// <summary>
            /// Gets the FFmpeg error mesage based on the error code
            /// </summary>
            /// <param name="code">The code.</param>
            /// <returns>The decoded error message</returns>
            public static unsafe string GetErrorMessage(int code)
            {
                var errorStrBytes = new byte[1024];
                var errorStrPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(byte)) * errorStrBytes.Length);
                ffmpeg.av_strerror(code, (byte*)errorStrPtr, (ulong)errorStrBytes.Length);
                Marshal.Copy(errorStrPtr, errorStrBytes, 0, errorStrBytes.Length);
                Marshal.FreeHGlobal(errorStrPtr);

                var errorMessage = Encoding.GetEncoding(0).GetString(errorStrBytes).Split('\0').FirstOrDefault();
                return errorMessage;
            }

            #region Ported Macros

            private static int MKTAG(params byte[] buff)
            {
                //  ((a) | ((b) << 8) | ((c) << 16) | ((unsigned)(d) << 24))
                if (BitConverter.IsLittleEndian == false)
                    buff = buff.Reverse().ToArray();

                return BitConverter.ToInt32(buff, 0);
            }

            private static int MKTAG(byte a, char b, char c, char d)
            {
                return MKTAG(new byte[] { a, (byte)b, (byte)c, (byte)d });
            }

            private static int MKTAG(char a, char b, char c, char d)
            {
                return MKTAG(new byte[] { (byte)a, (byte)b, (byte)c, (byte)d });
            }

            #endregion

            public static readonly int AVERROR_EOF = -MKTAG('E', 'O', 'F', ' '); // http://www-numi.fnal.gov/offline_software/srt_public_context/WebDocs/Errors/unix_system_errors.html
            public const long AV_NOPTS = long.MinValue;

            // public static readonly AVRational AV_TIME_BASE_Q = new AVRational { num = 1, den = ffmpeg.AV_TIME_BASE };
            // public const int AVERROR_EAGAIN = -11; // http://www-numi.fnal.gov/offline_software/srt_public_context/WebDocs/Errors/unix_system_errors.html
        }
    }
}
