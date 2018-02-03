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

        private readonly IWaitEvent m_PacketReadingCycle = WaitEventFactory.Create(isCompleted: false, useSlim: true);
        private readonly IWaitEvent m_FrameDecodingCycle = WaitEventFactory.Create(isCompleted: false, useSlim: true);
        private readonly IWaitEvent m_BlockRenderingCycle = WaitEventFactory.Create(isCompleted: false, useSlim: true);
        private readonly IWaitEvent m_SeekingDone = WaitEventFactory.Create(isCompleted: true, useSlim: true);

        private Thread PacketReadingTask = null;
        private Thread FrameDecodingTask = null;
        private Timer BlockRenderingWorker = null;

        private AtomicBoolean m_IsTaskCancellationPending = new AtomicBoolean(false);
        private AtomicBoolean m_HasDecoderSeeked = new AtomicBoolean(false);
        private IWaitEvent BlockRenderingWorkerExit = null;

        /// <summary>
        /// Holds the materialized block cache for each media type.
        /// </summary>
        public MediaTypeDictionary<MediaBlockBuffer> Blocks { get; } = new MediaTypeDictionary<MediaBlockBuffer>();

        /// <summary>
        /// Gets the packet reading cycle control evenet.
        /// </summary>
        internal IWaitEvent PacketReadingCycle => m_PacketReadingCycle;

        /// <summary>
        /// Gets the frame decoding cycle control event.
        /// </summary>
        internal IWaitEvent FrameDecodingCycle => m_FrameDecodingCycle;

        /// <summary>
        /// Gets the block rendering cycle control event.
        /// </summary>
        internal IWaitEvent BlockRenderingCycle => m_BlockRenderingCycle;

        /// <summary>
        /// Gets the seeking done control event.
        /// </summary>
        internal IWaitEvent SeekingDone => m_SeekingDone;

        /// <summary>
        /// Gets or sets a value indicating whether the workedrs have been requested
        /// an exit.
        /// </summary>
        internal bool IsTaskCancellationPending
        {
            get => m_IsTaskCancellationPending.Value;
            set => m_IsTaskCancellationPending.Value = value;
        }

        /// <summary>
        /// Gets or sets a value indicating whether the decoder has moved its byte position
        /// to something other than the normal continuous reads in the last read cycle.
        /// </summary>
        internal bool HasDecoderSeeked
        {
            get => m_HasDecoderSeeked.Value;
            set => m_HasDecoderSeeked.Value = value;
        }

        /// <summary>
        /// Holds the block renderers
        /// </summary>
        internal MediaTypeDictionary<IMediaRenderer> Renderers { get; } = new MediaTypeDictionary<IMediaRenderer>();

        /// <summary>
        /// Holds the last rendered StartTime for each of the media block types
        /// </summary>
        internal MediaTypeDictionary<TimeSpan> LastRenderTime { get; } = new MediaTypeDictionary<TimeSpan>();

        /// <summary>
        /// Gets a value indicating whether more packets can be read from the stream.
        /// This does not check if the packet queue is full.
        /// </summary>
        internal bool CanReadMorePackets => (Container?.IsReadAborted ?? true) == false
            && (Container?.IsAtEndOfStream ?? true) == false;

        /// <summary>
        /// Gets a value indicating whether room is available in the download cache.
        /// </summary>
        internal bool ShouldReadMorePackets
        {
            get
            {
                if (Container == null || Container.Components == null)
                    return false;

                // If it's a live stream always continue reading regardless
                if (State.IsLiveStream) return true;

                return Container.Components.PacketBufferLength < State.DownloadCacheLength;
            }
        }

        /// <summary>
        /// Gets a value indicating whether more frames can be decoded from the packet queue.
        /// That is, if we have packets in the packet buffer or if we are not at the end of the stream.
        /// </summary>
        internal bool CanReadMoreFrames => CanReadMorePackets || (Container?.Components.PacketBufferLength ?? 0) > 0;

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
                LastRenderTime[t] = TimeSpan.MinValue;
                Renderers[t] = Platform.CreateRenderer(t, this);
            }

            Clock.SpeedRatio = Constants.Controller.DefaultSpeedRatio;
            IsTaskCancellationPending = false;

            // Set the initial state of the task cycles.
            SeekingDone.Complete();
            BlockRenderingCycle.Begin();
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
            IsTaskCancellationPending = true;

            // Cause an immediate Packet read abort
            Container.SignalAbortReads(false);

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
                // w.Abort(); //Abort causes memory leaks bacause packets and frames might not
                // get disposed by the corresponding workers. We use Join instead.
                w.Join();
            }

            // Set the threads to null
            FrameDecodingTask = null;
            PacketReadingTask = null;

            // Remove the renderers disposing of them
            Renderers.Clear();

            // Reset the clock
            Clock.Reset();
        }

        /// <summary>
        /// Returns the value of a discrete video position if possible
        /// </summary>
        /// <param name="position">The position.</param>
        /// <returns>The snapped position</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal TimeSpan SnapToFramePosition(TimeSpan position)
        {
            // return position;
            if (Container == null)
                return position;

            // Set the clock to a discrete video position if possible
            if (Container.Components.Main.MediaType == MediaType.Video
               && Blocks[MediaType.Video].IsInRange(position))
            {
                var block = Blocks[MediaType.Video][position];
                if (block != null && block.Duration.Ticks > 0 && State.VideoFrameRate != 0d)
                    return block.SnapTime.Normalize();
            }

            return position.Normalize();
        }

        /// <summary>
        /// Gets a value indicating whether more frames can be converted into blocks of the given type.
        /// </summary>
        /// <param name="t">The t.</param>
        /// <returns>
        ///   <c>true</c> if this instance [can read more frames of] the specified t; otherwise, <c>false</c>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool CanReadMoreFramesOf(MediaType t)
        {
            return CanReadMorePackets || Container.Components[t].PacketBufferLength > 0;
        }

        /// <summary>
        /// Sends the given block to its corresponding media renderer.
        /// </summary>
        /// <param name="block">The block.</param>
        /// <param name="clockPosition">The clock position.</param>
        /// <returns>The number of blocks sent to the renderer</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int SendBlockToRenderer(MediaBlock block, TimeSpan clockPosition)
        {
            // Process property changes coming from video blocks
            if (block != null && block.MediaType == MediaType.Video)
            {
                if (block is VideoBlock videoBlock)
                {
                    State.VideoSmtpeTimecode = videoBlock.SmtpeTimecode;
                    State.VideoHardwareDecoder = (Container?.Components.Video?.IsUsingHardwareDecoding ?? false) ?
                        Container?.Components.Video?.HardwareAccelerator?.Name ?? string.Empty : string.Empty;
                }
            }

            // Send the block to its corresponding renderer
            Renderers[block.MediaType]?.Render(block, clockPosition);

            // Extension method for logging
            this.LogRenderBlock(block, clockPosition, Blocks[block.MediaType].IndexOf(clockPosition));
            LastRenderTime[block.MediaType] = block.StartTime;
            return 1;
        }

        /// <summary>
        /// Adds the blocks of the given media type.
        /// </summary>
        /// <param name="t">The t.</param>
        /// <returns>The number of blocks that were added</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int AddBlocks(MediaType t)
        {
            var decodedFrameCount = 0;

            // Decode the frames
            var frames = Container.Components[t].ReceiveFrames();

            // exit the loop if there was nothing more to decode
            foreach (var frame in frames)
            {
                // Add each decoded frame as a playback block
                if (frame == null) continue;
                Blocks[t].Add(frame, Container);
                decodedFrameCount += 1;
            }

            return decodedFrameCount;
        }

        #endregion
    }
}
