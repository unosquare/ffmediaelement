﻿namespace Unosquare.FFME
{
    using Core;
    using Decoding;
    using FFmpeg.AutoGen;
    using Primitives;
    using Shared;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;

    public partial class MediaEngine
    {
        #region Private Fields

        private static readonly string NotInitializedErrorMessage = $"{nameof(MediaEngine)} not initialized";

        /// <summary>
        /// The initialize lock
        /// </summary>
        private static readonly object InitLock = new object();

        /// <summary>
        /// The has intialized flag
        /// </summary>
        private static bool IsIntialized = default;

        /// <summary>
        /// The ffmpeg directory
        /// </summary>
        private static string m_FFmpegDirectory = Constants.FFmpegSearchPath;

        /// <summary>
        /// Stores the load mode flags
        /// </summary>
        private static int m_FFmpegLoadModeFlags = FFmpegLoadMode.FullFeatures;

        private static ReadOnlyCollection<string> m_InputFormatNames;

        private static ReadOnlyCollection<OptionMeta> m_GlobalInputFormatOptions;

        private static ReadOnlyDictionary<string, ReadOnlyCollection<OptionMeta>> m_InputFormatOptions;

        private static ReadOnlyCollection<string> m_DecoderNames;

        private static ReadOnlyCollection<OptionMeta> m_GlobalDecoderOptions;

        private static ReadOnlyDictionary<string, ReadOnlyCollection<OptionMeta>> m_DecoderOptions;

        private static unsafe AVCodec*[] m_AllCodecs;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the platform-specific implementation requirements.
        /// </summary>
        public static IPlatform Platform { get; private set; }

        /// <summary>
        /// Gets or sets the FFmpeg path from which to load the FFmpeg binaries.
        /// You must set this path before setting the Source property for the first time on any instance of this control.
        /// Settng this property when FFmpeg binaries have been registered will have no effect.
        /// </summary>
        public static string FFmpegDirectory
        {
            get => m_FFmpegDirectory;
            set
            {
                if (FFInterop.IsInitialized)
                    return;

                m_FFmpegDirectory = value;
            }
        }

        /// <summary>
        /// Gets the FFmpeg version information. Returns null
        /// when the libraries have not been loaded.
        /// </summary>
        public static string FFmpegVersionInfo
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets or sets the bitwise library identifiers to load.
        /// If FFmpeg is already loaded, the value cannot be changed.
        /// </summary>
        public static int FFmpegLoadModeFlags
        {
            get
            {
                return m_FFmpegLoadModeFlags;
            }
            set
            {
                if (FFInterop.IsInitialized)
                    return;

                m_FFmpegLoadModeFlags = value;
            }
        }

        /// <summary>
        /// Gets the registered FFmpeg input format names.
        /// </summary>
        /// <exception cref="InvalidOperationException">When the MediaEngine has not been initialized</exception>
        public static ReadOnlyCollection<string> InputFormatNames
        {
            get
            {
                lock (InitLock)
                {
                    if (IsIntialized == false)
                        throw new InvalidOperationException(NotInitializedErrorMessage);

                    if (m_InputFormatNames == null)
                        m_InputFormatNames = new ReadOnlyCollection<string>(FFInterop.RetrieveInputFormatNames());

                    return m_InputFormatNames;
                }
            }
        }

        /// <summary>
        /// Gets the global input format options information.
        /// </summary>
        /// <exception cref="InvalidOperationException">When the MediaEngine has not been initialized</exception>
        public static ReadOnlyCollection<OptionMeta> InputFormatOptionsGlobal
        {
            get
            {
                lock (InitLock)
                {
                    if (IsIntialized == false)
                        throw new InvalidOperationException(NotInitializedErrorMessage);

                    if (m_GlobalInputFormatOptions == null)
                    {
                        m_GlobalInputFormatOptions = new ReadOnlyCollection<OptionMeta>(
                            FFInterop.RetrieveGlobalFormatOptions().ToArray());
                    }

                    return m_GlobalInputFormatOptions;
                }
            }
        }

        /// <summary>
        /// Gets the input format options.
        /// </summary>
        /// <exception cref="InvalidOperationException">When the MediaEngine has not been initialized</exception>
        public static ReadOnlyDictionary<string, ReadOnlyCollection<OptionMeta>> InputFormatOptions
        {
            get
            {
                lock (InitLock)
                {
                    if (IsIntialized == false)
                        throw new InvalidOperationException(NotInitializedErrorMessage);

                    if (m_InputFormatOptions == null)
                    {
                        var result = new Dictionary<string, ReadOnlyCollection<OptionMeta>>(InputFormatNames.Count);
                        foreach (var formatName in InputFormatNames)
                        {
                            var optionsInfo = FFInterop.RetrieveInputFormatOptions(formatName);
                            result[formatName] = new ReadOnlyCollection<OptionMeta>(optionsInfo);
                        }

                        m_InputFormatOptions = new ReadOnlyDictionary<string, ReadOnlyCollection<OptionMeta>>(result);
                    }

                    return m_InputFormatOptions;
                }
            }
        }

        /// <summary>
        /// Gets the registered FFmpeg decoder codec names.
        /// </summary>
        /// <exception cref="InvalidOperationException">When the MediaEngine has not been initialized</exception>
        public static unsafe ReadOnlyCollection<string> DecoderNames
        {
            get
            {
                lock (InitLock)
                {
                    if (IsIntialized == false)
                        throw new InvalidOperationException(NotInitializedErrorMessage);

                    if (m_DecoderNames == null)
                        m_DecoderNames = new ReadOnlyCollection<string>(FFInterop.RetrieveDecoderNames(AllCodecs));

                    return m_DecoderNames;
                }
            }
        }

        /// <summary>
        /// Gets the global options that apply to all decoders
        /// </summary>
        /// <exception cref="InvalidOperationException">When the MediaEngine has not been initialized</exception>
        public static ReadOnlyCollection<OptionMeta> DecoderOptionsGlobal
        {
            get
            {
                lock (InitLock)
                {
                    if (IsIntialized == false)
                        throw new InvalidOperationException(NotInitializedErrorMessage);

                    if (m_GlobalDecoderOptions == null)
                    {
                        m_GlobalDecoderOptions = new ReadOnlyCollection<OptionMeta>(
                            FFInterop.RetrieveGlobalCodecOptions().Where(o => o.IsDecodingOption).ToArray());
                    }

                    return m_GlobalDecoderOptions;
                }
            }
        }

        /// <summary>
        /// Gets the decoder specific options.
        /// </summary>
        /// <exception cref="InvalidOperationException">When the MediaEngine has not been initialized</exception>
        public static unsafe ReadOnlyDictionary<string, ReadOnlyCollection<OptionMeta>> DecoderOptions
        {
            get
            {
                lock (InitLock)
                {
                    if (IsIntialized == false)
                        throw new InvalidOperationException(NotInitializedErrorMessage);

                    if (m_DecoderOptions == null)
                    {
                        var result = new Dictionary<string, ReadOnlyCollection<OptionMeta>>(DecoderNames.Count);
                        foreach (var c in AllCodecs)
                        {
                            if (c->decode.Pointer == IntPtr.Zero)
                                continue;

                            result[FFInterop.PtrToStringUTF8(c->name)] =
                                new ReadOnlyCollection<OptionMeta>(FFInterop.RetrieveCodecOptions(c));
                        }

                        m_DecoderOptions = new ReadOnlyDictionary<string, ReadOnlyCollection<OptionMeta>>(result);
                    }

                    return m_DecoderOptions;
                }
            }
        }

        /// <summary>
        /// Gets all registered encoder and decoder codecs.
        /// </summary>
        /// <exception cref="InvalidOperationException">When the MediaEngine has not been initialized</exception>
        internal static unsafe AVCodec*[] AllCodecs
        {
            get
            {
                lock (InitLock)
                {
                    if (IsIntialized == false)
                        throw new InvalidOperationException(NotInitializedErrorMessage);

                    if (m_AllCodecs == null)
                        m_AllCodecs = FFInterop.RetriveCodecs();

                    return m_AllCodecs;
                }
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Initializes the MedieElementCore.
        /// </summary>
        /// <param name="platform">The platform-specific implementation.</param>
        public static void Initialize(IPlatform platform)
        {
            lock (InitLock)
            {
                if (IsIntialized)
                    return;

                Platform = platform;
                IsIntialized = true;
            }
        }

        /// <summary>
        /// Forces the preloading of the FFmpeg libraries according to the values of the
        /// <see cref="FFmpegDirectory"/> and <see cref="FFmpegLoadModeFlags"/>
        /// Also, sets the <see cref="FFmpegVersionInfo"/> property. Thorws an exception
        /// if the libraries cannot be loaded.
        /// </summary>
        /// <returns>true if libraries were loaded, false if libraries were already loaded.</returns>
        public static bool LoadFFmpeg()
        {
            if (FFInterop.Initialize(FFmpegDirectory, FFmpegLoadModeFlags))
            {
                // Set the folders and lib identifiers
                FFmpegDirectory = FFInterop.LibrariesPath;
                FFmpegLoadModeFlags = FFInterop.LibraryIdentifiers;
                FFmpegVersionInfo = ffmpeg.av_version_info();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Retrieves the media information including all streams, chapters and programs.
        /// </summary>
        /// <param name="sourceUrl">The source URL.</param>
        /// <returns>The contants of the media information.</returns>
        public static MediaInfo RetrieveMediaInfo(string sourceUrl)
        {
            using (var container = new MediaContainer(sourceUrl, null, null))
            {
                return container.MediaInfo;
            }
        }

        /// <summary>
        /// Reads all the blocks of the specified media type from the source url.
        /// </summary>
        /// <param name="sourceUrl">The subtitles URL.</param>
        /// <param name="sourceType">Type of the source.</param>
        /// <param name="parent">The parent.</param>
        /// <returns>A buffer containing all the blocks</returns>
        internal static MediaBlockBuffer LoadBlocks(string sourceUrl, MediaType sourceType, IMediaLogger parent)
        {
            using (var tempContainer = new MediaContainer(sourceUrl, null, parent))
            {
                // Skip reading and decoding unused blocks
                tempContainer.MediaOptions.IsAudioDisabled = sourceType != MediaType.Audio;
                tempContainer.MediaOptions.IsVideoDisabled = sourceType != MediaType.Video;
                tempContainer.MediaOptions.IsSubtitleDisabled = sourceType != MediaType.Subtitle;

                // Open the container
                tempContainer.Open();
                if (tempContainer.Components.Main == null || tempContainer.Components.MainMediaType != sourceType)
                    throw new MediaContainerException($"Could not find a stream of type '{sourceType}' to load blocks from");

                // read all the packets and decode them
                var outputFrames = new List<MediaFrame>(1024 * 8);
                while (true)
                {
                    tempContainer.Read();
                    var frames = tempContainer.Decode();
                    foreach (var frame in frames)
                    {
                        if (frame.MediaType != sourceType)
                            continue;

                        outputFrames.Add(frame);
                    }

                    if (frames.Count <= 0 && tempContainer.IsAtEndOfStream)
                        break;
                }

                // Build the result
                var result = new MediaBlockBuffer(outputFrames.Count, sourceType);
                foreach (var frame in outputFrames)
                {
                    result.Add(frame, tempContainer);
                }

                tempContainer.Close();
                return result;
            }
        }

        #endregion
    }
}
