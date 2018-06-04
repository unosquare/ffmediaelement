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
            // TODO: Capture the current position
        }

        /// <summary>
        /// Provides the implementation of the command
        /// </summary>
        /// <returns>The awaitable task.</returns>
        internal override async Task ExecuteInternal()
        {
            var m = Manager.MediaCore;

            // Avoid running the command if run conditions are not met
            if (m == null || m.IsDisposed || m.State.IsOpen == false || m.State.IsOpening)
                return;

            try
            {
                // Signal the start of a changing event
                m.MediaChangingDone.Begin();

                // Wait for the cycles to complete
                var workerEvents = new IWaitEvent[] { m.BlockRenderingCycle, m.FrameDecodingCycle, m.PacketReadingCycle };
                foreach (var workerEvent in workerEvents)
                    workerEvent.Wait();

                // Send the changing event to the connector
                var oldComponentTypes = m.Container.Components.MediaTypes;
                await m.SendOnMediaChanging();
                m.Container.UpdateComponents();
                var newComponentTypes = m.Container.Components.MediaTypes;
                var disposableComponentTypes = oldComponentTypes
                    .Where(c => newComponentTypes.Contains(c) == false)
                    .ToArray();

                // Remove components that are no longer needed
                foreach (var t in disposableComponentTypes)
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
                foreach (var t in newComponentTypes)
                {
                    if (m.Blocks.ContainsKey(t) == false)
                        m.Blocks[t] = new MediaBlockBuffer(Constants.MaxBlocks[t], t);

                    if (m.Renderers.ContainsKey(t) == false)
                        m.Renderers[t] = MediaEngine.Platform.CreateRenderer(t, m);

                    m.Blocks[t].Clear();
                    m.Renderers[t].WaitForReadyState();
                    m.InvalidateRenderer(t);
                }

                if (m.State.IsSeekable)
                    Manager.EnqueueSeek(m.State.Position);
            }
            catch
            {
                // TODO: Handle errors here
            }
            finally
            {
                m.MediaChangingDone.Complete();
            }
        }
    }
}
