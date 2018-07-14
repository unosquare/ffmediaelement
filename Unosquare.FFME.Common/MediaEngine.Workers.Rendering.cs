namespace Unosquare.FFME
{
    using Primitives;
    using Shared;
    using System;
    using System.Linq;
    using System.Threading;

    public partial class MediaEngine
    {
        /// <summary>
        /// Starts the block rendering worker.
        /// </summary>
        private void StartBlockRenderingWorker()
        {
            if (BlockRenderingWorkerExit != null)
                return;

            BlockRenderingWorkerExit = WaitEventFactory.Create(isCompleted: false, useSlim: true);

            // Holds the main media type
            var main = Container.Components.Main.MediaType;

            // Holds all components
            var all = Renderers.Keys.ToArray();

            // Holds a snapshot of the current block to render
            var currentBlock = new MediaTypeDictionary<MediaBlock>();

            // Keeps track of how many blocks were rendered in the cycle.
            var renderedBlockCount = new MediaTypeDictionary<int>();

            // reset render times for all components
            foreach (var t in all)
                InvalidateRenderer(t);

            // Ensure packet reading is running
            PacketReadingCycle.Wait();

            // wait for main component blocks or EOF or cancellation pending
            while (CanReadMoreFramesOf(main) && Blocks[main].Count <= 0)
                FrameDecodingCycle.Wait();

            // Set the initial clock position
            Clock.Update(Blocks[main].RangeStartTime);
            var wallClock = WallClock;

            // Wait for renderers to be ready
            foreach (var t in all)
                Renderers[t]?.WaitForReadyState();

            // The Render timer is responsible for sending frames to renders
            BlockRenderingWorker = new Timer((s) =>
            {
                #region Detect Exit/Skip Conditions

                if (Commands.IsStopWorkersPending || BlockRenderingWorkerExit.IsCompleted || IsDisposed)
                {
                    BlockRenderingWorkerExit?.Complete();
                    return;
                }

                // Skip the cycle if it's already running
                if (BlockRenderingCycle.IsInProgress)
                {
                    Log(MediaLogMessageType.Trace, $"SKIP: {nameof(BlockRenderingWorker)} already in a cycle. {WallClock}");
                    return;
                }

                #endregion

                #region Run the Rendering Cycle

                try
                {
                    #region 1. Control and Capture

                    // Wait for the seek op to finish before we capture blocks
                    Commands.WaitForActiveSeekCommand();

                    // Signal the start of a block rendering cycle
                    BlockRenderingCycle.Begin();

                    // Skip the cycle if we are running a priority command
                    if (Commands.IsExecutingDirectCommand) return;

                    // Updatete Status Properties
                    main = Container.Components.Main.MediaType;
                    all = Renderers.Keys.ToArray();

                    // Reset the rendered count to 0
                    foreach (var t in all)
                        renderedBlockCount[t] = 0;

                    #endregion

                    #region 2. Handle Block Rendering

                    // capture the wall clock for this cycle
                    wallClock = WallClock;

                    // Capture the blocks to render
                    foreach (var t in all)
                    {
                        if (t == MediaType.Subtitle && PreloadedSubtitles != null)
                        {
                            // Get the preloaded, cached subtitle block
                            currentBlock[t] = PreloadedSubtitles[wallClock];
                        }
                        else
                        {
                            // Get the regular audio, video, or sub block
                            currentBlock[t] = Blocks[t][wallClock];
                        }
                    }

                    // Render each of the Media Types if it is time to do so.
                    foreach (var t in all)
                    {
                        // Skip rendering for nulls
                        if (currentBlock[t] == null || currentBlock[t].IsDisposed)
                            continue;

                        // Render by forced signal (TimeSpan.MinValue) or because simply it is time to do so
                        if (LastRenderTime[t] == TimeSpan.MinValue || currentBlock[t].StartTime != LastRenderTime[t])
                            renderedBlockCount[t] += SendBlockToRenderer(currentBlock[t], wallClock, main);
                    }

                    #endregion

                    #region 3. Finalize the Rendering Cycle

                    // Call the update method on all renderers so they receive what the new wall clock is.
                    foreach (var t in all)
                        Renderers[t]?.Update(wallClock);

                    #endregion

                }
                catch { throw; }
                finally
                {
                    // Update the Position
                    if (State.IsSeeking == false && Commands.HasQueuedSeekOrStopCommands == false)
                        State.UpdatePosition(wallClock);

                    // Always exit notifying the cycle is done.
                    BlockRenderingCycle.Complete();
                }

                #endregion

            },
            this, // the state argument passed on to the ticker
            0,
            Convert.ToInt32(Constants.Interval.HighPriority.TotalMilliseconds));
        }

        /// <summary>
        /// Stops the block rendering worker.
        /// </summary>
        private void StopBlockRenderingWorker()
        {
            if (BlockRenderingWorkerExit == null)
                return;

            BlockRenderingWorkerExit?.Wait();
            BlockRenderingWorker?.Dispose();
            BlockRenderingWorker = null;
            BlockRenderingWorkerExit?.Dispose();
            BlockRenderingWorkerExit = null;
        }
    }
}
