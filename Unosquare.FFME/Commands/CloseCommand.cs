namespace Unosquare.FFME.Commands
{
    using Core;
    using System;
    using System.Windows.Threading;

    /// <summary>
    /// Implements the logic to close a media stream.
    /// </summary>
    /// <seealso cref="Unosquare.FFME.Commands.MediaCommand" />
    internal sealed class CloseCommand : MediaCommand
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CloseCommand"/> class.
        /// </summary>
        /// <param name="manager">The media element.</param>
        public CloseCommand(MediaCommandManager manager)
            : base(manager, MediaCommandType.Close)
        {
            // placeholder
        }

        /// <summary>
        /// Executes this command.
        /// </summary>
        internal override void Execute()
        {
            var m = Manager.MediaElement;

            if (m.IsOpen == false || m.IsOpening) return;

            m.Logger.Log(MediaLogMessageType.Debug, $"{nameof(CloseCommand)}: Entered");
            m.Clock.Pause();
            m.UpdatePosition(TimeSpan.Zero);

            m.IsTaskCancellationPending = true;

            // Wait for cycles to complete.
            m.BlockRenderingCycle.WaitOne();
            m.FrameDecodingCycle.WaitOne();
            m.PacketReadingCycle.WaitOne();

            // Wait for threads to finish
            m.BlockRenderingTask?.Join();
            m.FrameDecodingTask?.Join();
            m.PacketReadingTask?.Join();

            // Set the threads to null
            m.BlockRenderingTask = null;
            m.FrameDecodingTask = null;
            m.PacketReadingTask = null;

            // Call close on all renderers and clear them
            foreach (var renderer in m.Renderers.Values) renderer.Close();
            m.Renderers.Clear();

            // Reset the clock
            m.Clock.Reset();

            // Dispose the container
            if (m.Container != null)
            {
                m.Container.Dispose();
                m.Container = null;
            }

            // Dispose the Blocks for all components
            foreach (var kvp in m.Blocks) kvp.Value.Dispose();
            m.Blocks.Clear();

            // Dispose the Frames for all components
            foreach (var kvp in m.Frames) kvp.Value.Dispose();
            m.Frames.Clear();

            // Clear the render times
            m.LastRenderTime.Clear();

            // Update notification properties
            Utils.UIInvoke(DispatcherPriority.DataBind, () => { m.NotifyPropertyChanges(); });

            m.MediaState = System.Windows.Controls.MediaState.Close;
            m.Logger.Log(MediaLogMessageType.Debug, $"{nameof(CloseCommand)}: Completed");
        }
    }
}
