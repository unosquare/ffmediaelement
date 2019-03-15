﻿namespace Unosquare.FFME.Core
{
    using Engine;
    using FFmpeg.AutoGen;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;

    /// <summary>
    /// Provides a set of utilities to perform logging, text formatting,
    /// conversion and other handy calculations.
    /// </summary>
    internal static unsafe class FFInterop
    {
        #region Private Declarations

        private static readonly object FFmpegLogBufferSyncLock = new object();
        private static readonly List<string> FFmpegLogBuffer = new List<string>(1024);
        private static readonly ReadOnlyDictionary<int, MediaLogMessageType> FFmpegLogLevels =
            new ReadOnlyDictionary<int, MediaLogMessageType>(
                new Dictionary<int, MediaLogMessageType>
                {
                    { ffmpeg.AV_LOG_DEBUG, MediaLogMessageType.Debug },
                    { ffmpeg.AV_LOG_ERROR, MediaLogMessageType.Error },
                    { ffmpeg.AV_LOG_FATAL, MediaLogMessageType.Error },
                    { ffmpeg.AV_LOG_INFO, MediaLogMessageType.Info },
                    { ffmpeg.AV_LOG_PANIC, MediaLogMessageType.Error },
                    { ffmpeg.AV_LOG_TRACE, MediaLogMessageType.Trace },
                    { ffmpeg.AV_LOG_WARNING, MediaLogMessageType.Warning }
                });

        private static readonly object SyncLock = new object();
        private static readonly List<OptionMeta> EmptyOptionMetaList = new List<OptionMeta>(0);
        private static readonly av_log_set_callback_callback FFmpegLogCallback = OnFFmpegMessageLogged;
        private static readonly ILoggingHandler LoggingHandler = new FFLoggingHandler();
        private static bool m_IsInitialized;
        private static string m_LibrariesPath = string.Empty;
        private static int m_LibraryIdentifiers;
        private static byte[] TempStringBuffer = new byte[512 * 1024]; // a temp buffer of 512kB
        private static int TempByteLength;

        #endregion

        #region Properties

        /// <summary>
        /// True when libraries were initialized correctly.
        /// </summary>
        public static bool IsInitialized
        {
            get { lock (SyncLock) { return m_IsInitialized; } }
        }

        /// <summary>
        /// Gets the libraries path. Only filled when initialized correctly.
        /// </summary>
        public static string LibrariesPath
        {
            get { lock (SyncLock) { return m_LibrariesPath; } }
        }

        /// <summary>
        /// Gets the bitwise FFmpeg library identifiers that were loaded.
        /// </summary>
        public static int LibraryIdentifiers
        {
            get { lock (SyncLock) { return m_LibraryIdentifiers; } }
        }

        #endregion

        #region FFmpeg Registration

        /// <summary>
        /// Registers FFmpeg library and initializes its components.
        /// It only needs to be called once but calling it more than once
        /// has no effect. Returns the path that FFmpeg was registered from.
        /// This method is thread-safe.
        /// </summary>
        /// <param name="overridePath">The override path.</param>
        /// <param name="libIdentifiers">The bit-wise flag identifiers corresponding to the libraries.</param>
        /// <returns>
        /// Returns true if it was a new initialization and it succeeded. False if there was no need to initialize
        /// as there is already a valid initialization.
        /// </returns>
        /// <exception cref="FileNotFoundException">When ffmpeg libraries are not found</exception>
        public static bool Initialize(string overridePath, int libIdentifiers)
        {
            lock (SyncLock)
            {
                if (m_IsInitialized)
                    return false;

                try
                {
                    // Get the temporary path where FFmpeg binaries are located
                    var ffmpegPath = string.IsNullOrWhiteSpace(overridePath) == false ?
                        Path.GetFullPath(overridePath) : Constants.FFmpegSearchPath;

                    var registrationIds = 0;

                    // Sometimes we need to set the DLL directory even if we try to load the
                    // library from the full path. In some Windows systems we get error 126 if we don't
                    MediaEngine.Platform.NativeMethods.SetDllDirectory(ffmpegPath);

                    // Load FFmpeg binaries by Library ID
                    foreach (var lib in FFLibrary.All)
                    {
                        if ((lib.FlagId & libIdentifiers) != 0 && lib.Load(ffmpegPath))
                            registrationIds |= lib.FlagId;
                    }

                    // Check if libraries were loaded correctly
                    if (FFLibrary.All.All(lib => lib.IsLoaded == false))
                    {
                        // Reset the search path
                        MediaEngine.Platform.NativeMethods.SetDllDirectory(null);
                        throw new FileNotFoundException($"Unable to load FFmpeg binaries from folder '{ffmpegPath}'.");
                    }

                    // Additional library initialization
                    if (FFLibrary.LibAVDevice.IsLoaded) ffmpeg.avdevice_register_all();

                    // Set logging levels and callbacks
                    ffmpeg.av_log_set_flags(ffmpeg.AV_LOG_SKIP_REPEATED);
                    ffmpeg.av_log_set_level(MediaEngine.Platform.IsInDebugMode ? ffmpeg.AV_LOG_VERBOSE : ffmpeg.AV_LOG_WARNING);
                    ffmpeg.av_log_set_callback(FFmpegLogCallback);

                    // set the static environment properties
                    m_LibrariesPath = ffmpegPath;
                    m_LibraryIdentifiers = registrationIds;
                    m_IsInitialized = true;
                }
                catch
                {
                    m_LibrariesPath = string.Empty;
                    m_LibraryIdentifiers = 0;
                    m_IsInitialized = false;

                    // rethrow the exception with the original stack trace.
                    throw;
                }
                finally
                {
                    // Reset the search path after registration
                    MediaEngine.Platform.NativeMethods.SetDllDirectory(null);
                }

                return m_IsInitialized;
            }
        }

        #endregion

        #region Interop Helper Methods

        /// <summary>
        /// Gets the FFmpeg error message based on the error code
        /// </summary>
        /// <param name="errorCode">The code.</param>
        /// <returns>The decoded error message</returns>
        public static unsafe string DecodeMessage(int errorCode)
        {
            var bufferSize = 1024;
            var buffer = stackalloc byte[bufferSize];
            ffmpeg.av_strerror(errorCode, buffer, (ulong)bufferSize);
            var message = PtrToStringUTF8(buffer);
            return message;
        }

        /// <summary>
        /// Converts a byte pointer to a UTF8 encoded string.
        /// </summary>
        /// <param name="stringAddress">The pointer to the starting character</param>
        /// <returns>The string</returns>
        public static unsafe string PtrToStringUTF8(byte* stringAddress)
        {
            lock (SyncLock)
            {
                if (stringAddress == null) return null;
                if (*stringAddress == 0) return string.Empty;
                var stringPointer = (IntPtr)stringAddress;

                TempByteLength = 0;
                while (true)
                {
                    if (Marshal.ReadByte(stringPointer, TempByteLength) == 0)
                        break;

                    TempByteLength += 1;
                }

                if (TempStringBuffer == null || TempStringBuffer.Length < TempByteLength)
                    TempStringBuffer = new byte[TempByteLength];

                Marshal.Copy(stringPointer, TempStringBuffer, 0, TempByteLength);
                return Encoding.UTF8.GetString(TempStringBuffer, 0, TempByteLength);
            }
        }

        /// <summary>
        /// Retrieves the options information associated with the given AVClass.
        /// </summary>
        /// <param name="avClass">The av class.</param>
        /// <returns>A list of option metadata</returns>
        public static unsafe List<OptionMeta> RetrieveOptions(AVClass* avClass)
        {
            // see: https://github.com/FFmpeg/FFmpeg/blob/e0f32286861ddf7666ba92297686fa216d65968e/tools/enum_options.c
            var result = new List<OptionMeta>(128);
            if (avClass == null) return result;

            var option = avClass->option;

            while (option != null)
            {
                if (option->type != AVOptionType.AV_OPT_TYPE_CONST)
                    result.Add(new OptionMeta(option));

                option = ffmpeg.av_opt_next(avClass, option);
            }

            return result;
        }

        /// <summary>
        /// Retrieves the codecs.
        /// </summary>
        /// <returns>The codecs</returns>
        public static unsafe AVCodec*[] RetrieveCodecs()
        {
            var result = new List<IntPtr>(1024);
            void* iterator;
            AVCodec* item;
            while ((item = ffmpeg.av_codec_iterate(&iterator)) != null)
            {
                result.Add((IntPtr)item);
            }

            var collection = new AVCodec*[result.Count];
            for (var i = 0; i < result.Count; i++)
            {
                collection[i] = (AVCodec*)result[i];
            }

            return collection;
        }

        /// <summary>
        /// Retrieves the input format names.
        /// </summary>
        /// <returns>The collection of names</returns>
        public static unsafe List<string> RetrieveInputFormatNames()
        {
            var result = new List<string>(128);
            void* iterator;
            AVInputFormat* item;
            while ((item = ffmpeg.av_demuxer_iterate(&iterator)) != null)
            {
                result.Add(PtrToStringUTF8(item->name));
            }

            return result;
        }

        /// <summary>
        /// Retrieves the decoder names.
        /// </summary>
        /// <param name="allCodecs">All codecs.</param>
        /// <returns>The collection of names</returns>
        public static unsafe List<string> RetrieveDecoderNames(AVCodec*[] allCodecs)
        {
            var codecNames = new List<string>(allCodecs.Length);
            foreach (var c in allCodecs)
            {
                if (ffmpeg.av_codec_is_decoder(c) != 0)
                    codecNames.Add(PtrToStringUTF8(c->name));
            }

            return codecNames;
        }

        /// <summary>
        /// Retrieves the global format options.
        /// </summary>
        /// <returns>The collection of option infos</returns>
        public static unsafe List<OptionMeta> RetrieveGlobalFormatOptions() =>
            RetrieveOptions(ffmpeg.avformat_get_class());

        /// <summary>
        /// Retrieves the global codec options.
        /// </summary>
        /// <returns>The collection of option infos</returns>
        public static unsafe List<OptionMeta> RetrieveGlobalCodecOptions() =>
            RetrieveOptions(ffmpeg.avcodec_get_class());

        /// <summary>
        /// Retrieves the input format options.
        /// </summary>
        /// <param name="formatName">Name of the format.</param>
        /// <returns>The collection of option infos</returns>
        public static unsafe List<OptionMeta> RetrieveInputFormatOptions(string formatName)
        {
            var item = ffmpeg.av_find_input_format(formatName);
            return item == null ? EmptyOptionMetaList : RetrieveOptions(item->priv_class);
        }

        /// <summary>
        /// Retrieves the codec options.
        /// </summary>
        /// <param name="codec">The codec.</param>
        /// <returns>The collection of option infos</returns>
        public static unsafe List<OptionMeta> RetrieveCodecOptions(AVCodec* codec) =>
            RetrieveOptions(codec->priv_class);

        /// <summary>
        /// Log message callback from ffmpeg library.
        /// </summary>
        /// <param name="p0">The p0.</param>
        /// <param name="level">The level.</param>
        /// <param name="format">The format.</param>
        /// <param name="vl">The vl.</param>
        private static unsafe void OnFFmpegMessageLogged(void* p0, int level, string format, byte* vl)
        {
            const int lineSize = 1024;
            lock (FFmpegLogBufferSyncLock)
            {
                if (level > ffmpeg.av_log_get_level()) return;
                var lineBuffer = stackalloc byte[lineSize];
                var printPrefix = 1;
                ffmpeg.av_log_format_line(p0, level, format, vl, lineBuffer, lineSize, &printPrefix);
                var line = PtrToStringUTF8(lineBuffer);
                FFmpegLogBuffer.Add(line);

                var messageType = MediaLogMessageType.Debug;
                if (FFmpegLogLevels.ContainsKey(level))
                    messageType = FFmpegLogLevels[level];

                if (!line.EndsWith("\n", StringComparison.InvariantCulture)) return;
                line = string.Join(string.Empty, FFmpegLogBuffer);
                line = line.TrimEnd();
                FFmpegLogBuffer.Clear();
                Logging.Log(LoggingHandler, messageType, Aspects.FFmpegLog, line);
            }
        }

        #endregion

        #region Supporting Classes

        /// <summary>
        /// Handles FFmpeg library messages
        /// </summary>
        /// <seealso cref="ILoggingHandler" />
        internal class FFLoggingHandler : ILoggingHandler
        {
            /// <inheritdoc />
            void ILoggingHandler.HandleLogMessage(MediaLogMessage message) =>
                MediaEngine.Platform?.HandleFFmpegLogMessage(message);
        }

        #endregion
    }
}
