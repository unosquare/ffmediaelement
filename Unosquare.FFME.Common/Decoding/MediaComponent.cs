﻿namespace Unosquare.FFME.Decoding
{
    using Core;
    using FFmpeg.AutoGen;
    using Shared;
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    /// <summary>
    /// Represents a media component of a given media type within a
    /// media container. Derived classes must implement frame handling
    /// logic.
    /// </summary>
    /// <seealso cref="IDisposable" />
    internal abstract unsafe class MediaComponent : IDisposable
    {
        #region Private Declarations

#pragma warning disable SA1401 // Field must be private

        /// <summary>
        /// Holds a reference to the Codec Context.
        /// </summary>
        internal AVCodecContext* CodecContext;

        /// <summary>
        /// Holds a reference to the associated input context stream
        /// </summary>
        internal AVStream* Stream;

#pragma warning restore SA1401 // Field must be private

        /// <summary>
        /// Related to issue 94, looks like FFmpeg requires exclusive access when calling avcodec_open2()
        /// </summary>
        private static readonly object CodecOpenLock = new object();

        /// <summary>
        /// Contains the packets pending to be sent to the decoder
        /// </summary>
        private readonly PacketQueue Packets = new PacketQueue();

        /// <summary>
        /// The packets that have been sent to the decoder. We keep track of them in order to dispose them
        /// once a frame has been decoded.
        /// </summary>
        private readonly PacketQueue SentPackets = new PacketQueue();

        /// <summary>
        /// Detects redundant, unmanaged calls to the Dispose method.
        /// </summary>
        private bool IsDisposed = false;

        /// <summary>
        /// Holds total bytes read in the lifetime of this object
        /// </summary>
        private ulong m_LifetimeBytesRead = 0;

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
            Container = container ?? throw new ArgumentNullException(nameof(container));
            CodecContext = ffmpeg.avcodec_alloc_context3(null);
            RC.Current.Add(CodecContext, $"134: {nameof(MediaComponent)}[{MediaType}].ctor()");
            StreamIndex = streamIndex;
            Stream = container.InputContext->streams[StreamIndex];
            StreamInfo = container.MediaInfo.Streams[StreamIndex];

            // Set codec options
            var setCodecParamsResult = ffmpeg.avcodec_parameters_to_context(CodecContext, Stream->codecpar);

            if (setCodecParamsResult < 0)
                Container.Parent?.Log(MediaLogMessageType.Warning, $"Could not set codec parameters. Error code: {setCodecParamsResult}");

            // We set the packet timebase in the same timebase as the stream as opposed to the tpyical AV_TIME_BASE
            ffmpeg.av_codec_set_pkt_timebase(CodecContext, Stream->time_base);

            // Find the codec and set it.
            var codec = ffmpeg.avcodec_find_decoder(Stream->codec->codec_id);
            if (codec == null)
            {
                var errorMessage = $"Fatal error. Unable to find suitable decoder for {Stream->codec->codec_id.ToString()}";
                CloseComponent();
                throw new MediaContainerException(errorMessage);
            }

            CodecContext->codec_id = codec->id;

            // Process the low res index option
            var lowResIndex = ffmpeg.av_codec_get_max_lowres(codec);
            if (Container.MediaOptions.EnableLowRes)
            {
                ffmpeg.av_codec_set_lowres(CodecContext, lowResIndex);
                CodecContext->flags |= ffmpeg.CODEC_FLAG_EMU_EDGE;
            }
            else
            {
                lowResIndex = 0;
            }

            // Configure the codec context flags
            if (Container.MediaOptions.EnableFastDecoding) CodecContext->flags2 |= ffmpeg.AV_CODEC_FLAG2_FAST;
            if (Container.MediaOptions.EnableLowDelay) CodecContext->flags |= ffmpeg.AV_CODEC_FLAG_LOW_DELAY;
            if ((codec->capabilities & ffmpeg.AV_CODEC_CAP_DR1) != 0) CodecContext->flags |= ffmpeg.CODEC_FLAG_EMU_EDGE;
            if ((codec->capabilities & ffmpeg.AV_CODEC_CAP_TRUNCATED) != 0) CodecContext->flags |= ffmpeg.AV_CODEC_CAP_TRUNCATED;
            if ((codec->capabilities & ffmpeg.CODEC_FLAG2_CHUNKS) != 0) CodecContext->flags |= ffmpeg.CODEC_FLAG2_CHUNKS;

            // Setup additional settings. The most important one is Threads -- Setting it to 1 decoding is very slow. Setting it to auto
            // decoding is very fast in most scenarios.
            var codecOptions = Container.MediaOptions.CodecOptions.FilterOptions(CodecContext->codec_id, Container.InputContext, Stream, codec);
            if (codecOptions.HasKey(MediaCodecOptions.Names.Threads) == false)
                codecOptions[MediaCodecOptions.Names.Threads] = "auto";

            if (lowResIndex != 0) codecOptions[MediaCodecOptions.Names.LowRes] = lowResIndex.ToString(CultureInfo.InvariantCulture);
            if (CodecContext->codec_type == AVMediaType.AVMEDIA_TYPE_VIDEO || CodecContext->codec_type == AVMediaType.AVMEDIA_TYPE_AUDIO)
                codecOptions[MediaCodecOptions.Names.RefCountedFrames] = 1.ToString(CultureInfo.InvariantCulture);

            // Enable Hardware acceleration if requested
            if (this is VideoComponent && container.MediaOptions.EnableHardwareAcceleration)
                HardwareAccelerator.Cuda.AttachDevice(this as VideoComponent);

            // Open the CodecContext. This requires exclusive FFmpeg access
            var codecOpenResult = 0;
            lock (CodecOpenLock)
            {
                fixed (AVDictionary** reference = &codecOptions.Pointer)
                    codecOpenResult = ffmpeg.avcodec_open2(CodecContext, codec, reference);
            }

            // Check if the codec opened successfully
            if (codecOpenResult < 0)
            {
                CloseComponent();
                throw new MediaContainerException($"Unable to open codec. Error code {codecOpenResult}");
            }

            // If there are any codec options left over from passing them, it means they were not consumed
            var currentEntry = codecOptions.First();
            while (currentEntry != null && currentEntry?.Key != null)
            {
                Container.Parent?.Log(MediaLogMessageType.Warning, $"Invalid codec option: '{currentEntry.Key}'");
                currentEntry = codecOptions.Next(currentEntry);
            }

            // Startup done. Set some options.
            Stream->discard = AVDiscard.AVDISCARD_DEFAULT;
            MediaType = (MediaType)CodecContext->codec_type;

            // Compute the start time
            if (Stream->start_time == ffmpeg.AV_NOPTS_VALUE)
                StartTimeOffset = Container.MediaStartTimeOffset;
            else
                StartTimeOffset = Stream->start_time.ToTimeSpan(Stream->time_base);

            // compute the duration
            if (Stream->duration == ffmpeg.AV_NOPTS_VALUE || Stream->duration == 0)
                Duration = Container.InputContext->duration.ToTimeSpan();
            else
                Duration = Stream->duration.ToTimeSpan(Stream->time_base);

            CodecId = Stream->codec->codec_id;
            CodecName = ffmpeg.avcodec_get_name(CodecId);
            Bitrate = Stream->codec->bit_rate < 0 ? 0 : Convert.ToUInt64(Stream->codec->bit_rate);
            Container.Parent?.Log(MediaLogMessageType.Debug,
                $"COMP {MediaType.ToString().ToUpperInvariant()}: Start Offset: {StartTimeOffset.Format()}; Duration: {Duration.Format()}");
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="MediaComponent"/> class.
        /// </summary>
        ~MediaComponent()
        {
            Dispose(false);
        }

        #endregion

        #region Properties

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
        /// </summary>
        public TimeSpan StartTimeOffset { get; }

        /// <summary>
        /// Gets the first packet DTS.
        /// </summary>
        public long? FirstPacketDts { get; private set; } = default;

        /// <summary>
        /// Gets the duration of this stream component.
        /// If there is no such information it will return TimeSpan.MinValue
        /// </summary>
        public TimeSpan Duration { get; }

        /// <summary>
        /// Gets the current length in bytes of the
        /// packet buffer. Limit your Reads to something reasonable before
        /// this becomes too large.
        /// </summary>
        public ulong PacketBufferLength => Packets.BufferLength;

        /// <summary>
        /// Gets the number of packets in the queue.
        /// Decode packets until this number becomes 0.
        /// </summary>
        public int PacketBufferCount => Packets.Count;

        /// <summary>
        /// Gets the total amount of bytes read by this component in the lifetime of this component.
        /// </summary>
        public ulong LifetimeBytesRead
        {
            get => m_LifetimeBytesRead;
            private set => m_LifetimeBytesRead = value;
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
        /// Gets the bitrate of this component as reported by the codec context.
        /// Returns 0 for unknown.
        /// </summary>
        public ulong Bitrate { get; }

        /// <summary>
        /// Gets the stream information.
        /// </summary>
        public StreamInfo StreamInfo { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Clears the pending and sent Packet Queues releasing all memory held by those packets.
        /// Additionally it flushes the codec buffered packets.
        /// </summary>
        public void ClearPacketQueues()
        {
            // Release packets that are already in the queue.
            SentPackets.Clear();
            Packets.Clear();

            // Discard any data that was buffered in codec's internal memory.
            // reset the buffer
            if (CodecContext != null)
                ffmpeg.avcodec_flush_buffers(CodecContext);
        }

        /// <summary>
        /// Sends a special kind of packet (an empty packet)
        /// that tells the decoder to enter draining mode.
        /// </summary>
        public void SendEmptyPacket()
        {
            var emptyPacket = ffmpeg.av_packet_alloc();
            RC.Current.Add(emptyPacket, $"259: {nameof(MediaComponent)}[{MediaType}].{nameof(SendEmptyPacket)}()");
            SendPacket(emptyPacket);
        }

        /// <summary>
        /// Pushes a packet into the decoding Packet Queue
        /// and processes the packet in order to try to decode
        /// 1 or more frames.
        /// </summary>
        /// <param name="packet">The packet.</param>
        public void SendPacket(AVPacket* packet)
        {
            if (packet == null) return;

            if (packet->size > 0)
            {
                LifetimeBytesRead += (ulong)packet->size;
                if (FirstPacketDts == null && packet->dts != ffmpeg.AV_NOPTS_VALUE)
                    FirstPacketDts = packet->dts;
            }

            Packets.Push(packet);
        }

        /// <summary>
        /// Decodes the next packet in the packet queue in this media component.
        /// Returns the decoded frames.
        /// </summary>
        /// <returns>The received Media Frames</returns>
        public List<MediaFrame> ReceiveFrames()
        {
            if (PacketBufferCount <= 0) return new List<MediaFrame>(0);
            var decodedFrames = DecodeNextPacketInternal();
            return decodedFrames;
        }

        /// <summary>
        /// Converts decoded, raw frame data in the frame source into a a usable frame. <br />
        /// The process includes performing picture, samples or text conversions
        /// so that the decoded source frame data is easily usable in multimedia applications
        /// </summary>
        /// <param name="input">The source frame to use as an input.</param>
        /// <param name="output">The target frame that will be updated with the source frame. If null is passed the frame will be instantiated.</param>
        /// <param name="siblings">The sibling blocks that may help guess some additional parameters for the input frame.</param>
        /// <returns>
        /// Return the updated output frame
        /// </returns>
        public abstract MediaBlock MaterializeFrame(MediaFrame input, ref MediaBlock output, List<MediaBlock> siblings);

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Determines whether the specified packet is a Null Packet (data = null, size = 0)
        /// These null packets are used to read multiple frames from a single packet.
        /// </summary>
        /// <param name="packet">The packet.</param>
        /// <returns>
        ///   <c>true</c> if [is empty packet] [the specified packet]; otherwise, <c>false</c>.
        /// </returns>
        protected static bool IsEmptyPacket(AVPacket* packet)
        {
            if (packet == null) return false;
            return packet->data == null && packet->size == 0;
        }

        /// <summary>
        /// Creates a frame source object given the raw FFmpeg subtitle reference.
        /// </summary>
        /// <param name="frame">The raw FFmpeg subtitle pointer.</param>
        /// <returns>The media frame</returns>
        protected virtual MediaFrame CreateFrameSource(AVSubtitle* frame)
        {
            return null;
        }

        /// <summary>
        /// Creates a frame source object given the raw FFmpeg frame reference.
        /// </summary>
        /// <param name="frame">The raw FFmpeg frame pointer.</param>
        /// <returns>The media frame</returns>
        protected virtual MediaFrame CreateFrameSource(ref AVFrame* frame)
        {
            return null;
        }

        /// <summary>
        /// Releases the existing codec context and clears and disposes the packet queues.
        /// </summary>
        protected void CloseComponent()
        {
            if (CodecContext != null)
            {
                RC.Current.Remove(CodecContext);
                fixed (AVCodecContext** codecContext = &CodecContext)
                    ffmpeg.avcodec_free_context(codecContext);

                // free all the pending and sent packets
                ClearPacketQueues();
                Packets.Dispose();
                SentPackets.Dispose();
            }

            CodecContext = null;
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="alsoManaged"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool alsoManaged)
        {
            if (!IsDisposed)
            {
                if (alsoManaged)
                {
                    // no code for managed dispose
                }

                CloseComponent();
                IsDisposed = true;
            }
        }

        /// <summary>
        /// Receives 0 or more frames from the next available packet in the Queue.
        /// This sends the first available packet to dequeue to the decoder
        /// and uses the decoded frames (if any) to their corresponding
        /// ProcessFrame method.
        /// </summary>
        /// <returns>The list of frames</returns>
        private List<MediaFrame> DecodeNextPacketInternal()
        {
            var result = new List<MediaFrame>();

            // Ensure there is at least one packet in the queue
            if (PacketBufferCount <= 0) return result;

            // Setup some initial state variables
            var packet = Packets.Dequeue();

            // The packets are alwasy sent. We dequeue them and keep a reference to them
            // in the SentPackets queue
            SentPackets.Push(packet);

            var receiveFrameResult = 0;

            if (MediaType == MediaType.Audio || MediaType == MediaType.Video)
            {
                // If it's audio or video, we use the new API and the decoded frames are stored in AVFrame
                // Let us send the packet to the codec for decoding a frame of uncompressed data later
                var sendPacketResult = ffmpeg.avcodec_send_packet(CodecContext, IsEmptyPacket(packet) ? null : packet);

                // Let's check and see if we can get 1 or more frames from the packet we just sent to the decoder.
                // Audio packets will typically contain 1 or more audioframes
                // Video packets might require several packets to decode 1 frame
                MediaFrame managedFrame = null;
                while (receiveFrameResult == 0)
                {
                    // Allocate a frame in unmanaged memory and
                    // Try to receive the decompressed frame data
                    var outputFrame = ffmpeg.av_frame_alloc();
                    RC.Current.Add(outputFrame, $"327: {nameof(MediaComponent)}[{MediaType}].{nameof(DecodeNextPacketInternal)}()");
                    receiveFrameResult = ffmpeg.avcodec_receive_frame(CodecContext, outputFrame);

                    try
                    {
                        managedFrame = null;
                        if (receiveFrameResult == 0)
                        {
                            // Send the frame to processing
                            managedFrame = CreateFrameSource(ref outputFrame);
                            if (managedFrame != null)
                                result.Add(managedFrame);
                        }

                        if (managedFrame == null)
                        {
                            RC.Current.Remove(outputFrame);
                            ffmpeg.av_frame_free(&outputFrame);
                        }
                    }
                    catch
                    {
                        // Release the frame as the decoded data could not be processed
                        RC.Current.Remove(outputFrame);
                        ffmpeg.av_frame_free(&outputFrame);
                        throw;
                    }
                }
            }
            else if (MediaType == MediaType.Subtitle)
            {
                // Fors subtitles we use the old API (new API send_packet/receive_frame) is not yet available
                var gotFrame = 0;
                var outputFrame = SubtitleFrame.AllocateSubtitle();
                receiveFrameResult = ffmpeg.avcodec_decode_subtitle2(CodecContext, outputFrame, &gotFrame, packet);

                // Check if there is an error decoding the packet.
                // If there is, remove the packet clear the sent packets
                if (receiveFrameResult < 0)
                {
                    SubtitleFrame.DeallocateSubtitle(outputFrame);
                    SentPackets.Clear();
                    Container.Parent?.Log(MediaLogMessageType.Error, $"{MediaType}: Error decoding. Error Code: {receiveFrameResult}");
                }
                else
                {
                    // Process the first frame if we got it from the packet
                    // Note that there could be more frames (subtitles) in the packet
                    if (gotFrame != 0)
                    {
                        try
                        {
                            // Send the frame to processing
                            var managedFrame = CreateFrameSource(outputFrame);
                            if (managedFrame == null)
                                throw new MediaContainerException($"{MediaType} Component does not implement {nameof(CreateFrameSource)}");
                            result.Add(managedFrame);
                        }
                        catch
                        {
                            // Once processed, we don't need it anymore. Release it.
                            SubtitleFrame.DeallocateSubtitle(outputFrame);
                            throw;
                        }
                    }

                    // Let's check if we have more decoded frames from the same single packet
                    // Packets may contain more than 1 frame and the decoder is drained
                    // by passing an empty packet (data = null, size = 0)
                    while (gotFrame != 0 && receiveFrameResult > 0)
                    {
                        outputFrame = SubtitleFrame.AllocateSubtitle();
                        var emptyPacket = ffmpeg.av_packet_alloc();
                        RC.Current.Add(emptyPacket, $"406: {nameof(MediaComponent)}[{MediaType}].{nameof(DecodeNextPacketInternal)}()");

                        // Receive the frames in a loop
                        try
                        {
                            receiveFrameResult = ffmpeg.avcodec_decode_subtitle2(CodecContext, outputFrame, &gotFrame, emptyPacket);
                            if (gotFrame != 0 && receiveFrameResult > 0)
                            {
                                // Send the subtitle to processing
                                var managedFrame = CreateFrameSource(outputFrame);
                                if (managedFrame == null)
                                    throw new MediaContainerException($"{MediaType} Component does not implement {nameof(CreateFrameSource)}");
                                result.Add(managedFrame);
                            }
                        }
                        catch
                        {
                            // once the subtitle is processed. Release it from memory
                            SubtitleFrame.DeallocateSubtitle(outputFrame);
                            throw;
                        }
                        finally
                        {
                            // free the empty packet
                            RC.Current.Remove(emptyPacket);
                            ffmpeg.av_packet_free(&emptyPacket);
                        }
                    }
                }
            }

            // Release the sent packets if 1 or more frames were received in the packet
            if (result.Count >= 1 || (Container.IsAtEndOfStream && IsEmptyPacket(packet) && PacketBufferCount == 0))
            {
                // We clear the sent packet queue (releasing packet from unmanaged memory also)
                // because we got at least 1 frame from the packet.
                SentPackets.Clear();
            }

            return result;
        }

        #endregion

    }
}
