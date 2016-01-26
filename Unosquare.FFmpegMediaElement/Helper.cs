namespace Unosquare.FFmpegMediaElement
{
    using FFmpeg.AutoGen;
    using System;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Windows;

    /// <summary>
    /// Provides methods and constants for miscellaneous operations
    /// </summary>
    internal static class Helper
    {

        /// <summary>
        /// Miscellaneous native methods
        /// </summary>
        public static class NativeMethods
        {
            [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern bool SetDllDirectory(string lpPathName);

            [DllImport("kernel32.dll")]
            public static extern void RtlMoveMemory(IntPtr dest, IntPtr src, uint len);
        }

        static private bool HasRegistered = false;
        static private bool? designTime;

        static private readonly object RegisterLock = new object();

        /// <summary>
        /// Extracts the FFmpeg Dlls.
        /// </summary>
        /// <param name="resourcePrefix">The resource prefix.</param>
        /// <returns></returns>
        private static string ExtractFFmpegDlls(string resourcePrefix)
        {
            var assembly = typeof(Helper).Assembly;
            var resourceNames = assembly.GetManifestResourceNames().Where(r => r.Contains(resourcePrefix)).ToArray();
            var targetDirectory = Path.Combine(Path.GetTempPath(), assembly.GetName().Name, assembly.GetName().Version.ToString(), resourcePrefix);

            if (Directory.Exists(targetDirectory) == false)
                Directory.CreateDirectory(targetDirectory);

            foreach (var dllResourceName in resourceNames)
            {
                var dllFilenameParts = dllResourceName.Split(new string[] { "." }, StringSplitOptions.RemoveEmptyEntries);
                var dllFilename = dllFilenameParts[dllFilenameParts.Length - 2] + "." + dllFilenameParts[dllFilenameParts.Length - 1];
                var targetFileName = Path.Combine(targetDirectory, dllFilename);

                if (File.Exists(targetFileName))
                    continue;

                byte[] dllContents = null;

                // read the contents of the resource into a byte array
                using (var stream = assembly.GetManifestResourceStream(dllResourceName))
                {
                    dllContents = new byte[(int)stream.Length];
                    stream.Read(dllContents, 0, Convert.ToInt32(stream.Length));
                }

                // check the hash and overwrite the file if the file does not exist.
                File.WriteAllBytes(targetFileName, dllContents);

            }

            // This now holds the name of the temp directory where files got extracted.
            var directoryInfo = new System.IO.DirectoryInfo(targetDirectory);
            return directoryInfo.FullName;
        }

        private static string AssemblyLocation
        {
            get
            {
                return System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
            }
        }

        /// <summary>
        /// Registers FFmpeg library and initializes its components.
        /// </summary>
        /// <exception cref="System.BadImageFormatException"></exception>
        public static void RegisterFFmpeg()
        {
            lock (RegisterLock)
            {
                if (HasRegistered)
                    return;

                var resourceFolderName = string.Empty;
                var assemblyMachineType = typeof(Helper).Assembly.GetName().ProcessorArchitecture;
                if (assemblyMachineType == ProcessorArchitecture.X86 || assemblyMachineType == ProcessorArchitecture.MSIL || assemblyMachineType == ProcessorArchitecture.Amd64)
                    resourceFolderName = "ffmpeg32";
                else
                    throw new BadImageFormatException(
                        string.Format("Cannot load FFmpeg for architecture '{0}'", assemblyMachineType.ToString()));

                MediaElement.FFmpegPaths.BasePath = ExtractFFmpegDlls(resourceFolderName);
                MediaElement.FFmpegPaths.FFmpeg = Path.Combine(MediaElement.FFmpegPaths.BasePath, "ffmpeg.exe");
                MediaElement.FFmpegPaths.FFplay = Path.Combine(MediaElement.FFmpegPaths.BasePath, "ffplay.exe");
                MediaElement.FFmpegPaths.FFprobe = Path.Combine(MediaElement.FFmpegPaths.BasePath, "ffprobe.exe");

                NativeMethods.SetDllDirectory(MediaElement.FFmpegPaths.BasePath);

                ffmpeg.avcodec_register_all();

                //FFmpegInvoke.avdevice_register_all();
                //FFmpegInvoke.avfilter_register_all();

                ffmpeg.av_register_all();
                ffmpeg.avformat_network_init();

                HasRegistered = true;
            }

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNoPtsValue(long timestamp)
        {
            return Convert.ToDouble(timestamp) == -Convert.ToDouble(0x8000000000000000L);
        }

        public static long RoundTicks(long ticks)
        {
            //return ticks;
            return Convert.ToInt64((Convert.ToDouble(ticks) / 1000d)) * 1000;
        }

        public static decimal RoundSeconds(decimal seconds)
        {
            //return seconds;
            return Math.Round(seconds, 4);
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
                if (!designTime.HasValue)
                {
                    designTime = (bool)DesignerProperties.IsInDesignModeProperty.GetMetadata(
                          typeof(DependencyObject)).DefaultValue;
                }
                return designTime.Value;
            }
        }

        /// <summary>
        /// Converts a Timestamp to seconds.
        /// </summary>
        /// <param name="ts">The ts.</param>
        /// <param name="streamTimeBase">The stream time base.</param>
        /// <returns></returns>
        public static decimal TimestampToSeconds(long ts, AVRational streamTimeBase)
        {
            return Convert.ToDecimal(Convert.ToDouble(ts) * Convert.ToDouble(streamTimeBase.num) / Convert.ToDouble(streamTimeBase.den));
        }

        /// <summary>
        /// Converts seconds to a timestamp value.
        /// </summary>
        /// <param name="seconds">The seconds.</param>
        /// <param name="streamTimeBase">The stream time base.</param>
        /// <returns></returns>
        public static long SecondsToTimestamp(decimal seconds, AVRational streamTimeBase)
        {
            return Convert.ToInt64(Convert.ToDouble(seconds) * Convert.ToDouble(streamTimeBase.den) / Convert.ToDouble(streamTimeBase.num));
        }

        /// <summary>
        /// Gets the FFmpeg error mesage based on the error code
        /// </summary>
        /// <param name="code">The code.</param>
        /// <returns></returns>
        public static unsafe string GetFFmpegErrorMessage(int code)
        {
            var errorStrBytes = new byte[1024];
            var errorStrPtr = Marshal.AllocHGlobal(System.Runtime.InteropServices.Marshal.SizeOf(typeof(sbyte)) * errorStrBytes.Length);

            //var errorStrPtr = &errorStr;
            ffmpeg.av_strerror(code, errorStrPtr, (ulong)errorStrBytes.Length);
            Marshal.Copy(errorStrPtr, errorStrBytes, 0, errorStrBytes.Length);
            Marshal.FreeHGlobal(errorStrPtr);

            var errorMessage = System.Text.Encoding.ASCII.GetString(errorStrBytes).Split('\0').FirstOrDefault();
            return errorMessage;
        }
    }

}
