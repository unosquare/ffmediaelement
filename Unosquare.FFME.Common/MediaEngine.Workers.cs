namespace Unosquare.FFME
{
    using Core;
    using Primitives;
    using Shared;
    using System;
    using System.Runtime.CompilerServices;
    using System.Threading;

    public partial class MediaEngine
    {
        /// <summary>
        /// This partial class implements:
        /// 1. Packet reading from the Container
        /// 2. Frame Decoding from packet buffer and Block buffering
        /// 3. Block Rendering from block buffer
        /// </summary>

        #region State Management

        private readonly AtomicBoolean m_IsSyncBuffering = new AtomicBoolean(false);

        private Thread PacketReadingTask = null;
        private Thread FrameDecodingTask = null;
        private Timer BlockRenderingWorker = null;
        private IWaitEvent BlockRenderingWorkerExit = null;

        /// <summary>
        /// Holds the materialized block cache for each media type.
        /// </summary>
        public MediaTypeDictionary<MediaBlockBuffer> Blocks { get; } = new MediaTypeDictionary<MediaBlockBuffer>();

        /// <summary>
        /// Gets the preloaded subtitle blocks.
        /// </summary>
        public MediaBlockBuffer PreloadedSubtitles { get; private set; } = null;

        /// <summary>
        /// Gets the packet reading cycle control evenet.
        /// </summary>
        internal IWaitEvent PacketReadingCycle { get; } = WaitEventFactory.Create(isCompleted: false, useSlim: true);

        /// <summary>
        /// Gets the frame decoding cycle control event.
        /// </summary>
        internal IWaitEvent FrameDecodingCycle { get; } = WaitEventFactory.Create(isCompleted: false, useSlim: true);

        /// <summary>
        /// Gets the block rendering cycle control event.
        /// </summary>
        internal IWaitEvent BlockRenderingCycle { get; } = WaitEventFactory.Create(isCompleted: false, useSlim: true);

        /// <summary>
        /// Completed whenever a change in the packet buffer is detected.
        /// This needs to be reset manually and prevents high CPU usage in the packet reading worker.
        /// </summary>
        internal IWaitEvent BufferChangedEvent { get; } = WaitEventFactory.Create(isCompleted: true, useSlim: true);

        /// <summary>
        /// Holds the block renderers
        /// </summary>
        internal MediaTypeDictionary<IMediaRenderer> Renderers { get; } = new MediaTypeDictionary<IMediaRenderer>();

        /// <summary>
        /// Holds the last rendered StartTime for each of the media block types
        /// </summary>
        internal MediaTypeDictionary<TimeSpan> LastRenderTime { get; } = new MediaTypeDictionary<TimeSpan>();

        /// <summary>
        /// Gets or sets a value indicating whether the decoder worker is sync-buffering
        /// </summary>
        internal bool IsSyncBuffering
        {
            get => m_IsSyncBuffering.Value;
            set => m_IsSyncBuffering.Value = value;
        }

        /// <summary>
        /// Gets a value indicating whether the packet reader has finished sync-buffering.
        /// </summary>
        internal bool CanExitSyncBuffering
        {
            get
            {
                if (IsSyncBuffering == false)
                    return false;

                if (Container.Components.BufferLength > BufferLengthMax)
                    return true;

                if (Container.Components.HasEnoughPackets)
                    return true;

                if (Container.IsLiveStream && Blocks[Container.Components.MainMediaType].IsFull)
                    return true;

                return false;
            }
        }

        /// <summary>
        /// Gets the buffer length maximum.
        /// port of MAX_QUEUE_SIZE (ffplay.c)
        /// </summary>
        internal long BufferLengthMax => 16 * 1024 * 1024;

        /// <summary>
        /// Gets a value indicating whether packets can be read and
        /// room is available in the download cache.
        /// </summary>
        internal bool ShouldReadMorePackets
        {
            get
            {
                if (Commands.IsStopWorkersPending || Container == null || Container.Components == null)
                    return false;

                if (Container.IsReadAborted || Container.IsAtEndOfStream)
                    return false;

                // If it's a live stream always continue reading, regardless
                if (Container.IsLiveStream)
                    return true;

                // For network streams always expect a minimum buffer length
                if (Container.IsNetworkStream && Container.Components.BufferLength < BufferLengthMax)
                    return true;

                // if we don't have enough packets queued we should read
                return Container.Components.HasEnoughPackets == false;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the reading worker can read packets at the current time.
        /// This is simply a bit-wise AND of negating <see cref="IsWorkerInterruptRequested"/> == false
        /// and <see cref="ShouldReadMorePackets"/>
        /// </summary>
        private bool ShouldWorkerReadPackets => IsWorkerInterruptRequested == false && ShouldReadMorePackets;

        /// <summary>
        /// Gets a value indicating whether a worker interrupt has been requested by the command manager.
        /// This instructs potentially long loops in workers to immediately exit.
        /// </summary>
        private bool IsWorkerInterruptRequested
        {
            get
            {
                return Commands.IsSeeking ||
                    Commands.IsChanging ||
                    Commands.IsClosing ||
                    Commands.IsStopWorkersPending;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Initializes the media block buffers and
        /// starts packet reader, frame decoder, and block rendering workers.
        /// </summary>
        internal void StartWorkers()
        {
            // Initialize the block buffers
            foreach (var t in Container.Components.MediaTypes)
            {
                Blocks[t] = new MediaBlockBuffer(Constants.MaxBlocks[t], t);
                Renderers[t] = Platform.CreateRenderer(t, this);
                InvalidateRenderer(t);
            }

            // Create the renderer for the preloaded subs
            if (PreloadedSubtitles != null)
            {
                var t = PreloadedSubtitles.MediaType;
                Renderers[t] = Platform.CreateRenderer(t, this);
                InvalidateRenderer(t);
            }

            Clock.SpeedRatio = Constants.Controller.DefaultSpeedRatio;
            Commands.IsStopWorkersPending = false;
            IsSyncBuffering = true;

            // Set the initial state of the task cycles.
            BlockRenderingCycle.Complete();
            FrameDecodingCycle.Begin();
            PacketReadingCycle.Begin();

            // Create the thread runners
            PacketReadingTask = new Thread(RunPacketReadingWorker)
            { IsBackground = true, Name = nameof(PacketReadingTask), Priority = ThreadPriority.Normal };

            FrameDecodingTask = new Thread(RunFrameDecodingWorker)
            { IsBackground = true, Name = nameof(FrameDecodingTask), Priority = ThreadPriority.AboveNormal };

            // Fire up the threads
            PacketReadingTask.Start();
            FrameDecodingTask.Start();
            StartBlockRenderingWorker();
        }

        /// <summary>
        /// Stops the packet reader, frame decoder, and block renderers
        /// </summary>
        internal void StopWorkers()
        {
            // Pause the clock so no further updates are propagated
            Clock.Pause();

            // Let the threads know a cancellation is pending.
            Commands.IsStopWorkersPending = true;

            // Cause an immediate Packet read abort
            Container?.SignalAbortReads(false);

            // Stop the rendering worker before anything else
            StopBlockRenderingWorker();

            // Call close on all renderers
            foreach (var renderer in Renderers.Values)
                renderer.Close();

            // Stop the rest of the workers
            // i.e. wait for worker threads to finish
            var wrokers = new[] { PacketReadingTask, FrameDecodingTask };
            foreach (var w in wrokers)
            {
                // Abort causes memory leaks bacause packets and frames might not
                // get disposed by the corresponding workers. We use Join instead.
                // w.Abort();
                w?.Join();
            }

            // Set the threads to null
            FrameDecodingTask = null;
            PacketReadingTask = null;

            // Remove the renderers disposing of them
            Renderers.Clear();

            // Reset the clock
            ResetPosition();
        }

        /// <summary>
        /// Preloads the subtitles from the MediaOptions.SubtitlesUrl.
        /// </summary>
        internal void PreloadSubtitles()
        {
            DisposePreloadedSubtitles();
            var subtitlesUrl = Container.MediaOptions.SubtitlesUrl;

            // Don't load a thing if we don't have to
            if (string.IsNullOrWhiteSpace(subtitlesUrl))
                return;

            try
            {
                PreloadedSubtitles = LoadBlocks(subtitlesUrl, MediaType.Subtitle, this);

                // Process and adjust subtitle delays if necessary
                if (Container.MediaOptions.SubtitlesDelay != TimeSpan.Zero)
                {
                    var delay = Container.MediaOptions.SubtitlesDelay;
                    for (var i = 0; i < PreloadedSubtitles.Count; i++)
                    {
                        var target = PreloadedSubtitles[i];
                        target.StartTime = TimeSpan.FromTicks(target.StartTime.Ticks + delay.Ticks);
                        target.EndTime = TimeSpan.FromTicks(target.EndTime.Ticks + delay.Ticks);
                        target.Duration = TimeSpan.FromTicks(target.EndTime.Ticks - target.StartTime.Ticks);
                    }
                }

                Container.MediaOptions.IsSubtitleDisabled = true;
            }
            catch (MediaContainerException mex)
            {
                DisposePreloadedSubtitles();
                Log(MediaLogMessageType.Warning,
                    $"No subtitles to side-load found in media '{subtitlesUrl}'. {mex.Message}");
            }
        }

        /// <summary>
        /// Returns the value of a discrete frame position of themain media component if possible.
        /// Otherwise, it simply rounds the position to the nearest millisecond.
        /// </summary>
        /// <param name="position">The position.</param>
        /// <returns>The snapped, discrete, normalized position</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal TimeSpan SnapPositionToBlockPosition(TimeSpan position)
        {
            // return position;
            if (Container == null)
                return position.Normalize();

            var blocks = Blocks[Container.Components.Main.MediaType];
            if (blocks == null) return position.Normalize();

            return blocks.GetSnapPosition(position) ?? position.Normalize();
        }

        /// <summary>
        /// Resumes the playback by resuming the clock and updating the playback state to state.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ResumePlayback()
        {
            Clock.Play();
            State.UpdateMediaState(PlaybackStatus.Play);
        }

        /// <summary>
        /// Updates the clock position and notifies the new
        /// position to the <see cref="State" />.
        /// </summary>
        /// <param name="position">The position.</param>
        /// <returns>The newly set postion</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal TimeSpan ChangePosition(TimeSpan position)
        {
            Clock.Update(position);
            State.UpdatePosition();
            return position;
        }

        /// <summary>
        /// Resets the clock to the zero position and notifies the new
        /// position to rhe <see cref="State"/>.
        /// </summary>
        /// <returns>The newly set postion</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal TimeSpan ResetPosition()
        {
            Clock.Reset();
            State.UpdatePosition();
            return TimeSpan.Zero;
        }

        /// <summary>
        /// Gets a value indicating whether more frames can be decoded into blocks of the given type.
        /// </summary>
        /// <param name="t">The media type.</param>
        /// <returns>
        ///   <c>true</c> if more frames can be decoded; otherwise, <c>false</c>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool CanReadMoreFramesOf(MediaType t)
        {
            return
                Container.Components[t].BufferLength > 0 ||
                Container.Components[t].HasPacketsInCodec ||
                ShouldReadMorePackets;
        }

        /// <summary>
        /// Sends the given block to its corresponding media renderer.
        /// </summary>
        /// <param name="block">The block.</param>
        /// <param name="clockPosition">The clock position.</param>
        /// <param name="main">The main component media type.</param>
        /// <returns>
        /// The number of blocks sent to the renderer
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int SendBlockToRenderer(MediaBlock block, TimeSpan clockPosition, MediaType main)
        {
            // No blocks were rendered
            if (block == null) return 0;

            // Process property changes coming from video blocks
            State.UpdateDynamicBlockProperties(block, main);

            // Send the block to its corresponding renderer
            Renderers[block.MediaType]?.Render(block, clockPosition);
            LastRenderTime[block.MediaType] = block.StartTime;

            // Extension method for logging
            this.LogRenderBlock(block, clockPosition, block.Index);
            return 1;
        }

        /// <summary>
        /// Tries to receive the next frame from the decoder by decoding queued
        /// Packets and converting the decoded frame into a Media Block which gets
        /// enqueued into the playback block buffer.
        /// </summary>
        /// <param name="t">The MediaType.</param>
        /// <returns>True if a block could be added. False otherwise.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool AddNextBlock(MediaType t)
        {
            // Decode the frames
            var block = Blocks[t].Add(Container.Components[t].ReceiveNextFrame(), Container);
            if (block != null) return true;

            return false;
        }

        #endregion
    }
}
