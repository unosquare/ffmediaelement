namespace Unosquare.FFME.Commands
{
    using Shared;
    using System;

    /// <summary>
    /// The Seek Command Implementation
    /// </summary>
    /// <seealso cref="CommandBase" />
    internal sealed class SeekCommand : CommandBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SeekCommand"/> class.
        /// </summary>
        /// <param name="mediaCore">The media core.</param>
        /// <param name="targetPosition">The target position.</param>
        public SeekCommand(MediaEngine mediaCore, TimeSpan targetPosition)
            : base(mediaCore)
        {
            TargetPosition = targetPosition;
            CommandType = CommandType.Seek;
            Category = CommandCategory.Delayed;
        }

        /// <summary>
        /// Gets the command type identifier.
        /// </summary>
        public override CommandType CommandType { get; }

        /// <summary>
        /// Gets the command category.
        /// </summary>
        public override CommandCategory Category { get; }

        /// <summary>
        /// Gets or sets the target seek position.
        /// </summary>
        public TimeSpan TargetPosition { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// Performs the actions represented by this deferred task.
        /// </summary>
        protected override void PerformActions()
        {
            var m = MediaCore;
            m.Clock.Pause();
            var initialPosition = m.WallClock;
            var hasDecoderSeeked = false;
            var startTime = DateTime.UtcNow;

            try
            {
                var main = m.Container.Components.Main.MediaType;
                var all = m.Container.Components.MediaTypes;

                // Check if we already have the block. If we do, simply set the clock position to the target position
                // we don't need anything else. This implements frame-by frame seeking and we need to snap to a discrete
                // position of the main component so it sticks on it.
                if (m.Blocks[main].IsInRange(TargetPosition))
                {
                    m.Clock.Update(m.SnapPositionToBlockPosition(TargetPosition));
                    return;
                }

                // Mark for debugger output
                m.State.SignalBufferingStarted();
                hasDecoderSeeked = true;

                // wait for the current reading and decoding cycles
                // to finish. We don't want to interfere with reading in progress
                // or decoding in progress
                m.PacketReadingCycle.Wait();

                // Capture seek target adjustment
                var adjustedSeekTarget = TargetPosition;
                if (main == MediaType.Video && m.Blocks[main].IsMonotonic)
                {
                    var targetSkewTicks = Convert.ToInt64(
                        m.Blocks[main][0].Duration.Ticks * (m.Blocks[main].Capacity / 2d));

                    if (adjustedSeekTarget.Ticks >= targetSkewTicks)
                        adjustedSeekTarget = TimeSpan.FromTicks(adjustedSeekTarget.Ticks - targetSkewTicks);
                }

                // Clear Blocks and frames, reset the render times
                foreach (var mt in all)
                {
                    m.Blocks[mt].Clear();
                    m.InvalidateRenderer(mt);
                }

                // Populate frame queues with after-seek operation
                var frames = m.Container.Seek(adjustedSeekTarget);
                m.State.UpdateMediaEnded(false);

                // Clear all the blocks. We don't need them
                foreach (var kvp in m.Blocks)
                    kvp.Value.Clear();

                // Create the blocks from the obtained seek frames
                foreach (var frame in frames)
                    m.Blocks[frame.MediaType]?.Add(frame, m.Container);

                // Now read blocks until we have reached at least the Target Position
                while (m.CanReadMorePackets
                    && m.Blocks[main].IsFull == false
                    && m.Blocks[main].IsInRange(TargetPosition) == false)
                {
                    // Read the next packet
                    m.Container.Read();

                    foreach (var mt in all)
                    {
                        if (m.Blocks[mt].IsFull == false)
                            m.Blocks[mt].Add(m.Container.Components[mt].ReceiveNextFrame(), m.Container);
                    }
                }

                // Find out what the final, best-effort position was
                var resultPosition = TargetPosition;
                if (m.Blocks[main].IsInRange(TargetPosition) == false)
                {
                    // We don't have a a valid main range
                    var minStartTimeTicks = m.Blocks[main].RangeStartTime.Ticks;
                    var maxStartTimeTicks = m.Blocks[main].RangeEndTime.Ticks;

                    m.Log(MediaLogMessageType.Warning,
                        $"SEEK TP: Target Pos {TargetPosition.Format()} not between {m.Blocks[main].RangeStartTime.TotalSeconds:0.000} " +
                        $"and {m.Blocks[main].RangeEndTime.TotalSeconds:0.000}");

                    resultPosition = TimeSpan.FromTicks(TargetPosition.Ticks.Clamp(minStartTimeTicks, maxStartTimeTicks));
                }
                else
                {
                    resultPosition = (m.Blocks[main].Count == 0 && TargetPosition != TimeSpan.Zero) ?
                        initialPosition : // Unsuccessful. This initial position is simply what the clock was :(
                        TargetPosition; // Successful seek with main blocks in range
                }

                // Write a new Real-time clock position now.
                m.Clock.Update(resultPosition);
            }
            catch (Exception ex)
            {
                // Log the exception
                m.Log(MediaLogMessageType.Error,
                    $"SEEK E: {ex.GetType()} - {ex.Message}. Stack Trace:\r\n{ex.StackTrace}");
            }
            finally
            {
                if (hasDecoderSeeked)
                {
                    m.Log(MediaLogMessageType.Trace,
                        $"SEEK D: Elapsed: {startTime.FormatElapsed()} | Target: {TargetPosition.Format()}");
                }
            }
        }
    }
}
