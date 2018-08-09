namespace Unosquare.FFME.Commands
{
    using Primitives;
    using Shared;
    using System;
    using System.Linq;

    /// <summary>
    /// Change Media Command Implementation
    /// </summary>
    /// <seealso cref="DirectCommandBase" />
    internal sealed class DirectChangeCommand : DirectCommandBase
    {
        private Exception ErrorException = default;

        /// <summary>
        /// Initializes a new instance of the <see cref="DirectChangeCommand" /> class.
        /// </summary>
        /// <param name="mediaCore">The media core.</param>
        public DirectChangeCommand(MediaEngine mediaCore)
                    : base(mediaCore)
        {
            CommandType = CommandType.ChangeMedia;
            PlayWhenCompleted = mediaCore.Clock.IsRunning;
        }

        /// <summary>
        /// Gets the command type identifier.
        /// </summary>
        public override CommandType CommandType { get; }

        /// <summary>
        /// Gets a value indicating whetherthe media resumes playback when postprocessing.
        /// </summary>
        public bool PlayWhenCompleted { get; }

        /// <summary>
        /// Performs actions when the command has been executed.
        /// This is useful to notify exceptions or update the state of the media.
        /// </summary>
        public override void PostProcess()
        {
            MediaCore.State.UpdateFixedContainerProperties();

            if (ErrorException == null)
            {
                MediaCore.SendOnMediaChanged();

                if (PlayWhenCompleted)
                    MediaCore.Clock.Play();

                MediaCore.State.UpdateMediaState(
                    MediaCore.Clock.IsRunning ? PlaybackStatus.Play : PlaybackStatus.Pause);
            }
            else
            {
                MediaCore.SendOnMediaFailed(ErrorException);
                MediaCore.State.UpdateMediaState(PlaybackStatus.Pause);
            }

            MediaCore.Log(MediaLogMessageType.Debug, $"Command {CommandType}: Completed");
        }

        /// <summary>
        /// Performs the actions represented by this deferred task.
        /// </summary>
        protected override void PerformActions()
        {
            var m = MediaCore;
            m.Log(MediaLogMessageType.Debug, $"Command {CommandType}: Entered");

            try
            {
                // Signal the start of a sync-buffering scenario
                m.Clock.Pause();

                // Wait for the cycles to complete
                var workerEvents = new IWaitEvent[] { m.BlockRenderingCycle, m.PacketReadingCycle };
                foreach (var workerEvent in workerEvents)
                    workerEvent.Wait();

                // Signal a change so the user get the chance to update
                // selected streams and options
                m.SendOnMediaChanging();

                // Side load subtitles
                m.PreloadSubtitles();

                // Capture the current media types before components change
                var oldMediaTypes = m.Container.Components.MediaTypes.ToArray();

                // Recreate selected streams as media components
                var mediaTypes = m.Container.UpdateComponents();
                m.State.UpdateFixedContainerProperties();

                // find all existing component blocks and renderers that no longer exist
                // We always remove the audio component in case there is a change in audio device
                var removableMediaTypes = oldMediaTypes
                    .Where(t => mediaTypes.Contains(t) == false)
                    .Union(new[] { MediaType.Audio })
                    .Distinct()
                    .ToArray();

                // find all existing component blocks and renderers that no longer exist
                foreach (var t in removableMediaTypes)
                {
                    // Remove the renderer for the component
                    if (m.Renderers.ContainsKey(t))
                    {
                        m.Renderers[t].Close();
                        m.Renderers.Remove(t);
                    }

                    // Remove the block buffer for the component
                    if (m.Blocks.ContainsKey(t))
                    {
                        m.Blocks[t]?.Dispose();
                        m.Blocks.Remove(t);
                    }
                }

                // Create the block buffers and renderers as necessary
                foreach (var t in mediaTypes)
                {
                    if (m.Blocks.ContainsKey(t) == false)
                        m.Blocks[t] = new MediaBlockBuffer(Constants.MaxBlocks[t], t);

                    if (m.Renderers.ContainsKey(t) == false)
                        m.Renderers[t] = MediaEngine.Platform.CreateRenderer(t, m);

                    m.Blocks[t].Clear();
                    m.Renderers[t].WaitForReadyState();
                }

                // Depending on whether or not the media is seekable
                // perform either a seek operation or a quick buffering operation.
                if (m.State.IsSeekable)
                {
                    // Let's simply do an automated seek
                    var seekCommand = new SeekCommand(m, m.WallClock);
                    seekCommand.Execute();
                    return;
                }
                else
                {
                    // Let's perform quick-buffering
                    m.Container.Components.RunQuickBuffering(m);

                    // Mark the renderers as invalidated
                    foreach (var t in mediaTypes)
                        m.InvalidateRenderer(t);
                }
            }
            catch (Exception ex)
            {
                ErrorException = ex;
            }
        }
    }
}
