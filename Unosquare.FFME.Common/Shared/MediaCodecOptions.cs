namespace Unosquare.FFME.Shared
{
    using Core;
    using FFmpeg.AutoGen;
    using System.Collections.Generic;

    /// <summary>
    /// Represents a set of codec options associated with a stream specifier.
    /// </summary>
    public class MediaCodecOptions
    {
        #region Private Members

        /// <summary>
        /// Holds the internal list of option items
        /// </summary>
        private readonly List<CodecOption> Options = new List<CodecOption>();

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaCodecOptions"/> class.
        /// </summary>
        public MediaCodecOptions()
        {
            // Placeholder
        }

        #endregion

        #region Methods

        /// <summary>
        /// Adds an option
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="streamType">Type of the stream.</param>
        public void Add(string key, string value, char streamType)
        {
            var option = new CodecOption(new StreamSpecifier(CharToMediaType(streamType)), key, value);
            Options.Add(option);
        }

        /// <summary>
        /// Adds an option
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="streamIndex">Index of the stream.</param>
        public void Add(string key, string value, int streamIndex)
        {
            var option = new CodecOption(new StreamSpecifier(streamIndex), key, value);
            Options.Add(option);
        }

        /// <summary>
        /// Adds an option
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="streamType">Type of the stream.</param>
        /// <param name="streamIndex">Index of the stream.</param>
        public void Add(string key, string value, char streamType, int streamIndex)
        {
            var option = new CodecOption(new StreamSpecifier(CharToMediaType(streamType), streamIndex), key, value);
            Options.Add(option);
        }

        /// <summary>
        /// Retrieves a dictionary with the options for the specified codec.
        /// Port of filter_codec_opts
        /// </summary>
        /// <param name="codecId">The codec identifier.</param>
        /// <param name="format">The format.</param>
        /// <param name="stream">The stream.</param>
        /// <param name="codec">The codec.</param>
        /// <returns>The filtered options</returns>
        internal unsafe FFDictionary FilterOptions(AVCodecID codecId, AVFormatContext* format, AVStream* stream, AVCodec* codec)
        {
            var result = new FFDictionary();

            if (codec == null)
            {
                codec = (format->oformat != null) ?
                    ffmpeg.avcodec_find_encoder(codecId) : ffmpeg.avcodec_find_decoder(codecId);
            }

            var codecClass = ffmpeg.avcodec_get_class();

            var flags = format->oformat != null ?
                ffmpeg.AV_OPT_FLAG_ENCODING_PARAM : ffmpeg.AV_OPT_FLAG_DECODING_PARAM;

            var streamType = (char)0;

            switch (stream->codecpar->codec_type)
            {
                case AVMediaType.AVMEDIA_TYPE_VIDEO:
                    streamType = 'v';
                    flags |= ffmpeg.AV_OPT_FLAG_VIDEO_PARAM;
                    break;
                case AVMediaType.AVMEDIA_TYPE_AUDIO:
                    streamType = 'a';
                    flags |= ffmpeg.AV_OPT_FLAG_AUDIO_PARAM;
                    break;
                case AVMediaType.AVMEDIA_TYPE_SUBTITLE:
                    streamType = 's';
                    flags |= ffmpeg.AV_OPT_FLAG_SUBTITLE_PARAM;
                    break;
            }

            foreach (var optionItem in Options)
            {
                // Inline port of check_stream_specifier
                var matched = ffmpeg.avformat_match_stream_specifier(format, stream, optionItem.StreamSpecifier.ToString()) > 0;
                if (matched == false) continue;

                if (ffmpeg.av_opt_find(&codecClass, optionItem.Key, null, flags, ffmpeg.AV_OPT_SEARCH_FAKE_OBJ) != null || codec == null
                   || (codec->priv_class != null && ffmpeg.av_opt_find(&codec->priv_class, optionItem.Key, null, flags, ffmpeg.AV_OPT_SEARCH_FAKE_OBJ) != null))
                {
                    result[optionItem.Key] = optionItem.Value;
                }
                else if (optionItem.StreamSpecifier.StreamSuffix[0] == streamType && ffmpeg.av_opt_find(&codecClass, optionItem.Key, null, flags, ffmpeg.AV_OPT_SEARCH_FAKE_OBJ) != null)
                {
                    result[optionItem.Key] = optionItem.Value;
                }
            }

            return result;
        }

        /// <summary>
        /// Retrieves an array of dictionaries, one for each stream index
        /// https://ffmpeg.org/ffplay.html#toc-Options
        /// Port of setup_find_stream_info_opts.
        /// </summary>
        /// <param name="format">The format.</param>
        /// <returns>The options per stream</returns>
        internal unsafe FFDictionary[] GetPerStreamOptions(AVFormatContext* format)
        {
            if (format->nb_streams == 0)
                return null;

            var result = new FFDictionary[format->nb_streams];
            for (var i = 0; i < format->nb_streams; i++)
                result[i] = FilterOptions(format->streams[i]->codecpar->codec_id, format, format->streams[i], null);

            return result;
        }

        /// <summary>
        /// Converts a character to a media type.
        /// </summary>
        /// <param name="c">The c.</param>
        /// <returns>The media type</returns>
        private static MediaType CharToMediaType(char c)
        {
            if (c == 'v') return MediaType.Video;
            if (c == 'a') return MediaType.Audio;
            if (c == 's') return MediaType.Subtitle;

            return MediaType.None;
        }

        #endregion

        /// <summary>
        /// Well-known codec option names
        /// </summary>
        public static class Names
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
