namespace Unosquare.FFME.Core
{
    using Decoding;
    using FFmpeg.AutoGen;
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Windows;

    /// <summary>
    /// Provides a set of utilities to perfrom conversion and other
    /// handly calculations
    /// </summary>
    internal static class Utils
    {
        #region Private Declarations

        static private bool HasFFmpegRegistered = false;
        static private bool? isInDesignTime;
        static private readonly object FFmpegRegisterLock = new object();
        static private string FFmpegRegisterPath = null;
        static private bool? isInDebugMode;
        static unsafe private readonly av_log_set_callback_callback FFmpegLogCallback = FFmpegLog;
        static private readonly object LogSyncLock = new object();
        #endregion

        #region Interop

        /// <summary>
        /// Sets the DLL directory in which external dependencies can be located.
        /// </summary>
        /// <param name="lpPathName">the full path.</param>
        /// <returns></returns>
        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool SetDllDirectory(string lpPathName);

        /// <summary>
        /// Fast pointer memory block copy function
        /// </summary>
        /// <param name="destination">The destination.</param>
        /// <param name="source">The source.</param>
        /// <param name="length">The length.</param>
        [DllImport("kernel32")]
        public static extern void CopyMemory(IntPtr destination, IntPtr source, uint length);

        /// <summary>
        /// Converts a byte pointer to a string
        /// </summary>
        /// <param name="bytePtr">The byte PTR.</param>
        /// <returns></returns>
        public static unsafe string PtrToString(byte* bytePtr)
        {
            return Marshal.PtrToStringAnsi(new IntPtr(bytePtr));
        }

        /// <summary>
        /// Converts a byte pointer to a UTF8 encoded string.
        /// </summary>
        /// <param name="bytePtr">The byte PTR.</param>
        /// <returns></returns>
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

        /// <summary>
        /// Gets the FFmpeg error mesage based on the error code
        /// </summary>
        /// <param name="code">The code.</param>
        /// <returns></returns>
        public static unsafe string FFErrorMessage(int code)
        {
            var errorStrBytes = new byte[1024];
            var errorStrPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(byte)) * errorStrBytes.Length);
            ffmpeg.av_strerror(code, (byte*)errorStrPtr, (ulong)errorStrBytes.Length);
            Marshal.Copy(errorStrPtr, errorStrBytes, 0, errorStrBytes.Length);
            Marshal.FreeHGlobal(errorStrPtr);

            var errorMessage = Encoding.GetEncoding(0).GetString(errorStrBytes).Split('\0').FirstOrDefault();
            return errorMessage;
        }

        #endregion

        #region Math 

        /// <summary>
        /// Converts the given value to a value that is of the given multiple. 
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="multiple">The multiple.</param>
        /// <returns></returns>
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
        /// <returns></returns>
        public static TimeSpan ToTimeSpan(this double pts, AVRational timeBase)
        {
            if (double.IsNaN(pts) || pts == ffmpeg.AV_NOPTS)
                return TimeSpan.MinValue;

            if (timeBase.den == 0)
                return TimeSpan.FromTicks((long)Math.Round(TimeSpan.TicksPerMillisecond * 1000 * pts / ffmpeg.AV_TIME_BASE, 0)); //) .FromSeconds(pts / ffmpeg.AV_TIME_BASE);

            return TimeSpan.FromTicks((long)Math.Round(TimeSpan.TicksPerMillisecond * 1000 * pts * timeBase.num / timeBase.den, 0)); //pts * timeBase.num / timeBase.den);
        }

        /// <summary>
        /// Gets a timespan given a timestamp and a timebase.
        /// </summary>
        /// <param name="pts">The PTS.</param>
        /// <param name="timeBase">The time base.</param>
        /// <returns></returns>
        public static TimeSpan ToTimeSpan(this long pts, AVRational timeBase)
        {
            return ((double)pts).ToTimeSpan(timeBase);
        }

        /// <summary>
        /// Gets a timespan given a timestamp and a timebase.
        /// </summary>
        /// <param name="pts">The PTS in seconds.</param>
        /// <param name="timeBase">The time base.</param>
        /// <returns></returns>
        public static TimeSpan ToTimeSpan(this double pts, double timeBase)
        {
            if (double.IsNaN(pts) || pts == ffmpeg.AV_NOPTS)
                return TimeSpan.MinValue;

            return TimeSpan.FromTicks((long)Math.Round(TimeSpan.TicksPerMillisecond * 1000 * pts / timeBase, 0)); //pts / timeBase);
        }

        /// <summary>
        /// Gets a timespan given a timestamp and a timebase.
        /// </summary>
        /// <param name="pts">The PTS.</param>
        /// <param name="timeBase">The time base.</param>
        /// <returns></returns>
        public static TimeSpan ToTimeSpan(this long pts, double timeBase)
        {
            return ((double)pts).ToTimeSpan(timeBase);
        }

        /// <summary>
        /// Gets a timespan given a timestamp (in AV_TIME_BASE units)
        /// </summary>
        /// <param name="pts">The PTS.</param>
        /// <returns></returns>
        public static TimeSpan ToTimeSpan(this double pts)
        {
            return ToTimeSpan(pts, ffmpeg.AV_TIME_BASE);
        }

        /// <summary>
        /// Gets a timespan given a timestamp (in AV_TIME_BASE units)
        /// </summary>
        /// <param name="pts">The PTS.</param>
        /// <returns></returns>
        public static TimeSpan ToTimeSpan(this long pts)
        {
            return ((double)pts).ToTimeSpan();
        }

        /// <summary>
        /// Converts a fraction to a double
        /// </summary>
        /// <param name="rational">The rational.</param>
        /// <returns></returns>
        public static double ToDouble(this AVRational rational)
        {
            return (double)rational.num / rational.den;
        }

        /// <summary>
        /// Rounds the ticks.
        /// </summary>
        /// <param name="ticks">The ticks.</param>
        /// <returns></returns>
        public static long RoundTicks(this long ticks)
        {
            //return ticks;
            return Convert.ToInt64((Convert.ToDouble(ticks) / 1000d)) * 1000;
        }

        /// <summary>
        /// Rounds the seconds to 4 decimals.
        /// </summary>
        /// <param name="seconds">The seconds.</param>
        /// <returns></returns>
        public static decimal RoundSeconds(this decimal seconds)
        {
            //return seconds;
            return Math.Round(seconds, 4);
        }

        #endregion

        #region Registration

        /// <summary>
        /// Gets the assembly location.
        /// </summary>
        /// <value>
        /// The assembly location.
        /// </value>
        private static string AssemblyLocation
        {
            get
            {
                return Path.GetFullPath(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location));
            }
        }

        /// <summary>
        /// Registers FFmpeg library and initializes its components.
        /// It only needs to be called once but calling it more than once
        /// has no effect. Returns the path that FFmpeg was registered from.
        /// </summary>
        /// <param name="overridePath">The override path.</param>
        /// <returns>Returns the path that FFmpeg was registered from.</returns>
        /// <exception cref="System.IO.FileNotFoundException"></exception>
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

                SetDllDirectory(ffmpegPath);


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

                HasFFmpegRegistered = true;

                FFmpegRegisterPath = ffmpegPath;
                return FFmpegRegisterPath;
            }

        }

        #endregion


        #region Misc

        /// <summary>
        /// Logs the specified message.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="messageType">Type of the message.</param>
        /// <param name="message">The message.</param>
        /// <exception cref="System.ArgumentNullException">sender</exception>
        internal static void Log(object sender, MediaLogMessageType messageType, string message)
        {
            lock (LogSyncLock)
            {
                if (sender == null) throw new ArgumentNullException(nameof(sender));

                try
                {
                    var eventArgs = new MediaLogMessagEventArgs(messageType, message);
                    if (sender != null && sender is MediaElement)
                        (sender as MediaElement)?.RaiseMessageLogged(eventArgs);
                    else
                        MediaElement.RaiseFFmpegMessageLogged(eventArgs);

                }
                catch { }
            }
        }

        /// <summary>
        /// The FFmpeg log buffer
        /// </summary>
        private static readonly List<string> FFmpegLogBuffer = new List<string>();

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
                    line = string.Join("", FFmpegLogBuffer);
                    line = line.TrimEnd();
                    FFmpegLogBuffer.Clear();
                    Utils.Log(typeof(MediaElement), messageType, line);
                }
            }

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
                ($"{block.MediaType.ToString().Substring(0, 1)} "
                    + $"BLK: {block.StartTime.Debug()} | "
                    + $"CLK: {clockPosition.Debug()} | "
                    + $"DFT: {drift.TotalMilliseconds,4:0} | "
                    + $"IX: {renderIndex,3} | "
                    + $"PQ: {element.Container?.Components[block.MediaType]?.PacketBufferLength / 1024d,7:0.0}k | "
                    + $"TQ: {element.Container?.Components.PacketBufferLength / 1024d,7:0.0}k"));
            }
            catch
            {
                // swallow
            }
        }

        /// <summary>
        /// Returns a formatted timestamp string in Seconds
        /// </summary>
        /// <param name="ts">The ts.</param>
        /// <returns></returns>
        internal static string Debug(this TimeSpan ts)
        {
            if (ts == TimeSpan.MinValue)
                return $"{"N/A",10}";
            else
                return $"{ts.TotalSeconds,10:0.000}";
        }

        /// <summary>
        /// Returns a formatted string fro elapsed stopwatch milliseconds
        /// </summary>
        /// <param name="sw">The sw.</param>
        /// <returns></returns>
        internal static string Debug(this Stopwatch sw)
        {
            return $"{sw.ElapsedMilliseconds,5}";
        }

        /// <summary>
        /// Returns a formatted string with elapsed milliseconds between now and
        /// the specified date.
        /// </summary>
        /// <param name="dt">The dt.</param>
        /// <returns></returns>
        internal static string DebugElapsedUtc(this DateTime dt)
        {
            return $"{DateTime.UtcNow.Subtract(dt).TotalMilliseconds,6:0}";
        }

        /// <summary>
        /// Returns a fromatted string, dividing by the specified
        /// factor. Useful for debugging longs with byte positions or sizes.
        /// </summary>
        /// <param name="ts">The ts.</param>
        /// <param name="divideBy">The divide by.</param>
        /// <returns></returns>
        internal static string Debug(this long ts, double divideBy = 1)
        {
            if (divideBy == 1)
                return $"{ts,10:#,##0}";
            else
                return $"{(ts / divideBy),10:#,##0.000}";
        }

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
                if (!isInDesignTime.HasValue)
                {
                    isInDesignTime = (bool)DesignerProperties.IsInDesignModeProperty.GetMetadata(
                          typeof(DependencyObject)).DefaultValue;
                }
                return isInDesignTime.Value;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this instance is in debug mode.
        /// </summary>
        public static bool IsInDebugMode
        {
            get
            {
                if (!isInDebugMode.HasValue)
                    isInDebugMode = Debugger.IsAttached;

                return isInDebugMode.Value;
            }
        }

        #endregion

    }

}
