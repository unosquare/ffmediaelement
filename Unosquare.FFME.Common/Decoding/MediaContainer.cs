﻿namespace Unosquare.FFME.Decoding
{
    using Core;
    using FFmpeg.AutoGen;
    using Primitives;
    using Shared;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// A container capable of opening an input url,
    /// reading packets from it, decoding frames, seeking, and pausing and resuming network streams
    /// Code based on https://raw.githubusercontent.com/FFmpeg/FFmpeg/release/3.2/ffplay.c
    /// The method pipeline should be:
    /// 1. Set Options (or don't, for automatic options) and Initialize,
    /// 2. Perform continuous packet reads,
    /// 3. Perform continuous frame decodes
    /// 4. Perform continuous block materialization
    /// </summary>
    /// <seealso cref="IDisposable" />
    internal sealed unsafe class MediaContainer : IDisposable
    {
        #region Private Fields

        /// <summary>
        /// The exception message no input context
        /// </summary>
        private const string ExceptionMessageNoInputContext = "Stream InputContext has not been initialized.";

        /// <summary>
        /// The read synchronize root
        /// </summary>
        private readonly object ReadSyncRoot = new object();

        /// <summary>
        /// The decode synchronize root
        /// </summary>
        private readonly object DecodeSyncRoot = new object();

        /// <summary>
        /// The convert synchronize root
        /// </summary>
        private readonly object ConvertSyncRoot = new object();

        /// <summary>
        /// Hold the value for the internal property with the same name.
        /// Picture attachments are required when video streams support them
        /// and these attached packets must be read before reading the first frame
        /// of the stream and after seeking.
        /// </summary>
        private bool RequiresPictureAttachments = true;

        /// <summary>
        /// The stream read interrupt callback.
        /// Used to detect read rimeouts.
        /// </summary>
        private AVIOInterruptCB_callback StreamReadInterruptCallback;

        /// <summary>
        /// The stream read interrupt start time.
        /// When a read operation is started, this is set to the ticks of UTC now.
        /// </summary>
        private AtomicLong StreamReadInterruptStartTime = new AtomicLong();

        /// <summary>
        /// The signal to request the abortion of the following read operation
        /// </summary>
        private AtomicBoolean SignalAbortReadsRequested = new AtomicBoolean(false);

        /// <summary>
        /// If set to true, it will reset the abort requested flag to false.
        /// </summary>
        private AtomicBoolean SignalAbortReadsAutoReset = new AtomicBoolean(false);

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaContainer" /> class.
        /// </summary>
        /// <param name="mediaUrl">The media URL.</param>
        /// <param name="config">The container configuration options.</param>
        /// <param name="parent">The logger.</param>
        /// <exception cref="ArgumentNullException">mediaUrl</exception>
        public MediaContainer(string mediaUrl, ContainerConfiguration config, IMediaLogger parent)
        {
            // Argument Validation
            if (string.IsNullOrWhiteSpace(mediaUrl))
                throw new ArgumentNullException($"{nameof(mediaUrl)}");

            // Initialize the library (if not already done)
            FFInterop.Initialize(null, FFmpegLoadMode.FullFeatures);

            // Create the options object
            Parent = parent;
            MediaUrl = mediaUrl;
            Configuration = config ?? new ContainerConfiguration();

            // drop the protocol prefix if it is redundant
            var protocolPrefix = Configuration.ProtocolPrefix;
            if (string.IsNullOrWhiteSpace(MediaUrl) == false && string.IsNullOrWhiteSpace(protocolPrefix) == false
                && MediaUrl.ToLowerInvariant().Trim().StartsWith(protocolPrefix.ToLowerInvariant() + ":"))
            {
                protocolPrefix = null;
            }

            Configuration.ProtocolPrefix = protocolPrefix;
            StreamInitialize();
        }

        #endregion

        #region Properties

        /// <summary>
        /// To detect redundat Dispose calls
        /// </summary>
        public bool IsDisposed { get; private set; } = false;

        /// <summary>
        /// Logging Messages will be sent to this parent object.
        /// </summary>
        public IMediaLogger Parent { get; }

        /// <summary>
        /// Gets the media URL. This is the input url, file or device that is read
        /// by this container.
        /// </summary>
        public string MediaUrl { get; }

        /// <summary>
        /// The container and demuxer initialization and configuration options.
        /// Options are applied when creating an instance of the container.
        /// After container creation, changing the configuration options passed in
        /// the constructor has no effect.
        /// </summary>
        public ContainerConfiguration Configuration { get; }

        /// <summary>
        /// Represetnts options that applied before initializing media components and their corresponding
        /// codecs. Once the container has created the media components, changing these options will have no effect.
        /// </summary>
        public MediaOptions MediaOptions { get; } = new MediaOptions();

        /// <summary>
        /// Provides stream, chapter and program info held by this container.
        /// This property is null if the the stream has not been opened.
        /// </summary>
        public MediaInfo MediaInfo { get; private set; }

        /// <summary>
        /// Gets the name of the media format.
        /// </summary>
        public string MediaFormatName { get; private set; }

        /// <summary>
        /// Gets the media bitrate (bits per second). Returns 0 if not available.
        /// </summary>
        public ulong MediaBitrate => MediaInfo?.BitRate ?? 0;

        /// <summary>
        /// Holds the metadata of the media file when the stream is initialized.
        /// </summary>
        public ReadOnlyDictionary<string, string> Metadata { get; private set; }

        /// <summary>
        /// Gets a value indicating whether an Input Context has been initialize.
        /// </summary>
        public bool IsInitialized => InputContext != null;

        /// <summary>
        /// Gets a value indicating whether this instance is open.
        /// </summary>
        public bool IsOpen => IsInitialized && Components.All.Count > 0;

        /// <summary>
        /// Gets the duration of the media.
        /// If this information is not available (i.e. realtime media) it will
        /// be set to TimeSpan.MinValue
        /// </summary>
        public TimeSpan MediaDuration => MediaInfo?.Duration ?? TimeSpan.MinValue;

        /// <summary>
        /// Will be set to true whenever an End Of File situation is reached.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is at end of stream; otherwise, <c>false</c>.
        /// </value>
        public bool IsAtEndOfStream { get; private set; } = false;

        /// <summary>
        /// Gets the byte position at which the stream is being read.
        /// Please note that this property gets updated after every Read.
        /// </summary>
        public long StreamPosition
        {
            get
            {
                if (InputContext == null || InputContext->pb == null) return 0;
                return InputContext->pb->pos;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the underlying media is seekable.
        /// </summary>
        public bool IsStreamSeekable => MediaDuration.TotalSeconds > 0 && MediaDuration != TimeSpan.MinValue;

        /// <summary>
        /// Gets a value indicating whether this container represents live media.
        /// If the stream is classified as a network stream and it is not seekable, then this property will return true.
        /// </summary>
        public bool IsLiveStream => IsNetworkStream && IsStreamSeekable == false;

        /// <summary>
        /// Gets a value indicating whether the input stream is a network stream.
        /// If the format name is rtp, rtsp, or sdp or if the url starts with udp:, http:, https:, tcp:, or rtp:
        /// then this property will be set to true.
        /// </summary>
        public bool IsNetworkStream { get; private set; }

        /// <summary>
        /// Provides direct access to the individual Media components of the input stream.
        /// </summary>
        public MediaComponentSet Components { get; } = new MediaComponentSet();

        /// <summary>
        /// Gets a value indicating whether reads are in the aborted state.
        /// </summary>
        public bool IsReadAborted => SignalAbortReadsRequested.Value;

        /// <summary>
        /// Gets the media start time by which all component streams are offset.
        /// Typically 0 but it could be something other than 0.
        /// </summary>
        public TimeSpan MediaStartTimeOffset { get; private set; }

        #endregion

        #region Internal Properties

        /// <summary>
        /// Holds a reference to the input context.
        /// </summary>
        internal AVFormatContext* InputContext { get; private set; } = null;

        #endregion

        #region Private State Management

        /// <summary>
        /// Gets the seek start timestamp of the main component.
        /// </summary>
        private long StartSeekTimestamp => Components.Main.FirstPacketDts ?? Components.Main.StartTimeOffset.ToLong(Components.Main.Stream->time_base);

        /// <summary>
        /// Gets the index of the seek stream to seek on for the BOF.
        /// </summary>
        private int StartSeekStreamIndex => Components.Main.StreamIndex;

        /// <summary>
        /// Gets the start seek byte position.
        /// </summary>
        private long StartSeekPosition => Components.FirstPacketPosition ?? 0;

        /// <summary>
        /// Gets the time the last packet was read from the input
        /// </summary>
        private DateTime StateLastReadTimeUtc { get; set; } = DateTime.MinValue;

        /// <summary>
        /// Gets a value indicating whether a packet read delay witll be enforced.
        /// RSTP formats or MMSH Urls will have this property set to true.
        /// Reading packets will block for at most 10 milliseconds depending on the last read time.
        /// This is a hack according to the source code in ffplay.c
        /// </summary>
        private bool StateRequiresReadDelay { get; set; }

        /// <summary>
        /// Picture attachments are required when video streams support them
        /// and these attached packets must be read before reading the first frame
        /// of the stream and after seeking. This property is not part of the public API
        /// and is meant more for internal purposes
        /// </summary>
        private bool StateRequiresPictureAttachments
        {
            get
            {
                var canRequireAttachments = Components.HasVideo
                    && (Components.Video.Stream->disposition & ffmpeg.AV_DISPOSITION_ATTACHED_PIC) != 0;

                return canRequireAttachments && RequiresPictureAttachments;
            }
            set
            {
                var canRequireAttachments = Components.HasVideo
                    && (Components.Video.Stream->disposition & ffmpeg.AV_DISPOSITION_ATTACHED_PIC) != 0;

                RequiresPictureAttachments = canRequireAttachments && value;
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Opens the individual stram components on the existing input context in order to start reading packets.
        /// Any Media Options must be set before this method is called.
        /// </summary>
        public void Open()
        {
            lock (ReadSyncRoot)
            {
                if (IsDisposed) throw new ObjectDisposedException(nameof(MediaContainer));
                if (InputContext == null) throw new InvalidOperationException(ExceptionMessageNoInputContext);

                StreamOpen();
            }
        }

        /// <summary>
        /// Seeks to the specified position in the stream. This method attempts to do so as
        /// precisely as possible, returning decoded frames of all available media type components
        /// just before or right on the requested position. The position must be given in 0-based time,
        /// so it converts component stream start time offset to absolute, 0-based time.
        /// Pass TimeSpan.Zero to seek to the beginning of the stream.
        /// </summary>
        /// <param name="position">The position.</param>
        /// <returns>
        /// The list of media frames
        /// </returns>
        /// <exception cref="InvalidOperationException">No input context initialized</exception>
        public List<MediaFrame> Seek(TimeSpan position)
        {
            lock (ReadSyncRoot)
            {
                if (IsDisposed) throw new ObjectDisposedException(nameof(MediaContainer));
                if (InputContext == null) throw new InvalidOperationException(ExceptionMessageNoInputContext);

                return StreamSeek(position);
            }
        }

        /// <summary>
        /// Reads the next available packet, sending the packet to the corresponding
        /// internal media component. It also sets IsAtEndOfStream property.
        /// Returns the media type if the packet was accepted by any of the media components.
        /// Returns None if the packet was not accepted by any of the media components
        /// or if reading failed (i.e. End of stream already or read error).
        /// Packets are queued internally. To dequeue them you need to call the receive frames
        /// method of each component until the packet buffer count becomes 0.
        /// </summary>
        /// <returns>The media type of the packet that was read</returns>
        /// <exception cref="InvalidOperationException">No input context initialized</exception>
        /// <exception cref="MediaContainerException">When a read error occurs</exception>
        public MediaType Read()
        {
            lock (ReadSyncRoot)
            {
                if (IsDisposed) throw new ObjectDisposedException(nameof(MediaContainer));
                if (InputContext == null) throw new InvalidOperationException(ExceptionMessageNoInputContext);

                return StreamRead();
            }
        }

        /// <summary>
        /// Decodes the next available packet in the packet queue for each of the components.
        /// Returns the list of decoded frames. You can call this method until the Components.PacketBufferCount
        /// becomes 0; The list of 0 or more decoded frames is returned in ascending StartTime order.
        /// A Packet may contain 0 or more frames. Once the frame source objects are returned, you
        /// are responsible for calling the Dispose method on them to free the underlying FFmpeg frame.
        /// Note that even after releasing them you can still use the managed properties.
        /// If you intend on Converting the frames to usable media frames (with Convert) you must not
        /// release the frame. Specify the release input argument as true and the frame will be automatically
        /// freed from memory.
        /// </summary>
        /// <returns>The list of media frames</returns>
        public List<MediaFrame> Decode()
        {
            lock (DecodeSyncRoot)
            {
                if (IsDisposed) throw new ObjectDisposedException(nameof(MediaContainer));
                if (InputContext == null) throw new InvalidOperationException(ExceptionMessageNoInputContext);

                var result = new List<MediaFrame>(64);
                foreach (var component in Components.All)
                    result.AddRange(component.ReceiveFrames());

                result.Sort();
                return result;
            }
        }

        /// <summary>
        /// Performs audio, video and subtitle conversions on the decoded input frame so data
        /// can be used as a Frame. Please note that if the output is passed as a reference.
        /// This works as follows: if the output reference is null it will be automatically instantiated
        /// and returned by this function. This enables to  either instantiate or reuse a previously allocated Frame.
        /// This is important because buffer allocations are exepnsive operations and this allows you
        /// to perform the allocation once and continue reusing thae same buffer.
        /// </summary>
        /// <param name="input">The raw frame source. Has to be compatiable with the target. (e.g. use VideoFrameSource to conver to VideoFrame)</param>
        /// <param name="output">The target frame. Has to be compatible with the source.</param>
        /// <param name="siblings">The siblings that may help guess additional output parameters.</param>
        /// <param name="releaseInput">if set to <c>true</c> releases the raw frame source from unmanaged memory.</param>
        /// <returns>
        /// The media block
        /// </returns>
        /// <exception cref="InvalidOperationException">No input context initialized</exception>
        /// <exception cref="MediaContainerException">MediaType</exception>
        /// <exception cref="ArgumentNullException">input</exception>
        /// <exception cref="ArgumentException">input
        /// or
        /// input</exception>
        public MediaBlock Convert(MediaFrame input, ref MediaBlock output, List<MediaBlock> siblings, bool releaseInput = true)
        {
            lock (ConvertSyncRoot)
            {
                if (IsDisposed) throw new ObjectDisposedException(nameof(MediaContainer));
                if (InputContext == null) throw new InvalidOperationException(ExceptionMessageNoInputContext);

                // Check the input parameters
                if (input == null)
                    throw new ArgumentNullException($"{nameof(input)} cannot be null.");

                if (input.IsStale)
                {
                    throw new ArgumentException(
                        $"The {nameof(input)} {nameof(MediaFrame)} ({input.MediaType}) has already been released (it's stale).");
                }

                try
                {
                    switch (input.MediaType)
                    {
                        case MediaType.Video:
                            if (Components.HasVideo) Components.Video.MaterializeFrame(input, ref output, siblings);
                            return output;

                        case MediaType.Audio:
                            if (Components.HasAudio) Components.Audio.MaterializeFrame(input, ref output, siblings);
                            return output;

                        case MediaType.Subtitle:
                            if (Components.HasSubtitles) Components.Subtitles.MaterializeFrame(input, ref output, siblings);
                            return output;

                        default:
                            throw new MediaContainerException($"Unable to materialize frame of {nameof(MediaType)} {input.MediaType}");
                    }
                }
                catch
                {
                    throw;
                }
                finally
                {
                    if (releaseInput)
                        input.Dispose();
                }
            }
        }

        /// <summary>
        /// Signals the packet reading operations to abort immediately.
        /// </summary>
        /// <param name="reset">if set to true, the read interrupt will reset the aborted state automatically</param>
        public void SignalAbortReads(bool reset)
        {
            if (IsDisposed) throw new ObjectDisposedException(nameof(MediaContainer));
            if (InputContext == null) throw new InvalidOperationException(ExceptionMessageNoInputContext);

            SignalAbortReadsAutoReset.Value = reset;
            SignalAbortReadsRequested.Value = true;
        }

        /// <summary>
        /// Signals the state for read operations to stop being in the aborted state
        /// </summary>
        public void SignalResumeReads()
        {
            if (IsDisposed) throw new ObjectDisposedException(nameof(MediaContainer));
            if (InputContext == null) throw new InvalidOperationException(ExceptionMessageNoInputContext);

            SignalAbortReadsRequested.Value = false;
            SignalAbortReadsAutoReset.Value = true;
        }

        /// <summary>
        /// Closes the input context immediately releasing all resources.
        /// This method is equivalent to calling the dispose method.
        /// </summary>
        public void Close()
        {
            Dispose();
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        #endregion

        #region Private Stream Methods

        /// <summary>
        /// Initializes the input context to start read operations.
        /// This does NOT create the stream components and therefore, there needs to be a call
        /// to the Open method.
        /// </summary>
        /// <exception cref="InvalidOperationException">The input context has already been initialized.</exception>
        /// <exception cref="MediaContainerException">When an error initializing the stream occurs.</exception>
        private void StreamInitialize()
        {
            if (IsInitialized)
                throw new InvalidOperationException("The input context has already been initialized.");

            // Retrieve the input format (null = auto for default)
            AVInputFormat* inputFormat = null;
            if (string.IsNullOrWhiteSpace(Configuration.ForcedInputFormat) == false)
            {
                inputFormat = ffmpeg.av_find_input_format(Configuration.ForcedInputFormat);
                if (inputFormat == null)
                {
                    Parent?.Log(MediaLogMessageType.Warning,
                        $"Format '{Configuration.ForcedInputFormat}' not found. Will use automatic format detection.");
                }
            }

            try
            {
                // Create the input format context, and open the input based on the provided format options.
                using (var privateOptions = new FFDictionary(Configuration.PrivateOptions))
                {
                    if (privateOptions.HasKey(ContainerConfiguration.ScanAllPmts) == false)
                        privateOptions.Set(ContainerConfiguration.ScanAllPmts, "1", true);

                    // Create the input context
                    StreamInitializeInputContext();

                    // Try to open the input
                    var inputContextPtr = InputContext;

                    // Open the input file
                    var openResult = 0;

                    // We set the start of the read operation time so tiomeouts can be detected
                    // and we open the URL so the input context can be initialized.
                    StreamReadInterruptStartTime.Value = DateTime.UtcNow.Ticks;
                    fixed (AVDictionary** privateOptionsRef = &privateOptions.Pointer)
                    {
                        var prefix = string.IsNullOrWhiteSpace(Configuration.ProtocolPrefix) ?
                            string.Empty : $"{Configuration.ProtocolPrefix.Trim()}:";

                        // Pass the private options dictionary
                        openResult = ffmpeg.avformat_open_input(&inputContextPtr, $"{prefix}{MediaUrl}", inputFormat, privateOptionsRef);
                        InputContext = inputContextPtr;
                    }

                    // Validate the open operation
                    if (openResult < 0)
                    {
                        throw new MediaContainerException($"Could not open '{MediaUrl}'. "
                            + $"Error {openResult}: {FFInterop.DecodeMessage(openResult)}");
                    }

                    // Set some general properties
                    MediaFormatName = FFInterop.PtrToStringUTF8(InputContext->iformat->name);

                    // If there are any options left in the dictionary, it means they did not get used (invalid options).
                    // Output the invalid options as warnings
                    privateOptions.Remove(ContainerConfiguration.ScanAllPmts);
                    var currentEntry = privateOptions.First();
                    while (currentEntry != null && currentEntry?.Key != null)
                    {
                        Parent?.Log(MediaLogMessageType.Warning, $"Invalid input option: '{currentEntry.Key}'");
                        currentEntry = privateOptions.Next(currentEntry);
                    }
                }

                ffmpeg.av_format_inject_global_side_data(InputContext);

                // This is useful for file formats with no headers such as MPEG. This function also computes
                // the real framerate in case of MPEG-2 repeat frame mode.
                if (ffmpeg.avformat_find_stream_info(InputContext, null) < 0)
                    Parent?.Log(MediaLogMessageType.Warning, $"{MediaUrl}: could not read stream information.");

                // HACK: From ffplay.c: maybe should not use avio_feof() to test for the end
                if (InputContext->pb != null) InputContext->pb->eof_reached = 0;

                // Setup initial state variables
                Metadata = new ReadOnlyDictionary<string, string>(FFDictionary.ToDictionary(InputContext->metadata));

                // If read_play is set, it is only relevant to network streams
                IsNetworkStream = InputContext->iformat->read_play.Pointer != IntPtr.Zero;
                if (IsNetworkStream == false && Uri.TryCreate(MediaUrl, UriKind.RelativeOrAbsolute, out var uri))
                {
                    try { IsNetworkStream = uri.IsFile == false || uri.IsUnc; }
                    catch { }
                }

                // Unsure how this works. Ported from ffplay
                StateRequiresReadDelay = MediaFormatName.Equals("rstp") || MediaUrl.StartsWith("mmsh:");

                // Extract the Media Info
                MediaInfo = new MediaInfo(this);

                // Determine the seek mode of the input format
                /*
                var inputAllowsDiscontinuities = (InputContext->iformat->flags & ffmpeg.AVFMT_TS_DISCONT) != 0;
                MediaSeeksByBytes = inputAllowsDiscontinuities && (MediaFormatName.Equals("ogg") == false);
                MediaSeeksByBytes = MediaSeeksByBytes && MediaBitrate > 0;
                */

                // Compute start time and duration (if possible)
                MediaStartTimeOffset = InputContext->start_time.ToTimeSpan();
                if (MediaStartTimeOffset == TimeSpan.MinValue)
                {
                    MediaStartTimeOffset = TimeSpan.Zero;
                    Parent?.Log(MediaLogMessageType.Warning,
                        $"Unable to determine the media start time offset. " +
                        $"Media start time offset will be assumed to start at {TimeSpan.Zero}.");
                }

                // Extract detailed media information and set the default streams to the
                // best available ones.
                foreach (var s in MediaInfo.BestStreams)
                {
                    if (s.Key == AVMediaType.AVMEDIA_TYPE_VIDEO)
                        MediaOptions.VideoStream = s.Value;
                    else if (s.Key == AVMediaType.AVMEDIA_TYPE_AUDIO)
                        MediaOptions.AudioStream = s.Value;
                    else if (s.Key == AVMediaType.AVMEDIA_TYPE_SUBTITLE)
                        MediaOptions.SubtitleStream = s.Value;
                }

                // Set disabled audio or video if scaling libs not found
                // This prevents the creation of unavailable audio or video components.
                if (FFLibrary.LibSWScale.IsLoaded == false)
                    MediaOptions.IsVideoDisabled = true;

                if (FFLibrary.LibSWResample.IsLoaded == false)
                    MediaOptions.IsAudioDisabled = true;
            }
            catch (Exception ex)
            {
                Parent?.Log(MediaLogMessageType.Error,
                    $"Fatal error initializing {nameof(MediaContainer)} instance. {ex.Message}");
                Close();
                throw;
            }
        }

        /// <summary>
        /// Initializes the InputContext and applies format options.
        /// https://www.ffmpeg.org/ffmpeg-formats.html#Format-Options
        /// </summary>
        private void StreamInitializeInputContext()
        {
            // Allocate the input context and save it
            InputContext = ffmpeg.avformat_alloc_context();

            // Setup an interrupt callback to detect read timeouts
            SignalAbortReadsRequested.Value = false;
            SignalAbortReadsAutoReset.Value = true;
            StreamReadInterruptCallback = OnStreamReadInterrupt;
            InputContext->interrupt_callback.callback = StreamReadInterruptCallback;
            InputContext->interrupt_callback.opaque = InputContext;

            // Acquire the format options to be applied
            var opts = Configuration.GlobalOptions;

            // Apply the options
            if (opts.EnableReducedBuffering) InputContext->avio_flags |= ffmpeg.AVIO_FLAG_DIRECT;
            if (opts.PacketSize != default) InputContext->packet_size = System.Convert.ToUInt32(opts.PacketSize);
            if (opts.ProbeSize != default) InputContext->probesize = Configuration.GlobalOptions.ProbeSize <= 32 ? 32 : opts.ProbeSize;

            // Flags
            InputContext->flags |= opts.FlagDiscardCorrupt ? ffmpeg.AVFMT_FLAG_DISCARD_CORRUPT : InputContext->flags;
            InputContext->flags |= opts.FlagEnableFastSeek ? ffmpeg.AVFMT_FLAG_FAST_SEEK : InputContext->flags;
            InputContext->flags |= opts.FlagEnableLatmPayload ? ffmpeg.AVFMT_FLAG_MP4A_LATM : InputContext->flags;
            InputContext->flags |= opts.FlagEnableNoFillin ? ffmpeg.AVFMT_FLAG_NOFILLIN : InputContext->flags;
            InputContext->flags |= opts.FlagGeneratePts ? ffmpeg.AVFMT_FLAG_GENPTS : InputContext->flags;
            InputContext->flags |= opts.FlagIgnoreDts ? ffmpeg.AVFMT_FLAG_IGNDTS : InputContext->flags;
            InputContext->flags |= opts.FlagIgnoreIndex ? ffmpeg.AVFMT_FLAG_IGNIDX : InputContext->flags;
            InputContext->flags |= opts.FlagKeepSideData ? ffmpeg.AVFMT_FLAG_KEEP_SIDE_DATA : InputContext->flags;
            InputContext->flags |= opts.FlagNoBuffer ? ffmpeg.AVFMT_FLAG_NOBUFFER : InputContext->flags;
            InputContext->flags |= opts.FlagSortDts ? ffmpeg.AVFMT_FLAG_SORT_DTS : InputContext->flags;
            InputContext->flags |= opts.FlagStopAtShortest ? ffmpeg.AVFMT_FLAG_SHORTEST : InputContext->flags;

            InputContext->seek2any = opts.SeekToAny ? 1 : 0;

            // Handle analyze duration overrides
            if (opts.MaxAnalyzeDuration != default)
            {
                InputContext->max_analyze_duration = opts.MaxAnalyzeDuration <= TimeSpan.Zero ? 0 :
                    System.Convert.ToInt64(opts.MaxAnalyzeDuration.TotalSeconds * ffmpeg.AV_TIME_BASE);
            }

            if (string.IsNullOrEmpty(opts.CryptoKey) == false)
            {
                // TODO: (Floyd) not yet implemented
            }
        }

        /// <summary>
        /// Opens the individual stream components to start reading packets.
        /// </summary>
        private void StreamOpen()
        {
            // Check no double calls to this method.
            if (IsOpen) throw new InvalidOperationException("The stream components are already open.");

            // Open the best suitable streams. Throw if no audio and/or video streams are found
            StreamOpenCreateComponents();

            // Verify the stream input start offset. This is the zero measure for all sub-streams.
            var minOffset = Components.All.Count > 0 ? Components.All.Min(c => c.StartTimeOffset) : MediaStartTimeOffset;
            if (minOffset != MediaStartTimeOffset)
            {
                Parent?.Log(MediaLogMessageType.Warning, $"Input Start: {MediaStartTimeOffset.Format()} Comp. Start: {minOffset.Format()}. Input start will be updated.");
                MediaStartTimeOffset = minOffset;
            }

            // Initially and depending on the video component, rquire picture attachments.
            // Picture attachments are only required after the first read or after a seek.
            StateRequiresPictureAttachments = true;
        }

        /// <summary>
        /// Creates the stream components by first finding the best available streams.
        /// Then it initializes the components of the correct type each.
        /// </summary>
        /// <exception cref="MediaContainerException">The exception ifnromation</exception>
        private void StreamOpenCreateComponents()
        {
            // Create the audio component
            try
            {
                if (MediaOptions.AudioStream != null
                    && MediaOptions.AudioStream.CodecType == AVMediaType.AVMEDIA_TYPE_AUDIO
                    && MediaOptions.IsAudioDisabled == false)
                    Components[MediaType.Audio] = new AudioComponent(this, MediaOptions.AudioStream.StreamIndex);
            }
            catch (Exception ex)
            {
                Parent?.Log(MediaLogMessageType.Error, $"Unable to initialize {MediaType.Audio} component. {ex.Message}");
            }

            // Create the video component
            try
            {
                if (MediaOptions.VideoStream != null
                    && MediaOptions.VideoStream.CodecType == AVMediaType.AVMEDIA_TYPE_VIDEO
                    && MediaOptions.IsVideoDisabled == false)
                    Components[MediaType.Video] = new VideoComponent(this, MediaOptions.VideoStream.StreamIndex);
            }
            catch (Exception ex)
            {
                Parent?.Log(MediaLogMessageType.Error, $"Unable to initialize {MediaType.Video} component. {ex.Message}");
            }

            // Create the subtitle component
            try
            {
                if (MediaOptions.SubtitleStream != null
                    && MediaOptions.SubtitleStream.CodecType == AVMediaType.AVMEDIA_TYPE_SUBTITLE
                    && MediaOptions.IsSubtitleDisabled == false)
                    Components[MediaType.Subtitle] = new SubtitleComponent(this, MediaOptions.SubtitleStream.StreamIndex);
            }
            catch (Exception ex)
            {
                Parent?.Log(MediaLogMessageType.Error, $"Unable to initialize {MediaType.Subtitle} component. {ex.Message}");
            }

            // Verify we have at least 1 stream component to work with.
            if (Components.HasVideo == false && Components.HasAudio == false && Components.HasSubtitles == false)
                throw new MediaContainerException($"{MediaUrl}: No audio, video, or subtitle streams found to decode.");
        }

        /// <summary>
        /// Reads the next packet in the underlying stream and enqueues in the corresponding media component.
        /// Returns None of no packet was read.
        /// </summary>
        /// <returns>The type of media packet that was read</returns>
        /// <exception cref="InvalidOperationException">Initialize</exception>
        /// <exception cref="MediaContainerException">Raised when an error reading from the stream occurs.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private MediaType StreamRead()
        {
            // Check the context has been initialized
            if (IsOpen == false)
                throw new InvalidOperationException($"Please call the {nameof(Open)} method before attempting this operation.");

            if (IsReadAborted)
                return MediaType.None;

#if CONFIG_RTSP_DEMUXER
            // I am unsure how this code ported from ffplay provides any advantage or functionality
            // I have tested with several streams and it does not make any difference other than 
            // making the reads much longer and the buffers fill up more slowly.
            if (RequiresReadDelay)
            {
                // in ffplay.c this is referenced via CONFIG_RTSP_DEMUXER || CONFIG_MMSH_PROTOCOL
                var millisecondsDifference = System.Convert.ToInt32(DateTime.UtcNow.Subtract(StreamLastReadTimeUtc).TotalMilliseconds);
                var sleepMilliseconds = 10 - millisecondsDifference;

                // wait at least 10 ms to avoid trying to get another packet
                if (sleepMilliseconds > 0)
                    Task.Delay(sleepMilliseconds).Wait(); // XXX: horrible
            }
#endif

            if (StateRequiresPictureAttachments)
            {
                var attachedPacket = ffmpeg.av_packet_clone(&Components.Video.Stream->attached_pic);
                RC.Current.Add(attachedPacket, $"710: {nameof(MediaComponent)}.{nameof(StreamRead)}()");
                if (attachedPacket != null)
                {
                    Components.Video.SendPacket(attachedPacket);
                    Components.Video.SendEmptyPacket();
                }

                StateRequiresPictureAttachments = false;
            }

            // Allocate the packet to read
            var readPacket = ffmpeg.av_packet_alloc();
            RC.Current.Add(readPacket, $"725: {nameof(MediaComponent)}.{nameof(StreamRead)}()");
            StreamReadInterruptStartTime.Value = DateTime.UtcNow.Ticks;
            var readResult = ffmpeg.av_read_frame(InputContext, readPacket);
            StateLastReadTimeUtc = DateTime.UtcNow;

            if (readResult < 0)
            {
                // Handle failed packet reads. We don't need the allocated packet anymore
                RC.Current.Remove(readPacket);
                ffmpeg.av_packet_free(&readPacket);

                // Detect an end of file situation (makes the readers enter draining mode)
                if (readResult == ffmpeg.AVERROR_EOF || ffmpeg.avio_feof(InputContext->pb) != 0)
                {
                    // Force the decoders to enter draining mode (with empry packets)
                    if (IsAtEndOfStream == false)
                        Components.SendEmptyPackets();

                    IsAtEndOfStream = true;
                    return MediaType.None;
                }
                else
                {
                    if (InputContext->pb != null && InputContext->pb->error != 0)
                        throw new MediaContainerException($"Input has produced an error. Error Code {readResult}, {FFInterop.DecodeMessage(readResult)}");
                }
            }
            else
            {
                IsAtEndOfStream = false;
            }

            // Check if we were able to feed the packet. If not, simply discard it
            if (readPacket != null)
            {
                var componentType = Components.SendPacket(readPacket);

                // Discard the packet -- it was not accepted by any component
                if (componentType == MediaType.None)
                {
                    RC.Current.Remove(readPacket);
                    ffmpeg.av_packet_free(&readPacket);
                }

                return componentType;
            }

            return MediaType.None;
        }

        /// <summary>
        /// The interrupt callback to handle stream reading timeouts
        /// </summary>
        /// <param name="opaque">A pointer to the format input context</param>
        /// <returns>0 for OK, 1 for error (timeout)</returns>
        private unsafe int OnStreamReadInterrupt(void* opaque)
        {
            const int ErrorResult = 1;
            const int OkResult = 0;

            // Check if a forced quit was triggered
            if (SignalAbortReadsRequested.Value)
            {
                Parent?.Log(MediaLogMessageType.Info, $"{nameof(OnStreamReadInterrupt)} was requested an immediate read exit.");
                if (SignalAbortReadsAutoReset.Value)
                    SignalAbortReadsRequested.Value = false;

                return ErrorResult;
            }

            var nowTicks = DateTime.UtcNow.Ticks;

            // We use Interlocked read because in 32 bits it takes 2 trips!
            var startTicks = StreamReadInterruptStartTime.Value;
            var timeDifference = TimeSpan.FromTicks(nowTicks - startTicks);

            if (Configuration.ReadTimeout.Ticks >= 0 && timeDifference.Ticks > Configuration.ReadTimeout.Ticks)
            {
                Parent?.Log(MediaLogMessageType.Error, $"{nameof(OnStreamReadInterrupt)} timed out with  {timeDifference.Format()}");
                return ErrorResult;
            }

            return OkResult;
        }

        /// <summary>
        /// Seeks to the exact or prior frame of the main stream.
        /// Supports byte seeking. Target time is in absolute, zero-based time.
        /// </summary>
        /// <param name="targetTime">The target time in absolute, 0-based time.</param>
        /// <returns>
        /// The list of media frames
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private List<MediaFrame> StreamSeek(TimeSpan targetTime)
        {
            // Create the output result object
            var result = new List<MediaFrame>(256);

            // A special kind of seek is the zero seek. Execute it if requested.
            if (targetTime <= TimeSpan.Zero)
            {
                StreamSeekToStart();
                return result; // this will have no frames at this point.
            }

            #region Setup

            // Cancel the seek operation if the stream does not support it.
            if (IsStreamSeekable == false)
            {
                Parent?.Log(MediaLogMessageType.Warning, $"Unable to seek. Underlying stream does not support seeking.");
                return result;
            }

            // Select the main component
            var main = Components.Main;
            if (main == null) return result;

            // clamp the minimum time to zero (can't be less than 0)
            if (targetTime.Ticks + main.StartTimeOffset.Ticks < main.StartTimeOffset.Ticks)
                targetTime = TimeSpan.Zero;

            // Clamp the maximum seek value to main component's duration (can't be more than duration)
            if (targetTime.Ticks > main.Duration.Ticks)
                targetTime = TimeSpan.FromTicks(main.Duration.Ticks);

            // Stream seeking by main component
            // The backward flag means that we want to seek to at MOST the target position
            var seekFlags = ffmpeg.AVSEEK_FLAG_BACKWARD;
            var streamIndex = main.StreamIndex;
            var timeBase = main.Stream->time_base;

            // Perform the stream seek
            var seekResult = 0;
            var startPos = StreamPosition;

            #endregion

            #region Perform FFmpeg API Seek

            // The relative target time keeps track of where to seek.
            // if the seeking is not successful we decrement this time and try the seek
            // again by subtracting 1 second from it.
            var startTime = DateTime.UtcNow;
            var streamSeekRelativeTime = TimeSpan.FromTicks(targetTime.Ticks + main.StartTimeOffset.Ticks); // Offset by start time

            // Perform long seeks until we end up with a relative target time where decoding
            // of frames before or on target time is possible.
            var isAtStartOfStream = false;
            while (isAtStartOfStream == false)
            {
                // Compute the seek target, mostly based on the relative Target Time
                var seekTarget = streamSeekRelativeTime.ToLong(timeBase);

                // Perform the seek. There is also avformat_seek_file which is the older version of av_seek_frame
                // Check if we are seeking before the start of the stream in this cyle. If so, simply seek to the
                // begining of the stream. Otherwise, seek normally.
                if (IsReadAborted)
                {
                    seekResult = ffmpeg.AVERROR_EXIT;
                }
                else
                {
                    StreamReadInterruptStartTime.Value = DateTime.UtcNow.Ticks;
                    if (streamSeekRelativeTime.Ticks <= main.StartTimeOffset.Ticks)
                    {
                        seekTarget = StartSeekTimestamp;
                        streamIndex = StartSeekStreamIndex;
                        isAtStartOfStream = true;
                    }
                    else
                    {
                        isAtStartOfStream = false;
                    }

                    seekResult = ffmpeg.av_seek_frame(InputContext, streamIndex, seekTarget, seekFlags);
                    Parent?.Log(MediaLogMessageType.Trace,
                        $"SEEK L: Elapsed: {startTime.FormatElapsed()} | Target: {streamSeekRelativeTime.Format()} | Seek: {seekTarget.Format()} | P0: {startPos.Format(1024)} | P1: {StreamPosition.Format(1024)} ");
                }

                // Flush the buffered packets and codec on every seek.
                Components.ClearPacketQueues();
                StateRequiresPictureAttachments = true;
                IsAtEndOfStream = false;

                // Ensure we had a successful seek operation
                if (seekResult < 0)
                {
                    Parent?.Log(MediaLogMessageType.Error, $"SEEK R: Elapsed: {startTime.FormatElapsed()} | Seek operation failed. Error code {seekResult}, {FFInterop.DecodeMessage(seekResult)}");
                    break;
                }

                // Read and decode frames for all components and check if the decoded frames
                // are on or right before the target time.
                StreamSeekDecode(
                    result,
                    targetTime,
                    Components.Main.MediaType == MediaType.Audio ? SeekRequirement.MainComponentOnly : SeekRequirement.AudioAndVideo);

                var firstAudioFrame = result.FirstOrDefault(f => f.MediaType == MediaType.Audio && f.StartTime <= targetTime);
                var firstVideoFrame = result.FirstOrDefault(f => f.MediaType == MediaType.Video && f.StartTime <= targetTime);

                var isAudioSeekInRange = Components.HasAudio == false
                    || (firstAudioFrame == null && Components.Main.MediaType != MediaType.Audio)
                    || (firstAudioFrame != null && firstAudioFrame.StartTime <= targetTime);

                var isVideoSeekInRange = Components.HasVideo == false
                    || (firstVideoFrame == null && Components.Main.MediaType != MediaType.Video)
                    || (firstVideoFrame != null && firstVideoFrame.StartTime <= targetTime);

                // If we have the correct range, no further processing is required.
                if (isAudioSeekInRange && isVideoSeekInRange)
                    break;

                // At this point the result is useless. Simply discard the decoded frames
                foreach (var frame in result) frame.Dispose();
                result.Clear();

                // Subtract 1 second from the relative target time.
                // a new seek target will be computed and we will do a av_seek_frame again.
                streamSeekRelativeTime = streamSeekRelativeTime.Subtract(TimeSpan.FromSeconds(1));
            }

            Parent?.Log(MediaLogMessageType.Trace,
                $"SEEK R: Elapsed: {startTime.FormatElapsed()} | Target: {streamSeekRelativeTime.Format()} | Seek: {default(long).Format()} | P0: {startPos.Format(1024)} | P1: {StreamPosition.Format(1024)} ");
            return result;

            #endregion

        }

        /// <summary>
        /// Seeks to the position at the start of the stream.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void StreamSeekToStart()
        {
            if (MediaStartTimeOffset == TimeSpan.MinValue) return;
            StreamReadInterruptStartTime.Value = DateTime.UtcNow.Ticks;

            var seekResult = ffmpeg.av_seek_frame(
                InputContext, StartSeekStreamIndex, StartSeekTimestamp, ffmpeg.AVSEEK_FLAG_BACKWARD);

            // Flush packets, state, and codec buffers
            Components.ClearPacketQueues();
            StateRequiresPictureAttachments = true;
            IsAtEndOfStream = false;

            if (seekResult < 0)
            {
                Parent?.Log(MediaLogMessageType.Warning,
                    $"SEEK 0: {nameof(StreamSeekToStart)} operation failed. Error code {seekResult}, {FFInterop.DecodeMessage(seekResult)}");
            }
        }

        /// <summary>
        /// Reads and decodes packets untill the required media components have frames on or right before the target time.
        /// </summary>
        /// <param name="result">The list of frames that is currently being processed. Frames will be added here.</param>
        /// <param name="targetTime">The target time in absolute 0-based time.</param>
        /// <param name="requirement">The requirement.</param>
        /// <returns>The number of decoded frames</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int StreamSeekDecode(List<MediaFrame> result, TimeSpan targetTime, SeekRequirement requirement)
        {
            var readSeekCycles = 0;

            // Create a holder of frame lists; one for each type of media
            var outputFrames = new Dictionary<MediaType, List<MediaFrame>>();
            foreach (var mediaType in Components.MediaTypes)
                outputFrames[mediaType] = new List<MediaFrame>();

            // Create a component requirement
            var requiredComponents = new List<MediaType>(8);
            if (requirement == SeekRequirement.AllComponents)
            {
                requiredComponents.AddRange(Components.MediaTypes.ToArray());
            }
            else if (requirement == SeekRequirement.AudioAndVideo)
            {
                if (Components.HasVideo) requiredComponents.Add(MediaType.Video);
                if (Components.HasAudio) requiredComponents.Add(MediaType.Audio);
            }
            else
            {
                requiredComponents.Add(Components.Main.MediaType);
            }

            // Start reading and decoding util we reach the target
            var isDoneSeeking = false;
            while (SignalAbortReadsRequested.Value == false && IsAtEndOfStream == false && isDoneSeeking == false)
            {
                readSeekCycles++;

                // Read the next packet
                var mediaType = Read();

                // Check if packet contains valid output
                if (outputFrames.ContainsKey(mediaType) == false)
                    continue;

                // Decode and add the frames to the corresponding output
                outputFrames[mediaType].AddRange(Components[mediaType].ReceiveFrames());

                // keept the frames list short
                foreach (var componentFrames in outputFrames.Values)
                {
                    // cleanup frames if the output becomes too big
                    if (componentFrames.Count >= 24)
                        StreamSeekDiscardFrames(componentFrames, targetTime);
                }

                // Based on the required components check the target time ranges
                isDoneSeeking = outputFrames.Where(t => requiredComponents.Contains(t.Key)).Select(t => t.Value)
                    .All(frames => frames.Count > 0 && frames.Max(f => f.StartTime) >= targetTime);
            }

            // Perform one final cleanup and aggregate the frames into a single,
            // interleaved collection
            foreach (var kvp in outputFrames)
            {
                var componentFrames = kvp.Value;
                StreamSeekDiscardFrames(componentFrames, targetTime);
                result.AddRange(componentFrames);
            }

            result.Sort();
            return readSeekCycles;
        }

        /// <summary>
        /// Drops the seek frames that are no longer needed.
        /// Target time should be provided in absolute, 0-based time
        /// </summary>
        /// <param name="frames">The frames.</param>
        /// <param name="targetTime">The target time.</param>
        /// <returns>The number of dropped frames</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int StreamSeekDiscardFrames(List<MediaFrame> frames, TimeSpan targetTime)
        {
            var result = 0;
            if (frames.Count <= 1) return result;
            frames.Sort();

            var framesToDrop = new List<int>(frames.Count);
            var frameType = frames[0].MediaType;

            for (var i = 0; i < frames.Count - 1; i++)
            {
                var currentFrame = frames[i];
                var nextFrame = frames[i + 1];

                if (currentFrame.StartTime >= targetTime)
                    break;
                if (currentFrame.StartTime < targetTime && nextFrame.StartTime <= targetTime)
                    framesToDrop.Add(i);
            }

            for (var i = framesToDrop.Count - 1; i >= 0; i--)
            {
                var dropIndex = framesToDrop[i];
                var frame = frames[dropIndex];
                frames.RemoveAt(dropIndex);
                frame.Dispose();
                result++;
            }

            return result;
        }

        #endregion

        #region IDisposable Support

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="alsoManaged"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        private void Dispose(bool alsoManaged)
        {
            if (IsDisposed) return;

            lock (ReadSyncRoot)
            {
                lock (DecodeSyncRoot)
                {
                    lock (ConvertSyncRoot)
                    {
                        Components.Dispose();
                        if (InputContext != null)
                        {
                            SignalAbortReads(false);
                            var inputContextPtr = InputContext;
                            ffmpeg.avformat_close_input(&inputContextPtr);
                            InputContext = null;
                        }

                        IsDisposed = true;
                    }
                }
            }
        }

        #endregion

    }
}
