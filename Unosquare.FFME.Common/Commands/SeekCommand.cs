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
        /// Initializes a new instance of the <see cref="SeekCommand" /> class.
        /// </summary>
        /// <param name="mediaCore">The media core.</param>
        /// <param name="targetPosition">The target position.</param>
        /// <param name="targetSeekMode">The target seek mode.</param>
        public SeekCommand(MediaEngine mediaCore, TimeSpan targetPosition, SeekMode targetSeekMode)
            : base(mediaCore)
        {
            TargetPosition = targetPosition;
            TargetSeekMode = targetSeekMode;
            CommandType = CommandType.Seek;
        }

        /// <summary>
        /// Enumerates sepcial seek modes
        /// </summary>
        public enum SeekMode
        {
            /// <summary>Normal seek mode</summary>
            Normal,

            /// <summary>Stop seek mode</summary>
            Stop,

            /// <summary>Frame step forward</summary>
            StepForward,

            /// <summary>Frame step backward</summary>
            StepBackward
        }

        /// <inheritdoc />
        public override CommandType CommandType { get; }

        /// <summary>
        /// Gets or sets the target seek mode.
        /// </summary>
        public SeekMode TargetSeekMode { get; }

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

                if (TargetSeekMode == SeekMode.StepBackward || TargetSeekMode == SeekMode.StepForward)
                {
                    var neighbors = mainBlocks.Neighbors(initialPosition);
                    TargetPosition = neighbors[TargetSeekMode == SeekMode.StepBackward ? 0 : 1]?.StartTime ??
                        TimeSpan.FromTicks(neighbors[2].StartTime.Ticks - Convert.ToInt64(neighbors[2].Duration.Ticks / 2d));
                }
                else if (TargetSeekMode == SeekMode.Stop)
                {
                    TargetPosition = TimeSpan.Zero;
                }

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

                // wait for the current reading and decoding cycles
                // to finish. We don't want to interfere with reading in progress
                // or decoding in progress. For decoding we already know we are not
                // in a cycle because the decoding worker called this logic.
                m.Workers.Pause(true);

                // Signal the starting state clearing the packet buffer cache
                m.Container.Components.ClearQueuedPackets(flushBuffers: true);

                // Capture seek target adjustment
                var adjustedSeekTarget = TargetPosition;
                if (TargetPosition != TimeSpan.Zero && mainBlocks.IsMonotonic)
                {
                    var targetSkewTicks = Convert.ToInt64(
                        mainBlocks.MonotonicDuration.Ticks * (mainBlocks.Capacity / 2d));

                    if (adjustedSeekTarget.Ticks >= targetSkewTicks)
                        adjustedSeekTarget = TimeSpan.FromTicks(adjustedSeekTarget.Ticks - targetSkewTicks);
                }

                // Populate frame queues with after-seek operation
                var firstFrame = m.Container.Seek(adjustedSeekTarget);
                if (firstFrame != null)
                {
                    // Ensure we signal media has not ended
                    m.State.UpdateMediaEnded(false, TimeSpan.Zero);

                    // Clear Blocks and frames, reset the render times
                    foreach (var mt in all)
                    {
                        m.Blocks[mt].Clear();
                        m.InvalidateRenderer(mt);
                    }

                    // Create the blocks from the obtained seek frames
                    m.Blocks[firstFrame.MediaType]?.Add(firstFrame, m.Container);

                    // Decode all available queued packets into the media component blocks
                    foreach (var mt in all)
                    {
                        while (m.Blocks[mt].IsFull == false)
                        {
                            var frame = m.Container.Components[mt].ReceiveNextFrame();
                            if (frame == null) break;
                            m.Blocks[mt].Add(frame, m.Container);
                        }
                    }

                    // Align to the exact requested position on the main component
                    while (m.ShouldReadMorePackets)
                    {
                        // Check if we are already in range
                        if (mainBlocks.IsInRange(TargetPosition)) break;

                        // Read the next packet
                        var packetType = m.Container.Read();
                        var blocks = m.Blocks[packetType];
                        if (blocks == null) continue;

                        // Get the next frame
                        if (blocks.RangeEndTime.Ticks < TargetPosition.Ticks || blocks.IsFull == false)
                            blocks.Add(m.Container.Components[packetType].ReceiveNextFrame(), m.Container);
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
