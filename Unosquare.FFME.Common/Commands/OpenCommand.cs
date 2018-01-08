namespace Unosquare.FFME.Commands
{
    using Core;
    using Decoding;
    using FFmpeg.AutoGen;
    using Shared;
    using System;
    using System.Threading;

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
        public Uri Source { get; }

        /// <summary>
        /// Performs the actions that this command implements.
        /// </summary>
        internal override void ExecuteInternal()
        {
            var m = Manager.MediaCore;

            if (m.IsDisposed || m.IsOpen || m.IsOpening) return;

            try
            {
                // Register FFmpeg if not already done
                if (MediaEngine.IsFFmpegLoaded.Value == false)
                {
                    MediaEngine.FFmpegDirectory = FFInterop.RegisterFFmpeg(MediaEngine.FFmpegDirectory);
                    m.Log(MediaLogMessageType.Info, $"INIT FFMPEG: {ffmpeg.av_version_info()}");
                }

                MediaEngine.Platform.GuiInvoke(
                    ActionPriority.DataBind, () => { m.ResetControllerProperties(); });
                MediaEngine.IsFFmpegLoaded.Value = true;
                m.IsOpening = true;
                m.MediaState = MediaEngineState.Manual;

                var mediaUrl = Source.IsFile ? Source.LocalPath : Source.ToString();

                // the async protocol prefix allows for increased performance for local files.
                m.Container = new MediaContainer(mediaUrl, m, Source.IsFile ? "async" : null);
                m.SendOnMediaOpening();
                m.Log(MediaLogMessageType.Debug, $"{nameof(OpenCommand)}: Entered");
                m.Container.Open();

                m.MediaState = MediaEngineState.Stop;

                foreach (var t in m.Container.Components.MediaTypes)
                {
                    m.Blocks[t] = new MediaBlockBuffer(MediaEngine.MaxBlocks[t], t);
                    m.LastRenderTime[t] = TimeSpan.MinValue;
                    m.Renderers[t] = MediaEngine.Platform.CreateRenderer(t, Manager.MediaCore);
                }

                m.Clock.SpeedRatio = Defaults.DefaultSpeedRatio;
                m.IsTaskCancellationPending = false;

                // Set the initial state of the task cycles.
                m.SeekingDone.Set();
                m.BlockRenderingCycle.Reset();
                m.FrameDecodingCycle.Reset();
                m.PacketReadingCycle.Reset();

                // Create the thread runners
                m.PacketReadingTask = new Thread(m.RunPacketReadingWorker)
                { IsBackground = true, Name = nameof(m.PacketReadingTask), Priority = ThreadPriority.Normal };

                m.FrameDecodingTask = new Thread(m.RunFrameDecodingWorker)
                { IsBackground = true, Name = nameof(m.FrameDecodingTask), Priority = ThreadPriority.AboveNormal };

                m.BlockRenderingTask = new Thread(m.RunBlockRenderingWorker)
                { IsBackground = true, Name = nameof(m.BlockRenderingTask), Priority = ThreadPriority.Normal };

                // Fire up the threads
                m.PacketReadingTask.Start();
                m.FrameDecodingTask.Start();
                m.BlockRenderingTask.Start();

                // Signal we are no longer in the opening state 
                // so we can enqueue commands in the event handler
                m.IsOpening = false;

                // Raise the opened event
                m.SendOnMediaOpened();
            }
            catch (Exception ex)
            {
                m.MediaState = MediaEngineState.Close;
                m.SendOnMediaFailed(ex);
            }
            finally
            {
                m.IsOpening = false;
                MediaEngine.Platform.GuiInvoke(ActionPriority.DataBind, () => { m.NotifyPropertyChanges(); });
                m.Log(MediaLogMessageType.Debug, $"{nameof(OpenCommand)}: Completed");
            }
        }
    }
}
