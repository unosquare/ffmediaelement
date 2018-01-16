namespace Unosquare.FFME
{
    using Core;
    using Shared;

    public partial class MediaEngine
    {
        /// <summary>
        /// The initialize lock
        /// </summary>
        private static readonly object InitLock = new object();

        /// <summary>
        /// The has intialized flag
        /// </summary>
        private static bool IsIntialized = default(bool);

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
    }
}
