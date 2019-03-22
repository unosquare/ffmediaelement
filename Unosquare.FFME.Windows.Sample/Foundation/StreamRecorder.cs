namespace Unosquare.FFME.Windows.Sample.Foundation
{
    using FFmpeg.AutoGen;
    using System.Collections.Generic;

    internal unsafe sealed class StreamRecorder
    {
        private readonly Dictionary<int, int> StreamMappings = new Dictionary<int, int>(3);
        private AVFormatContext* OutputContext;
        private MediaElement Media;
        private bool HasInitialized = false;

        public StreamRecorder(string outputFilePath, MediaElement media)
        {
            AVFormatContext* outputContext;
            var allocResult = ffmpeg.avformat_alloc_output_context2(&outputContext, null, null, outputFilePath);
            allocResult = ffmpeg.avio_open(&outputContext->pb, outputFilePath, ffmpeg.AVIO_FLAG_WRITE);
            OutputContext = outputContext;

            Media = media;
            Media.PacketRead += PacketRead;
        }

        public void Close()
        {
            ffmpeg.av_write_trailer(OutputContext);
            var outputContext = OutputContext;
            ffmpeg.avio_closep(&outputContext->pb);
            ffmpeg.avformat_free_context(outputContext);
            OutputContext = null;
        }

        private void PacketRead(object sender, Events.PacketReadEventArgs e)
        {
            if (!HasInitialized)
            {
                var activeStreams = new List<int>(3);
                if (Media.VideoStreamIndex >= 0) activeStreams.Add(Media.VideoStreamIndex);
                if (Media.AudioStreamIndex >= 0) activeStreams.Add(Media.AudioStreamIndex);
                if (Media.SubtitleStreamIndex >= 0) activeStreams.Add(Media.SubtitleStreamIndex);

                foreach (var streamIndex in activeStreams)
                {
                    var codecParams = e.InputContext->streams[streamIndex]->codecpar;
                    var outputStream = ffmpeg.avformat_new_stream(OutputContext, null);
                    ffmpeg.avcodec_parameters_copy(outputStream->codecpar, codecParams);
                    StreamMappings[streamIndex] = outputStream->index;
                }

                ffmpeg.avformat_write_header(OutputContext, null);
                HasInitialized = true;
            }

            var packet = ffmpeg.av_packet_clone(e.Packet);
            var in_stream = e.InputContext->streams[packet->stream_index];
            packet->stream_index = StreamMappings[packet->stream_index];
            var out_stream = OutputContext->streams[packet->stream_index];

            packet->pts = ffmpeg.av_rescale_q_rnd(
                packet->pts, in_stream->time_base, out_stream->time_base, AVRounding.AV_ROUND_NEAR_INF | AVRounding.AV_ROUND_PASS_MINMAX);

            packet->dts = ffmpeg.av_rescale_q_rnd(
                packet->dts, in_stream->time_base, out_stream->time_base, AVRounding.AV_ROUND_NEAR_INF | AVRounding.AV_ROUND_PASS_MINMAX);

            packet->duration = ffmpeg.av_rescale_q(
                packet->duration, in_stream->time_base, out_stream->time_base);

            packet->pos = -1;
            ffmpeg.av_interleaved_write_frame(OutputContext, packet);
            ffmpeg.av_packet_unref(packet);
        }
    }
}
