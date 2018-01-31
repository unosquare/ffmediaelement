namespace Unosquare.FFME
{
    using Primitives;
    using Shared;
    using System;
    using System.Threading;

    public partial class MediaEngine
    {
        /// <summary>
        /// Starts the block rendering worker.
        /// </summary>
        private void StartBlockRenderingWorker()
        {
            if (HasBlockRenderingWorkerExited != null)
                return;

            HasBlockRenderingWorkerExited = new ManualResetEvent(false);

            // Synchronized access to parts of the run cycle
            var isRunningRenderingCycle = false;

            // Holds the main media type
            var main = Container.Components.Main.MediaType;

            // Holds the auxiliary media types
            var auxs = Container.Components.MediaTypes.ExcludeMediaType(main);

            // Holds all components
            var all = Container.Components.MediaTypes.DeepCopy();

            // Holds a snapshot of the current block to render
            var currentBlock = new MediaTypeDictionary<MediaBlock>();

            // Keeps track of how many blocks were rendered in the cycle.
            var renderedBlockCount = new MediaTypeDictionary<int>();

            // reset render times for all components
            foreach (var t in all)
                LastRenderTime[t] = TimeSpan.MinValue;

            // Ensure packet reading is running
            PacketReadingCycle.WaitOne();

            // wait for main component blocks or EOF or cancellation pending
            while (CanReadMoreFramesOf(main) && Blocks[main].Count <= 0)
                FrameDecodingCycle.WaitOne();

            // Set the initial clock position
            // TODO: maybe update media start time offset to this Minimum, initial Start Time intead of relying on contained meta?
            Clock.Update(Blocks[main].RangeStartTime); // .GetMinStartTime()
            var wallClock = WallClock;

            // Wait for renderers to be ready
            foreach (var t in all)
                Renderers[t]?.WaitForReadyState();

            // The Render timer is responsible for sending frames to renders
            BlockRenderingWorker = new Timer((s) =>
            {
                #region Detect a Timer Stop

                if (IsTaskCancellationPending || HasBlockRenderingWorkerExited.IsSet() || IsDisposed)
                {
                    HasBlockRenderingWorkerExited.Set();
                    return;
                }

                #endregion

                #region Run the Rendering Cycle

                // Updatete Status  Properties
                State.UpdateBufferingProperties();

                // Don't run the cycle if it's already running
                if (isRunningRenderingCycle)
                {
                    // TODO: Maybe Log a frame skip here?
                    return;
                }

                try
                {
                    #region 1. Control and Capture

                    // Flag the current rendering cycle
                    isRunningRenderingCycle = true;

                    // Reset the rendered count to 0
                    foreach (var t in all)
                        renderedBlockCount[t] = 0;

                    // Capture current clock position for the rest of this cycle
                    BlockRenderingCycle.Reset();

                    #endregion

                    #region 2. Handle Block Rendering

                    // Wait for the seek op to finish before we capture blocks
                    if (HasDecoderSeeked)
                        SeekingDone.WaitOne();

                    // capture the wall clock for this cycle
                    wallClock = WallClock;

                    // Capture the blocks to render
                    foreach (var t in all)
                        currentBlock[t] = Blocks[t][wallClock];

                    // Render each of the Media Types if it is time to do so.
                    foreach (var t in all)
                    {
                        // Skip rendering for nulls
                        if (currentBlock[t] == null)
                            continue;

                        // Render by forced signal (TimeSpan.MinValue)
                        if (LastRenderTime[t] == TimeSpan.MinValue)
                        {
                            renderedBlockCount[t] += SendBlockToRenderer(currentBlock[t], wallClock);
                            continue;
                        }

                        // Render because we simply have not rendered
                        if (currentBlock[t].StartTime != LastRenderTime[t])
                        {
                            renderedBlockCount[t] += SendBlockToRenderer(currentBlock[t], wallClock);
                            continue;
                        }
                    }

                    #endregion

                    #region 6. Finalize the Rendering Cycle

                    // Call the update method on all renderers so they receive what the new wall clock is.
                    foreach (var t in all)
                        Renderers[t]?.Update(wallClock);

                    #endregion

                }
                catch (ThreadAbortException) { /* swallow */ }
                catch { if (!IsDisposed) throw; }
                finally
                {
                    // Always exit notifying the cycle is done.
                    BlockRenderingCycle.Set();
                    isRunningRenderingCycle = false;
                }

                #endregion

            },
            this, // the state argument passed on to the ticker
            0,
            (int)Constants.Interval.HighPriority.TotalMilliseconds);
        }

        /// <summary>
        /// Stops the block rendering worker.
        /// </summary>
        private void StopBlockRenderingWorker()
        {
            if (HasBlockRenderingWorkerExited == null)
                return;

            HasBlockRenderingWorkerExited.WaitOne();
            BlockRenderingWorker?.Dispose();
            BlockRenderingWorker = null;
            HasBlockRenderingWorkerExited.Dispose();
            HasBlockRenderingWorkerExited = null;
        }
    }
}
