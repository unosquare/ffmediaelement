namespace Unosquare.FFME.Decoding
{
    using Core;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Represents a set of preallocated media blocks of the same media type.
    /// A block buffer contains playback and pool blocks. Pool blocks are blocks that
    /// can be reused. Playback blocks are blocks that have been filled.
    /// This class is thread safe.
    /// </summary>
    internal sealed class MediaBlockBuffer : IDisposable
    {
        #region Private Declarations

        private readonly object SyncRoot = new object();

        /// <summary>
        /// The blocks that are available to be filled.
        /// </summary>
        private readonly Queue<MediaBlock> PoolBlocks = new Queue<MediaBlock>();

        /// <summary>
        /// The blocks that are available for rendering.
        /// </summary>
        private readonly List<MediaBlock> PlaybackBlocks = new List<MediaBlock>();

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaBlockBuffer"/> class.
        /// </summary>
        /// <param name="capacity">The capacity.</param>
        /// <param name="mediaType">Type of the media.</param>
        public MediaBlockBuffer(int capacity, MediaType mediaType)
        {
            Capacity = capacity;
            MediaType = mediaType;

            // allocate the blocks
            for (var i = 0; i < capacity; i++)
                PoolBlocks.Enqueue(CreateBlock());
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the media type of the block buffer.
        /// </summary>
        public MediaType MediaType { get; private set; }

        /// <summary>
        /// Gets the start time of the first block.
        /// </summary>
        public TimeSpan RangeStartTime
        {
            get
            {
                lock (SyncRoot)
                    return PlaybackBlocks.Count == 0 ? TimeSpan.Zero : PlaybackBlocks[0].StartTime;
            }
        }

        /// <summary>
        /// Gets the end time of the last block.
        /// </summary>
        public TimeSpan RangeEndTime
        {
            get
            {
                lock (SyncRoot)
                {
                    if (PlaybackBlocks.Count == 0) return TimeSpan.Zero;
                    var lastBlock = PlaybackBlocks[PlaybackBlocks.Count - 1];
                    return TimeSpan.FromTicks(lastBlock.EndTime.Ticks);
                }
            }
        }

        /// <summary>
        /// Gets the range of time between the first block and the end time of the last block.
        /// </summary>
        public TimeSpan RangeDuration
        {
            get
            {
                lock (SyncRoot)
                    return TimeSpan.FromTicks(RangeEndTime.Ticks - RangeStartTime.Ticks);
            }
        }

        /// <summary>
        /// Gets the average duration of the currently available playback blocks.
        /// </summary>
        public TimeSpan AverageBlockDuration
        {
            get
            {
                lock (SyncRoot)
                {
                    if (PlaybackBlocks.Count <= 0) return TimeSpan.Zero;
                    return TimeSpan.FromTicks((long)PlaybackBlocks.Average(b => Convert.ToDouble(b.Duration.Ticks)));
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether all the durations of the blocks are equal
        /// </summary>
        public bool IsMonotonic
        {
            get
            {
                lock (SyncRoot)
                {
                    if (PlaybackBlocks.Count > 1)
                    {
                        var firstBlockDuration = PlaybackBlocks[0].Duration;
                        return PlaybackBlocks.All(b => b.Duration == firstBlockDuration);
                    }

                    return false;
                }
            }
        }

        /// <summary>
        /// Gets the number of available playback blocks.
        /// </summary>
        public int Count
        {
            get { lock (SyncRoot) return PlaybackBlocks.Count; }
        }

        /// <summary>
        /// Gets the maximum count of this buffer.
        /// </summary>
        public int Capacity { get; private set; }

        /// <summary>
        /// Gets the usage percent from 0.0 to 1.0
        /// </summary>
        public double CapacityPercent
        {
            get { lock (SyncRoot) return (double)Count / Capacity; }
        }

        /// <summary>
        /// Gets a value indicating whether the playback blocks are all allocated.
        /// </summary>
        public bool IsFull
        {
            get { lock (SyncRoot) return Count >= Capacity; }
        }

        /// <summary>
        /// Gets the <see cref="MediaBlock" /> at the specified index.
        /// </summary>
        /// <value>
        /// The <see cref="MediaBlock"/>.
        /// </value>
        /// <param name="index">The index.</param>
        /// <returns>The media block</returns>
        public MediaBlock this[int index]
        {
            get { lock (SyncRoot) return PlaybackBlocks[index]; }
        }

        /// <summary>
        /// Gets the <see cref="MediaBlock" /> at the specified timestamp.
        /// </summary>
        /// <value>
        /// The <see cref="MediaBlock"/>.
        /// </value>
        /// <param name="at">At time.</param>
        /// <returns>The media block</returns>
        public MediaBlock this[TimeSpan at]
        {
            get
            {
                lock (SyncRoot)
                {
                    var index = IndexOf(at);
                    if (index >= 0) { return PlaybackBlocks[index]; }

                    return null;
                }
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Gets the percentage of the range for the given time position.
        /// </summary>
        /// <param name="position">The position.</param>
        /// <returns>The percent of the range</returns>
        public double GetRangePercent(TimeSpan position)
        {
            lock (SyncRoot)
            {
                return RangeDuration.Ticks != 0 ?
                    ((double)position.Ticks - RangeStartTime.Ticks) / RangeDuration.Ticks : 0d;
            }
        }

        /// <summary>
        /// Retrieves the block following the provided current block
        /// </summary>
        /// <param name="current">The current block.</param>
        /// <returns>The next media block</returns>
        public MediaBlock Next(MediaBlock current)
        {
            lock (SyncRoot)
            {
                var currentIndex = PlaybackBlocks.IndexOf(current);
                if (currentIndex < 0) return null;

                if (currentIndex + 1 < PlaybackBlocks.Count)
                    return PlaybackBlocks[currentIndex + 1];

                return null;
            }
        }

        /// <summary>
        /// Adds a block to the playback blocks by converting the given frame.
        /// If there are no more blocks in the pool, the oldest block is returned to the pool
        /// and reused for the new block. The source frame is automatically disposed.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="container">The container.</param>
        /// <returns>The filled block.</returns>
        public MediaBlock Add(MediaFrame source, MediaContainer container)
        {
            lock (SyncRoot)
            {
                // Check if we already have a block at the given time
                if (IsInRange(source.StartTime))
                {
                    var reapeatedBlock = PlaybackBlocks.FirstOrDefault(f => f.StartTime.Ticks == source.StartTime.Ticks);
                    if (reapeatedBlock != null)
                    {
                        PlaybackBlocks.Remove(reapeatedBlock);
                        PoolBlocks.Enqueue(reapeatedBlock);
                    }
                }

                // if there are no available blocks, make room!
                if (PoolBlocks.Count <= 0)
                {
                    var firstBlock = PlaybackBlocks[0];
                    PlaybackBlocks.RemoveAt(0);
                    PoolBlocks.Enqueue(firstBlock);
                }

                // Get a block reference from the pool and convert it!
                var targetBlock = PoolBlocks.Dequeue();
                container.Convert(source, ref targetBlock, true);

                // Add the converted block to the playback list and sort it.
                PlaybackBlocks.Add(targetBlock);
                PlaybackBlocks.Sort();
                return targetBlock;
            }
        }

        /// <summary>
        /// Clears all the playback blocks returning them to the 
        /// block pool.
        /// </summary>
        public void Clear()
        {
            lock (SyncRoot)
            {
                // return all the blocks to the block pool
                foreach (var block in PlaybackBlocks)
                    PoolBlocks.Enqueue(block);

                PlaybackBlocks.Clear();
            }
        }

        /// <summary>
        /// Determines whether the given render time is within the range of playback blocks.
        /// </summary>
        /// <param name="renderTime">The render time.</param>
        /// <returns>
        ///   <c>true</c> if [is in range] [the specified render time]; otherwise, <c>false</c>.
        /// </returns>
        public bool IsInRange(TimeSpan renderTime)
        {
            lock (SyncRoot)
            {
                if (PlaybackBlocks.Count == 0) return false;
                return renderTime.Ticks >= RangeStartTime.Ticks && renderTime.Ticks <= RangeEndTime.Ticks;
            }
        }

        /// <summary>
        /// Retrieves the index of the playback block corresponding to the specified
        /// render time. This uses very fast binary and linear search commbinations.
        /// If there are no playback blocks it returns -1.
        /// If the render time is greater than the range end time, it returns the last playback block index.
        /// If the render time is less than the range start time, it returns the first playback block index.
        /// </summary>
        /// <param name="renderTime">The render time.</param>
        /// <returns>The media block's index</returns>
        public int IndexOf(TimeSpan renderTime)
        {
            lock (SyncRoot)
            {
                var blockCount = PlaybackBlocks.Count;

                // fast condition checking
                if (blockCount <= 0) return -1;
                if (blockCount == 1) return 0;

                // variable setup
                var lowIndex = 0;
                var highIndex = blockCount - 1;
                var midIndex = 1 + lowIndex + ((highIndex - lowIndex) / 2);

                // edge condition cheching
                if (PlaybackBlocks[lowIndex].StartTime >= renderTime) return lowIndex;
                if (PlaybackBlocks[highIndex].StartTime <= renderTime) return highIndex;

                // First guess, very low cost, very fast
                if (midIndex < highIndex
                    && renderTime >= PlaybackBlocks[midIndex].StartTime
                    && renderTime < PlaybackBlocks[midIndex + 1].StartTime)
                    return midIndex;

                // binary search
                while (highIndex - lowIndex > 1)
                {
                    midIndex = lowIndex + ((highIndex - lowIndex) / 2);
                    if (renderTime < PlaybackBlocks[midIndex].StartTime)
                        highIndex = midIndex;
                    else
                        lowIndex = midIndex;
                }

                // linear search
                for (var i = highIndex; i >= lowIndex; i--)
                {
                    if (PlaybackBlocks[i].StartTime <= renderTime)
                        return i;
                }

                return -1;
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            lock (SyncRoot)
            {
                while (PoolBlocks.Count > 0)
                {
                    var block = PoolBlocks.Dequeue();
                    block.Dispose();
                }

                for (var i = PlaybackBlocks.Count - 1; i >= 0; i--)
                {
                    var block = PlaybackBlocks[i];
                    PlaybackBlocks.RemoveAt(i);
                    block.Dispose();
                }
            }
        }

        /// <summary>
        /// Returns a formatted string with information about this buffer
        /// </summary>
        /// <returns>The formatted string</returns>
        internal string Debug()
        {
            lock (SyncRoot)
                return $"{MediaType,-12} - CAP: {Capacity,10} | FRE: {PoolBlocks.Count,7} | USD: {PlaybackBlocks.Count,4} |  TM: {RangeStartTime.Format(),8} to {RangeEndTime.Format().Trim()}";
        }

        /// <summary>
        /// Block factory method.
        /// </summary>
        /// <returns>The media frame</returns>
        /// <exception cref="System.InvalidCastException">MediaBlock</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private MediaBlock CreateBlock()
        {
            if (MediaType == MediaType.Video) return new VideoBlock();
            if (MediaType == MediaType.Audio) return new AudioBlock();
            if (MediaType == MediaType.Subtitle) return new SubtitleBlock();

            throw new InvalidCastException($"No {nameof(MediaBlock)} constructor for {nameof(MediaType)} '{MediaType}'");
        }

        #endregion

    }
}
