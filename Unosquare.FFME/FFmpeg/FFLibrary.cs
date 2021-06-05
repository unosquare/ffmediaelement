namespace FFmpeg.AutoGen
{
    using FFmpeg.AutoGen.Native;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Defines FFmpeg library metadata and access.
    /// It allows for the loading of individual libraries.
    /// </summary>
    internal class FFLibrary
    {
        #region Private Members

        /// <summary>
        /// The load lock preventing libraries to load at the same time.
        /// </summary>
        private static readonly object LoadLock = new object();

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes static members of the <see cref="FFLibrary"/> class.
        /// </summary>
        static FFLibrary()
        {
            // Populate libraries in order of dependency (from least dependent to more dependent)
            All = new List<FFLibrary>(16)
            {
                LibAVUtil,
                LibSWResample,
                LibSWScale,
                LibAVCodec,
                LibAVFormat,
                LibPostProc,
                LibAVFilter,
                LibAVDevice
            };
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FFLibrary" /> class.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="version">The version.</param>
        /// <param name="flagId">The flag identifier.</param>
        private FFLibrary(string name, int version, int flagId)
        {
            Name = name;
            Version = version;
            FlagId = flagId;
        }

        #endregion

        #region Static Properties

        /// <summary>
        /// Gets all the libraries as a collection.
        /// </summary>
        public static IReadOnlyList<FFLibrary> All { get; }

        /// <summary>
        /// Gets the AVCodec library.
        /// </summary>
        public static FFLibrary LibAVCodec { get; } = new FFLibrary(Names.AVCodec, 58, 1);

        /// <summary>
        /// Gets the AVFormat library.
        /// </summary>
        public static FFLibrary LibAVFormat { get; } = new FFLibrary(Names.AVFormat, 58, 2);

        /// <summary>
        /// Gets the AVUtil library.
        /// </summary>
        public static FFLibrary LibAVUtil { get; } = new FFLibrary(Names.AVUtil, 56, 4);

        /// <summary>
        /// Gets the SW Resample library.
        /// </summary>
        public static FFLibrary LibSWResample { get; } = new FFLibrary(Names.SWResample, 3, 8);

        /// <summary>
        /// Gets the SWScale library.
        /// </summary>
        public static FFLibrary LibSWScale { get; } = new FFLibrary(Names.SWScale, 5, 16);

        /// <summary>
        /// Gets the AVDevice library.
        /// </summary>
        public static FFLibrary LibAVDevice { get; } = new FFLibrary(Names.AVDevice, 58, 32);

        /// <summary>
        /// Gets the Post-processing library.
        /// </summary>
        public static FFLibrary LibPostProc { get; } = new FFLibrary(Names.PostProc, 55, 64);

        /// <summary>
        /// Gets the AVFilter library.
        /// </summary>
        public static FFLibrary LibAVFilter { get; } = new FFLibrary(Names.AVFilter, 7, 128);

        #endregion

        #region Instance Properties

        /// <summary>
        /// Gets the flag identifier.
        /// </summary>
        public int FlagId { get; }

        /// <summary>
        /// Gets the name of the library.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the base path from where the library was loaded.
        /// Returns null if it has not been loaded.
        /// </summary>
        public string BasePath { get; private set; }

        /// <summary>
        /// Gets the library version.
        /// </summary>
        public int Version { get; }

        /// <summary>
        /// Gets the pointer reference to the library.
        /// </summary>>
        public IntPtr Reference { get; private set; } = IntPtr.Zero;

        /// <summary>
        /// Gets a value indicating whether the library has already been loaded.
        /// </summary>
        public bool IsLoaded => Reference != IntPtr.Zero;

        /// <summary>
        /// Gets the load error code. 0 for success.
        /// </summary>
        public int LoadErrorCode { get; private set; }

        #endregion

        #region Methods

        /// <summary>
        /// Loads the library from the specified path.
        /// </summary>
        /// <returns>True if the registration was successful.</returns>
        /// <exception cref="InvalidOperationException">When library has already been loaded.</exception>
        public bool Load()
        {
            lock (LoadLock)
            {
                if (Reference != IntPtr.Zero)
                    return true;

                var result = LibraryLoader.LoadNativeLibrary(ffmpeg.RootPath, Name, Version);

                if (result == IntPtr.Zero)
                    return false;

                Reference = result;
                BasePath = ffmpeg.RootPath;
                LoadErrorCode = 0;
                return true;
            }
        }

        #endregion

        #region Constants

        /// <summary>
        /// Defines the library names as constants.
        /// </summary>
        private static class Names
        {
            public const string AVCodec = "avcodec";
            public const string AVFilter = "avfilter";
            public const string AVFormat = "avformat";
            public const string AVUtil = "avutil";
            public const string SWResample = "swresample";
            public const string SWScale = "swscale";
            public const string AVDevice = "avdevice";
            public const string PostProc = "postproc";
        }

        #endregion
    }
}
