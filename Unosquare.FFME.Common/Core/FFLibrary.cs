namespace Unosquare.FFME.Core
{
    using FFmpeg.AutoGen.Native;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;

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
            All = new ReadOnlyCollection<FFLibrary>(new List<FFLibrary>
            {
                LibAVCodec,
                LibAVFilter,
                LibAVFormat,
                LibAVUtil,
                LibSWResample,
                LibSWScale,
                LibAVDevice
            });

            MinimumSet = new ReadOnlyCollection<FFLibrary>(All.Where(l => l.IsRequired).ToList());
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FFLibrary"/> class.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="version">The version.</param>
        /// <param name="isRequired">if set to <c>true</c> [is required].</param>
        private FFLibrary(string name, int version, bool isRequired)
        {
            Name = name;
            Version = version;
            IsRequired = isRequired;
        }

        #endregion

        #region Static Properties

        /// <summary>
        /// Gets all the libraries as a collection.
        /// </summary>
        public static ReadOnlyCollection<FFLibrary> All { get; }

        /// <summary>
        /// Gets the minimum required set of libraries as a collection.
        /// </summary>
        public static ReadOnlyCollection<FFLibrary> MinimumSet { get; }

        /// <summary>
        /// Gets the AVCodec library.
        /// </summary>
        public static FFLibrary LibAVCodec { get; } = new FFLibrary(Names.AVCodec, 57, true);

        /// <summary>
        /// Gets the AVFilter library.
        /// </summary>
        public static FFLibrary LibAVFilter { get; } = new FFLibrary(Names.AVFilter, 6, false);

        /// <summary>
        /// Gets the AVFormat library.
        /// </summary>
        public static FFLibrary LibAVFormat { get; } = new FFLibrary(Names.AVFormat, 57, true);

        /// <summary>
        /// Gets the AVUtil library.
        /// </summary>
        public static FFLibrary LibAVUtil { get; } = new FFLibrary(Names.AVUtil, 55, true);

        /// <summary>
        /// Gets the SWResample library.
        /// </summary>
        public static FFLibrary LibSWResample { get; } = new FFLibrary(Names.SWResample, 2, true);

        /// <summary>
        /// Gets the SWScale library.
        /// </summary>
        public static FFLibrary LibSWScale { get; } = new FFLibrary(Names.SWScale, 4, false);

        /// <summary>
        /// Gets the AVDevice library.
        /// </summary>
        public static FFLibrary LibAVDevice { get; } = new FFLibrary(Names.AVDevice, 57, false);

        #endregion

        #region Instance Properties

        /// <summary>
        /// Gets a value indicating whether the library is part of the minimum required set.
        /// </summary>
        public bool IsRequired { get; }

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
        public bool IsAvailable => Reference != IntPtr.Zero;

        #endregion

        #region Methods

        /// <summary>
        /// Loads the library from the specified path.
        /// </summary>
        /// <param name="basePath">The base path.</param>
        /// <returns>True if the registration was successful</returns>
        /// <exception cref="InvalidOperationException">When library has already been loaded.</exception>
        public bool Load(string basePath)
        {
            lock (LoadLock)
            {
                if (Reference != IntPtr.Zero)
                    throw new InvalidOperationException($"Library {Name} was already loaded.");

                var result = LibraryLoader.LoadNativeLibraryUsingPlatformNamingConvention(basePath, Name, Version);
                if (result != IntPtr.Zero)
                {
                    Reference = result;
                    BasePath = basePath;
                    return true;
                }

                return false;
            }
        }

        #endregion

        #region Constants

        /// <summary>
        /// Defines the library names as constants
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
        }

        #endregion
    }
}
