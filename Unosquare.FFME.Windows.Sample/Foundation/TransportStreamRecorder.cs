namespace Unosquare.FFME.Windows.Sample.Foundation
{
    using Events;
    using FFmpeg.AutoGen;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;

    /// <summary>
    /// An recorder that simply copies input packets into an output file.
    /// Loosely based on the ideas from
    /// https://github.com/FFmpeg/FFmpeg/blob/5252d594a155cdb0a0e2529961b999cda96f0fa5/doc/examples/remuxing.c#L80
    /// This example does not cover re-encoding. Only re-muxing packets into a file.
    /// For re-encoding you will need to handle the following events and perform encoding on your own
    /// <see cref="MediaElement.VideoFrameDecoded"/>
    /// <see cref="MediaElement.AudioFrameDecoded"/>
    /// <see cref="MediaElement.SubtitleDecoded"/>.
    /// </summary>
    internal sealed unsafe class TransportStreamRecorder
    {
        private readonly object SyncLock = new object();
        private readonly Dictionary<int, int> StreamMappings = new Dictionary<int, int>(3);
        private readonly MediaElement Media;
        private readonly string FilePath;
        private AVFormatContext* OutputContext;
        private bool HasInitialized;
        private bool HasClosed;

        /// <summary>
        /// Initializes a new instance of the <see cref="TransportStreamRecorder"/> class.
        /// </summary>
        /// <param name="outputFilePath">The output file path. The extension will be guessed according to the input.</param>
        /// <param name="media">The parent media element.</param>
        public TransportStreamRecorder(string outputFilePath, MediaElement media)
        {
            FilePath = outputFilePath;
            Media = media;
            Media.PacketRead += OnMediaPacketRead;
        }

        /// <summary>
        /// Writes the file trailer and releases the output context and file.
        /// Make sure you call this method when finishing the capture process.
        /// </summary>
        public void Close()
        {
            lock (SyncLock)
            {
                Media.PacketRead -= OnMediaPacketRead;
                ffmpeg.av_write_trailer(OutputContext);
                Release();
            }
        }

        /// <summary>
        /// Releases unmanaged resources and prvents more packets from being handled.
        /// </summary>
        private void Release()
        {
            var outputContext = OutputContext;

            if (outputContext != null)
            {
                var outputFormat = outputContext->oformat;
                if (outputFormat != null && (outputFormat->flags & ffmpeg.AVFMT_NOFILE) == 0)
                    ffmpeg.avio_closep(&outputContext->pb);

                ffmpeg.avformat_free_context(outputContext);
            }

            OutputContext = null;
            HasInitialized = false;
            HasClosed = true;
        }

        /// <summary>
        /// Guesses the file path with an appropriate extension according to the input context.
        /// </summary>
        /// <param name="inputContext">The input context.</param>
        /// <returns>A suitable file path with appropriate file extension.</returns>
        private string GuessOutputFilePath(AVFormatContext* inputContext)
        {
            var currentExtension = Path.GetExtension(FilePath);
            var inputFormatExtensions = Utilities.PtrToStringUTF8(inputContext->iformat->extensions);
            var extension = !string.IsNullOrWhiteSpace(inputFormatExtensions)
                ? inputFormatExtensions.Split(',')[0]
                : currentExtension;

            return Path.ChangeExtension(FilePath, extension);
        }

        /// <summary>
        /// Initializes the output context and writes the file header.
        /// </summary>
        /// <param name="inputContext">The input context.</param>
        private void Initialize(AVFormatContext* inputContext)
        {
            var result = 0;
            try
            {
                var outputFilePath = GuessOutputFilePath(inputContext);
                AVFormatContext* outputContext;
                result = ffmpeg.avformat_alloc_output_context2(&outputContext, null, null, outputFilePath);

                if (result < 0)
                    throw new InvalidOperationException("Unable to allocate output context");

                OutputContext = outputContext;
                StreamMappings.Clear();

                foreach (var streamIndex in Media.MediaInfo.Streams.Keys)
                {
                    var codecParams = inputContext->streams[streamIndex]->codecpar;
                    var stream = ffmpeg.avformat_new_stream(OutputContext, null);

                    if (stream == null)
                    {
                        result = -ffmpeg.ENOMEM;
                        throw new InvalidOperationException($"Unable to create output stream for stream index {streamIndex}");
                    }

                    result = ffmpeg.avcodec_parameters_copy(stream->codecpar, codecParams);

                    if (result < 0)
                        throw new InvalidOperationException($"Unable to copy codec parameters to stream index {streamIndex}");

                    stream->codecpar->codec_tag = 0;
                    StreamMappings[streamIndex] = stream->index;
                }

                result = ffmpeg.avio_open(&outputContext->pb, outputFilePath, ffmpeg.AVIO_FLAG_WRITE);
                if (result < 0)
                    throw new InvalidOperationException($"Could not open output file '{outputFilePath}'");

                result = ffmpeg.avformat_write_header(OutputContext, null);
                if (result < 0)
                    throw new InvalidOperationException($"Could not write header to '{outputFilePath}'");

                HasInitialized = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{nameof(TransportStreamRecorder)} - Error Code {result}: {ex.Message}");
                Release();
            }
        }

        /// <summary>
        /// Handles the Media.PacketRead event writing the packet to the output context.
        /// </summary>
        /// <param name="sender">the media element that sent this event.</param>
        /// <param name="e">The event arguments containing the packet data.</param>
        private void OnMediaPacketRead(object sender, PacketReadEventArgs e)
        {
            lock (SyncLock)
            {
                if (HasClosed)
                    return;

                if (!HasInitialized)
                    Initialize(e.InputContext);

                var inputStreamIndex = e.Packet->stream_index;
                var outputStreamIndex = StreamMappings[inputStreamIndex];

                var inputStream = e.InputContext->streams[inputStreamIndex];
                var outputStream = OutputContext->streams[outputStreamIndex];

                var packet = ffmpeg.av_packet_clone(e.Packet);
                packet->stream_index = outputStreamIndex;
                packet->pts = ffmpeg.av_rescale_q_rnd(
                    packet->pts, inputStream->time_base, outputStream->time_base, AVRounding.AV_ROUND_NEAR_INF | AVRounding.AV_ROUND_PASS_MINMAX);

                packet->dts = ffmpeg.av_rescale_q_rnd(
                    packet->dts, inputStream->time_base, outputStream->time_base, AVRounding.AV_ROUND_NEAR_INF | AVRounding.AV_ROUND_PASS_MINMAX);

                packet->duration = ffmpeg.av_rescale_q(
                    packet->duration, inputStream->time_base, outputStream->time_base);

                packet->pos = -1;

                ffmpeg.av_interleaved_write_frame(OutputContext, packet);
                ffmpeg.av_packet_unref(packet);
            }
        }
    }
}
