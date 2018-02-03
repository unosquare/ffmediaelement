namespace Unosquare.FFME.Commands
{
    using Shared;
    using System;

    /// <summary>
    /// Implements the logic to seek on the media stream
    /// </summary>
    /// <seealso cref="MediaCommand" />
    internal sealed class SeekCommand : MediaCommand
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SeekCommand" /> class.
        /// </summary>
        /// <param name="manager">The media element.</param>
        /// <param name="targetPosition">The target position.</param>
        public SeekCommand(MediaCommandManager manager, TimeSpan targetPosition)
            : base(manager, MediaCommandType.Seek)
        {
            TargetPosition = targetPosition.Normalize();
        }

        /// <summary>
        /// Gets or sets the target position.
        /// </summary>
        /// <value>
        /// The target position.
        /// </value>
        public TimeSpan TargetPosition { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// Performs the actions that this command implements.
        /// </summary>
        internal override void ExecuteInternal()
        {
            var m = Manager.MediaCore;
            m.Clock.Pause();
            var initialPosition = m.WallClock;
            m.State.UpdateMediaState(PlaybackStatus.Manual);
            m.SeekingDone.Begin();
            var startTime = DateTime.UtcNow;

            try
            {
                var main = m.Container.Components.Main.MediaType;
                var all = m.Container.Components.MediaTypes;
                var t = MediaType.None;

                // Check if we already have the block. If we do, simply set the clock position to the target position
                // we don't need anything else.
                if (m.Blocks[main].IsInRange(TargetPosition))
                {
                    m.Clock.Update(TargetPosition);
                    return;
                }

                // Signal to wait one more frame decoding cycle before
                // sending blocks to the renderer.
                m.HasDecoderSeeked = true;

                // wait for the current reading and decoding cycles
                // to finish. We don't want to interfere with reading in progress
                // or decoding in progress
                m.PacketReadingCycle.Wait();

                // Capture seek target adjustment
                var adjustedSeekTarget = TargetPosition;
                if (main == MediaType.Video && m.Blocks[main].IsMonotonic)
                {
                    var targetSkewTicks = (long)Math.Round(
                        m.Blocks[main][0].Duration.Ticks * (m.Blocks[main].Capacity / 2d), 2);
                    adjustedSeekTarget = TimeSpan.FromTicks(adjustedSeekTarget.Ticks - targetSkewTicks);
                }

                // Clear Blocks and frames, reset the render times
                foreach (var mt in all)
                {
                    m.Blocks[mt].Clear();
                    m.LastRenderTime[mt] = TimeSpan.MinValue;
                }

                // Populate frame queues with after-seek operation
                var frames = m.Container.Seek(adjustedSeekTarget);
                m.State.HasMediaEnded = false;

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
                    t = m.Container.Read();

                    // Ignore if we don't have an acceptable packet
                    if (m.Blocks.ContainsKey(t) == false)
                        continue;

                    // move on if we have plenty
                    if (m.Blocks[t].IsFull) continue;

                    // Decode and add the frames to the corresponding output
                    frames.Clear();
                    frames.AddRange(m.Container.Components[t].ReceiveFrames());

                    foreach (var frame in frames)
                        m.Blocks[t].Add(frame, m.Container);
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
                        initialPosition : // Unsuccessful. This initial position is simply
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
                if (m.HasDecoderSeeked)
                {
                    m.Log(MediaLogMessageType.Debug,
                        $"SEEK D: Elapsed: {startTime.FormatElapsed()} | Target: {TargetPosition.Format()}");
                }

                m.SeekingDone.Complete();
            }
        }
    }
}
