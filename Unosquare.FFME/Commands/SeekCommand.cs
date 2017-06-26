namespace Unosquare.FFME.Commands
{
    using Core;
    using System;
    using System.Linq;

    /// <summary>
    /// Implements the logic to seek on the media stream
    /// </summary>
    /// <seealso cref="Unosquare.FFME.Commands.MediaCommand" />
    internal sealed class SeekCommand : MediaCommand
    {
        public TimeSpan TargetPosition = TimeSpan.Zero;

        /// <summary>
        /// Initializes a new instance of the <see cref="SeekCommand" /> class.
        /// </summary>
        /// <param name="manager">The media element.</param>
        /// <param name="targetPosition">The target position.</param>
        public SeekCommand(MediaCommandManager manager, TimeSpan targetPosition)
            : base(manager, MediaCommandType.Seek)
        {
            TargetPosition = targetPosition;
        }

        /// <summary>
        /// Performs the actions that this command implements.
        /// </summary>
        internal override void Execute()
        {
            var m = Manager.MediaElement;

            var pause = new PauseCommand(Manager);
            pause.Execute();

            var initialPosition = m.Clock.Position;
            m.SeekingDone.Reset();
            var startTime = DateTime.UtcNow;

            try
            {
                var main = m.Container.Components.Main.MediaType;
                var blocks = m.Blocks[main];
                var isInRange = blocks.IsInRange(TargetPosition);
                var renderIndex = blocks.IndexOf(TargetPosition);


                // 1. Check if we already have the block. If we do, simply set the clock position to the target position
                // we don't need anything 
                if (m.Blocks[main].IsInRange(TargetPosition))
                {
                    m.Clock.Position = TargetPosition;
                    return;
                }

                m.PacketReadingCycle.WaitOne();
                m.FrameDecodingCycle.WaitOne();

                // Clear Blocks and frames, reset the render times
                foreach (var t in m.Container.Components.MediaTypes)
                {
                    m.Frames[t].Clear();
                    m.Blocks[t].Clear();
                    m.LastRenderTime[t] = TimeSpan.MinValue;
                }

                // Populate frame queues with after-seek operation
                var frames = m.Container.Seek(TargetPosition);

                foreach (var frame in frames)
                    m.Frames[frame.MediaType].Push(frame);

                if (frames.Count > 0)
                {
                    var minStartTime = frames.Min(f => f.StartTime.Ticks);
                    var maxStartTime = frames.Max(f => f.StartTime.Ticks);

                    if (TargetPosition.Ticks < minStartTime)
                        m.Clock.Position = TimeSpan.FromTicks(minStartTime);
                    else if (TargetPosition.Ticks > maxStartTime)
                        m.Clock.Position = TimeSpan.FromTicks(maxStartTime);
                    else
                        m.Clock.Position = TargetPosition;
                }
                else
                {
                    if (frames.Count == 0 && TargetPosition != TimeSpan.Zero)
                    {
                        m.Clock.Position = initialPosition;
                    }
                }
            }
            catch (Exception)
            {
                // swallow
            }
            finally
            {
                // Call the seek method on all renderers
                foreach (var kvp in m.Renderers)
                    kvp.Value.Seek();

                m.Logger.Log(MediaLogMessageType.Debug,
                    $"SEEK D: Elapsed: {startTime.FormatElapsed()}");

                m.SeekingDone.Set();
            }
        }
    }
}
