namespace Unosquare.FFME.Commands
{
    using System.Linq;
    using System.Threading.Tasks;
    using Shared;
    using Unosquare.FFME.Primitives;

    /// <summary>
    /// Allows the Media Engine to switch to newly assigned streams or close existing ones.
    /// </summary>
    /// <seealso cref="MediaCommand" />
    internal sealed class ChangeMediaCommand : MediaCommand
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ChangeMediaCommand" /> class.
        /// </summary>
        /// <param name="manager">The manager.</param>
        public ChangeMediaCommand(MediaCommandManager manager)
            : base(manager, MediaCommandType.ChangeMedia)
        {
            // placheholder
        }

        /// <summary>
        /// Provides the implementation of the command
        /// </summary>
        /// <returns>
        /// The awaitable task.
        /// </returns>
        internal override Task ExecuteInternal()
        {
            var m = Manager.MediaCore;

            // Avoid running the command if run conditions are not met
            if (m == null || m.IsDisposed || m.State.IsOpen == false || m.State.IsOpening || m.State.IsChanging)
                return Task.CompletedTask;

            m.State.IsChanging = true;
            var resumeClock = false;
            var isSeeking = m.State.IsSeeking;

            try
            {
                // Signal the start of a changing event
                m.MediaChangingDone.Begin();
                m.State.IsSeeking = true;

                // Signal the start of a sync-buffering scenario
                m.HasDecoderSeeked = true;
                resumeClock = m.Clock.IsRunning;
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
                    var seekCommand = new SeekCommand(Manager, m.WallClock);
                    seekCommand.RunSynchronously();
                    return Task.CompletedTask;
                }

                // We need to perform some packet reading and decoding
                var main = m.Container.Components.Main.MediaType;
                var auxs = m.Container.Components.MediaTypes.ExcludeMediaType(main);

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
            catch
            {
                // TODO: Handle errors here
            }
            finally
            {
                if (resumeClock) m?.Clock?.Play();
                m.State.IsSeeking = isSeeking;
                m.MediaChangingDone.Complete();
                m.State.IsChanging = false;
                m.SendOnMediaChanged();
            }

            return Task.CompletedTask;
        }
    }
}
