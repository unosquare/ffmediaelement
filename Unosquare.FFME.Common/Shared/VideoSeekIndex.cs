namespace Unosquare.FFME.Shared
{
    using Decoding;
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
        private const string VersionPrefix = "v1";
        private static readonly string SectionHeaderText = $"{VersionPrefix}:{nameof(VideoSeekIndex)}.{nameof(Entries)}";
        private static readonly string SectionHeaderFields = $"{nameof(StreamIndex)},{nameof(SourceUrl)}";
        private static readonly string SectionDataText = $"{VersionPrefix}:{nameof(VideoSeekIndex)}.{nameof(Entries)}";
        private static readonly string SectionDataFields =
            $"{nameof(VideoSeekIndexEntry.StreamIndex)}" +
            $"{nameof(VideoSeekIndexEntry.StreamTimeBase)}Num" +
            $"{nameof(VideoSeekIndexEntry.StreamTimeBase)}Den" +
            $",{nameof(VideoSeekIndexEntry.StartTime)}" +
            $",{nameof(VideoSeekIndexEntry.PresentationTime)}" +
            $",{nameof(VideoSeekIndexEntry.DecodingTime)}";

        private readonly object SyncLock = new object();
        private readonly VideoSeekIndexEntryComparer LookupComparer = new VideoSeekIndexEntryComparer();

        /// <summary>
        /// Initializes a new instance of the <see cref="VideoSeekIndex"/> class.
        /// </summary>
        /// <param name="sourceUrl">The source URL.</param>
        /// <param name="streamIndex">Index of the stream.</param>
        public VideoSeekIndex(string sourceUrl, int streamIndex)
        {
            SourceUrl = sourceUrl;
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
        public string SourceUrl { get; internal set; }

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
                    if (state == 0 && SectionHeaderText.Equals(line))
                    {
                        state = 1;
                        continue;
                    }

                    if (state == 1 && SectionHeaderFields.Equals(line))
                    {
                        state = 2;
                        continue;
                    }

                    if (state == 2 && string.IsNullOrWhiteSpace(line) == false)
                    {
                        var parts = line.Split(separator, 2);
                        if (parts.Length >= 2)
                        {
                            if (int.TryParse(parts[0], out var index))
                                result.StreamIndex = index;

                            result.SourceUrl = parts[1].Trim(trimQuotes).Replace("\"\"", "\"");
                        }

                        state = 3;
                    }

                    if (state == 3 && SectionDataText.Equals(line))
                    {
                        state = 4;
                        continue;
                    }

                    if (state == 4 && SectionDataFields.Equals(line))
                    {
                        state = 5;
                        continue;
                    }

                    if (state == 5 && string.IsNullOrWhiteSpace(line) == false)
                    {
                        var parts = line.Split(separator);
                        if (parts.Length >= 6 &&
                            int.TryParse(parts[0], out var streamIndex) &&
                            int.TryParse(parts[1], out var timeBaseNum) &&
                            int.TryParse(parts[2], out var timeBaseDen) &&
                            long.TryParse(parts[3], out var startTimeTicks) &&
                            long.TryParse(parts[4], out var presentationTime) &&
                            long.TryParse(parts[5], out var decodingTime))
                        {
                            var entry = new VideoSeekIndexEntry(
                                streamIndex, timeBaseNum, timeBaseDen, startTimeTicks, presentationTime, decodingTime);

                            result.Entries.Add(entry);
                        }
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
                writer.WriteLine($"{StreamIndex},\"{SourceUrl?.Replace("\"", "\"\"")}\"");

                writer.WriteLine(SectionDataText);
                writer.WriteLine(SectionDataFields);
                foreach (var entry in Entries) writer.WriteLine(entry);
            }
        }

        /// <summary>
        /// Finds the closest seek entry that is on or prior to the seek target.
        /// </summary>
        /// <param name="seekTarget">The seek target.</param>
        /// <returns>The seek entry or null of not found</returns>
        public VideoSeekIndexEntry Find(TimeSpan seekTarget)
        {
            VideoSeekIndexEntry result = null;
            for (var i = 0; i < Entries.Count; i++)
            {
                // if we are past the seek target, we are done.
                if (Entries[i].StartTime.Ticks > seekTarget.Ticks)
                    break;

                result = Entries[i];
            }

            return result;
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
        /// A comparer for <see cref="VideoSeekIndexEntry"/>
        /// </summary>
        private class VideoSeekIndexEntryComparer : IComparer<VideoSeekIndexEntry>
        {
            /// <inheritdoc />
            public int Compare(VideoSeekIndexEntry x, VideoSeekIndexEntry y) =>
                x.StartTime.Ticks.CompareTo(y.StartTime.Ticks);
        }
    }
}
