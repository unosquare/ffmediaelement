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
        private bool ResumeClock = default;
        private PlaybackStatus MediaState = default;
        private Exception ErrorException = default;

        /// <summary>
        /// Initializes a new instance of the <see cref="DirectChangeCommand" /> class.
        /// </summary>
        /// <param name="mediaCore">The media core.</param>
        public DirectChangeCommand(MediaEngine mediaCore)
                    : base(mediaCore)
        {
            CommandType = CommandType.ChangeMedia;
        }

        /// <summary>
        /// Gets the command type identifier.
        /// </summary>
        public override CommandType CommandType { get; }

        /// <summary>
        /// Performs actions when the command has been executed.
        /// This is useful to notify exceptions or update the state of the media.
        /// </summary>
        public override void PostProcess()
        {
            if (ErrorException == null)
                MediaCore.SendOnMediaChanged();
            else
                MediaCore.SendOnMediaFailed(ErrorException);

            MediaCore.State.UpdateMediaState(MediaState, MediaCore.WallClock);
            if (ResumeClock) MediaCore.Clock.Play();

            MediaCore.Log(MediaLogMessageType.Debug, $"Command {CommandType}: Completed");
        }

        /// <summary>
        /// Performs the actions represented by this deferred task.
        /// </summary>
        protected override void PerformActions()
        {
            var m = MediaCore;

            m.Log(MediaLogMessageType.Debug, $"Command {CommandType}: Entered");
            ResumeClock = false;
            MediaState = m.State.MediaState;

            try
            {
                m.State.UpdateMediaState(PlaybackStatus.Manual);

                // Signal the start of a changing event
                m.State.IsSeeking = true;

                // Signal the start of a sync-buffering scenario
                m.HasDecoderSeeked = true;
                ResumeClock = m.Clock.IsRunning;
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

                // remove all exiting component blocks and renderers that no longer exist
                var removableMediaTypes = oldMediaTypes
                    .Where(t => mediaTypes.Contains(t) == false).ToArray();

                foreach (var t in removableMediaTypes)
                {
                    if (m.Renderers.ContainsKey(t))
                    {
                        m.Renderers[t].Close();
                        m.Renderers.Remove(t);
                    }

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

                // Mark a seek operation in order to invalidate renderers
                if (m.State.IsSeekable)
                {
                    // Let's simply do an automated seek
                    var seekCommand = new SeekCommand(m, m.WallClock);
                    seekCommand.Execute();
                    return;
                }

                // We need to perform some packet reading and decoding
                var main = m.Container.Components.Main.MediaType;
                var auxs = m.Container.Components.MediaTypes.Except(main);

                // Read and decode blocks until the main component is half full
                while (m.ShouldReadMorePackets && m.CanReadMorePackets)
                {
                    // Read some packets
                    m.Container.Read();

                    // Decode frames and add the blocks
                    foreach (var t in mediaTypes)
                    {
                        var frames = m.Container.Components[t].ReceiveFrames();
                        foreach (var frame in frames)
                        {
                            if (frame != null)
                                m.Blocks[t].Add(frame, m.Container);
                        }
                    }

                    // Check if we have at least a half a buffer on main
                    if (m.Blocks[main].CapacityPercent >= 0.5)
                        break;
                }

                // Check if we have a valid range. If not, just set it what the main component is dictating
                if (m.Blocks[main].Count > 0 && m.Blocks[main].IsInRange(m.WallClock) == false)
                    m.Clock.Update(m.Blocks[main].RangeStartTime);

                // Have the other components catch up
                foreach (var t in auxs)
                {
                    if (m.Blocks[main].Count <= 0) break;
                    if (t != MediaType.Audio && t != MediaType.Video)
                        continue;

                    while (m.Blocks[t].RangeEndTime < m.Blocks[main].RangeEndTime)
                    {
                        if (m.ShouldReadMorePackets == false || m.CanReadMorePackets == false)
                            break;

                        // Read some packets
                        m.Container.Read();

                        // Decode frames and add the blocks
                        var frames = m.Container.Components[t].ReceiveFrames();
                        foreach (var frame in frames)
                        {
                            if (frame != null)
                                m.Blocks[t].Add(frame, m.Container);
                        }
                    }
                }

                foreach (var t in mediaTypes)
                    m.InvalidateRenderer(t);

                m.HasDecoderSeeked = true;
            }
            catch (Exception ex)
            {
                ErrorException = ex;
            }
            finally
            {
                m.State.IsSeeking = false;
            }
        }
    }
}
