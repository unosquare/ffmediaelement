namespace Unosquare.FFME.Windows.Sample.Foundation
{
    using Events;
    using FFmpeg.AutoGen;
    using System.Collections.Generic;

    internal sealed unsafe class TransportStreamRecorder
    {
        private readonly object SyncLock = new object();
        private readonly Dictionary<int, int> StreamMappings = new Dictionary<int, int>(3);
        private readonly MediaElement Media;
        private readonly string FilePath;
        private AVFormatContext* OutputContext;
        private bool HasInitialized;
        private bool HasClosed;

        public TransportStreamRecorder(string outputFilePath, MediaElement media)
        {
            FilePath = outputFilePath;
            Media = media;
            Media.PacketRead += PacketRead;
        }

        public void Close()
        {
            lock (SyncLock)
            {
                ffmpeg.av_write_trailer(OutputContext);
                var outputContext = OutputContext;
                ffmpeg.avio_closep(&outputContext->pb);
                ffmpeg.avformat_free_context(outputContext);
                OutputContext = null;
                HasInitialized = false;
                HasClosed = true;
            }
        }

        private void Initialize(AVFormatContext* inputContext)
        {
            AVFormatContext* outputContext;
            ffmpeg.avformat_alloc_output_context2(&outputContext, null, null, FilePath);
            OutputContext = outputContext;
            StreamMappings.Clear();

            foreach (var streamIndex in Media.MediaInfo.Streams.Keys)
            {
                var codecParams = inputContext->streams[streamIndex]->codecpar;
                var stream = ffmpeg.avformat_new_stream(OutputContext, null);
                ffmpeg.avcodec_parameters_copy(stream->codecpar, codecParams);
                stream->codecpar->codec_tag = 0;
                StreamMappings[streamIndex] = stream->index;
            }

            ffmpeg.avio_open(&outputContext->pb, FilePath, ffmpeg.AVIO_FLAG_WRITE);
            ffmpeg.avformat_write_header(OutputContext, null);
            HasInitialized = true;
        }

        private void PacketRead(object sender, PacketReadEventArgs e)
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
