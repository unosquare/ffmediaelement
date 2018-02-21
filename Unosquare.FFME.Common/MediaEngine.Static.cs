namespace Unosquare.FFME
{
    using Core;
    using Decoding;
    using Primitives;
    using Shared;
    using System.Collections.Generic;

    public partial class MediaEngine
    {
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
                if (tempContainer.Components.Main == null || tempContainer.Components.Main.MediaType != sourceType)
                    throw new MediaContainerException($"Could not find a stream of type '{sourceType}' to load blocks from");

                // read all the packets and decode them
                var outputFrames = new List<MediaFrame>(1024 * 8);
                while (tempContainer.IsAtEndOfStream == false)
                {
                    tempContainer.Read();
                    var frames = tempContainer.Decode();
                    foreach (var frame in frames)
                    {
                        if (frame.MediaType != sourceType)
                            continue;

                        outputFrames.Add(frame);
                    }
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
    }
}
