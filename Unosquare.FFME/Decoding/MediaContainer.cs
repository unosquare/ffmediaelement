namespace Unosquare.FFME.Decoding
{
    using Core;
    using FFmpeg.AutoGen;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// A container capable of opening an input url,
    /// reading packets from it, decoding frames, seeking, and pausing and resuming network streams
    /// Code heavily based on https://raw.githubusercontent.com/FFmpeg/FFmpeg/release/3.2/ffplay.c
    /// The method pipeline should be: 
    /// 1. Set Options (or don't, for automatic options) and Initialize, 
    /// 2. Perform continuous Reads, 
    /// 3. Perform continuous Decodes and Converts/Materialize
    /// </summary>
    /// <seealso cref="System.IDisposable" />
    internal unsafe sealed class MediaContainer : IDisposable
    {

        #region Constants

        private static class EntryName
        {
            public const string ScanAllPMTs = "scan_all_pmts";
            public const string Title = "title";
        }

        #endregion

        #region Private Fields

        /// <summary>
        /// To detect redundat Dispose calls
        /// </summary>
        private bool IsDisposed = false;

        /// <summary>
        /// Holds a reference to an input context.
        /// </summary>
        internal AVFormatContext* InputContext = null;

        /// <summary>
        /// Determines if the stream seeks by bytes always
        /// </summary>
        private bool MediaSeeksByBytes = false;

        /// <summary>
        /// Hold the value for the internal property with the same name.
        /// Picture attachments are required when video streams support them
        /// and these attached packets must be read before reading the first frame
        /// of the stream and after seeking.
        /// </summary>
        private bool m_RequiresPictureAttachments = true;

        private readonly object LogSyncRoot = new object();

        private readonly object ReadSyncRoot = new object();

        private readonly object DecodeSyncRoot = new object();

        private readonly object ConvertSyncRoot = new object();

        #endregion

        #region Properties

        /// <summary>
        /// Gets the media URL. This is the input url, file or device that is read
        /// by this container.
        /// </summary>
        public string MediaUrl { get; private set; }

        /// <summary>
        /// The media initialization options.
        /// Options are applied when calling the Initialize method.
        /// After initialization, changing the options has no effect.
        /// </summary>
        public MediaOptions MediaOptions { get; } = new MediaOptions();

        /// <summary>
        /// Gets the name of the media format.
        /// </summary>
        public string MediaFormatName { get; private set; }

        /// <summary>
        /// Gets the media bitrate (bits per second). Returns 0 if not available.
        /// </summary>
        public long MediaBitrate
        {
            get
            {
                if (InputContext == null) return 0;
                return InputContext->bit_rate;
            }
        }

        /// <summary>
        /// Holds the metadata of the media file when the stream is initialized.
        /// </summary>
        public ReadOnlyDictionary<string, string> Metadata { get; private set; }

        /// <summary>
        /// Gets a value indicating whether an Input Context has been initialize.
        /// </summary>
        public bool IsInitialized { get { return InputContext != null; } }

        /// <summary>
        /// Gets the media start time by which all component streams are offset. 
        /// Typically 0 but it could be something other than 0.
        /// </summary>
        internal TimeSpan MediaStartTimeOffset { get; private set; }

        /// <summary>
        /// Gets the seek start timestamp.
        /// </summary>
        internal long SeekStartTimestamp
        {
            get
            {
                var startSeekTime = (long)(MediaStartTimeOffset.TotalSeconds * ffmpeg.AV_TIME_BASE);
                if (MediaSeeksByBytes) startSeekTime = 0;
                return startSeekTime;
            }
        }

        /// <summary>
        /// Gets the duration of the media.
        /// If this information is not available (i.e. realtime media) it will
        /// be set to TimeSpan.MinValue
        /// </summary>
        public TimeSpan MediaDuration { get; private set; }

        /// <summary>
        /// Will be set to true whenever an End Of File situation is reached.
        /// </summary>
        public bool IsAtEndOfStream { get; private set; }

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
        public bool IsStreamSeekable { get { return MediaDuration.TotalSeconds > 0 && MediaDuration != TimeSpan.MinValue; } }

        /// <summary>
        /// Gets a value indicating whether this container represents realtime media.
        /// If the format name is rtp, rtsp, or sdp or if the url starts with udp: or rtp:
        /// then this property will be set to true.
        /// </summary>
        public bool IsStreamRealtime { get; private set; }

        /// <summary>
        /// Provides direct access to the individual Media components of the input stream.
        /// </summary>
        public MediaComponentSet Components { get; } = new MediaComponentSet();

        #endregion

        #region Internal Properties

        /// <summary>
        /// Gets the time the last packet was read from the input
        /// </summary>
        internal DateTime StreamLastReadTimeUtc { get; private set; } = DateTime.MinValue;

        /// <summary>
        /// For RTSP and other realtime streams reads can be suspended.
        /// </summary>
        internal bool CanReadSuspend { get; private set; }

        /// <summary>
        /// For RTSP and other realtime streams reads can be suspended.
        /// This property will return true if reads have been suspended.
        /// </summary>
        internal bool IsReadSuspended { get; private set; }

        /// <summary>
        /// Gets a value indicating whether a packet read delay witll be enforced.
        /// RSTP formats or MMSH Urls will have this property set to true.
        /// Reading packets will block for at most 10 milliseconds depending on the last read time.
        /// This is a hack according to the source code in ffplay.c
        /// </summary>
        internal bool RequiresReadDelay { get; private set; }

        /// <summary>
        /// Picture attachments are required when video streams support them
        /// and these attached packets must be read before reading the first frame
        /// of the stream and after seeking. This property is not part of the public API
        /// and is meant more for internal purposes
        internal bool RequiresPictureAttachments
        {
            get
            {
                var canRequireAttachments = Components.HasVideo
                    && (Components.Video.Stream->disposition & ffmpeg.AV_DISPOSITION_ATTACHED_PIC) != 0;

                if (canRequireAttachments == false)
                    return false;
                else
                    return m_RequiresPictureAttachments;
            }
            set
            {
                var canRequireAttachments = Components.HasVideo
                    && (Components.Video.Stream->disposition & ffmpeg.AV_DISPOSITION_ATTACHED_PIC) != 0;

                if (canRequireAttachments)
                    m_RequiresPictureAttachments = value;
                else
                    m_RequiresPictureAttachments = false;
            }
        }

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaContainer"/> class.
        /// </summary>
        /// <param name="mediaUrl">The media URL.</param>
        /// <exception cref="System.ArgumentNullException">mediaUrl</exception>
        public MediaContainer(string mediaUrl)
        {
            // Argument Validation
            if (string.IsNullOrWhiteSpace(mediaUrl))
                throw new ArgumentNullException($"{nameof(mediaUrl)}");

            // Initialize the library (if not already done)
            Utils.RegisterFFmpeg(null);

            // Create the options object
            MediaUrl = mediaUrl;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Initializes the input context in order to start reading from the Media URL.
        /// Any Media Options must be set before this method is called.
        /// </summary>
        public void Initialize()
        {
            lock (ReadSyncRoot)
            {
                StreamInitialize();
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
        /// <returns></returns>
        public List<MediaFrame> Seek(TimeSpan position)
        {
            lock (ReadSyncRoot)
            {
                if (IsDisposed || InputContext == null) throw new InvalidOperationException("No input context initialized");
                return StreamSeek(position);
            }
        }

        /// <summary>
        /// Reads the next available packet, sending the packet to the corresponding
        /// internal media component. It also sets IsAtEndOfStream property.
        /// Returns true if the packet was accepted by any of the media components.
        /// Returns false if the packet was not accepted by any of the media components
        /// or if reading failed (i.e. End of stream already or read error).
        /// Packets are queued internally. To dequeue them you can call the DocodeNext
        /// method until the packet buffer count becomes 0.
        /// </summary>
        /// <exception cref="MediaContainerException"></exception>
        public MediaType Read()
        {
            lock (ReadSyncRoot)
            {
                if (IsDisposed || InputContext == null) throw new InvalidOperationException("No input context initialized");
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
        /// <returns></returns>
        public List<MediaFrame> Decode()
        {
            lock (DecodeSyncRoot)
            {
                var result = new List<MediaFrame>(64);
                foreach (var component in Components.All)
                    result.AddRange(component.DecodeNextPacket());

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
        /// <param name="releaseInput">if set to <c>true</c> releases the raw frame source from unmanaged memory.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">input</exception>
        /// <exception cref="System.ArgumentException">
        /// input
        /// or
        /// input
        /// </exception>
        /// <exception cref="MediaContainerException">MediaType</exception>
        public MediaBlock Convert(MediaFrame input, ref MediaBlock output, bool releaseInput = true)
        {
            lock (ConvertSyncRoot)
            {
                if (IsDisposed || InputContext == null) throw new InvalidOperationException("No input context initialized");

                // Check the input parameters
                if (input == null)
                    throw new ArgumentNullException($"{nameof(input)} cannot be null.");

                try
                {
                    switch (input.MediaType)
                    {
                        case MediaType.Video:
                            if (input.IsStale) throw new ArgumentException(
                                $"The {nameof(input)} {nameof(MediaFrame)} has already been released (it's stale).");

                            if (Components.HasVideo) Components.Video.MaterializeFrame(input, ref output);
                            return output;

                        case MediaType.Audio:
                            if (input.IsStale) throw new ArgumentException(
                                $"The {nameof(input)} {nameof(MediaFrame)} has already been released (it's stale).");

                            if (Components.HasAudio) Components.Audio.MaterializeFrame(input, ref output);
                            return output;

                        case MediaType.Subtitle:
                            // We don't need to heck if subtitles are stale because they are immediately released
                            // upon decoding. This is because there is no unmanaged allocator for AVSubtitle.

                            if (Components.HasSubtitles) Components.Subtitles.MaterializeFrame(input, ref output);
                            return output;

                        default:
                            throw new MediaContainerException($"Unable to materialize {nameof(MediaType)} {(int)input.MediaType}");
                    }
                }
                finally
                {
                    if (releaseInput)
                        input.Dispose();
                }
            }
        }

        /// <summary>
        /// Closes the input context immediately releasing all resources.
        /// This method is equivalent to calling the dispose method.
        /// </summary>
        public void Close()
        {
            lock (ReadSyncRoot)
            {
                lock (DecodeSyncRoot)
                {
                    if (IsDisposed || InputContext == null) throw new InvalidOperationException("No input context initialized");
                    Dispose();
                }
            }
        }

        /// <summary>
        /// Logs the specified message.
        /// </summary>
        /// <param name="t">The t.</param>
        /// <param name="message">The message.</param>
        public void Log(MediaLogMessageType t, string message)
        {
            lock (LogSyncRoot)
                MediaOptions.LogMessageCallback?.Invoke(t, message);
        }

        #endregion

        #region Private Stream Methods

        /// <summary>
        /// Initializes the input context to start read operations.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">The input context has already been initialized.</exception>
        /// <exception cref="MediaContainerException"></exception>
        private void StreamInitialize()
        {
            if (IsInitialized)
                throw new InvalidOperationException("The input context has already been initialized.");

            // Retrieve the input format (null = auto for default)
            AVInputFormat* inputFormat = null;
            if (string.IsNullOrWhiteSpace(MediaOptions.ForcedInputFormat) == false)
            {
                inputFormat = ffmpeg.av_find_input_format(MediaOptions.ForcedInputFormat);
                Log(MediaLogMessageType.Warning, $"Format '{MediaOptions.ForcedInputFormat}' not found. Will use automatic format detection.");
            }

            try
            {
                // Create the input format context, and open the input based on the provided format options.
                using (var formatOptions = new FFDictionary(MediaOptions.FormatOptions))
                {
                    if (formatOptions.HasKey(EntryName.ScanAllPMTs) == false)
                        formatOptions.Set(EntryName.ScanAllPMTs, "1", true);

                    // Allocate the input context and save it
                    InputContext = ffmpeg.avformat_alloc_context();

                    // Try to open the input
                    fixed (AVFormatContext** inputContext = &InputContext)
                    {
                        // Open the input file
                        var openResult = 0;
                        fixed (AVDictionary** reference = &formatOptions.Pointer)
                            openResult = ffmpeg.avformat_open_input(inputContext, MediaUrl, inputFormat, reference);

                        // Validate the open operation
                        if (openResult < 0) throw new MediaContainerException($"Could not open '{MediaUrl}'. Error {openResult}: {Utils.FFErrorMessage(openResult)}");
                    }

                    // Set some general properties
                    MediaFormatName = Utils.PtrToString(InputContext->iformat->name);

                    // If there are any optins left in the dictionary, it means they did not get used (invalid options).
                    formatOptions.Remove(EntryName.ScanAllPMTs);

                    var currentEntry = formatOptions.First();
                    while (currentEntry != null && currentEntry?.Key != null)
                    {
                        Log(MediaLogMessageType.Warning, $"Invalid format option: '{currentEntry.Key}'");
                        currentEntry = formatOptions.Next(currentEntry);
                    }

                }

                // Inject Codec Parameters
                if (MediaOptions.GeneratePts) InputContext->flags |= ffmpeg.AVFMT_FLAG_GENPTS;
                ffmpeg.av_format_inject_global_side_data(InputContext);

                // This is useful for file formats with no headers such as MPEG. This function also computes the real framerate in case of MPEG-2 repeat frame mode.
                if (ffmpeg.avformat_find_stream_info(InputContext, null) < 0)
                    Log(MediaLogMessageType.Warning, $"{MediaUrl}: could read stream info.");

                // HACK: From ffplay.c: maybe should not use avio_feof() to test for the end
                if (InputContext->pb != null) InputContext->pb->eof_reached = 0;

                // Setup initial state variables
                {
                    var metadataDictionary = new Dictionary<string, string>();

                    var metadataEntry = ffmpeg.av_dict_get(InputContext->metadata, "", null, ffmpeg.AV_DICT_IGNORE_SUFFIX);
                    while (metadataEntry != null)
                    {
                        metadataDictionary[Utils.PtrToString(metadataEntry->key)] = Utils.PtrToString(metadataEntry->value);
                        metadataEntry = ffmpeg.av_dict_get(InputContext->metadata, "", metadataEntry, ffmpeg.AV_DICT_IGNORE_SUFFIX);
                    }

                    Metadata = new ReadOnlyDictionary<string, string>(metadataDictionary);
                }

                IsStreamRealtime = new[] { "rtp", "rtsp", "sdp" }.Any(s => MediaFormatName.Equals(s)) ||
                    (InputContext->pb != null && new[] { "rtp:", "udp:" }.Any(s => MediaUrl.StartsWith(s)));

                RequiresReadDelay = MediaFormatName.Equals("rstp") || MediaUrl.StartsWith("mmsh:");
                var inputAllowsDiscontinuities = (InputContext->iformat->flags & ffmpeg.AVFMT_TS_DISCONT) != 0;
                MediaSeeksByBytes = inputAllowsDiscontinuities && (MediaFormatName.Equals("ogg") == false);
                MediaSeeksByBytes = MediaSeeksByBytes && MediaBitrate > 0;

                // Compute start time and duration (if possible)
                MediaStartTimeOffset = InputContext->start_time.ToTimeSpan();
                if (MediaStartTimeOffset == TimeSpan.MinValue)
                {
                    Log(MediaLogMessageType.Warning, $"Unable to determine the media start time offset. Media start time offset will be set to zero.");
                    MediaStartTimeOffset = TimeSpan.Zero;
                }

                MediaDuration = InputContext->duration.ToTimeSpan();

                // Open the best suitable streams. Throw if no audio and/or video streams are found
                StreamCreateComponents();

                // Verify the stream input start offset. This is the zero measure for all sub-streams.
                var minOffset = Components.All.Count > 0 ? Components.All.Min(c => c.StartTimeOffset) : MediaStartTimeOffset;
                if (minOffset != MediaStartTimeOffset)
                {
                    Log(MediaLogMessageType.Warning, $"Input Start: {MediaStartTimeOffset.Debug()} Comp. Start: {minOffset.Debug()}. Input start will be updated.");
                    MediaStartTimeOffset = minOffset;
                }

                // For network streams, figure out if reads can be paused and then start them.
                CanReadSuspend = ffmpeg.av_read_pause(InputContext) == 0;
                ffmpeg.av_read_play(InputContext);
                IsReadSuspended = false;

                // Initially and depending on the video component, rquire picture attachments.
                // Picture attachments are only required after the first read or after a seek.
                RequiresPictureAttachments = true;

                // Seek to the begining of the file
                StreamSeekToStart();

            }
            catch (Exception ex)
            {
                Log(MediaLogMessageType.Error,
                    $"Fatal error initializing {nameof(MediaContainer)} instance. {ex.Message}");
                Dispose(true);
                throw;
            }
        }

        /// <summary>
        /// Creates the stream components by first finding the best available streams.
        /// Then it initializes the components of the correct type each.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Unosquare.FFME.MediaContainerException"></exception>
        private void StreamCreateComponents()
        {
            // Display stream information in the console if we are debugging
            if (Debugger.IsAttached)
                ffmpeg.av_dump_format(InputContext, 0, MediaUrl, 0);

            // Initialize and clear all the stream indexes.
            var streamIndexes = new int[(int)AVMediaType.AVMEDIA_TYPE_NB];
            for (var i = 0; i < (int)AVMediaType.AVMEDIA_TYPE_NB; i++)
                streamIndexes[i] = -1;

            { // Find best streams for each component

                // if we passed null instead of the requestedCodec pointer, then
                // find_best_stream would not validate whether a valid decoder is registed.
                AVCodec* requestedCodec = null;

                if (MediaOptions.IsVideoDisabled == false)
                    streamIndexes[(int)AVMediaType.AVMEDIA_TYPE_VIDEO] =
                        ffmpeg.av_find_best_stream(InputContext, AVMediaType.AVMEDIA_TYPE_VIDEO,
                                            streamIndexes[(int)AVMediaType.AVMEDIA_TYPE_VIDEO], -1,
                                            &requestedCodec, 0);

                if (MediaOptions.IsAudioDisabled == false)
                    streamIndexes[(int)AVMediaType.AVMEDIA_TYPE_AUDIO] =
                    ffmpeg.av_find_best_stream(InputContext, AVMediaType.AVMEDIA_TYPE_AUDIO,
                                        streamIndexes[(int)AVMediaType.AVMEDIA_TYPE_AUDIO],
                                        streamIndexes[(int)AVMediaType.AVMEDIA_TYPE_VIDEO],
                                        &requestedCodec, 0);

                if (MediaOptions.IsSubtitleDisabled == false)
                    streamIndexes[(int)AVMediaType.AVMEDIA_TYPE_SUBTITLE] =
                    ffmpeg.av_find_best_stream(InputContext, AVMediaType.AVMEDIA_TYPE_SUBTITLE,
                                        streamIndexes[(int)AVMediaType.AVMEDIA_TYPE_SUBTITLE],
                                        (streamIndexes[(int)AVMediaType.AVMEDIA_TYPE_AUDIO] >= 0 ?
                                         streamIndexes[(int)AVMediaType.AVMEDIA_TYPE_AUDIO] :
                                         streamIndexes[(int)AVMediaType.AVMEDIA_TYPE_VIDEO]),
                                        &requestedCodec, 0);
            }


            foreach (var t in Constants.MediaTypes)
            {
                if (t < 0) continue;

                try
                {
                    if (streamIndexes[(int)t] >= 0)
                    {
                        switch (t)
                        {
                            case MediaType.Video:
                                Components[t] = new VideoComponent(this, streamIndexes[(int)t]);
                                break;
                            case MediaType.Audio:
                                Components[t] = new AudioComponent(this, streamIndexes[(int)t]);
                                break;
                            case MediaType.Subtitle:
                                Components[t] = new SubtitleComponent(this, streamIndexes[(int)t]);
                                break;
                            default:
                                continue;
                        }

                        Log(MediaLogMessageType.Debug, $"{t}: Selected Stream Index = {Components[t].StreamIndex}");
                    }

                }
                catch (Exception ex)
                {
                    Log(MediaLogMessageType.Error, $"Unable to initialize {t.ToString()} component. {ex.Message}");
                }
            }

            // Verify we have at least 1 valid stream component to work with.
            if (Components.HasVideo == false && Components.HasAudio == false)
                throw new MediaContainerException($"{MediaUrl}: No audio or video streams found to decode.");

        }

        /// <summary>
        /// Reads the next packet in the underlying stream and enqueues in the corresponding media component
        /// </summary>
        /// <returns></returns>
        /// <exception cref="System.InvalidOperationException">Initialize</exception>
        /// <exception cref="MediaContainerException"></exception>
        private MediaType StreamRead()
        {
            // Check the context has been initialized
            if (IsInitialized == false)
                throw new InvalidOperationException($"Please call the {nameof(Initialize)} method before attempting this operation.");

            // Ensure read is not suspended
            StreamReadResume();

            if (RequiresReadDelay)
            {
                // in ffplay.c this is referenced via CONFIG_RTSP_DEMUXER || CONFIG_MMSH_PROTOCOL
                var millisecondsDifference = (int)Math.Round(DateTime.UtcNow.Subtract(StreamLastReadTimeUtc).TotalMilliseconds, 2);
                var sleepMilliseconds = 10 - millisecondsDifference;

                // wait at least 10 ms to avoid trying to get another packet
                if (sleepMilliseconds > 0)
                    Task.Delay(sleepMilliseconds).Wait(); // XXX: horrible
            }

            if (RequiresPictureAttachments)
            {
                var attachedPacket = ffmpeg.av_packet_alloc();
                var copyPacketResult = ffmpeg.av_copy_packet(attachedPacket, &Components.Video.Stream->attached_pic);
                if (copyPacketResult >= 0 && attachedPacket != null)
                {
                    Components.Video.SendPacket(attachedPacket);
                    Components.Video.SendEmptyPacket();
                }

                RequiresPictureAttachments = false;
            }

            // Allocate the packet to read
            var readPacket = ffmpeg.av_packet_alloc();
            // TODO: for network streams av_read_frame will sometimes block forever. We need a way to retry or timeout or exit.
            var readResult = ffmpeg.av_read_frame(InputContext, readPacket);
            StreamLastReadTimeUtc = DateTime.UtcNow;

            if (readResult < 0)
            {
                // Handle failed packet reads. We don't need the allocated packet anymore
                ffmpeg.av_packet_free(&readPacket);

                // Detect an end of file situation (makes the readers enter draining mode)
                if ((readResult == Constants.AVERROR_EOF || ffmpeg.avio_feof(InputContext->pb) != 0))
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
                        throw new MediaContainerException($"Input has produced an error. Error Code {readResult}, {Utils.FFErrorMessage(readResult)}");
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
                    ffmpeg.av_packet_free(&readPacket);

                return componentType;
            }

            return MediaType.None;
        }

        /// <summary>
        /// Suspends / pauses network streams
        /// This should only be called upon Dispose
        /// </summary>
        private void StreamReadSuspend()
        {
            if (InputContext == null || CanReadSuspend == false || IsReadSuspended) return;
            ffmpeg.av_read_pause(InputContext);
            IsReadSuspended = true;
        }

        /// <summary>
        /// Resumes the reads of network streams
        /// </summary>
        private void StreamReadResume()
        {
            if (InputContext == null || CanReadSuspend == false || IsReadSuspended == false) return;
            ffmpeg.av_read_play(InputContext);
            IsReadSuspended = false;
        }

        /// <summary>
        /// Drops the seek frames that are no longer needed.
        /// Target time should be provided in absolute, 0-based time
        /// </summary>
        /// <param name="frames">The frames.</param>
        /// <param name="targetTime">The target time.</param>
        /// <returns></returns>
        private int DropSeekFrames(List<MediaFrame> frames, TimeSpan targetTime)
        {
            var result = 0;
            if (frames.Count < 2) return result;
            frames.Sort();

            var framesToDrop = new List<int>(frames.Count);
            var frameType = frames[0].MediaType;

            for (var i = 0; i < frames.Count - 1; i++)
            {
                var currentFrame = frames[i];
                var nextFrame = frames[i + 1];

                if (currentFrame.StartTime >= targetTime)
                    break;
                else if (currentFrame.StartTime < targetTime && nextFrame.StartTime <= targetTime)
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

        /// <summary>
        /// Seeks to the position at the start of the stream.
        /// </summary>
        private void StreamSeekToStart()
        {
            if (MediaStartTimeOffset == TimeSpan.MinValue) return;
            var seekResult = ffmpeg.av_seek_frame(InputContext, -1, SeekStartTimestamp,
                MediaSeeksByBytes ? ffmpeg.AVSEEK_FLAG_BYTE : ffmpeg.AVSEEK_FLAG_BACKWARD);

            Components.ClearPacketQueues();
            RequiresPictureAttachments = true;
            IsAtEndOfStream = false;
        }

        /// <summary>
        /// Seeks to the exact or prior frame of the main stream.
        /// Supports byte seeking.
        /// </summary>
        /// <param name="targetTime">The target time.</param>
        /// <param name="doPreciseSeek">if set to <c>true</c> [do precise seek].</param>
        /// <returns></returns>
        private List<MediaFrame> StreamSeek(TimeSpan targetTime)
        {
            // Create the output result object
            var result = new List<MediaFrame>();

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
                Log(MediaLogMessageType.Warning, $"Unable to seek. Underlying stream does not support seeking.");
                return result;
            }

            // Select the main component
            var main = Components.Main;
            if (main == null) return result;

            // clamp the target time to the component's bounds
            // TODO: Check bounds of byte-based seeking and bounds of end time
            if (targetTime.Ticks + main.StartTimeOffset.Ticks < main.StartTimeOffset.Ticks)
                targetTime = TimeSpan.FromTicks(main.StartTimeOffset.Ticks);

            if (targetTime.Ticks > main.Duration.Ticks)
                targetTime = TimeSpan.FromTicks(main.Duration.Ticks);

            // Stream seeking by main component
            // The backward flag means that we want to seek to at MOST the target position
            var seekFlags = ffmpeg.AVSEEK_FLAG_BACKWARD;
            var streamIndex = main.StreamIndex;
            var timeBase = main.Stream->time_base;

            // The seek target is computed by using the absolute, 0-based target time and adding the component stream's start time
            if (MediaSeeksByBytes)
            {
                seekFlags = ffmpeg.AVSEEK_FLAG_BYTE;
                streamIndex = -1;
            }

            // Perform the stream seek
            var seekResult = 0;
            var startPos = StreamPosition;

            #endregion

            #region Perform FFmpeg API Seek

            // The relative target time keeps track of where to seek.
            // if the seeking is not successful we decrement this time and try the seek
            // again by subtracting 1 second from it.
            var startTime = DateTime.UtcNow;
            var relativeTargetTime = MediaSeeksByBytes ?
                targetTime :
                TimeSpan.FromTicks(targetTime.Ticks + main.StartTimeOffset.Ticks); // TODO: check this calculation

            // Perform long seeks until we end up with a relative target time where decoding
            // of frames before or on target time is possible.
            var isAtStartOfStream = false;
            while (isAtStartOfStream == false)
            {
                // Compute the seek target, mostly based on the relative Target Time
                var seekTarget = MediaSeeksByBytes ?
                    (long)(MediaBitrate * relativeTargetTime.TotalSeconds / 8d) :
                    (long)Math.Round((relativeTargetTime.TotalSeconds) * timeBase.den / timeBase.num, 0);

                // Perform the seek. There is also avformat_seek_file which is the older version of av_seek_frame
                // Check if we are seeking before the start of the stream in this cyle. If so, simply seek to the
                // begining of the stream. Otherwise, seek normally.
                if (relativeTargetTime.Ticks <= main.StartTimeOffset.Ticks)
                {
                    seekResult = ffmpeg.av_seek_frame(InputContext, -1, SeekStartTimestamp, seekFlags);
                    isAtStartOfStream = true;
                }
                else
                {
                    seekResult = ffmpeg.av_seek_frame(InputContext, streamIndex, seekTarget, seekFlags);
                    isAtStartOfStream = false;
                }


                Log(MediaLogMessageType.Trace,
                    $"SEEK L: Elapsed: {startTime.DebugElapsedUtc()} | Target: {relativeTargetTime.Debug()} | Seek: {seekTarget.Debug()} | P0: {startPos.Debug(1024)} | P1: {StreamPosition.Debug(1024)} ");

                // Flush the buffered packets and codec on every seek.
                Components.ClearPacketQueues();
                RequiresPictureAttachments = true;
                IsAtEndOfStream = false;

                // Ensure we had a successful seek operation
                if (seekResult < 0)
                {
                    Log(MediaLogMessageType.Trace, $"SEEK R: Elapsed: {startTime.DebugElapsedUtc()} | Seek operation failed. Error code {seekResult}, {Utils.FFErrorMessage(seekResult)}");
                    break;
                }

                // Read and decode frames for all components and check if the decoded frames
                // are on or right before the target time.
                StreamSeekDecode(result, targetTime);
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
                relativeTargetTime = relativeTargetTime.Subtract(TimeSpan.FromSeconds(1));

            }

            Log(MediaLogMessageType.Trace,
                $"SEEK R: Elapsed: {startTime.DebugElapsedUtc()} | Target: {relativeTargetTime.Debug()} | Seek: {default(long).Debug()} | P0: {startPos.Debug(1024)} | P1: {StreamPosition.Debug(1024)} ");
            return result;

            #endregion

        }

        /// <summary>
        /// Reads and decodes packets untill all media components have frames on or after the start time.
        /// </summary>
        /// <param name="result">The list of frames that is currently being processed. Frames will be added here.</param>
        /// <param name="targetTime">The target time in absolute 0-based time.</param>
        /// <returns></returns>
        private int StreamSeekDecode(List<MediaFrame> result, TimeSpan targetTime)
        {
            var readSeekCycles = 0;

            // Create a holder of frame lists; one for each type of media
            var outputFrames = new Dictionary<MediaType, List<MediaFrame>>();
            foreach (var mediaType in Components.MediaTypes)
                outputFrames[mediaType] = new List<MediaFrame>();

            // Start reading and decoding util we reach the target
            while (IsAtEndOfStream == false)
            {
                readSeekCycles++;

                // Read the next packet
                var mediaType = Read();

                // Check if packet contains valid output
                if (outputFrames.ContainsKey(mediaType) == false)
                    continue;

                // Decode and add the frames to the corresponding output
                outputFrames[mediaType].AddRange(Components[mediaType].DecodeNextPacket());

                // check if we are done with seeking
                // all streams must have at least 1 frame on or after thae target time
                var isDoneSeeking = true;
                foreach (var kvp in outputFrames)
                {
                    var componentFrames = kvp.Value;

                    // Ignore seek target requirement if we did not get 
                    // frames from a non-primary component
                    if (Components.All.Count > 1
                        && Components.Main.MediaType != kvp.Key
                        && componentFrames.Count == 0)
                        continue;

                    // cleanup frames if the output becomes too big
                    if (componentFrames.Count >= 24)
                        DropSeekFrames(componentFrames, targetTime);

                    // check if we have a valid range
                    var hasTargetRange = componentFrames.Count > 0
                        && componentFrames.Max(f => f.StartTime) >= targetTime;

                    // Set done seeking = false because range is non-matching
                    if (hasTargetRange == false)
                    {
                        isDoneSeeking = false;
                        break;
                    }
                }

                if (isDoneSeeking)
                    break;
            }

            // Perform one final cleanup and aggregate the frames into a single,
            // interleaved collection
            foreach (var kvp in outputFrames)
            {
                var componentFrames = kvp.Value;
                DropSeekFrames(componentFrames, targetTime);
                result.AddRange(componentFrames);
            }

            result.Sort();
            return readSeekCycles;
        }

        #endregion

        #region IDisposable Support

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="alsoManaged">
        ///   <c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        private void Dispose(bool alsoManaged)
        {
            if (IsDisposed) return;

            if (alsoManaged)
            {
                Components.Dispose();

                if (InputContext != null)
                {
                    StreamReadSuspend();
                    fixed (AVFormatContext** inputContext = &InputContext)
                        ffmpeg.avformat_close_input(inputContext);

                    ffmpeg.avformat_free_context(InputContext);

                    InputContext = null;
                }
            }

            IsDisposed = true;
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        #endregion
    }
}
