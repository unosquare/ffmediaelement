namespace FFmpeg.AutoGen
{
    using System.Runtime.InteropServices;
    using Unosquare.FFME.Core;

    internal unsafe static partial class ffmpeg
    {

        /// <summary>
        /// Gets the maximum lowres value for a codec.
        /// </summary>
        /// <param name="codec">The codec.</param>
        /// <returns></returns>
        [DllImport(Constants.DllAVCodec, EntryPoint = nameof(av_codec_get_max_lowres), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int av_codec_get_max_lowres(AVCodec* @codec);

        /// <summary>
        /// Sets the codec's lowres value
        /// </summary>
        /// <param name="avctx">The avctx.</param>
        /// <param name="val">The value.</param>
        [DllImport(Constants.DllAVCodec, EntryPoint = nameof(av_codec_set_lowres), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern void av_codec_set_lowres(AVCodecContext* @avctx, int @val);

        /// <summary>
        /// Sets the packet tamebase
        /// </summary>
        /// <param name="avctx">The avctx.</param>
        /// <param name="val">The value.</param>
        [DllImport(Constants.DllAVCodec, EntryPoint = nameof(av_codec_set_pkt_timebase), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern void av_codec_set_pkt_timebase(AVCodecContext* @avctx, AVRational @val);

        /// <summary>Copy packet, including contents</summary>
        [DllImport(Constants.DllAVCodec, EntryPoint = nameof(av_copy_packet), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int av_copy_packet(AVPacket* @dst, AVPacket* @src);

        /// <summary>Allocate an AVPacket and set its fields to default values. The resulting struct must be freed using av_packet_free().</summary>
        [DllImport(Constants.DllAVCodec, EntryPoint = nameof(av_packet_alloc), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern AVPacket* av_packet_alloc();

        /// <summary>Free the packet, if the packet is reference counted, it will be unreferenced first.</summary>
        [DllImport(Constants.DllAVCodec, EntryPoint = nameof(av_packet_free), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern void av_packet_free(AVPacket** @pkt);

        /// <summary>Allocate an AVCodecContext and set its fields to default values. The resulting struct should be freed with avcodec_free_context().</summary>
        /// <param name="codec">if non-NULL, allocate private data and initialize defaults for the given codec. It is illegal to then call avcodec_open2() with a different codec. If NULL, then the codec-specific defaults won&apos;t be initialized, which may result in suboptimal default settings (this is important mainly for encoders, e.g. libx264).</param>
        [DllImport(Constants.DllAVCodec, EntryPoint = nameof(avcodec_alloc_context3), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern AVCodecContext* avcodec_alloc_context3(AVCodec* @codec);

        /// <summary>Decode a subtitle message. Return a negative value on error, otherwise return the number of bytes used. If no subtitle could be decompressed, got_sub_ptr is zero. Otherwise, the subtitle is stored in *sub. Note that AV_CODEC_CAP_DR1 is not available for subtitle codecs. This is for simplicity, because the performance difference is expect to be negligible and reusing a get_buffer written for video codecs would probably perform badly due to a potentially very different allocation pattern.</summary>
        /// <param name="avctx">the codec context</param>
        /// <param name="sub">The Preallocated AVSubtitle in which the decoded subtitle will be stored, must be freed with avsubtitle_free if *got_sub_ptr is set.</param>
        /// <param name="got_sub_ptr">Zero if no subtitle could be decompressed, otherwise, it is nonzero.</param>
        /// <param name="avpkt">The input AVPacket containing the input buffer.</param>
        [DllImport(Constants.DllAVCodec, EntryPoint = nameof(avcodec_decode_subtitle2), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int avcodec_decode_subtitle2(AVCodecContext* @avctx, AVSubtitle* @sub, int* @got_sub_ptr, AVPacket* @avpkt);

        /// <summary>Find a registered decoder with a matching codec ID.</summary>
        /// <param name="id">AVCodecID of the requested decoder</param>
        [DllImport(Constants.DllAVCodec, EntryPoint = nameof(avcodec_find_decoder), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern AVCodec* avcodec_find_decoder(AVCodecID @id);

        /// <summary>Find a registered encoder with a matching codec ID.</summary>
        /// <param name="id">AVCodecID of the requested encoder</param>
        [DllImport(Constants.DllAVCodec, EntryPoint = nameof(avcodec_find_encoder), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern AVCodec* avcodec_find_encoder(AVCodecID @id);

        /// <summary>Reset the internal decoder state / flush internal buffers. Should be called e.g. when seeking or when switching to a different stream.</summary>
        [DllImport(Constants.DllAVCodec, EntryPoint = nameof(avcodec_flush_buffers), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern void avcodec_flush_buffers(AVCodecContext* @avctx);

        /// <summary>Free the codec context and everything associated with it and write NULL to the provided pointer.</summary>
        [DllImport(Constants.DllAVCodec, EntryPoint = nameof(avcodec_free_context), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern void avcodec_free_context(AVCodecContext** @avctx);

        /// <summary>Get the AVClass for AVCodecContext. It can be used in combination with AV_OPT_SEARCH_FAKE_OBJ for examining options.</summary>
        [DllImport(Constants.DllAVCodec, EntryPoint = nameof(avcodec_get_class), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern AVClass* avcodec_get_class();

        /// <summary>Get the name of a codec.</summary>
        [DllImport(Constants.DllAVCodec, EntryPoint = nameof(avcodec_get_name), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern byte* avcodec_get_name(AVCodecID @id);

        /// <summary>Initialize the AVCodecContext to use the given AVCodec. Prior to using this function the context has to be allocated with avcodec_alloc_context3().</summary>
        /// <param name="avctx">The context to initialize.</param>
        /// <param name="codec">The codec to open this context for. If a non-NULL codec has been previously passed to avcodec_alloc_context3() or for this context, then this parameter MUST be either NULL or equal to the previously passed codec.</param>
        /// <param name="options">A dictionary filled with AVCodecContext and codec-private options. On return this object will be filled with options that were not found.</param>
        [DllImport(Constants.DllAVCodec, EntryPoint = nameof(avcodec_open2), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int avcodec_open2(AVCodecContext* @avctx, AVCodec* @codec, AVDictionary** @options);

        /// <summary>Fill the codec context based on the values from the supplied codec parameters. Any allocated fields in codec that have a corresponding field in par are freed and replaced with duplicates of the corresponding field in par. Fields in codec that do not have a counterpart in par are not touched.</summary>
        [DllImport(Constants.DllAVCodec, EntryPoint = nameof(avcodec_parameters_to_context), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int avcodec_parameters_to_context(AVCodecContext* @codec, AVCodecParameters* @par);

        /// <summary>Return decoded output data from a decoder.</summary>
        /// <param name="avctx">codec context</param>
        /// <param name="frame">This will be set to a reference-counted video or audio frame (depending on the decoder type) allocated by the decoder. Note that the function will always call av_frame_unref(frame) before doing anything else.</param>
        [DllImport(Constants.DllAVCodec, EntryPoint = nameof(avcodec_receive_frame), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int avcodec_receive_frame(AVCodecContext* @avctx, AVFrame* @frame);

        /// <summary>Register all the codecs, parsers and bitstream filters which were enabled at configuration time. If you do not call this function you can select exactly which formats you want to support, by using the individual registration functions.</summary>
        [DllImport(Constants.DllAVCodec, EntryPoint = nameof(avcodec_register_all), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern void avcodec_register_all();

        /// <summary>Supply raw packet data as input to a decoder.</summary>
        /// <param name="avctx">codec context</param>
        /// <param name="avpkt">The input AVPacket. Usually, this will be a single video frame, or several complete audio frames. Ownership of the packet remains with the caller, and the decoder will not write to the packet. The decoder may create a reference to the packet data (or copy it if the packet is not reference-counted). Unlike with older APIs, the packet is always fully consumed, and if it contains multiple frames (e.g. some audio codecs), will require you to call avcodec_receive_frame() multiple times afterwards before you can send a new packet. It can be NULL (or an AVPacket with data set to NULL and size set to 0); in this case, it is considered a flush packet, which signals the end of the stream. Sending the first flush packet will return success. Subsequent ones are unnecessary and will return AVERROR_EOF. If the decoder still has frames buffered, it will return them after sending a flush packet.</param>
        [DllImport(Constants.DllAVCodec, EntryPoint = nameof(avcodec_send_packet), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int avcodec_send_packet(AVCodecContext* @avctx, AVPacket* @avpkt);

        /// <summary>Free all allocated data in the given subtitle struct.</summary>
        /// <param name="sub">AVSubtitle to free.</param>
        [DllImport(Constants.DllAVCodec, EntryPoint = nameof(avsubtitle_free), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern void avsubtitle_free(AVSubtitle* @sub);

        /// <summary>Initialize libavdevice and register all the input and output devices.</summary>
        [DllImport(Constants.DllAVDevice, EntryPoint = nameof(avdevice_register_all), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern void avdevice_register_all();

        /// <summary>Get a frame with filtered data from sink and put it in frame.</summary>
        /// <param name="ctx">pointer to a buffersink or abuffersink filter context.</param>
        /// <param name="frame">pointer to an allocated frame that will be filled with data. The data must be freed using av_frame_unref() / av_frame_free()</param>
        /// <param name="flags">a combination of AV_BUFFERSINK_FLAG_* flags</param>
        [DllImport(Constants.DllAVFilter, EntryPoint = nameof(av_buffersink_get_frame_flags), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int av_buffersink_get_frame_flags(AVFilterContext* @ctx, AVFrame* @frame, int @flags);

        /// <summary>Add a frame to the buffer source.</summary>
        /// <param name="ctx">an instance of the buffersrc filter</param>
        /// <param name="frame">frame to be added. If the frame is reference counted, this function will take ownership of the reference(s) and reset the frame. Otherwise the frame data will be copied. If this function returns an error, the input frame is not touched.</param>
        [DllImport(Constants.DllAVFilter, EntryPoint = nameof(av_buffersrc_add_frame), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int av_buffersrc_add_frame(AVFilterContext* @ctx, AVFrame* @frame);

        [DllImport(Constants.DllAVFilter, EntryPoint = nameof(avfilter_get_by_name), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern AVFilter* avfilter_get_by_name([MarshalAs(UnmanagedType.LPStr)] string @name);

        /// <summary>Allocate a filter graph.</summary>
        [DllImport(Constants.DllAVFilter, EntryPoint = nameof(avfilter_graph_alloc), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern AVFilterGraph* avfilter_graph_alloc();

        /// <summary>Check validity and configure all the links and formats in the graph.</summary>
        /// <param name="graphctx">the filter graph</param>
        /// <param name="log_ctx">context used for logging</param>
        [DllImport(Constants.DllAVFilter, EntryPoint = nameof(avfilter_graph_config), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int avfilter_graph_config(AVFilterGraph* @graphctx, void* @log_ctx);

        /// <summary>Create and add a filter instance into an existing graph. The filter instance is created from the filter filt and inited with the parameters args and opaque.</summary>
        /// <param name="name">the instance name to give to the created filter instance</param>
        /// <param name="graph_ctx">the filter graph</param>
        [DllImport(Constants.DllAVFilter, EntryPoint = nameof(avfilter_graph_create_filter), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int avfilter_graph_create_filter(AVFilterContext** @filt_ctx, AVFilter* @filt, [MarshalAs(UnmanagedType.LPStr)] string @name, [MarshalAs(UnmanagedType.LPStr)] string @args, void* @opaque, AVFilterGraph* @graph_ctx);

        /// <summary>Free a graph, destroy its links, and set *graph to NULL. If *graph is NULL, do nothing.</summary>
        [DllImport(Constants.DllAVFilter, EntryPoint = nameof(avfilter_graph_free), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern void avfilter_graph_free(AVFilterGraph** @graph);

        /// <summary>Add a graph described by a string to a graph.</summary>
        /// <param name="graph">the filter graph where to link the parsed graph context</param>
        /// <param name="filters">string to be parsed</param>
        /// <param name="inputs">linked list to the inputs of the graph</param>
        /// <param name="outputs">linked list to the outputs of the graph</param>
        [DllImport(Constants.DllAVFilter, EntryPoint = nameof(avfilter_graph_parse), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int avfilter_graph_parse(AVFilterGraph* @graph, [MarshalAs(UnmanagedType.LPStr)] string @filters, AVFilterInOut* @inputs, AVFilterInOut* @outputs, void* @log_ctx);

        /// <summary>Queue a command for one or more filter instances.</summary>
        /// <param name="graph">the filter graph</param>
        /// <param name="target">the filter(s) to which the command should be sent &quot;all&quot; sends to all filters otherwise it can be a filter or filter instance name which will send the command to all matching filters.</param>
        /// <param name="cmd">the command to sent, for handling simplicity all commands must be alphanumeric only</param>
        /// <param name="arg">the argument for the command</param>
        /// <param name="ts">time at which the command should be sent to the filter</param>
        [DllImport(Constants.DllAVFilter, EntryPoint = nameof(avfilter_graph_queue_command), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int avfilter_graph_queue_command(AVFilterGraph* @graph, [MarshalAs(UnmanagedType.LPStr)] string @target, [MarshalAs(UnmanagedType.LPStr)] string @cmd, [MarshalAs(UnmanagedType.LPStr)] string @arg, int @flags, double @ts);

        /// <summary>Allocate a single AVFilterInOut entry. Must be freed with avfilter_inout_free().</summary>
        [DllImport(Constants.DllAVFilter, EntryPoint = nameof(avfilter_inout_alloc), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern AVFilterInOut* avfilter_inout_alloc();

        /// <summary>Link two filters together.</summary>
        /// <param name="src">the source filter</param>
        /// <param name="srcpad">index of the output pad on the source filter</param>
        /// <param name="dst">the destination filter</param>
        /// <param name="dstpad">index of the input pad on the destination filter</param>
        [DllImport(Constants.DllAVFilter, EntryPoint = nameof(avfilter_link), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int avfilter_link(AVFilterContext* @src, uint @srcpad, AVFilterContext* @dst, uint @dstpad);

        /// <summary>Initialize the filter system. Register all builtin filters.</summary>
        [DllImport(Constants.DllAVFilter, EntryPoint = nameof(avfilter_register_all), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern void avfilter_register_all();

        /// <summary>Print detailed information about the input or output format, such as duration, bitrate, streams, container, programs, metadata, side data, codec and time base.</summary>
        /// <param name="ic">the context to analyze</param>
        /// <param name="index">index of the stream to dump information about</param>
        /// <param name="url">the URL to print, such as source or destination file</param>
        /// <param name="is_output">Select whether the specified context is an input(0) or output(1)</param>
        [DllImport(Constants.DllAVFormat, EntryPoint = nameof(av_dump_format), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern void av_dump_format(AVFormatContext* @ic, int @index, [MarshalAs(UnmanagedType.LPStr)] string @url, int @is_output);

        /// <summary>Find the &quot;best&quot; stream in the file. The best stream is determined according to various heuristics as the most likely to be what the user expects. If the decoder parameter is non-NULL, av_find_best_stream will find the default decoder for the stream&apos;s codec; streams for which no decoder can be found are ignored.</summary>
        /// <param name="ic">media file handle</param>
        /// <param name="type">stream type: video, audio, subtitles, etc.</param>
        /// <param name="wanted_stream_nb">user-requested stream number, or -1 for automatic selection</param>
        /// <param name="related_stream">try to find a stream related (eg. in the same program) to this one, or -1 if none</param>
        /// <param name="decoder_ret">if non-NULL, returns the decoder for the selected stream</param>
        /// <param name="flags">flags; none are currently defined</param>
        [DllImport(Constants.DllAVFormat, EntryPoint = nameof(av_find_best_stream), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int av_find_best_stream(AVFormatContext* @ic, AVMediaType @type, int @wanted_stream_nb, int @related_stream, AVCodec** @decoder_ret, int @flags);

        /// <summary>Find AVInputFormat based on the short name of the input format.</summary>
        [DllImport(Constants.DllAVFormat, EntryPoint = nameof(av_find_input_format), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern AVInputFormat* av_find_input_format([MarshalAs(UnmanagedType.LPStr)] string @short_name);

        /// <summary>This function will cause global side data to be injected in the next packet of each stream as well as after any subsequent seek.</summary>
        [DllImport(Constants.DllAVFormat, EntryPoint = nameof(av_format_inject_global_side_data), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern void av_format_inject_global_side_data(AVFormatContext* @s);

        /// <summary>Guess the frame rate, based on both the container and codec information.</summary>
        /// <param name="ctx">the format context which the stream is part of</param>
        /// <param name="stream">the stream which the frame is part of</param>
        /// <param name="frame">the frame for which the frame rate should be determined, may be NULL</param>
        [DllImport(Constants.DllAVFormat, EntryPoint = nameof(av_guess_frame_rate), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern AVRational av_guess_frame_rate(AVFormatContext* @ctx, AVStream* @stream, AVFrame* @frame);

        /// <summary>Return the next frame of a stream. This function returns what is stored in the file, and does not validate that what is there are valid frames for the decoder. It will split what is stored in the file into frames and return one for each call. It will not omit invalid data between valid frames so as to give the decoder the maximum information possible for decoding.</summary>
        [DllImport(Constants.DllAVFormat, EntryPoint = nameof(av_read_frame), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int av_read_frame(AVFormatContext* @s, AVPacket* @pkt);

        /// <summary>Pause a network-based stream (e.g. RTSP stream).</summary>
        [DllImport(Constants.DllAVFormat, EntryPoint = nameof(av_read_pause), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int av_read_pause(AVFormatContext* @s);

        /// <summary>Start playing a network-based stream (e.g. RTSP stream) at the current position.</summary>
        [DllImport(Constants.DllAVFormat, EntryPoint = nameof(av_read_play), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int av_read_play(AVFormatContext* @s);

        /// <summary>Initialize libavformat and register all the muxers, demuxers and protocols. If you do not call this function, then you can select exactly which formats you want to support.</summary>
        [DllImport(Constants.DllAVFormat, EntryPoint = nameof(av_register_all), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern void av_register_all();

        /// <summary>Seek to the keyframe at timestamp. &apos;timestamp&apos; in &apos;stream_index&apos;.</summary>
        /// <param name="s">media file handle</param>
        /// <param name="stream_index">If stream_index is (-1), a default stream is selected, and timestamp is automatically converted from AV_TIME_BASE units to the stream specific time_base.</param>
        /// <param name="timestamp">Timestamp in AVStream.time_base units or, if no stream is specified, in AV_TIME_BASE units.</param>
        /// <param name="flags">flags which select direction and seeking mode</param>
        [DllImport(Constants.DllAVFormat, EntryPoint = nameof(av_seek_frame), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int av_seek_frame(AVFormatContext* @s, int @stream_index, long @timestamp, int @flags);

        /// <summary>Allocate an AVFormatContext. avformat_free_context() can be used to free the context and everything allocated by the framework within it.</summary>
        [DllImport(Constants.DllAVFormat, EntryPoint = nameof(avformat_alloc_context), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern AVFormatContext* avformat_alloc_context();

        /// <summary>Close an opened input AVFormatContext. Free it and all its contents and set *s to NULL.</summary>
        [DllImport(Constants.DllAVFormat, EntryPoint = nameof(avformat_close_input), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern void avformat_close_input(AVFormatContext** @s);

        /// <summary>Read packets of a media file to get stream information. This is useful for file formats with no headers such as MPEG. This function also computes the real framerate in case of MPEG-2 repeat frame mode. The logical file position is not changed by this function; examined packets may be buffered for later processing.</summary>
        /// <param name="ic">media file handle</param>
        /// <param name="options">If non-NULL, an ic.nb_streams long array of pointers to dictionaries, where i-th member contains options for codec corresponding to i-th stream. On return each dictionary will be filled with options that were not found.</param>
        [DllImport(Constants.DllAVFormat, EntryPoint = nameof(avformat_find_stream_info), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int avformat_find_stream_info(AVFormatContext* @ic, AVDictionary** @options);

        /// <summary>Free an AVFormatContext and all its streams.</summary>
        /// <param name="s">context to free</param>
        [DllImport(Constants.DllAVFormat, EntryPoint = nameof(avformat_free_context), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern void avformat_free_context(AVFormatContext* @s);

        /// <summary>Check if the stream st contained in s is matched by the stream specifier spec.</summary>
        [DllImport(Constants.DllAVFormat, EntryPoint = nameof(avformat_match_stream_specifier), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int avformat_match_stream_specifier(AVFormatContext* @s, AVStream* @st, [MarshalAs(UnmanagedType.LPStr)] string @spec);

        /// <summary>Do global initialization of network components. This is optional, but recommended, since it avoids the overhead of implicitly doing the setup for each session.</summary>
        [DllImport(Constants.DllAVFormat, EntryPoint = nameof(avformat_network_init), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int avformat_network_init();

        /// <summary>Open an input stream and read the header. The codecs are not opened. The stream must be closed with avformat_close_input().</summary>
        /// <param name="ps">Pointer to user-supplied AVFormatContext (allocated by avformat_alloc_context). May be a pointer to NULL, in which case an AVFormatContext is allocated by this function and written into ps. Note that a user-supplied AVFormatContext will be freed on failure.</param>
        /// <param name="url">URL of the stream to open.</param>
        /// <param name="fmt">If non-NULL, this parameter forces a specific input format. Otherwise the format is autodetected.</param>
        /// <param name="options">A dictionary filled with AVFormatContext and demuxer-private options. On return this parameter will be destroyed and replaced with a dict containing options that were not found. May be NULL.</param>
        [DllImport(Constants.DllAVFormat, EntryPoint = nameof(avformat_open_input), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int avformat_open_input(AVFormatContext** @ps, [MarshalAs(UnmanagedType.LPStr)] string @url, AVInputFormat* @fmt, AVDictionary** @options);

        /// <summary>feof() equivalent for AVIOContext.</summary>
        [DllImport(Constants.DllAVFormat, EntryPoint = nameof(avio_feof), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int avio_feof(AVIOContext* @s);

        /// <summary>Get number of entries in dictionary.</summary>
        /// <param name="m">dictionary</param>
        [DllImport(Constants.DllAVUtil, EntryPoint = nameof(av_dict_count), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int av_dict_count(AVDictionary* @m);

        /// <summary>Free all the memory allocated for an AVDictionary struct and all keys and values.</summary>
        [DllImport(Constants.DllAVUtil, EntryPoint = nameof(av_dict_free), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern void av_dict_free(AVDictionary** @m);

        /// <summary>Get a dictionary entry with matching key.</summary>
        /// <param name="key">matching key</param>
        /// <param name="prev">Set to the previous matching element to find the next. If set to NULL the first matching element is returned.</param>
        /// <param name="flags">a collection of AV_DICT_* flags controlling how the entry is retrieved</param>
        [DllImport(Constants.DllAVUtil, EntryPoint = nameof(av_dict_get), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern AVDictionaryEntry* av_dict_get(AVDictionary* @m, [MarshalAs(UnmanagedType.LPStr)] string @key, AVDictionaryEntry* @prev, int @flags);

        /// <summary>Set the given entry in *pm, overwriting an existing entry.</summary>
        /// <param name="pm">pointer to a pointer to a dictionary struct. If *pm is NULL a dictionary struct is allocated and put in *pm.</param>
        /// <param name="key">entry key to add to *pm (will either be av_strduped or added as a new key depending on flags)</param>
        /// <param name="value">entry value to add to *pm (will be av_strduped or added as a new key depending on flags). Passing a NULL value will cause an existing entry to be deleted.</param>
        [DllImport(Constants.DllAVUtil, EntryPoint = nameof(av_dict_set), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int av_dict_set(AVDictionary** @pm, [MarshalAs(UnmanagedType.LPStr)] string @key, [MarshalAs(UnmanagedType.LPStr)] string @value, int @flags);

        /// <summary>Allocate an AVFrame and set its fields to default values. The resulting struct must be freed using av_frame_free().</summary>
        [DllImport(Constants.DllAVUtil, EntryPoint = nameof(av_frame_alloc), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern AVFrame* av_frame_alloc();

        /// <summary>Create a new frame that references the same data as src.</summary>
        [DllImport(Constants.DllAVUtil, EntryPoint = nameof(av_frame_clone), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern AVFrame* av_frame_clone(AVFrame* @src);

        /// <summary>Free the frame and any dynamically allocated objects in it, e.g. extended_data. If the frame is reference counted, it will be unreferenced first.</summary>
        /// <param name="frame">frame to be freed. The pointer will be set to NULL.</param>
        [DllImport(Constants.DllAVUtil, EntryPoint = nameof(av_frame_free), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern void av_frame_free(AVFrame** @frame);

        /// <summary>Accessors for some AVFrame fields. The position of these field in the structure is not part of the ABI, they should not be accessed directly outside libavutil.</summary>
        [DllImport(Constants.DllAVUtil, EntryPoint = nameof(av_frame_get_best_effort_timestamp), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern long av_frame_get_best_effort_timestamp(AVFrame* @frame);

        [DllImport(Constants.DllAVUtil, EntryPoint = nameof(av_frame_get_channel_layout), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern long av_frame_get_channel_layout(AVFrame* @frame);

        [DllImport(Constants.DllAVUtil, EntryPoint = nameof(av_frame_get_channels), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int av_frame_get_channels(AVFrame* @frame);

        /// <summary>Return default channel layout for a given number of channels.</summary>
        [DllImport(Constants.DllAVUtil, EntryPoint = nameof(av_get_default_channel_layout), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern long av_get_default_channel_layout(int @nb_channels);

        /// <summary>Return the size in bytes of the amount of data required to store an image with the given parameters.</summary>
        /// <param name="align">the assumed linesize alignment</param>
        [DllImport(Constants.DllAVUtil, EntryPoint = nameof(av_image_get_buffer_size), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int av_image_get_buffer_size(AVPixelFormat @pix_fmt, int @width, int @height, int @align);

        /// <summary>Compute the size of an image line with format pix_fmt and width width for the plane plane.</summary>
        [DllImport(Constants.DllAVUtil, EntryPoint = nameof(av_image_get_linesize), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int av_image_get_linesize(AVPixelFormat @pix_fmt, int @width, int @plane);

        [DllImport(Constants.DllAVUtil, EntryPoint = nameof(av_log_set_flags), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern void av_log_set_flags(int @arg);

        /// <summary>Look for an option in an object. Consider only options which have all the specified flags set.</summary>
        /// <param name="obj">A pointer to a struct whose first element is a pointer to an AVClass. Alternatively a double pointer to an AVClass, if AV_OPT_SEARCH_FAKE_OBJ search flag is set.</param>
        /// <param name="name">The name of the option to look for.</param>
        /// <param name="unit">When searching for named constants, name of the unit it belongs to.</param>
        /// <param name="opt_flags">Find only options with all the specified flags set (AV_OPT_FLAG).</param>
        /// <param name="search_flags">A combination of AV_OPT_SEARCH_*.</param>
        [DllImport(Constants.DllAVUtil, EntryPoint = nameof(av_opt_find), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern AVOption* av_opt_find(void* @obj, [MarshalAs(UnmanagedType.LPStr)] string @name, [MarshalAs(UnmanagedType.LPStr)] string @unit, int @opt_flags, int @search_flags);

        /// <summary>Get the required buffer size for the given audio parameters.</summary>
        /// <param name="linesize">calculated linesize, may be NULL</param>
        /// <param name="nb_channels">the number of channels</param>
        /// <param name="nb_samples">the number of samples in a single channel</param>
        /// <param name="sample_fmt">the sample format</param>
        /// <param name="align">buffer size alignment (0 = default, 1 = no alignment)</param>
        [DllImport(Constants.DllAVUtil, EntryPoint = nameof(av_samples_get_buffer_size), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int av_samples_get_buffer_size(int* @linesize, int @nb_channels, int @nb_samples, AVSampleFormat @sample_fmt, int @align);

        /// <summary>Duplicate a string.</summary>
        /// <param name="s">String to be duplicated</param>
        [DllImport(Constants.DllAVUtil, EntryPoint = nameof(av_strdup), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern byte* av_strdup([MarshalAs(UnmanagedType.LPStr)] string @s);

        /// <summary>Put a description of the AVERROR code errnum in errbuf. In case of failure the global variable errno is set to indicate the error. Even in case of failure av_strerror() will print a generic error message indicating the errnum provided to errbuf.</summary>
        /// <param name="errnum">error code to describe</param>
        /// <param name="errbuf">buffer to which description is written</param>
        /// <param name="errbuf_size">the size in bytes of errbuf</param>
        [DllImport(Constants.DllAVUtil, EntryPoint = nameof(av_strerror), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int av_strerror(int @errnum, byte* @errbuf, ulong @errbuf_size);

        /// <summary>Allocate SwrContext if needed and set/reset common parameters.</summary>
        /// <param name="s">existing Swr context if available, or NULL if not</param>
        /// <param name="out_ch_layout">output channel layout (AV_CH_LAYOUT_*)</param>
        /// <param name="out_sample_fmt">output sample format (AV_SAMPLE_FMT_*).</param>
        /// <param name="out_sample_rate">output sample rate (frequency in Hz)</param>
        /// <param name="in_ch_layout">input channel layout (AV_CH_LAYOUT_*)</param>
        /// <param name="in_sample_fmt">input sample format (AV_SAMPLE_FMT_*).</param>
        /// <param name="in_sample_rate">input sample rate (frequency in Hz)</param>
        /// <param name="log_offset">logging level offset</param>
        /// <param name="log_ctx">parent logging context, can be NULL</param>
        [DllImport(Constants.DllSWResample, EntryPoint = nameof(swr_alloc_set_opts), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern SwrContext* swr_alloc_set_opts(SwrContext* @s, long @out_ch_layout, AVSampleFormat @out_sample_fmt, int @out_sample_rate, long @in_ch_layout, AVSampleFormat @in_sample_fmt, int @in_sample_rate, int @log_offset, void* @log_ctx);

        /// <summary>Convert audio.</summary>
        /// <param name="s">allocated Swr context, with parameters set</param>
        /// <param name="out">output buffers, only the first one need be set in case of packed audio</param>
        /// <param name="out_count">amount of space available for output in samples per channel</param>
        /// <param name="in">input buffers, only the first one need to be set in case of packed audio</param>
        /// <param name="in_count">number of input samples available in one channel</param>
        [DllImport(Constants.DllSWResample, EntryPoint = nameof(swr_convert), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int swr_convert(SwrContext* @s, byte** @out, int @out_count, byte** @in, int @in_count);

        /// <summary>Free the given SwrContext and set the pointer to NULL.</summary>
        /// <param name="s">a pointer to a pointer to Swr context</param>
        [DllImport(Constants.DllSWResample, EntryPoint = nameof(swr_free), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern void swr_free(SwrContext** @s);

        /// <summary>Initialize context after user parameters have been set.</summary>
        /// <param name="s">Swr context to initialize</param>
        [DllImport(Constants.DllSWResample, EntryPoint = nameof(swr_init), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int swr_init(SwrContext* @s);

        /// <summary>Free the swscaler context swsContext. If swsContext is NULL, then does nothing.</summary>
        [DllImport(Constants.DllSWScale, EntryPoint = nameof(sws_freeContext), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern void sws_freeContext(SwsContext* @swsContext);

        /// <summary>Check if context can be reused, otherwise reallocate a new one.</summary>
        [DllImport(Constants.DllSWScale, EntryPoint = nameof(sws_getCachedContext), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern SwsContext* sws_getCachedContext(SwsContext* @context, int @srcW, int @srcH, AVPixelFormat @srcFormat, int @dstW, int @dstH, AVPixelFormat @dstFormat, int @flags, SwsFilter* @srcFilter, SwsFilter* @dstFilter, double* @param);

        /// <summary>Scale the image slice in srcSlice and put the resulting scaled slice in the image in dst. A slice is a sequence of consecutive rows in an image.</summary>
        /// <param name="c">the scaling context previously created with sws_getContext()</param>
        /// <param name="srcSlice">the array containing the pointers to the planes of the source slice</param>
        /// <param name="srcStride">the array containing the strides for each plane of the source image</param>
        /// <param name="srcSliceY">the position in the source image of the slice to process, that is the number (counted starting from zero) in the image of the first row of the slice</param>
        /// <param name="srcSliceH">the height of the source slice, that is the number of rows in the slice</param>
        /// <param name="dst">the array containing the pointers to the planes of the destination image</param>
        /// <param name="dstStride">the array containing the strides for each plane of the destination image</param>
        [DllImport(Constants.DllSWScale, EntryPoint = nameof(sws_scale), CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int sws_scale(SwsContext* @c, byte*[] @srcSlice, int[] @srcStride, int @srcSliceY, int @srcSliceH, byte*[] @dst, int[] @dstStride);

    }
}
