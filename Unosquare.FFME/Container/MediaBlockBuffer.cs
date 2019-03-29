﻿namespace Unosquare.FFME.Container
{
    using Common;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Represents a set of pre-allocated media blocks of the same media type.
    /// A block buffer contains playback and pool blocks. Pool blocks are blocks that
    /// can be reused. Playback blocks are blocks that have been filled.
    /// This class is thread safe.
    /// </summary>
    internal sealed class MediaBlockBuffer : IDisposable
    {
        #region Private Declarations

        /// <summary>
        /// The blocks that are available to be filled.
        /// </summary>
        private readonly Queue<MediaBlock> PoolBlocks;

        /// <summary>
        /// The blocks that are available for rendering.
        /// </summary>
        private readonly List<MediaBlock> PlaybackBlocks;

        /// <summary>
        /// Controls multiple reads and exclusive writes.
        /// </summary>
        private readonly object SyncLock = new object();

        private bool IsNonMonotonic;
        private TimeSpan m_RangeStartTime;
        private TimeSpan m_RangeEndTime;
        private TimeSpan m_RangeMidTime;
        private TimeSpan m_RangeDuration;
        private TimeSpan m_AverageBlockDuration;
        private TimeSpan m_MonotonicDuration;
        private int m_Count;
        private long m_RangeBitRate;
        private double m_CapacityPercent;
        private bool m_IsMonotonic;
        private bool m_IsFull;
        private bool m_IsDisposed;

        // Fast Last Lookup.
        private long LastLookupTimeTicks = TimeSpan.MinValue.Ticks;
        private int LastLookupIndex = -1;

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
            PoolBlocks = new Queue<MediaBlock>(capacity + 1); // +1 to be safe and not degrade performance
            PlaybackBlocks = new List<MediaBlock>(capacity + 1); // +1 to be safe and not degrade performance

            // allocate the blocks
            for (var i = 0; i < capacity; i++)
                PoolBlocks.Enqueue(CreateBlock(mediaType));
        }

        #endregion

        #region Regular Properties

        /// <summary>
        /// Gets the media type of the block buffer.
        /// </summary>
        public MediaType MediaType { get; }

        /// <summary>
        /// Gets the maximum count of this buffer.
        /// </summary>
        public int Capacity { get; }

        /// <summary>
        /// Gets a value indicating whether this instance is disposed.
        /// </summary>
        public bool IsDisposed { get { lock (SyncLock) return m_IsDisposed; } }

        #endregion

        #region Collection Discrete Properties

        /// <summary>
        /// Gets the start time of the first block.
        /// </summary>
        public TimeSpan RangeStartTime { get { lock (SyncLock) return m_RangeStartTime; } }

        /// <summary>
        /// Gets the middle time of the range.
        /// </summary>
        public TimeSpan RangeMidTime { get { lock (SyncLock) return m_RangeMidTime; } }

        /// <summary>
        /// Gets the end time of the last block.
        /// </summary>
        public TimeSpan RangeEndTime { get { lock (SyncLock) return m_RangeEndTime; } }

        /// <summary>
        /// Gets the range of time between the first block and the end time of the last block.
        /// </summary>
        public TimeSpan RangeDuration { get { lock (SyncLock) return m_RangeDuration; } }

        /// <summary>
        /// Gets the compressed data bit rate from which media blocks were created.
        /// </summary>
        public long RangeBitRate { get { lock (SyncLock) return m_RangeBitRate; } }

        /// <summary>
        /// Gets the average duration of the currently available playback blocks.
        /// </summary>
        public TimeSpan AverageBlockDuration { get { lock (SyncLock) return m_AverageBlockDuration; } }

        /// <summary>
        /// Gets a value indicating whether all the durations of the blocks are equal.
        /// </summary>
        public bool IsMonotonic { get { lock (SyncLock) return m_IsMonotonic; } }

        /// <summary>
        /// Gets the duration of the blocks. If the blocks are not monotonic returns zero.
        /// </summary>
        public TimeSpan MonotonicDuration { get { lock (SyncLock) return m_MonotonicDuration; } }

        /// <summary>
        /// Gets the number of available playback blocks.
        /// </summary>
        public int Count { get { lock (SyncLock) return m_Count; } }

        /// <summary>
        /// Gets the usage percent from 0.0 to 1.0.
        /// </summary>
        public double CapacityPercent { get { lock (SyncLock) return m_CapacityPercent; } }

        /// <summary>
        /// Gets a value indicating whether the playback blocks are all allocated.
        /// </summary>
        public bool IsFull { get { lock (SyncLock) return m_IsFull; } }

        #endregion

        #region Indexer Properties

        /// <summary>
        /// Gets the <see cref="MediaBlock" /> at the specified index.
        /// </summary>
        /// <value>
        /// The <see cref="MediaBlock"/>.
        /// </value>
        /// <param name="index">The index.</param>
        /// <returns>The media block.</returns>
        public MediaBlock this[int index]
        {
            get { lock (SyncLock) return PlaybackBlocks[index]; }
        }

        /// <summary>
        /// Gets the <see cref="MediaBlock" /> at the specified timestamp.
        /// </summary>
        /// <value>
        /// The <see cref="MediaBlock"/>.
        /// </value>
        /// <param name="positionTicks">The position to lookup.</param>
        /// <returns>The media block.</returns>
        public MediaBlock this[long positionTicks]
        {
            get
            {
                lock (SyncLock)
                {
                    var index = IndexOf(positionTicks);
                    return index >= 0 ? PlaybackBlocks[index] : null;
                }
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Gets the percentage of the range for the given time position.
        /// A value of less than 0 means the position is behind (lagging).
        /// A value of more than 1 means the position is beyond the range).
        /// </summary>
        /// <param name="position">The position.</param>
        /// <returns>The percent of the range.</returns>
        public double GetRangePercent(TimeSpan position)
        {
            lock (SyncLock)
            {
                return RangeDuration.Ticks != 0 ?
                    Convert.ToDouble(position.Ticks - RangeStartTime.Ticks) / RangeDuration.Ticks : 0d;
            }
        }

        /// <summary>
        /// Gets the neighboring blocks in an atomic operation.
        /// The first item in the array is the previous block. The second is the next block. The third is the current block.
        /// </summary>
        /// <param name="current">The current block to get neighbors from.</param>
        /// <returns>The previous (if any) and next (if any) blocks.</returns>
        public MediaBlock[] Neighbors(MediaBlock current)
        {
            lock (SyncLock)
            {
                var result = new MediaBlock[3];
                if (current == null) return result;

                result[0] = current.Previous;
                result[1] = current.Next;
                result[2] = current;

                return result;
            }
        }

        /// <summary>
        /// Gets the neighboring blocks in an atomic operation.
        /// The first item in the array is the previous block. The second is the next block. The third is the current block.
        /// </summary>
        /// <param name="position">The current block position to get neighbors from.</param>
        /// <returns>The previous (if any) and next (if any) blocks.</returns>
        public MediaBlock[] Neighbors(TimeSpan position)
        {
            lock (SyncLock)
            {
                var current = this[position.Ticks];
                return Neighbors(current);
            }
        }

        /// <summary>
        /// Retrieves the block following the provided current block.
        /// If the argument is null and there are blocks, the first block is returned.
        /// </summary>
        /// <param name="current">The current block.</param>
        /// <returns>The next media block.</returns>
        public MediaBlock Next(MediaBlock current)
        {
            if (current == null) return null;

            lock (SyncLock)
                return current.Next;
        }

        /// <summary>
        /// Retrieves the next time-continuous block.
        /// </summary>
        /// <param name="current">The current.</param>
        /// <returns>The next time-continuous block.</returns>
        public MediaBlock ContinuousNext(MediaBlock current)
        {
            if (current == null) return null;
            lock (SyncLock)
            {
                // capture the next frame
                var next = current.Next;
                if (next == null) return null;

                // capture the spacing between the current and the next frame
                var discontinuity = TimeSpan.FromTicks(
                    next.StartTime.Ticks - current.EndTime.Ticks);

                // return null if we have a discontinuity of more than half of the duration
                var discontinuityThreshold = IsMonotonic ?
                    TimeSpan.FromTicks(current.Duration.Ticks / 2) :
                    TimeSpan.FromMilliseconds(1);

                return discontinuity.Ticks > discontinuityThreshold.Ticks ? null : next;
            }
        }

        /// <summary>
        /// Retrieves the block prior the provided current block.
        /// If the argument is null and there are blocks, the last block is returned.
        /// </summary>
        /// <param name="current">The current block.</param>
        /// <returns>The next media block.</returns>
        public MediaBlock Previous(MediaBlock current)
        {
            if (current == null) return null;

            lock (SyncLock)
                return current.Previous;
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
            lock (SyncLock)
            {
                if (PlaybackBlocks.Count == 0) return false;
                return renderTime.Ticks >= RangeStartTime.Ticks && renderTime.Ticks <= RangeEndTime.Ticks;
            }
        }

        /// <summary>
        /// Retrieves the index of the playback block corresponding to the specified
        /// render time. This uses very fast binary and linear search combinations.
        /// If there are no playback blocks it returns -1.
        /// If the render time is greater than the range end time, it returns the last playback block index.
        /// If the render time is less than the range start time, it returns the first playback block index.
        /// </summary>
        /// <param name="renderTimeTicks">The render time.</param>
        /// <returns>The media block's index.</returns>
        public int IndexOf(long renderTimeTicks)
        {
            lock (SyncLock)
            {
                if (LastLookupTimeTicks != TimeSpan.MinValue.Ticks && renderTimeTicks == LastLookupTimeTicks)
                    return LastLookupIndex;

                LastLookupTimeTicks = renderTimeTicks;
                LastLookupIndex = PlaybackBlocks.Count > 0 && renderTimeTicks <= PlaybackBlocks[0].StartTime.Ticks ? 0 :
                    PlaybackBlocks.StartIndexOf(LastLookupTimeTicks);

                return LastLookupIndex;
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            lock (SyncLock)
            {
                if (m_IsDisposed) return;
                m_IsDisposed = true;

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

                UpdateCollectionProperties();
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
        internal MediaBlock Add(MediaFrame source, MediaContainer container)
        {
            if (source == null) return null;

            lock (SyncLock)
            {
                try
                {
                    // Check if we already have a block at the given time
                    if (IsInRange(source.StartTime) && source.HasValidStartTime)
                    {
                        var repeatedBlock = PlaybackBlocks.FirstOrDefault(f => f.StartTime.Ticks == source.StartTime.Ticks);
                        if (repeatedBlock != null)
                        {
                            PlaybackBlocks.Remove(repeatedBlock);
                            PoolBlocks.Enqueue(repeatedBlock);
                        }
                    }

                    // if there are no available blocks, make room!
                    if (PoolBlocks.Count <= 0)
                    {
                        // Remove the first block from playback
                        var firstBlock = PlaybackBlocks[0];
                        PlaybackBlocks.RemoveAt(0);
                        PoolBlocks.Enqueue(firstBlock);
                    }

                    // Get a block reference from the pool and convert it!
                    var targetBlock = PoolBlocks.Dequeue();
                    var lastBlock = PlaybackBlocks.Count > 0 ? PlaybackBlocks[PlaybackBlocks.Count - 1] : null;

                    if (container.Convert(source, ref targetBlock, true, lastBlock) == false)
                    {
                        // return the converted block to the pool
                        PoolBlocks.Enqueue(targetBlock);
                        return null;
                    }

                    // Add the target block to the playback blocks
                    PlaybackBlocks.Add(targetBlock);

                    // return the new target block
                    return targetBlock;
                }
                finally
                {
                    // update collection-wide properties
                    UpdateCollectionProperties();
                }
            }
        }

        /// <summary>
        /// Clears all the playback blocks returning them to the
        /// block pool.
        /// </summary>
        internal void Clear()
        {
            lock (SyncLock)
            {
                // return all the blocks to the block pool
                foreach (var block in PlaybackBlocks)
                    PoolBlocks.Enqueue(block);

                PlaybackBlocks.Clear();
                UpdateCollectionProperties();
            }
        }

        /// <summary>
        /// Returns a formatted string with information about this buffer.
        /// </summary>
        /// <returns>The formatted string.</returns>
        internal string Debug()
        {
            lock (SyncLock)
            {
                return $"{MediaType,-12} - CAP: {Capacity,10} | FRE: {PoolBlocks.Count,7} | " +
                    $"USD: {PlaybackBlocks.Count,4} |  RNG: {RangeStartTime.Format(),8} to {RangeEndTime.Format().Trim()}";
            }
        }

        /// <summary>
        /// Gets the snap, discrete position of the corresponding block.
        /// If the position is greater than the end time of the block, the
        /// start time of the next available block is returned.
        /// </summary>
        /// <param name="position">The analog position.</param>
        /// <returns>A discrete frame position.</returns>
        internal TimeSpan? GetSnapPosition(TimeSpan position)
        {
            lock (SyncLock)
            {
                if (IsMonotonic == false)
                    return this[position.Ticks]?.StartTime;

                var block = this[position.Ticks];
                if (block == null)
                    return default;

                if (block.EndTime > position)
                    return block.StartTime;

                var nextBlock = Next(block);
                return nextBlock?.StartTime ?? block.StartTime;
            }
        }

        /// <summary>
        /// Block factory method.
        /// </summary>
        /// <param name="mediaType">Type of the media.</param>
        /// <exception cref="InvalidCastException">MediaBlock does not have a valid type.</exception>
        /// <returns>An instance of the block of the specified type.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static MediaBlock CreateBlock(MediaType mediaType)
        {
            if (mediaType == MediaType.Video) return new VideoBlock();
            if (mediaType == MediaType.Audio) return new AudioBlock();
            if (mediaType == MediaType.Subtitle) return new SubtitleBlock();

            throw new InvalidCastException($"No {nameof(MediaBlock)} constructor for {nameof(MediaType)} '{mediaType}'");
        }

        /// <summary>
        /// Updates the <see cref="PlaybackBlocks"/> collection properties.
        /// This method must be called whenever the collection is modified.
        /// The reason this exists is to avoid computing and iterating over these values every time they are read.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateCollectionProperties()
        {
            // Update the playback blocks sorting
            if (PlaybackBlocks.Count > 0)
            {
                var maxBlockIndex = PlaybackBlocks.Count - 1;

                // Perform the sorting and assignment of Previous and Next blocks
                PlaybackBlocks.Sort();
                PlaybackBlocks[0].Index = 0;
                PlaybackBlocks[0].Previous = null;
                PlaybackBlocks[0].Next = maxBlockIndex > 0 ? PlaybackBlocks[1] : null;

                for (var blockIndex = 1; blockIndex <= maxBlockIndex; blockIndex++)
                {
                    PlaybackBlocks[blockIndex].Index = blockIndex;
                    PlaybackBlocks[blockIndex].Previous = PlaybackBlocks[blockIndex - 1];
                    PlaybackBlocks[blockIndex].Next = blockIndex + 1 <= maxBlockIndex ? PlaybackBlocks[blockIndex + 1] : null;
                }
            }

            LastLookupIndex = -1;
            LastLookupTimeTicks = TimeSpan.MinValue.Ticks;

            m_Count = PlaybackBlocks.Count;
            m_RangeStartTime = PlaybackBlocks.Count == 0 ? TimeSpan.Zero : PlaybackBlocks[0].StartTime;
            m_RangeEndTime = PlaybackBlocks.Count == 0 ? TimeSpan.Zero : PlaybackBlocks[PlaybackBlocks.Count - 1].EndTime;
            m_RangeDuration = TimeSpan.FromTicks(RangeEndTime.Ticks - RangeStartTime.Ticks);
            m_RangeMidTime = TimeSpan.FromTicks(m_RangeStartTime.Ticks + (m_RangeDuration.Ticks / 2));
            m_CapacityPercent = Convert.ToDouble(m_Count) / Capacity;
            m_IsFull = m_Count >= Capacity;
            m_RangeBitRate = m_RangeDuration.TotalSeconds <= 0 || m_Count <= 1 ? 0 :
                Convert.ToInt64(8d * PlaybackBlocks.Sum(m => m.CompressedSize) / m_RangeDuration.TotalSeconds);

            // don't compute an average if we don't have blocks
            if (m_Count <= 0)
            {
                m_AverageBlockDuration = default;
                return;
            }

            // Don't compute if we've already determined that it's non-monotonic
            if (IsNonMonotonic)
            {
                m_AverageBlockDuration = TimeSpan.FromTicks(
                    Convert.ToInt64(PlaybackBlocks.Average(b => Convert.ToDouble(b.Duration.Ticks))));

                return;
            }

            // Monotonic verification
            var lastBlockDuration = PlaybackBlocks[m_Count - 1].Duration;
            IsNonMonotonic = PlaybackBlocks.Any(b => b.Duration.Ticks != lastBlockDuration.Ticks);
            m_IsMonotonic = !IsNonMonotonic;
            m_MonotonicDuration = m_IsMonotonic ? lastBlockDuration : default;
            m_AverageBlockDuration = m_IsMonotonic ? lastBlockDuration : TimeSpan.FromTicks(
                Convert.ToInt64(PlaybackBlocks.Average(b => Convert.ToDouble(b.Duration.Ticks))));
        }

        #endregion
    }
}
