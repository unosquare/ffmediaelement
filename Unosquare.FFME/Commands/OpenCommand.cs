namespace Unosquare.FFME.Commands
{
    using Core;
    using Decoding;
    using FFmpeg.AutoGen;
    using Rendering;
    using System;
    using System.Threading.Tasks;
    using System.Windows.Threading;

    /// <summary>
    /// Implements the logic to open a media stream.
    /// </summary>
    /// <seealso cref="Unosquare.FFME.Commands.MediaCommand" />
    internal sealed class OpenCommand : MediaCommand
    {
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
        /// Gets the source uri of the media stream.
        /// </summary>
        public Uri Source { get; private set; }

        /// <summary>
        /// Performs the actions that this command implements.
        /// </summary>
        internal override void ExecuteInternal()
        {
            var m = Manager.MediaElement;

            if (m.IsDisposed || m.IsOpen || m.IsOpening) return;

            try
            {
                // Register FFmpeg if not already done
                if (MediaElement.IsFFmpegLoaded == false)
                {
                    MediaElement.FFmpegDirectory = Utils.RegisterFFmpeg(MediaElement.FFmpegDirectory);
                    m.Logger.Log(MediaLogMessageType.Info, $"INIT FFMPEG: {ffmpeg.av_version_info()}");
                }

                Runner.UIInvoke(DispatcherPriority.DataBind, () => { m.ResetDependencyProperies(); });
                MediaElement.IsFFmpegLoaded = true;
                m.IsOpening = true;
                m.MediaState = System.Windows.Controls.MediaState.Manual;

                var mediaUrl = Source.IsFile ? Source.LocalPath : Source.ToString();

                // the async protocol prefix allows for increased performance for local files.
                m.Container = new MediaContainer(mediaUrl, m.Logger, Source.IsFile ? "async" : null);
                m.RaiseMediaOpeningEvent();
                m.Logger.Log(MediaLogMessageType.Debug, $"{nameof(OpenCommand)}: Entered");
                m.Container.Open();

                m.MediaState = System.Windows.Controls.MediaState.Stop;

                foreach (var t in m.Container.Components.MediaTypes)
                {
                    m.Blocks[t] = new MediaBlockBuffer(MediaElement.MaxBlocks[t], t);
                    m.LastRenderTime[t] = TimeSpan.MinValue;
                    m.Renderers[t] = CreateRenderer(t);
                }

                m.Clock.SpeedRatio = Constants.DefaultSpeedRatio;
                m.IsTaskCancellationPending = false;

                // Set the initial state of the task cycles.
                m.SeekingDone.Set();
                m.BlockRenderingCycle.Reset();
                m.FrameDecodingCycle.Reset();
                m.PacketReadingCycle.Reset();

                // Start the tasks
                m.PacketReadingTask = Task.Run(() => m.RunPacketReadingWorker());
                m.FrameDecodingTask = Task.Run(() => m.RunFrameDecodingWorker());
                m.BlockRenderingTask = Task.Run(() => m.RunBlockRenderingWorker());

                // Raise the opened event
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
                Runner.UIInvoke(DispatcherPriority.DataBind, () => { m.NotifyPropertyChanges(); });
                m.Logger.Log(MediaLogMessageType.Debug, $"{nameof(OpenCommand)}: Completed");
            }
        }

        /// <summary>
        /// Creates a new instance of the renderer of the given type.
        /// </summary>
        /// <param name="mediaType">Type of the media.</param>
        /// <returns>The renderer that was created</returns>
        /// <exception cref="ArgumentException">mediaType has to be of a vild type</exception>
        private IRenderer CreateRenderer(MediaType mediaType)
        {
            var m = Manager.MediaElement;
            if (mediaType == MediaType.Audio) return new AudioRenderer(m);
            else if (mediaType == MediaType.Video) return new VideoRenderer(m);
            else if (mediaType == MediaType.Subtitle) return new SubtitleRenderer(m);

            throw new ArgumentException($"No suitable renderer for Media Type '{mediaType}'");
        }
    }
}
