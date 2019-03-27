namespace Unosquare.FFME.Container
{
    using Diagnostics;
    using FFmpeg.AutoGen;
    using Primitives;
    using System;
    using System.Globalization;
    using System.Runtime.CompilerServices;

    /// <inheritdoc />
    /// <summary>
    /// Represents a media component of a given media type within a
    /// media container. Derived classes must implement frame handling
    /// logic.
    /// </summary>
    /// <seealso cref="IDisposable" />
    internal abstract unsafe class MediaComponent : IDisposable, ILoggingSource
    {
        #region Private Declarations

        /// <summary>
        /// Related to issue 94, looks like FFmpeg requires exclusive access when calling avcodec_open2()
        /// </summary>
        private static readonly object CodecLock = new object();

        /// <summary>
        /// The logging handler
        /// </summary>
        private readonly ILoggingHandler m_LoggingHandler;

        /// <summary>
        /// Contains the packets pending to be sent to the decoder
        /// </summary>
        private readonly PacketQueue Packets = new PacketQueue();

        /// <summary>
        /// The decode packet function
        /// </summary>
        private readonly Func<MediaFrame> DecodePacketFunction;

        /// <summary>
        /// Detects redundant, unmanaged calls to the Dispose method.
        /// </summary>
        private readonly AtomicBoolean m_IsDisposed = new AtomicBoolean(false);

        /// <summary>
        /// Determines if packets have been fed into the codec and frames can be decoded.
        /// </summary>
        private readonly AtomicBoolean m_HasCodecPackets = new AtomicBoolean(false);

        /// <summary>
        /// Holds a reference to the associated input context stream
        /// </summary>
        private readonly IntPtr m_Stream;

        /// <summary>
        /// Holds a reference to the Codec Context.
        /// </summary>
        private IntPtr m_CodecContext;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaComponent"/> class.
        /// </summary>
        /// <param name="container">The container.</param>
        /// <param name="streamIndex">Index of the stream.</param>
        /// <exception cref="ArgumentNullException">container</exception>
        /// <exception cref="MediaContainerException">The container exception.</exception>
        protected MediaComponent(MediaContainer container, int streamIndex)
        {
            // Ported from: https://github.com/FFmpeg/FFmpeg/blob/master/fftools/ffplay.c#L2559
            Container = container ?? throw new ArgumentNullException(nameof(container));
            m_LoggingHandler = ((ILoggingSource)Container).LoggingHandler;
            m_CodecContext = new IntPtr(ffmpeg.avcodec_alloc_context3(null));
            RC.Current.Add(CodecContext);
            StreamIndex = streamIndex;
            m_Stream = new IntPtr(container.InputContext->streams[streamIndex]);
            StreamInfo = container.MediaInfo.Streams[streamIndex];

            // Set default codec context options from probed stream
            var setCodecParamsResult = ffmpeg.avcodec_parameters_to_context(CodecContext, Stream->codecpar);

            if (setCodecParamsResult < 0)
            {
                this.LogWarning(Aspects.Component,
                    $"Could not set codec parameters. Error code: {setCodecParamsResult}");
            }

            // We set the packet timebase in the same timebase as the stream as opposed to the typical AV_TIME_BASE
            if (this is VideoComponent && Container.MediaOptions.VideoForcedFps > 0)
            {
                var fpsRational = ffmpeg.av_d2q(Container.MediaOptions.VideoForcedFps, 1000000);
                Stream->r_frame_rate = fpsRational;
                CodecContext->pkt_timebase = new AVRational { num = fpsRational.den, den = fpsRational.num };
            }
            else
            {
                CodecContext->pkt_timebase = Stream->time_base;
            }

            // Find the default decoder codec from the stream and set it.
            var defaultCodec = ffmpeg.avcodec_find_decoder(Stream->codec->codec_id);
            AVCodec* forcedCodec = null;

            // If set, change the codec to the forced codec.
            if (Container.MediaOptions.DecoderCodec.ContainsKey(StreamIndex) &&
                string.IsNullOrWhiteSpace(Container.MediaOptions.DecoderCodec[StreamIndex]) == false)
            {
                var forcedCodecName = Container.MediaOptions.DecoderCodec[StreamIndex];
                forcedCodec = ffmpeg.avcodec_find_decoder_by_name(forcedCodecName);
                if (forcedCodec == null)
                {
                    this.LogWarning(Aspects.Component,
                        $"COMP {MediaType.ToString().ToUpperInvariant()}: " +
                        $"Unable to set decoder codec to '{forcedCodecName}' on stream index {StreamIndex}");
                }
            }

            // Check we have a valid codec to open and process the stream.
            if (defaultCodec == null && forcedCodec == null)
            {
                var errorMessage = $"Fatal error. Unable to find suitable decoder for {Stream->codec->codec_id.ToString()}";
                CloseComponent();
                throw new MediaContainerException(errorMessage);
            }

            var codecCandidates = new[] { forcedCodec, defaultCodec };
            AVCodec* selectedCodec = null;
            var codecOpenResult = 0;

            foreach (var codec in codecCandidates)
            {
                if (codec == null)
                    continue;

                // Pass default codec stuff to the codec context
                CodecContext->codec_id = codec->id;

                // Process the decoder options
                {
                    var decoderOptions = Container.MediaOptions.DecoderParams;

                    // Configure the codec context flags
                    if (decoderOptions.EnableFastDecoding) CodecContext->flags2 |= ffmpeg.AV_CODEC_FLAG2_FAST;
                    if (decoderOptions.EnableLowDelayDecoding) CodecContext->flags |= ffmpeg.AV_CODEC_FLAG_LOW_DELAY;

                    // process the low res option
                    if (decoderOptions.LowResolutionIndex != ResolutionDivider.Full && codec->max_lowres > 0)
                    {
                        var lowResOption = Math.Min((byte)decoderOptions.LowResolutionIndex, codec->max_lowres)
                            .ToString(CultureInfo.InvariantCulture);
                        decoderOptions.LowResIndexOption = lowResOption;
                    }

                    // Ensure ref counted frames for audio and video decoding
                    if (CodecContext->codec_type == AVMediaType.AVMEDIA_TYPE_VIDEO || CodecContext->codec_type == AVMediaType.AVMEDIA_TYPE_AUDIO)
                        decoderOptions.RefCountedFrames = "1";
                }

                // Setup additional settings. The most important one is Threads -- Setting it to 1 decoding is very slow. Setting it to auto
                // decoding is very fast in most scenarios.
                var codecOptions = Container.MediaOptions.DecoderParams.GetStreamCodecOptions(Stream->index);

                // Enable Hardware acceleration if requested
                (this as VideoComponent)?.AttachHardwareDevice(container.MediaOptions.VideoHardwareDevice);

                // Open the CodecContext. This requires exclusive FFmpeg access
                lock (CodecLock)
                {
                    var codecOptionsRef = codecOptions.Pointer;
                    codecOpenResult = ffmpeg.avcodec_open2(CodecContext, codec, &codecOptionsRef);
                    codecOptions.UpdateReference(codecOptionsRef);
                }

                // Check if the codec opened successfully
                if (codecOpenResult < 0)
                {
                    this.LogWarning(Aspects.Component,
                        $"Unable to open codec '{Utilities.PtrToStringUTF8(codec->name)}' on stream {streamIndex}");

                    continue;
                }

                // If there are any codec options left over from passing them, it means they were not consumed
                var currentEntry = codecOptions.First();
                while (currentEntry?.Key != null)
                {
                    this.LogWarning(Aspects.Component,
                        $"Invalid codec option: '{currentEntry.Key}' for codec '{Utilities.PtrToStringUTF8(codec->name)}', stream {streamIndex}");
                    currentEntry = codecOptions.Next(currentEntry);
                }

                selectedCodec = codec;
                break;
            }

            if (selectedCodec == null)
            {
                CloseComponent();
                throw new MediaContainerException($"Unable to find suitable decoder codec for stream {streamIndex}. Error code {codecOpenResult}");
            }

            // Startup done. Set some options.
            Stream->discard = AVDiscard.AVDISCARD_DEFAULT;
            MediaType = (MediaType)CodecContext->codec_type;

            switch (MediaType)
            {
                case MediaType.Audio:
                case MediaType.Video:
                    BufferCountThreshold = 25;
                    BufferDurationThreshold = TimeSpan.FromSeconds(1);
                    DecodePacketFunction = DecodeNextAVFrame;
                    break;
                case MediaType.Subtitle:
                    BufferCountThreshold = 0;
                    BufferDurationThreshold = TimeSpan.Zero;
                    DecodePacketFunction = DecodeNextAVSubtitle;
                    break;
                default:
                    throw new NotSupportedException($"A component of MediaType '{MediaType}' is not supported");
            }

            if (StreamInfo.IsAttachedPictureDisposition)
            {
                BufferCountThreshold = 0;
                BufferDurationThreshold = TimeSpan.Zero;
            }

            // Compute the start time
            StartTime = Stream->start_time == ffmpeg.AV_NOPTS_VALUE ?
                TimeSpan.MinValue :
                Stream->start_time.ToTimeSpan(Stream->time_base);

            // compute the duration
            Duration = (Stream->duration == ffmpeg.AV_NOPTS_VALUE || Stream->duration <= 0) ?
                TimeSpan.MinValue :
                Stream->duration.ToTimeSpan(Stream->time_base);

            CodecId = Stream->codec->codec_id;
            CodecName = Utilities.PtrToStringUTF8(selectedCodec->name);
            BitRate = Stream->codec->bit_rate < 0 ? 0 : Stream->codec->bit_rate;
            this.LogDebug(Aspects.Component,
                $"{MediaType.ToString().ToUpperInvariant()} - Start Time: {StartTime.Format()}; Duration: {Duration.Format()}");

            // Begin processing with a flush packet
            SendFlushPacket();
        }

        #endregion

        #region Properties

        /// <inheritdoc />
        ILoggingHandler ILoggingSource.LoggingHandler => m_LoggingHandler;

        /// <summary>
        /// Gets the pointer to the codec context.
        /// </summary>
        public AVCodecContext* CodecContext => (AVCodecContext*)m_CodecContext;

        /// <summary>
        /// Gets a pointer to the component's stream.
        /// </summary>
        public AVStream* Stream => (AVStream*)m_Stream;

        /// <summary>
        /// Gets the media container associated with this component.
        /// </summary>
        public MediaContainer Container { get; }

        /// <summary>
        /// Gets the type of the media.
        /// </summary>
        public MediaType MediaType { get; }

        /// <summary>
        /// Gets the index of the associated stream.
        /// </summary>
        public int StreamIndex { get; }

        /// <summary>
        /// Gets the component's stream start timestamp as reported
        /// by the start time of the stream.
        /// Returns TimeSpan.MinValue when unknown.
        /// </summary>
        public TimeSpan StartTime { get; internal set; }

        /// <summary>
        /// Gets the duration of this stream component.
        /// If there is no such information it will return TimeSpan.MinValue
        /// </summary>
        public TimeSpan Duration { get; internal set; }

        /// <summary>
        /// Gets the component's stream end timestamp as reported
        /// by the start and duration time of the stream.
        /// Returns TimeSpan.MinValue when unknown.
        /// </summary>
        public TimeSpan EndTime => (StartTime != TimeSpan.MinValue && Duration != TimeSpan.MinValue)
            ? TimeSpan.FromTicks(StartTime.Ticks + Duration.Ticks)
            : TimeSpan.MinValue;

        /// <summary>
        /// Gets the current length in bytes of the
        /// packet buffer. Limit your Reads to something reasonable before
        /// this becomes too large.
        /// </summary>
        public long BufferLength => Packets.BufferLength;

        /// <summary>
        /// Gets the duration of the packet buffer.
        /// </summary>
        public TimeSpan BufferDuration => Packets.GetDuration(StreamInfo.TimeBase);

        /// <summary>
        /// Gets the number of packets in the queue.
        /// Decode packets until this number becomes 0.
        /// </summary>
        public int BufferCount => Packets.Count;

        /// <summary>
        /// Gets the number of packets to cache before <see cref="HasEnoughPackets"/> returns true.
        /// </summary>
        public int BufferCountThreshold { get; }

        /// <summary>
        /// Gets the packet buffer duration threshold before <see cref="HasEnoughPackets"/> returns true.
        /// </summary>
        public TimeSpan BufferDurationThreshold { get; }

        /// <summary>
        /// Gets or sets a value indicating whether the packet queue contains enough packets.
        /// Port of ffplay.c stream_has_enough_packets
        /// </summary>
        public bool HasEnoughPackets
        {
            get
            {
                // We want to return true when we can't really get a buffer.
                if (IsDisposed ||
                    BufferCountThreshold <= 0 ||
                    StreamInfo.IsAttachedPictureDisposition ||
                    (Container?.IsReadAborted ?? false) ||
                    (Container?.IsAtEndOfStream ?? false))
                    return true;

                // Enough packets means we have a duration of at least 1 second (if the packets report duration)
                // and that we have enough of a packet count depending on the type of media
                return (BufferDuration <= TimeSpan.Zero || BufferDuration.Ticks >= BufferDurationThreshold.Ticks) &&
                    BufferCount >= BufferCountThreshold;
            }
        }

        /// <summary>
        /// Gets the ID of the codec for this component.
        /// </summary>
        public AVCodecID CodecId { get; }

        /// <summary>
        /// Gets the name of the codec for this component.
        /// </summary>
        public string CodecName { get; }

        /// <summary>
        /// Gets the bit rate of this component as reported by the codec context.
        /// Returns 0 for unknown.
        /// </summary>
        public long BitRate { get; }

        /// <summary>
        /// Gets the stream information.
        /// </summary>
        public StreamInfo StreamInfo { get; }

        /// <summary>
        /// Gets whether packets have been fed into the codec and frames can be decoded.
        /// </summary>
        public bool HasPacketsInCodec
        {
            get => m_HasCodecPackets.Value;
            private set => m_HasCodecPackets.Value = value;
        }

        /// <summary>
        /// Gets a value indicating whether this instance is disposed.
        /// </summary>
        public bool IsDisposed
        {
            get => m_IsDisposed.Value;
            private set => m_IsDisposed.Value = value;
        }

        /// <summary>
        /// Gets or sets the last frame PTS.
        /// </summary>
        internal long? LastFramePts { get; set; }

        #endregion

        #region Methods

        /// <summary>
        /// Clears the pending and sent Packet Queues releasing all memory held by those packets.
        /// Additionally it flushes the codec buffered packets.
        /// </summary>
        /// <param name="flushBuffers">if set to <c>true</c> flush codec buffers.</param>
        public void ClearQueuedPackets(bool flushBuffers)
        {
            // Release packets that are already in the queue.
            Packets.Clear();

            if (flushBuffers)
                FlushCodecBuffers();

            Container.Components.ProcessPacketQueueChanges(PacketQueueOp.Clear, null, MediaType);
        }

        /// <summary>
        /// Sends a special kind of packet (an empty/null packet)
        /// that tells the decoder to refresh the attached picture or enter draining mode.
        /// This is a port of packet_queue_put_nullpacket
        /// </summary>
        public void SendEmptyPacket()
        {
            var packet = MediaPacket.CreateEmptyPacket(Stream->index);
            SendPacket(packet);
        }

        /// <summary>
        /// Pushes a packet into the decoding Packet Queue
        /// and processes the packet in order to try to decode
        /// 1 or more frames.
        /// </summary>
        /// <param name="packet">The packet.</param>
        public void SendPacket(MediaPacket packet)
        {
            if (packet == null)
            {
                SendEmptyPacket();
                return;
            }

            Packets.Push(packet);
            Container.Components.ProcessPacketQueueChanges(PacketQueueOp.Queued, packet, MediaType);
        }

        /// <summary>
        /// Feeds the decoder buffer and tries to return the next available frame.
        /// </summary>
        /// <returns>The received Media Frame. It is null if no frame could be retrieved.</returns>
        public MediaFrame ReceiveNextFrame() => DecodePacketFunction();

        /// <summary>
        /// Converts decoded, raw frame data in the frame source into a a usable frame. <br />
        /// The process includes performing picture, samples or text conversions
        /// so that the decoded source frame data is easily usable in multimedia applications
        /// </summary>
        /// <param name="input">The source frame to use as an input.</param>
        /// <param name="output">The target frame that will be updated with the source frame. If null is passed the frame will be instantiated.</param>
        /// <param name="previousBlock">The previous block from which to derive information in case the current frame contains invalid data.</param>
        /// <returns>
        /// Returns true of the operation succeeded. False otherwise.
        /// </returns>
        public abstract bool MaterializeFrame(MediaFrame input, ref MediaBlock output, MediaBlock previousBlock);

        /// <inheritdoc />
        public void Dispose() => Dispose(true);

        /// <summary>
        /// Creates a frame source object given the raw FFmpeg AVFrame or AVSubtitle reference.
        /// </summary>
        /// <param name="framePointer">The raw FFmpeg pointer.</param>
        /// <returns>The media frame</returns>
        protected abstract MediaFrame CreateFrameSource(IntPtr framePointer);

        /// <summary>
        /// Releases the existing codec context and clears and disposes the packet queues.
        /// </summary>
        protected void CloseComponent()
        {
            if (m_CodecContext == IntPtr.Zero) return;
            RC.Current.Remove(m_CodecContext);
            var codecContext = CodecContext;
            ffmpeg.avcodec_free_context(&codecContext);
            m_CodecContext = IntPtr.Zero;

            // free all the pending and sent packets
            ClearQueuedPackets(true);
            Packets.Dispose();
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="alsoManaged"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool alsoManaged)
        {
            lock (CodecLock)
            {
                if (IsDisposed) return;
                CloseComponent();
                IsDisposed = true;
            }
        }

        /// <summary>
        /// Sends a special kind of packet (a flush packet)
        /// that tells the decoder to flush it internal buffers
        /// This an encapsulation of flush_pkt
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SendFlushPacket()
        {
            var packet = MediaPacket.CreateFlushPacket(Stream->index);
            SendPacket(packet);
        }

        /// <summary>
        /// Flushes the codec buffers.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void FlushCodecBuffers()
        {
            if (m_CodecContext != IntPtr.Zero)
                ffmpeg.avcodec_flush_buffers(CodecContext);

            HasPacketsInCodec = false;
        }

        /// <summary>
        /// Feeds the packets to decoder.
        /// </summary>
        /// <param name="fillDecoderBuffer">if set to <c>true</c> fills the decoder buffer with packets.</param>
        /// <returns>The number of packets fed</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int FeedPacketsToDecoder(bool fillDecoderBuffer)
        {
            var packetCount = 0;
            int sendPacketResult;

            while (Packets.Count > 0)
            {
                var packet = Packets.Peek();
                if (packet.IsFlushPacket)
                {
                    FlushCodecBuffers();

                    // Dequeue the flush packet. We don't add to the decode
                    // count or call the OnPacketDequeued callback because the size is 0
                    packet = Packets.Dequeue();

                    packet.Dispose();
                    continue;
                }

                // Send packet to the decoder but prevent null packets to be sent to it
                // Null packets have never been detected but it's just a safeguard
                sendPacketResult = packet.SafePointer != IntPtr.Zero
                    ? ffmpeg.avcodec_send_packet(CodecContext, packet.Pointer) : -ffmpeg.EINVAL;

                // EAGAIN means we have filled the decoder buffer
                if (sendPacketResult != -ffmpeg.EAGAIN)
                {
                    // Dequeue the packet and release it.
                    packet = Packets.Dequeue();
                    Container.Components.ProcessPacketQueueChanges(PacketQueueOp.Dequeued, packet, MediaType);

                    packet.Dispose();
                    packetCount++;
                }

                if (sendPacketResult >= 0)
                    HasPacketsInCodec = true;

                if (fillDecoderBuffer && sendPacketResult >= 0)
                    continue;

                break;
            }

            return packetCount;
        }

        /// <summary>
        /// Receives the next available frame from decoder.
        /// </summary>
        /// <param name="receiveFrameResult">The receive frame result.</param>
        /// <returns>The frame or null if no frames could be decoded</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private MediaFrame ReceiveFrameFromDecoder(out int receiveFrameResult)
        {
            MediaFrame managedFrame = null;
            var outputFrame = MediaFrame.CreateAVFrame();
            receiveFrameResult = ffmpeg.avcodec_receive_frame(CodecContext, outputFrame);

            if (receiveFrameResult >= 0)
                managedFrame = CreateFrameSource(new IntPtr(outputFrame));

            if (managedFrame == null)
                MediaFrame.ReleaseAVFrame(outputFrame);

            if (receiveFrameResult == ffmpeg.AVERROR_EOF)
                FlushCodecBuffers();

            if (receiveFrameResult == -ffmpeg.EAGAIN)
                HasPacketsInCodec = false;

            return managedFrame;
        }

        /// <summary>
        /// Decodes the next Audio or Video frame.
        /// Reference: https://www.ffmpeg.org/doxygen/4.0/group__lavc__encdec.html
        /// </summary>
        /// <returns>A decoder result containing the decoder frames (if any)</returns>
        private MediaFrame DecodeNextAVFrame()
        {
            var frame = ReceiveFrameFromDecoder(out var receiveFrameResult);
            if (frame == null)
            {
                FeedPacketsToDecoder(false);
                frame = ReceiveFrameFromDecoder(out receiveFrameResult);
            }

            while (frame == null && FeedPacketsToDecoder(true) > 0)
            {
                frame = ReceiveFrameFromDecoder(out receiveFrameResult);
                if (receiveFrameResult < 0)
                    break;
            }

            if (frame == null || Container.Components.OnFrameDecoded == null)
                return frame;

            if (MediaType == MediaType.Audio && frame is AudioFrame audioFrame)
                Container.Components.OnFrameDecoded?.Invoke((IntPtr)audioFrame.Pointer, MediaType);
            else if (MediaType == MediaType.Video && frame is VideoFrame videoFrame)
                Container.Components.OnFrameDecoded?.Invoke((IntPtr)videoFrame.Pointer, MediaType);

            return frame;
        }

        /// <summary>
        /// Decodes the next subtitle frame.
        /// </summary>
        /// <returns>The managed frame</returns>
        private MediaFrame DecodeNextAVSubtitle()
        {
            // For subtitles we use the old API (new API send_packet/receive_frame) is not yet available
            // We first try to flush anything we've already sent by using an empty packet.
            MediaFrame managedFrame = null;
            var packet = MediaPacket.CreateEmptyPacket(Stream->index);
            var gotFrame = 0;
            var outputFrame = MediaFrame.CreateAVSubtitle();
            var receiveFrameResult = ffmpeg.avcodec_decode_subtitle2(CodecContext, outputFrame, &gotFrame, packet.Pointer);

            // If we don't get a frame from flushing. Feed the packet into the decoder and try getting a frame.
            if (gotFrame == 0)
            {
                packet.Dispose();

                // Dequeue the packet and try to decode with it.
                packet = Packets.Dequeue();

                if (packet != null)
                {
                    Container.Components.ProcessPacketQueueChanges(PacketQueueOp.Dequeued, packet, MediaType);
                    receiveFrameResult = ffmpeg.avcodec_decode_subtitle2(CodecContext, outputFrame, &gotFrame, packet.Pointer);
                }
            }

            // If we got a frame, turn into a managed frame
            if (gotFrame != 0)
            {
                Container.Components.OnSubtitleDecoded?.Invoke((IntPtr)outputFrame);
                managedFrame = CreateFrameSource((IntPtr)outputFrame);
            }

            // Free the packet if we have dequeued it
            packet?.Dispose();

            // deallocate the subtitle frame if we did not associate it with a managed frame.
            if (managedFrame == null)
                MediaFrame.ReleaseAVSubtitle(outputFrame);

            if (receiveFrameResult < 0)
                HasPacketsInCodec = false;

            return managedFrame;
        }

        #endregion
    }
}
