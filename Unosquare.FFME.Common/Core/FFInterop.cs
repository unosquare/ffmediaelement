namespace Unosquare.FFME.Core
{
    using FFmpeg.AutoGen;
    using Shared;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;

    /// <summary>
    /// Provides a set of utilities to perfrom logging, text formatting, 
    /// conversion and other handy calculations.
    /// </summary>
    internal static class FFInterop
    {
        #region Private Declarations

        private static readonly object FFmpegRegisterLock = new object();
        private static bool HasFFmpegRegistered = false;
        private static string FFmpegRegisterPath = null;

        #endregion

        #region Interop

        /// <summary>
        /// Gets the FFmpeg error mesage based on the error code
        /// </summary>
        /// <param name="errorCode">The code.</param>
        /// <returns>The decoded error message</returns>
        public static unsafe string DecodeMessage(int errorCode)
        {
            var bufferSize = 1024;
            var buffer = stackalloc byte[bufferSize];
            ffmpeg.av_strerror(errorCode, buffer, (ulong)bufferSize);
            var message = Marshal.PtrToStringAnsi((IntPtr)buffer);
            return message;
        }

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
        /// <param name="bitwiseFlagIdentifiers">The bitwaise flag identifiers corresponding to the libraries.</param>
        /// <returns>
        /// Returns the path that FFmpeg was registered from.
        /// </returns>
        /// <exception cref="FileNotFoundException">When ffmpeg libraries are not found</exception>
        /// <exception cref="System.IO.FileNotFoundException">When the folder is not found</exception>
        public static unsafe string RegisterFFmpeg(string overridePath, int bitwiseFlagIdentifiers)
        {
            lock (FFmpegRegisterLock)
            {
                if (HasFFmpegRegistered)
                    return FFmpegRegisterPath;

                // Get the temporary path where FFmpeg binaries are located
                var ffmpegPath = string.IsNullOrWhiteSpace(overridePath) == false ?
                    Path.GetFullPath(overridePath) : Defaults.EntryAssemblyPath;

                // Sometimes we need to set the DLL directory even if we try to load the
                // library from the full path. In some Windows systems we get error 126
                MediaEngine.Platform.NativeMethods.SetDllDirectory(ffmpegPath);

                // Load the minimum set of FFmpeg binaries
                foreach (var lib in FFLibrary.All)
                {
                    if ((lib.FlagId & bitwiseFlagIdentifiers) != 0)
                        lib.Load(ffmpegPath);
                }

                // Check if libraries were loaded correctly
                if (FFLibrary.All.All(lib => lib.IsLoaded == false))
                {
                    // Reset the search path
                    MediaEngine.Platform.NativeMethods.SetDllDirectory(null);
                    throw new FileNotFoundException($"Unable to load FFmpeg binaries from folder '{ffmpegPath}'.");
                }

                if (FFLibrary.LibAVDevice.IsLoaded)
                    ffmpeg.avdevice_register_all();

                if (FFLibrary.LibAVFilter.IsLoaded)
                    ffmpeg.avfilter_register_all();

                ffmpeg.av_register_all();
                ffmpeg.avcodec_register_all();
                ffmpeg.avformat_network_init();

                LoggingWorker.ConnectToFFmpeg();
                FFLockManager.Register();

                // Reset the search path
                MediaEngine.Platform.NativeMethods.SetDllDirectory(null);

                HasFFmpegRegistered = true;
                FFmpegRegisterPath = ffmpegPath;
                return FFmpegRegisterPath;
            }
        }

        #endregion
    }
}
