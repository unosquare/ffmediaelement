namespace Unosquare.FFME.Media
{
    using Container;
    using FFmpeg.AutoGen;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;

    /// <summary>
    /// Provides a collection of <see cref="VideoSeekIndexEntry"/>.
    /// Seek entries are contain specific positions where key frames (or I frames) are located
    /// within a seekable stream.
    /// </summary>
    public sealed class VideoSeekIndex
    {
        private const string VersionPrefix = "FILE-SECTION-V01";
        private static readonly string SectionHeaderText = $"{VersionPrefix}:{nameof(VideoSeekIndex)}.{nameof(Entries)}";
        private static readonly string SectionHeaderFields = $"{nameof(StreamIndex)},{nameof(MediaSource)}";
        private static readonly string SectionDataText = $"{VersionPrefix}:{nameof(VideoSeekIndex)}.{nameof(Entries)}";
        private static readonly string SectionDataFields =
            $"{nameof(VideoSeekIndexEntry.StreamIndex)}" +
            $",{nameof(VideoSeekIndexEntry.StreamTimeBase)}Num" +
            $",{nameof(VideoSeekIndexEntry.StreamTimeBase)}Den" +
            $",{nameof(VideoSeekIndexEntry.StartTime)}" +
            $",{nameof(VideoSeekIndexEntry.PresentationTime)}" +
            $",{nameof(VideoSeekIndexEntry.DecodingTime)}";

        private readonly VideoSeekIndexEntryComparer LookupComparer = new VideoSeekIndexEntryComparer();

        /// <summary>
        /// Initializes a new instance of the <see cref="VideoSeekIndex"/> class.
        /// </summary>
        /// <param name="mediaSource">The source URL.</param>
        /// <param name="streamIndex">Index of the stream.</param>
        public VideoSeekIndex(string mediaSource, int streamIndex)
        {
            MediaSource = mediaSource;
            StreamIndex = streamIndex;
        }

        /// <summary>
        /// Provides access to the seek entries.
        /// </summary>
        public List<VideoSeekIndexEntry> Entries { get; } = new List<VideoSeekIndexEntry>(2048);

        /// <summary>
        /// Gets the stream index this seeking index belongs to.
        /// </summary>
        public int StreamIndex { get; internal set; }

        /// <summary>
        /// Gets the source URL this seeking index belongs to.
        /// </summary>
        public string MediaSource { get; internal set; }

        /// <summary>
        /// Loads the specified stream in the CSV-like UTF8 format it was written by the <see cref="Save(Stream)"/> method.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <returns>The loaded index from the specified stream.</returns>
        public static VideoSeekIndex Load(Stream stream)
        {
            var separator = new[] { ',' };
            var trimQuotes = new[] { '"' };
            var result = new VideoSeekIndex(null, -1);

            using (var reader = new StreamReader(stream, Encoding.UTF8, true))
            {
                var state = 0;

                while (reader.EndOfStream == false)
                {
                    var line = reader.ReadLine()?.Trim() ?? string.Empty;
                    if (state == 0 && line == SectionHeaderText)
                    {
                        state = 1;
                        continue;
                    }

                    if (state == 1 && line == SectionHeaderFields)
                    {
                        state = 2;
                        continue;
                    }

                    if (state == 2 && !string.IsNullOrWhiteSpace(line))
                    {
                        var parts = line.Split(separator, 2);
                        if (parts.Length >= 2)
                        {
                            if (int.TryParse(parts[0], out var index))
                                result.StreamIndex = index;

                            result.MediaSource = parts[1]
                                .Trim(trimQuotes)
                                .ReplaceOrdinal("\"\"", "\"");
                        }

                        state = 3;
                    }

                    if (state == 3 && line == SectionDataText)
                    {
                        state = 4;
                        continue;
                    }

                    if (state == 4 && line == SectionDataFields)
                    {
                        state = 5;
                        continue;
                    }

                    if (state == 5 && !string.IsNullOrWhiteSpace(line))
                    {
                        if (VideoSeekIndexEntry.FromCsvString(line) is VideoSeekIndexEntry entry)
                            result.Entries.Add(entry);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Writes the index data to the specified stream in CSV-like UTF8 text format.
        /// </summary>
        /// <param name="stream">The stream to write data to.</param>
        public void Save(Stream stream)
        {
            using (var writer = new StreamWriter(stream, Encoding.UTF8, 4096, true))
            {
                writer.WriteLine(SectionHeaderText);
                writer.WriteLine(SectionHeaderFields);
                writer.WriteLine($"{StreamIndex},\"{MediaSource?.ReplaceOrdinal("\"", "\"\"")}\"");

                writer.WriteLine(SectionDataText);
                writer.WriteLine(SectionDataFields);
                foreach (var entry in Entries) writer.WriteLine(entry.ToCsvString());
            }
        }

        /// <summary>
        /// Finds the closest seek entry that is on or prior to the seek target.
        /// </summary>
        /// <param name="seekTarget">The seek target.</param>
        /// <returns>The seek entry or null of not found.</returns>
        public VideoSeekIndexEntry Find(TimeSpan seekTarget)
        {
            var index = Entries.StartIndexOf(seekTarget);
            if (index < 0) return null;
            return Entries[index];
        }

        /// <summary>
        /// Tries to add an entry created from the frame.
        /// </summary>
        /// <param name="managedFrame">The managed frame.</param>
        /// <returns>
        /// True if the index entry was created from the frame.
        /// False if the frame is of wrong picture type or if it already existed.
        /// </returns>
        internal bool TryAdd(VideoFrame managedFrame)
        {
            // Update the Seek index
            if (managedFrame.PictureType != AVPictureType.AV_PICTURE_TYPE_I)
                return false;

            // Create the seek entry
            var seekEntry = new VideoSeekIndexEntry(managedFrame);

            // Check if the entry already exists.
            if (Entries.BinarySearch(seekEntry, LookupComparer) >= 0)
                return false;

            // Add the seek entry and ensure they are sorted.
            Entries.Add(seekEntry);
            Entries.Sort(LookupComparer);
            return true;
        }

        /// <summary>
        /// Adds the monotonic entries up to a stream duration.
        /// </summary>
        /// <param name="streamDuration">Duration of the stream.</param>
        internal void AddMonotonicEntries(TimeSpan streamDuration)
        {
            if (Entries.Count < 2) return;

            while (true)
            {
                var lastEntry = Entries[Entries.Count - 1];
                var prevEntry = Entries[Entries.Count - 2];

                var presentationTime = lastEntry.PresentationTime == ffmpeg.AV_NOPTS_VALUE ?
                    ffmpeg.AV_NOPTS_VALUE :
                    lastEntry.PresentationTime + (lastEntry.PresentationTime - prevEntry.PresentationTime);

                var decodingTime = lastEntry.DecodingTime == ffmpeg.AV_NOPTS_VALUE ?
                    ffmpeg.AV_NOPTS_VALUE :
                    lastEntry.DecodingTime + (lastEntry.DecodingTime - prevEntry.DecodingTime);

                var startTimeTicks = lastEntry.StartTime.Ticks + (lastEntry.StartTime.Ticks - prevEntry.StartTime.Ticks);

                var entry = new VideoSeekIndexEntry(
                    lastEntry.StreamIndex,
                    lastEntry.StreamTimeBase.num,
                    lastEntry.StreamTimeBase.den,
                    startTimeTicks,
                    presentationTime,
                    decodingTime);

                if (entry.StartTime.Ticks > streamDuration.Ticks)
                    return;

                Entries.Add(entry);
            }
        }

        /// <summary>
        /// Gets the monotonic presentation distance units that separate the last entries in the index.
        /// Returns -1 if there are less than 2 entries or if the entries are not monotonic.
        /// </summary>
        /// <returns>-1 if the entries are not monotonic.</returns>
        internal long ComputeMonotonicDistance()
        {
            if (Entries.Count < 2) return -1L;
            var lastDistance = -1L;
            var currentDistance = -1L;

            for (var i = Entries.Count - 1; i > 0; i--)
            {
                currentDistance = Entries[i].PresentationTime - Entries[i - 1].PresentationTime;
                if (lastDistance != -1L && lastDistance != currentDistance)
                    return -1L;

                lastDistance = currentDistance;
            }

            return currentDistance;
        }

        /// <summary>
        /// A comparer for <see cref="VideoSeekIndexEntry"/>.
        /// </summary>
        private class VideoSeekIndexEntryComparer : IComparer<VideoSeekIndexEntry>
        {
            /// <inheritdoc />
            public int Compare(VideoSeekIndexEntry x, VideoSeekIndexEntry y) =>
                x.StartTime.Ticks.CompareTo(y.StartTime.Ticks);
        }
    }
}
