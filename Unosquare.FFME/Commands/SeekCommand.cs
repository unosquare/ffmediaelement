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

                // 1. Check if we already have the block. If we do, simply set the clock position to the target position
                // we don't need anything else.
                if (m.Blocks[main].IsInRange(TargetPosition))
                {
                    m.Clock.Position = TargetPosition;
                    return;
                }

                m.PacketReadingCycle.WaitOne();
                m.FrameDecodingCycle.WaitOne();

                // Capture seek target adjustment
                var adjustedSeekTarget = TargetPosition;
                if (main == MediaType.Video && m.Blocks[main].IsMonotonic)
                {
                    var targetSkewTicks = (long)Math.Round(
                        m.Blocks[main][0].Duration.Ticks * (m.Blocks[main].Capacity / 2d), 2);
                    adjustedSeekTarget = TimeSpan.FromTicks(adjustedSeekTarget.Ticks - targetSkewTicks);
                }

                // Clear Blocks and frames, reset the render times
                foreach (var t in m.Container.Components.MediaTypes)
                {
                    m.Blocks[t].Clear();
                    m.LastRenderTime[t] = TimeSpan.MinValue;
                }

                // Populate frame queues with after-seek operation
                var frames = m.Container.Seek(adjustedSeekTarget);
                m.HasMediaEnded = false;

                foreach (var kvp in m.Blocks)
                    kvp.Value.Clear();

                foreach (var frame in frames)
                    m.Blocks[frame.MediaType]?.Add(frame,  m.Container);

                if (m.Blocks[main].Count > 0)
                {
                    var minStartTime = m.Blocks[main].RangeStartTime.Ticks;
                    var maxStartTime = m.Blocks[main].RangeEndTime.Ticks;

                    if (adjustedSeekTarget.Ticks < minStartTime)
                        m.Clock.Position = TimeSpan.FromTicks(minStartTime);
                    else if (adjustedSeekTarget.Ticks > maxStartTime)
                        m.Clock.Position = TimeSpan.FromTicks(maxStartTime);
                    else
                        m.Clock.Position = TargetPosition;
                }
                else
                {
                    if (m.Blocks[main].Count == 0 && TargetPosition != TimeSpan.Zero)
                        m.Clock.Position = initialPosition;
                }
            }
            catch (Exception ex)
            {
                // Log the exception
                m.Logger.Log(MediaLogMessageType.Error,
                    $"SEEK E: {ex.GetType()} - {ex.Message}. Stack Trace:\r\n{ex.StackTrace}");
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
