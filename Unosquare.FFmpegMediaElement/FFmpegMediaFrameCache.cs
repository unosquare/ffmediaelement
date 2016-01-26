namespace Unosquare.FFmpegMediaElement
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Represents a set of ordered media frames of a given type
    /// </summary>
    internal sealed class FFmpegMediaFrameCache
    {
        private readonly List<FFmpegMediaFrame> Frames = new List<FFmpegMediaFrame>();
        private readonly IComparer<FFmpegMediaFrame> FrameStartTimeComparer = Comparer<FFmpegMediaFrame>.Create((a, b) => a.StartTime.CompareTo(b.StartTime));

        #region Properties

        /// <summary>
        /// Gets the capacity in number frames.
        /// </summary>
        public int Capacity { get; private set; }

        /// <summary>
        /// Gets the current amount of frames.
        /// </summary>
        public int Count { get; private set; }

        /// <summary>
        /// Gets the index of the middle frame.
        /// </summary>
        public int MiddleIndex { get; private set; }

        /// <summary>
        /// Gets the PTS in seconds of the first frame
        /// </summary>
        public decimal FirstFrameTime { get; private set; }

        /// <summary>
        /// Gets the PTS in seconds of the last frame
        /// </summary>
        public decimal LastFrameTime { get; private set; }

        /// <summary>
        /// Gets the PTS in seconds of the middle frame
        /// </summary>
        public decimal MiddleFrameTime { get; private set; }

        /// <summary>
        /// Gets the best effort resentation time (PTS) of the first frame.
        /// </summary>
        public decimal StartTime { get; private set; }

        /// <summary>
        /// Gets the end time. Last Frame Time + Last Frame Duration
        /// </summary>
        public decimal EndTime { get; private set; }

        /// <summary>
        /// Gets the total duration from the first frame start time to 
        /// the last frame start time + its duration.
        /// </summary>
        public decimal Duration { get; private set; }

        /// <summary>
        /// Gets a value indicating whether this cache is full.
        /// </summary>
        public bool IsFull { get; private set; }

        /// <summary>
        /// Gets a value indicating whether this cache is empty.
        /// </summary>
        public bool IsEmpty { get; private set; }

        /// <summary>
        /// Gets the type of frames this cache is holding.
        /// </summary>
        public MediaFrameType Type { get; private set; }

        /// <summary>
        /// Gets the frame right at the middle of the collection
        /// </summary>
        public FFmpegMediaFrame MiddleFrame
        {
            get
            {
                if (this.Count == 0) return null;
                return this.Frames[this.MiddleIndex];
            }
        }

        /// <summary>
        /// Gets the first frame.
        /// </summary>
        public FFmpegMediaFrame FirstFrame
        {
            get
            {
                if (this.Count == 0) return null;
                return this.Frames[0];
            }
        }

        /// <summary>
        /// Gets the last frame.
        /// </summary>
        public FFmpegMediaFrame LastFrame
        {
            get
            {
                if (this.Count == 0) return null;
                return this.Frames[this.Frames.Count - 1];
            }
        }


        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="FFmpegMediaFrameCache"/> class.
        /// This copies all properties from an existing cache excluding frames of course.
        /// </summary>
        /// <param name="otherCache">The other cache.</param>
        public FFmpegMediaFrameCache(FFmpegMediaFrameCache otherCache)
        {
            var capacity = otherCache.Capacity;
            this.Type = otherCache.Type;
            this.Capacity = capacity;
            RecomputeProperties();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FFmpegMediaFrameCache"/> class.
        /// </summary>
        /// <param name="frameRate">The frame rate.</param>
        /// <param name="type">The type.</param>
        public FFmpegMediaFrameCache(decimal frameRate, MediaFrameType type)
        {
            if (frameRate <= 0) frameRate = 25;
            var capacity = (int)Math.Round(frameRate, 0) * 2;
            this.Type = type;
            this.Capacity = capacity;
            RecomputeProperties();
        }

        #endregion

        #region Methods

        /// <summary>
        /// Recomputes the properties.
        /// </summary>
        private void RecomputeProperties()
        {
            this.Count = Frames.Count;
            this.MiddleIndex = Frames.Count / 2;
            this.FirstFrameTime = Frames.Count > 0 ? Frames[0].StartTime : 0M;
            this.LastFrameTime = Frames.Count > 0 ? Frames[Frames.Count - 1].StartTime : 0M;
            this.StartTime = this.FirstFrameTime > 0M ? this.FirstFrameTime : 0M;
            this.EndTime = this.LastFrameTime > 0M ? this.LastFrameTime + LastFrame.Duration : 0M;
            this.Duration = this.EndTime - this.StartTime;
            this.IsFull = this.Count >= this.Capacity;
            this.IsEmpty = this.Count <= 0;
            this.MiddleFrameTime = Frames.Count > 0 ? this.MiddleFrame.StartTime : 0M;
        }

        /// <summary>
        /// Throws the invalid frame type exception.
        /// </summary>
        /// <param name="frameType">Type of the frame.</param>
        /// <exception cref="System.InvalidCastException"></exception>
        private void ThrowInvalidFrameTypeException(MediaFrameType frameType)
        {
            throw new InvalidCastException(string.Format("Provided a frame of type '{0}' but the cache is of type '{1}'", frameType, this.Type));
        }

        /// <summary>
        /// Replaces the internally-held frames with the specified new frames.
        /// </summary>
        /// <param name="newFrames">The new frames.</param>
        /// <exception cref="System.IndexOutOfRangeException">Buffer does not support the capacity of new elements</exception>
        public void Replace(FFmpegMediaFrameCache newFrames)
        {

            if (newFrames.Count > Capacity)
                throw new IndexOutOfRangeException("Buffer does not support the capacity of new elements");

            foreach (var frame in Frames)
                frame.EnqueueRelease();

            Frames.Clear();

            foreach (var frame in newFrames.Frames)
            {
                if (frame.Type != this.Type)
                    this.ThrowInvalidFrameTypeException(frame.Type);

                Frames.Add(frame);
            }

            RecomputeProperties();

        }

        /// <summary>
        /// Adds the specified frame at the right location.
        /// This method ensures the collection stays ordered
        /// </summary>
        /// <param name="frame">The frame.</param>
        /// <exception cref="System.IndexOutOfRangeException">Buffer is already at capacity.</exception>
        public void Add(FFmpegMediaFrame frame)
        {
            if (Frames.Count >= Capacity)
                throw new IndexOutOfRangeException("Buffer is already at capacity.");

            if (frame.Type != this.Type)
                this.ThrowInvalidFrameTypeException(frame.Type);

            Frames.Add(frame);

            if (frame.StartTime < LastFrameTime)
                Frames.Sort(FrameStartTimeComparer);

            RecomputeProperties();
        }

        /// <summary>
        /// Removes the first frame and releases it.
        /// </summary>
        public void RemoveFirst()
        {
            if (Frames.Count == 0) return;

            var index = 0;
            var frame = Frames[index];
            Frames.RemoveAt(index);
            frame.EnqueueRelease();

            RecomputeProperties();
        }

        /// <summary>
        /// Removes the last frame and releases it.
        /// </summary>
        public void RemoveLast()
        {
            if (Frames.Count == 0) return;

            var index = Frames.Count - 1;
            var frame = Frames[index];
            Frames.RemoveAt(index);
            frame.EnqueueRelease();

            RecomputeProperties();
        }

        /// <summary>
        /// Clears all the frames and releases them.
        /// </summary>
        public void Clear()
        {
            foreach (var frame in Frames)
                frame.EnqueueRelease();

            Frames.Clear();
            RecomputeProperties();
        }

        /// <summary>
        /// Highly optimized frame search function combining guess, binary and finally, linear search
        /// </summary>
        /// <param name="renderTime">The render time.</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private FFmpegMediaFrame SearchFrame(decimal renderTime)
        {
            var frameCount = Frames.Count;

            // fast condition checking
            if (frameCount <= 0) return null;
            if (frameCount == 1) return Frames[0];

            // variable setup
            var lowIndex = 0;
            var highIndex = frameCount - 1;
            var midIndex = 1 + lowIndex + (highIndex - lowIndex) / 2;

            // edge condition cheching
            if (Frames[lowIndex].StartTime >= renderTime) return Frames[lowIndex];
            if (Frames[highIndex].StartTime <= renderTime) return Frames[highIndex];

            // First guess, very low cost, very fast
            if (midIndex < highIndex && renderTime >= Frames[midIndex].StartTime && renderTime < Frames[midIndex + 1].StartTime)
                return Frames[midIndex];

            // binary search
            while (highIndex - lowIndex > 1)
            {
                midIndex = lowIndex + (highIndex - lowIndex) / 2;
                if (renderTime < Frames[midIndex].StartTime)
                    highIndex = midIndex;
                else
                    lowIndex = midIndex;
            }

            // linear search
            for (var i = highIndex; i >= lowIndex; i--)
            {
                if (Frames[i].StartTime <= renderTime)
                    return Frames[i];
            }

            return null;
        }

        /// <summary>
        /// Gets the frame at the given index.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <returns></returns>
        public FFmpegMediaFrame GetFrameAt(int index)
        {
            if (index < Frames.Count && index >= 0)
                return Frames[index];
            else
                return null;
        }

        /// <summary>
        /// Gets a frame at the given render time.
        /// </summary>
        /// <param name="renderTime">The render time.</param>
        /// <returns></returns>
        public FFmpegMediaFrame GetFrame(decimal renderTime, bool checkBounds)
        {
            if (Frames.Count <= 0) return null;
            if (checkBounds && renderTime < this.StartTime) return null;
            if (checkBounds && renderTime > this.EndTime) return null;

            if (renderTime < Frames[0].StartTime) return Frames[0];

            return SearchFrame(renderTime);
        }

        /// <summary>
        /// Gets a maximum of frameCount frames at the given starting renderTime
        /// </summary>
        /// <param name="renderTime">The render time.</param>
        /// <param name="frameCount">The frame count.</param>
        /// <returns></returns>
        public List<FFmpegMediaFrame> GetFrames(decimal renderTime, int frameCount)
        {
            var result = new List<FFmpegMediaFrame>();
            var startFrameIndex = IndexOf(renderTime, false);
            if (startFrameIndex < 0) return result;

            for (var i = startFrameIndex; i < Frames.Count; i++)
            {
                if (result.Count >= frameCount)
                    break;
                else
                    result.Add(Frames[i]);
            }

            return result;
        }

        /// <summary>
        /// Gets a maximum duration of frames at the given starting renderTime
        /// </summary>
        /// <param name="renderTime">The render time.</param>
        /// <param name="duration">The duration.</param>
        /// <returns></returns>
        public List<FFmpegMediaFrame> GetFrames(decimal renderTime, decimal duration)
        {
            var result = new List<FFmpegMediaFrame>();
            var startFrameIndex = IndexOf(renderTime, false);
            if (startFrameIndex < 0) return result;
            var endTime = renderTime + duration;

            for (var i = startFrameIndex; i < Frames.Count; i++)
            {
                if (Frames[i].StartTime <= endTime)
                    result.Add(Frames[i]);
                else
                    break;
            }

            return result;
        }

        /// <summary>
        /// Gets the index of the frame. Returns -1 for not found.
        /// </summary>
        /// <param name="frame">The frame.</param>
        /// <returns></returns>
        public int IndexOf(FFmpegMediaFrame frame)
        {
            if (frame == null)
                return -1;

            return Frames.IndexOf(frame);
        }

        /// <summary>
        /// Gets the index of the frame.
        /// Returns -1 if not found.
        /// </summary>
        /// <param name="renderTime">The render time.</param>
        /// <returns></returns>
        public int IndexOf(decimal renderTime, bool checkBounds)
        {
            var frame = GetFrame(renderTime, checkBounds);
            if (frame == null) return -1;
            return Frames.IndexOf(frame);
        }

        #endregion

    }
}
