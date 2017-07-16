namespace Unosquare.FFME
{
    using Core;
    using Decoding;
    using FFmpeg.AutoGen;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Windows;

    /// <summary>
    /// Holds media information about the input, its chapters, programs and individual stream components
    /// </summary>
    public unsafe class MediaInfo
    {
        #region Constructor and Initialization

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaInfo"/> class.
        /// </summary>
        /// <param name="container">The container.</param>
        internal MediaInfo(MediaContainer container)
        {
            // The below logic was implemented using the same ideas conveyed by the following code:
            // https://ffmpeg.org/doxygen/3.2/dump_8c_source.html

            var ic = container.InputContext;
            InputUrl = container.MediaUrl;
            Format = Utils.PtrToString(ic->iformat->name);
            Metadata = container.Metadata;
            Duration = ic->duration != Utils.FFmpeg.AV_NOPTS ?
                ic->duration.ToTimeSpan() :
                TimeSpan.MinValue;
            StartTime = ic->start_time != Utils.FFmpeg.AV_NOPTS ?
                ic->start_time.ToTimeSpan() :
                new TimeSpan?();
            BitRate = ic->bit_rate;

            Streams = new ReadOnlyDictionary<int, StreamInfo>(ExtractStreams(ic).ToDictionary(k => k.StreamIndex, v => v));
            Chapters = new ReadOnlyCollection<ChapterInfo>(ExtractChapters(ic));
            Programs = new ReadOnlyCollection<ProgramInfo>(ExtractPrograms(ic, Streams));
            BestStreams = new ReadOnlyDictionary<AVMediaType, StreamInfo>(FindBestStreams(ic, Streams));
        }

        /// <summary>
        /// Extracts the stream infos from the input.
        /// </summary>
        /// <param name="ic">The ic.</param>
        /// <returns></returns>
        private static List<StreamInfo> ExtractStreams(AVFormatContext* ic)
        {
            var result = new List<StreamInfo>();
            if (ic->streams == null) return result;

            for (var i = 0; i < ic->nb_streams; i++)
            {
                var avObject = ic->streams[i];

                var codecContext = ffmpeg.avcodec_alloc_context3(null);
                ffmpeg.avcodec_parameters_to_context(codecContext, avObject->codecpar);

                // Fields which are missing from AVCodecParameters need to be taken from the AVCodecContext
                codecContext->properties = avObject->codec->properties;
                codecContext->codec = avObject->codec->codec;
                codecContext->qmin = avObject->codec->qmin;
                codecContext->qmax = avObject->codec->qmax;
                codecContext->coded_width = avObject->codec->coded_height;
                codecContext->coded_height = avObject->codec->coded_width;


                var bitsPerSample = codecContext->codec_type == AVMediaType.AVMEDIA_TYPE_AUDIO ?
                    ffmpeg.av_get_bits_per_sample(codecContext->codec_id) : 0;

                var dar = avObject->display_aspect_ratio;
                var sar = avObject->sample_aspect_ratio;
                var codecSar = avObject->codecpar->sample_aspect_ratio;

                if (sar.num != 0 && (sar.num != avObject->codecpar->sample_aspect_ratio.num || sar.den != avObject->codecpar->sample_aspect_ratio.den))
                {
                    ffmpeg.av_reduce(&dar.num, &dar.den,
                        avObject->codecpar->width * avObject->sample_aspect_ratio.num,
                        avObject->codecpar->height * avObject->sample_aspect_ratio.den,
                        1024 * 1024);
                }

                var stream = new StreamInfo
                {
                    StreamId = avObject->id,
                    StreamIndex = avObject->index,
                    Metadata = new ReadOnlyDictionary<string, string>(FFDictionary.ToDictionary(avObject->metadata)),
                    CodecType = codecContext->codec_type,
                    CodecTypeName = ffmpeg.av_get_media_type_string(codecContext->codec_type),
                    Codec = codecContext->codec_id,
                    CodecName = ffmpeg.avcodec_get_name(codecContext->codec_id),
                    CodecProfile = ffmpeg.avcodec_profile_name(codecContext->codec_id, codecContext->profile),
                    ReferenceFrameCount = codecContext->refs,
                    CodecTag = codecContext->codec_tag,
                    PixelFormat = codecContext->pix_fmt,
                    FieldOrder = codecContext->field_order,
                    ColorRange = codecContext->color_range,
                    PixelWidth = codecContext->width,
                    PixelHeight = codecContext->height,
                    HasClosedCaptions = (codecContext->properties & ffmpeg.FF_CODEC_PROPERTY_CLOSED_CAPTIONS) != 0,
                    IsLossless = (codecContext->properties & ffmpeg.FF_CODEC_PROPERTY_LOSSLESS) != 0,
                    BitRate = bitsPerSample > 0 ?
                        bitsPerSample * codecContext->channels * codecContext->sample_rate :
                        codecContext->bit_rate,
                    MaxBitRate = codecContext->rc_max_rate,
                    InfoFrameCount = avObject->codec_info_nb_frames,
                    TimeBase = avObject->time_base,
                    SampleFormat = codecContext->sample_fmt,
                    SampleRate = codecContext->sample_rate,
                    DisplayAspectRatio = dar,
                    SampleAspectRatio = sar,
                    Disposition = avObject->disposition,
                    FPS = avObject->avg_frame_rate.ToDouble(),
                    TBR = avObject->r_frame_rate.ToDouble(),
                    TBN = 1d / avObject->time_base.ToDouble(),
                    TBC = 1d / avObject->codec->time_base.ToDouble(),
                };

                // TODO: I chose not to include Side data but I could easily do so
                // https://ffmpeg.org/doxygen/3.2/dump_8c_source.html
                // dump_sidedata

                ffmpeg.avcodec_free_context(&codecContext);


                result.Add(stream);
            }

            return result;
        }

        /// <summary>
        /// Finds the best streams for audio video, and subtitles.
        /// </summary>
        /// <param name="ic">The ic.</param>
        /// <param name="streams">The streams.</param>
        /// <returns></returns>
        private static Dictionary<AVMediaType, StreamInfo> FindBestStreams(AVFormatContext* ic, ReadOnlyDictionary<int, StreamInfo> streams)
        {

            // Initialize and clear all the stream indexes.
            var streamIndexes = new Dictionary<AVMediaType, int>();

            for (var i = 0; i < (int)AVMediaType.AVMEDIA_TYPE_NB; i++)
                streamIndexes[(AVMediaType)i] = -1;

            { // Find best streams for each component

                // if we passed null instead of the requestedCodec pointer, then
                // find_best_stream would not validate whether a valid decoder is registed.
                AVCodec* requestedCodec = null;

                streamIndexes[AVMediaType.AVMEDIA_TYPE_VIDEO] =
                    ffmpeg.av_find_best_stream(ic, AVMediaType.AVMEDIA_TYPE_VIDEO,
                                        streamIndexes[(int)AVMediaType.AVMEDIA_TYPE_VIDEO], -1,
                                        &requestedCodec, 0);

                streamIndexes[AVMediaType.AVMEDIA_TYPE_AUDIO] =
                    ffmpeg.av_find_best_stream(ic, AVMediaType.AVMEDIA_TYPE_AUDIO,
                                        streamIndexes[AVMediaType.AVMEDIA_TYPE_AUDIO],
                                        streamIndexes[AVMediaType.AVMEDIA_TYPE_VIDEO],
                                        &requestedCodec, 0);

                streamIndexes[AVMediaType.AVMEDIA_TYPE_SUBTITLE] =
                    ffmpeg.av_find_best_stream(ic, AVMediaType.AVMEDIA_TYPE_SUBTITLE,
                                        streamIndexes[AVMediaType.AVMEDIA_TYPE_SUBTITLE],
                                        (streamIndexes[AVMediaType.AVMEDIA_TYPE_AUDIO] >= 0 ?
                                         streamIndexes[AVMediaType.AVMEDIA_TYPE_AUDIO] :
                                         streamIndexes[AVMediaType.AVMEDIA_TYPE_VIDEO]),
                                        &requestedCodec, 0);
            }

            var result = new Dictionary<AVMediaType, StreamInfo>();
            foreach (var kvp in streamIndexes.Where(n => n.Value >= 0))
            {
                result[kvp.Key] = streams[kvp.Value];
            }

            return result;
        }

        /// <summary>
        /// Extracts the chapters from the input.
        /// </summary>
        /// <param name="ic">The ic.</param>
        /// <returns></returns>
        private static List<ChapterInfo> ExtractChapters(AVFormatContext* ic)
        {
            var result = new List<ChapterInfo>();
            if (ic->chapters == null) return result;

            for (var i = 0; i < ic->nb_chapters; i++)
            {
                var avObject = ic->chapters[i];

                var chapter = new ChapterInfo
                {
                    StartTime = avObject->start.ToTimeSpan(avObject->time_base),
                    EndTime = avObject->end.ToTimeSpan(avObject->time_base),
                    Index = i,
                    ChapterId = avObject->id,
                    Metadata = new ReadOnlyDictionary<string, string>(FFDictionary.ToDictionary(avObject->metadata))
                };

                result.Add(chapter);
            }

            return result;
        }

        /// <summary>
        /// Extracts the programs from the input and creates associations between programs and streams.
        /// </summary>
        /// <param name="ic">The ic.</param>
        /// <param name="streams">The streams.</param>
        /// <returns></returns>
        private static List<ProgramInfo> ExtractPrograms(AVFormatContext* ic, ReadOnlyDictionary<int, StreamInfo> streams)
        {
            var result = new List<ProgramInfo>();
            if (ic->programs == null) return result;

            for (var i = 0; i < ic->nb_programs; i++)
            {
                var avObject = ic->programs[i];

                var program = new ProgramInfo
                {
                    Metadata = new ReadOnlyDictionary<string, string>(FFDictionary.ToDictionary(avObject->metadata)),
                    ProgramId = avObject->id,
                    ProgramNumber = avObject->program_num,
                };

                var associatedStreams = new List<StreamInfo>();
                for (var s = 0; s < avObject->nb_stream_indexes; s++)
                {
                    var streamIndex = (int)avObject->stream_index[s];
                    if (streams.ContainsKey(streamIndex))
                        associatedStreams.Add(streams[streamIndex]);
                }

                program.Streams = new ReadOnlyCollection<StreamInfo>(associatedStreams);

                result.Add(program);
            }

            return result;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the input URL string used to access and create the media container
        /// </summary>
        public string InputUrl { get; private set; }

        /// <summary>
        /// Gets the name of the container format.
        /// </summary>
        public string Format { get; private set; }

        /// <summary>
        /// Gets the metadata for the input. This may include stuff like title, creation date, company name, etc.
        /// Individual stream components may contain additional metadata.
        /// The metadata 
        /// </summary>
        public ReadOnlyDictionary<string, string> Metadata { get; private set; }

        /// <summary>
        /// Gets the duration of the input as reported by the container format.
        /// Individual stream components may have different values
        /// </summary>
        public TimeSpan Duration { get; private set; }

        /// <summary>
        /// Gets the start timestamp of the input as reported by the container format.
        /// Individual stream components may have different values
        /// </summary>
        public TimeSpan? StartTime { get; private set; }

        /// <summary>
        /// If available, returns a non-zero value as reported by the container format.
        /// </summary>
        public long BitRate { get; private set; }

        /// <summary>
        /// Gets a list of chapters
        /// </summary>
        public ReadOnlyCollection<ChapterInfo> Chapters { get; private set; }

        /// <summary>
        /// Gets a list of programs with their associated streams.
        /// </summary>
        public ReadOnlyCollection<ProgramInfo> Programs { get; private set; }

        /// <summary>
        /// Gets the dictionary of stream components.
        /// </summary>
        public ReadOnlyDictionary<int, StreamInfo> Streams { get; private set; }

        /// <summary>
        /// Provides access to the best streams of each media type found in the container.
        /// This uses some internal FFmpeg heuristics.
        /// </summary>
        public ReadOnlyDictionary<AVMediaType, StreamInfo> BestStreams { get; private set; }

        #endregion
    }

    /// <summary>
    /// Represents media stream information
    /// </summary>
    public class StreamInfo
    {
        /// <summary>
        /// Gets the stream identifier. This is different from the stream index.
        /// Typically this value is not very useful.
        /// </summary>
        public int StreamId { get; internal set; }

        /// <summary>
        /// Gets the index of the stream.
        /// </summary>
        public int StreamIndex { get; internal set; }

        /// <summary>
        /// Gets the type of the codec.
        /// </summary>
        public AVMediaType CodecType { get; internal set; }

        /// <summary>
        /// Gets the name of the codec type. Audio, Video, Subtitle, Data, etc.
        /// </summary>
        public string CodecTypeName { get; internal set; }

        /// <summary>
        /// Gets the codec identifier.
        /// </summary>
        public AVCodecID Codec { get; internal set; }

        /// <summary>
        /// Gets the name of the codec.
        /// </summary>
        public string CodecName { get; internal set; }

        /// <summary>
        /// Gets the codec profile. Only valid for H.264 or 
        /// video codecs that use profiles. Otherwise empty.
        /// </summary>
        public string CodecProfile { get; internal set; }

        /// <summary>
        /// Gets the codec tag. Not very useful except for fixing bugs with
        /// some demuxer scenarios.
        /// </summary>
        public uint CodecTag { get; internal set; }

        /// <summary>
        /// Gets a value indicating whether this stream has closed captions.
        /// Typically this is set for video streams.
        /// </summary>
        public bool HasClosedCaptions { get; internal set; }

        /// <summary>
        /// Gets a value indicating whether this stream contains lossless compressed data.
        /// </summary>
        public bool IsLossless { get; internal set; }

        /// <summary>
        /// Gets the pixel format. Only valid for Vide streams.
        /// </summary>
        public AVPixelFormat PixelFormat { get; internal set; }

        /// <summary>
        /// Gets the width of the video frames. 
        /// </summary>
        public int PixelWidth { get; internal set; }

        /// <summary>
        /// Gets the height of the video frames.
        /// </summary>
        public int PixelHeight { get; internal set; }

        /// <summary>
        /// Gets the field order. This is useful to determine
        /// if the video needs deinterlacing
        /// </summary>
        public AVFieldOrder FieldOrder { get; internal set; }

        /// <summary>
        /// Gets the video color range.
        /// </summary>
        public AVColorRange ColorRange { get; internal set; }

        /// <summary>
        /// Gets the audio sample rate.
        /// </summary>
        public int SampleRate { get; internal set; }

        /// <summary>
        /// Gets the audio sample format.
        /// </summary>
        public AVSampleFormat SampleFormat { get; internal set; }

        /// <summary>
        /// Gets the stream time base unit in seconds.
        /// </summary>
        public AVRational TimeBase { get; internal set; }

        /// <summary>
        /// Gets the sample aspect ratio.
        /// </summary>
        public AVRational SampleAspectRatio { get; internal set; }

        /// <summary>
        /// Gets the display aspect ratio.
        /// </summary>
        public AVRational DisplayAspectRatio { get; internal set; }

        /// <summary>
        /// Gets the reported bit rate. 9 for unavalable.
        /// </summary>
        public long BitRate { get; internal set; }

        /// <summary>
        /// Gets the maximum bit rate for variable bitrate streams. 0 if unavailable.
        /// </summary>
        public long MaxBitRate { get; internal set; }

        /// <summary>
        /// Gets the number of frames that were read to obtain the stream's information.
        /// </summary>
        public int InfoFrameCount { get; internal set; }

        /// <summary>
        /// Gets the number of reference frames.
        /// </summary>
        public int ReferenceFrameCount { get; internal set; }

        /// <summary>
        /// Gets the average FPS reported by the stream.
        /// </summary>
        public double FPS { get; internal set; }

        /// <summary>
        /// Gets the real (base) framerate of the stream
        /// </summary>
        public double TBR { get; internal set; }

        /// <summary>
        /// Gets the fundamental unit of time in 1/seconds used to represent timestamps in the stream, according to the stream data
        /// </summary>
        public double TBN { get; internal set; }

        /// <summary>
        /// Gets the fundamental unit of time in 1/seconds used to represent timestamps in the stream ,accoring to the codec
        /// </summary>
        public double TBC { get; internal set; }

        /// <summary>
        /// Gets the disposition flags.
        /// Please see ffmpeg.AV_DISPOSITION_* fields.
        /// </summary>
        public int Disposition { get; internal set; }

        /// <summary>
        /// Gets the stream's metadata.
        /// </summary>
        public ReadOnlyDictionary<string, string> Metadata { get; internal set; }

        /// <summary>
        /// Gets the language string from the stream's metadata.
        /// </summary>
        public string Language
        {
            get
            {
                if (Metadata.ContainsKey("language")) return Metadata["language"];
                return string.Empty;
            }
        }
    }

    /// <summary>
    /// Represents a chapter within a container
    /// </summary>
    public class ChapterInfo
    {
        /// <summary>
        /// Gets the chapter index.
        /// </summary>
        public int Index { get; internal set; }

        /// <summary>
        /// Gets the chapter identifier.
        /// </summary>
        public int ChapterId { get; internal set; }

        /// <summary>
        /// Gets the start time of the chapter.
        /// </summary>
        public TimeSpan StartTime { get; internal set; }

        /// <summary>
        /// Gets the end time of the chapter.
        /// </summary>
        public TimeSpan EndTime { get; internal set; }

        /// <summary>
        /// Gets the chapter metadata.
        /// </summary>
        public ReadOnlyDictionary<string, string> Metadata { get; internal set; }
    }

    /// <summary>
    /// Represents a program and its associated streams within a container.
    /// </summary>
    public class ProgramInfo
    {
        /// <summary>
        /// Gets the program number.
        /// </summary>
        public int ProgramNumber { get; internal set; }

        /// <summary>
        /// Gets the program identifier.
        /// </summary>
        public int ProgramId { get; internal set; }

        /// <summary>
        /// Gets the program metadata.
        /// </summary>
        public ReadOnlyDictionary<string, string> Metadata { get; internal set; }

        /// <summary>
        /// Gets the associated program streams.
        /// </summary>
        public ReadOnlyCollection<StreamInfo> Streams { get; internal set; }

        /// <summary>
        /// Gets the name of the program. Empty if unavailable.
        /// </summary>
        public string Name
        {
            get
            {
                if (Metadata.ContainsKey("name")) return Metadata["name"];
                return string.Empty;
            }
        }
    }

}
