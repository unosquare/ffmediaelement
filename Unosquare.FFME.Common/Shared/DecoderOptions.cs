﻿namespace Unosquare.FFME.Shared
{
    using Core;
    using System.Collections.Generic;

    /// <summary>
    /// Represents decoder global and private options for all streams
    /// </summary>
    public sealed class DecoderOptions
    {
        private readonly Dictionary<string, string> GlobalOptions = new Dictionary<string, string>(64);
        private readonly Dictionary<int, Dictionary<string, string>> PrivateOptions = new Dictionary<int, Dictionary<string, string>>();

        /// <summary>
        /// Initializes a new instance of the <see cref="DecoderOptions"/> class.
        /// </summary>
        internal DecoderOptions()
        {
            Threads = "auto";
        }

        /// <summary>
        /// Gets or sets a value indicating whether [enable low resource].
        /// In theroy this should be 0,1,2,3 for 1, 1/2, 1,4 and 1/8 resolutions.
        /// TODO: We are for now just supporting 1/2 resolution (true value)
        /// Port of lowres.
        /// </summary>
        public bool EnableLowRes { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether [enable fast decoding].
        /// Port of fast
        /// </summary>
        public bool EnableFastDecoding { get; set; } = false;

        /// <summary>
        /// Enables low_delay flag for low latency streaming.
        /// </summary>
        public bool EnableLowDelay { get; set; } = false;

        /// <summary>
        /// Gets or sets the threads.
        /// </summary>
        public string Threads
        {
            get => this[GlobalOptionNames.Threads];
            set => this[GlobalOptionNames.Threads] = value;
        }

        /// <summary>
        /// Gets or sets whether to use reference counted frames.
        /// </summary>
        public string RefCountedFrames
        {
            get => this[GlobalOptionNames.RefCountedFrames];
            set => this[GlobalOptionNames.RefCountedFrames] = value;
        }

        /// <summary>
        /// Gets or sets the index of the low resolution index.
        /// </summary>
        internal string LowResIndex
        {
            get => this[GlobalOptionNames.LowRes];
            set => this[GlobalOptionNames.LowRes] = value;
        }

        /// <summary>
        /// Gets or sets the specified global option.
        /// See: https://www.ffmpeg.org/ffmpeg-codecs.html#Codec-Options
        /// </summary>
        /// <param name="globalOptionName">Name of the global option.</param>
        /// <returns>The value of the option</returns>
        public string this[string globalOptionName]
        {
            get => GlobalOptions.ContainsKey(globalOptionName) ? GlobalOptions[globalOptionName] : null;
            set => GlobalOptions[globalOptionName] = value;
        }

        /// <summary>
        /// Gets or sets the specified private option
        /// See: https://www.ffmpeg.org/ffmpeg-codecs.html#toc-Decoders
        /// </summary>
        /// <param name="streamIndex">Index of the stream.</param>
        /// <param name="privateOptionName">Name of the private option.</param>
        /// <returns>The private option value</returns>
        public string this[int streamIndex, string privateOptionName]
        {
            get
            {
                if (PrivateOptions.ContainsKey(streamIndex) == false) return null;
                if (PrivateOptions[streamIndex].ContainsKey(privateOptionName) == false) return null;
                return PrivateOptions[streamIndex][privateOptionName];
            }
            set
            {
                if (PrivateOptions.ContainsKey(streamIndex) == false)
                    PrivateOptions[streamIndex] = new Dictionary<string, string>();

                PrivateOptions[streamIndex][privateOptionName] = value;
            }
        }

        /// <summary>
        /// Gets the combined global and private stream codec options as a dictionary.
        /// </summary>
        /// <param name="streamIndex">Index of the stream.</param>
        /// <returns>An options dictionary</returns>
        internal FFDictionary GetStreamCodecOptions(int streamIndex)
        {
            var result = new Dictionary<string, string>(GlobalOptions);
            if (PrivateOptions.ContainsKey(streamIndex))
            {
                foreach (var kvp in PrivateOptions[streamIndex])
                {
                    result[kvp.Key] = kvp.Value;
                }
            }

            return new FFDictionary(result);
        }

        /// <summary>
        /// Well-known codec option names
        /// </summary>
        private static class GlobalOptionNames
        {
            /// <summary>
            /// The threads
            /// </summary>
            public const string Threads = "threads";

            /// <summary>
            /// The reference counted frames
            /// </summary>
            public const string RefCountedFrames = "refcounted_frames";

            /// <summary>
            /// The low resource
            /// </summary>
            public const string LowRes = "lowres";
        }
    }
}
