namespace Unosquare.FFME.Commands
{
    using Core;
    using Decoding;
    using FFmpeg.AutoGen;
    using Rendering;
    using System;
    using System.Threading;
    using System.Windows.Threading;

    /// <summary>
    /// Implements the logic to open a media stream.
    /// </summary>
    /// <seealso cref="Unosquare.FFME.Commands.MediaCommand" />
    internal sealed class OpenCommand : MediaCommand
    {
        /// <summary>
        /// Gets the source uri of the media stream.
        /// </summary>
        public Uri Source { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenCommand" /> class.
        /// </summary>
        /// <param name="manager">The manager.</param>
        /// <param name="source">The source.</param>
        public OpenCommand(MediaCommandManager manager, Uri source)
            : base(manager, MediaCommandType.Open)
        {
            Source = source;
        }

        /// <summary>
        /// Creates a new instance of the renderer of the given type.
        /// </summary>
        /// <param name="mediaType">Type of the media.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentException"></exception>
        private IRenderer CreateRenderer(MediaType mediaType)
        {
            var m = Manager.MediaElement;
            if (mediaType == MediaType.Audio) return new AudioRenderer(m);
            else if (mediaType == MediaType.Video) return new VideoRenderer(m);
            else if (mediaType == MediaType.Subtitle) return new SubtitleRenderer(m);

            throw new ArgumentException($"No suitable renderer for Media Type '{mediaType}'");
        }

        /// <summary>
        /// Performs the actions that this command implements.
        /// </summary>
        internal override void Execute()
        {
            var m = Manager.MediaElement;

            try
            {
                // Register FFmpeg if not already done
                if (MediaElement.IsFFmpegLoaded == false)
                {
                    MediaElement.FFmpegDirectory = Utils.RegisterFFmpeg(MediaElement.FFmpegDirectory);
                    m.Logger.Log(MediaLogMessageType.Info, $"INIT FFMPEG: {ffmpeg.av_version_info()}");
                }
                    

                MediaElement.IsFFmpegLoaded = true;
                m.IsOpening = true;
                m.MediaState = System.Windows.Controls.MediaState.Manual;

                var mediaUrl = Source.IsFile ? Source.LocalPath : Source.ToString();
                m.Container = new MediaContainer(mediaUrl, m.Logger);
                m.RaiseMediaOpeningEvent();
                m.Logger.Log(MediaLogMessageType.Debug, $"{nameof(OpenCommand)}: Entered");
                m.Container.Open();

                m.MediaState = System.Windows.Controls.MediaState.Stop;

                foreach (var t in m.Container.Components.MediaTypes)
                {
                    m.Blocks[t] = new MediaBlockBuffer(MediaElement.MaxBlocks[t], t);
                    m.Frames[t] = new MediaFrameQueue();
                    m.LastRenderTime[t] = TimeSpan.MinValue;
                    m.Renderers[t] = CreateRenderer(t);
                }

                m.Clock.SpeedRatio = Constants.DefaultSpeedRatio;
                m.IsTaskCancellationPending = false;

                m.BlockRenderingCycle.Set();
                m.FrameDecodingCycle.Set();
                m.PacketReadingCycle.Set();

                m.PacketReadingTask = new Thread(m.RunPacketReadingWorker) { IsBackground = true, Name = nameof(m.PacketReadingTask) };
                m.FrameDecodingTask = new Thread(m.RunFrameDecodingWorker) { IsBackground = true, Name = nameof(m.FrameDecodingTask) };
                m.BlockRenderingTask = new Thread(m.RunBlockRenderingWorker) { IsBackground = true, Name = nameof(m.BlockRenderingTask) };

                m.PacketReadingTask.Start();
                m.FrameDecodingTask.Start();
                m.BlockRenderingTask.Start();

                m.RaiseMediaOpenedEvent();
            }
            catch (Exception ex)
            {
                m.MediaState = System.Windows.Controls.MediaState.Close;
                m.RaiseMediaFailedEvent(ex);
            }
            finally
            {
                m.IsOpening = false;
                Utils.UIInvoke(DispatcherPriority.DataBind, () => { m.NotifyPropertyChanges(); });
                m.Logger.Log(MediaLogMessageType.Debug, $"{nameof(OpenCommand)}: Completed");
            }
        }
    }
}
