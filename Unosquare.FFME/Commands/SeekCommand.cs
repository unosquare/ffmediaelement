﻿namespace Unosquare.FFME.Commands
{
    using Core;
    using System;
    using System.Linq;
    using System.Threading;

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
                var t = MediaType.None;

                // 1. Check if we already have the block. If we do, simply set the clock position to the target position
                // we don't need anything else.
                if (m.Blocks[main].IsInRange(TargetPosition))
                {
                    m.Clock.Position = TargetPosition;
                    return;
                }

                // Signal to wait one more frame dcoding cycle before 
                // sending blocks to the renderer.
                m.HasDecoderSeeked = true;

                // wait for the current reading and decoding cycles
                // to finish. We don't want to interfere with reading in progress
                // or decoding in progress
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
                foreach (var mt in m.Container.Components.MediaTypes)
                {
                    m.Blocks[mt].Clear();
                    m.LastRenderTime[mt] = TimeSpan.MinValue;
                }

                // Populate frame queues with after-seek operation
                var frames = m.Container.Seek(adjustedSeekTarget);
                m.HasMediaEnded = false;

                // Clear all the blocks. We don't need them
                foreach (var kvp in m.Blocks)
                    kvp.Value.Clear();

                // Create the blocks from the obtained seek frames
                foreach (var frame in frames)
                    m.Blocks[frame.MediaType]?.Add(frame, m.Container);

                // Now read blocks until we have reached at least the Target Position
                while (m.Container.IsAtEndOfStream == false
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

                // Handle out-of sync scenarios
                if (m.Blocks[main].IsInRange(TargetPosition) == false)
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
                    // TODO: handle this case correctly. The way this is handled currently sucks.
                    if (m.Blocks[main].Count == 0 && TargetPosition != TimeSpan.Zero)
                    {
                        m.Clock.Position = initialPosition;
                    }
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
                if (DateTime.UtcNow.Subtract(startTime).TotalMilliseconds > 5)
                    m.Logger.Log(MediaLogMessageType.Trace,
                        $"SEEK D: Elapsed: {startTime.FormatElapsed()} | Target: {TargetPosition.Format()}");

                m.SeekingDone.Set();
            }
        }
    }
}
