namespace Unosquare.FFME.Core
{
    using FFmpeg.AutoGen;
    using Shared;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
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
        private static readonly unsafe av_log_set_callback_callback FFmpegLogCallback = LoggingWorker.OnFFmpegMessageLogged;

        private static readonly unsafe av_lockmgr_register_cb FFmpegLockManagerCallback = FFmpegManageLocking;
        private static readonly Dictionary<IntPtr, ManualResetEvent> FFmpegOpDone = new Dictionary<IntPtr, ManualResetEvent>();

        private static bool HasFFmpegRegistered = false;
        private static string FFmpegRegisterPath = null;

        #endregion

        #region Properties

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

                MediaEngine.Platform.NativeMethods.SetDllDirectory(ffmpegPath);

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
                ffmpeg.av_log_set_level(MediaEngine.Platform.IsInDebugMode ? ffmpeg.AV_LOG_VERBOSE : ffmpeg.AV_LOG_WARNING);
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

        /// <summary>
        /// Gets the FFmpeg error mesage based on the error code
        /// </summary>
        /// <param name="code">The code.</param>
        /// <returns>The decoded error message</returns>
        public static unsafe string DecodeFFmpegMessage(int code)
        {
            var bufferSize = 1024;
            var buffer = stackalloc byte[bufferSize];
            ffmpeg.av_strerror(code, buffer, (ulong)bufferSize);
            var message = Marshal.PtrToStringAnsi((IntPtr)buffer);
            return message;
        }

        #endregion

        #region Private Methods and Callbacks

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

        #endregion
    }
}
