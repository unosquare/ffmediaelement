namespace Unosquare.FFME.Commands
{
    using Shared;
    using System;

    /// <summary>
    /// The Seek Command Implementation
    /// </summary>
    /// <seealso cref="CommandBase" />
    internal class SeekCommand : CommandBase
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
        }

        /// <inheritdoc />
        public override CommandType CommandType { get; }

        /// <summary>
        /// Gets or sets the target seek position.
        /// </summary>
        public TimeSpan TargetPosition { get; set; }

        /// <inheritdoc />
        protected override void PerformActions()
        {
            var m = MediaCore;
            m.Clock.Pause();
            var initialPosition = m.WallClock;
            var hasDecoderSeeked = false;
            var startTime = DateTime.UtcNow;

            try
            {
                var main = m.Container.Components.MainMediaType;
                var all = m.Container.Components.MediaTypes;
                var mainBlocks = m.Blocks[main];

                // Check if we already have the block. If we do, simply set the clock position to the target position
                // we don't need anything else. This implements frame-by frame seeking and we need to snap to a discrete
                // position of the main component so it sticks on it.
                if (mainBlocks.IsInRange(TargetPosition))
                {
                    m.ChangePosition(TargetPosition);
                    return;
                }

                // Mark for debugger output
                hasDecoderSeeked = true;

                // Signal the starting state clearing the packet buffer cache
                m.Container.Components.ClearQueuedPackets(flushBuffers: true);

                // wait for the current reading and decoding cycles
                // to finish. We don't want to interfere with reading in progress
                // or decoding in progress. For decoding we already know we are not
                // in a cycle because the decoding worker called this logic.
                m.PacketReadingCycle.Wait();

                // Capture seek target adjustment
                var adjustedSeekTarget = TargetPosition;
                if (mainBlocks.IsMonotonic)
                {
                    var targetSkewTicks = Convert.ToInt64(
                        mainBlocks.MonotonicDuration.Ticks * (mainBlocks.Capacity / 2d));

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
                // TODO: This might not be entirely right
                while (m.ShouldReadMorePackets
                    && mainBlocks.IsFull == false
                    && mainBlocks.IsInRange(TargetPosition) == false)
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
                TimeSpan resultPosition;
                if (mainBlocks.IsInRange(TargetPosition) == false)
                {
                    // We don't have a a valid main range
                    var minStartTimeTicks = mainBlocks.RangeStartTime.Ticks;
                    var maxStartTimeTicks = mainBlocks.RangeEndTime.Ticks;

                    this.LogWarning(Aspects.EngineCommand,
                        $"SEEK TP: Target Pos {TargetPosition.Format()} not between {mainBlocks.RangeStartTime.TotalSeconds:0.000} " +
                        $"and {mainBlocks.RangeEndTime.TotalSeconds:0.000}");

                    resultPosition = TimeSpan.FromTicks(TargetPosition.Ticks.Clamp(minStartTimeTicks, maxStartTimeTicks));
                }
                else
                {
                    resultPosition = mainBlocks.Count == 0 && TargetPosition != TimeSpan.Zero ?
                        initialPosition : // Unsuccessful. This initial position is simply what the clock was :(
                        TargetPosition; // Successful seek with main blocks in range
                }

                // Write a new Real-time clock position now.
                m.ChangePosition(resultPosition);
            }
            catch (Exception ex)
            {
                // Log the exception
                this.LogError(Aspects.EngineCommand, "SEEK ERROR", ex);
            }
            finally
            {
                if (hasDecoderSeeked)
                {
                    this.LogTrace(Aspects.EngineCommand,
                        $"SEEK D: Elapsed: {startTime.FormatElapsed()} | Target: {TargetPosition.Format()}");
                }
            }
        }
    }
}
