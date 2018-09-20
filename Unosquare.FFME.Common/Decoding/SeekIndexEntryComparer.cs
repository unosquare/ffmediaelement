namespace Unosquare.FFME.Decoding
{
    using System.Collections.Generic;

    /// <summary>
    /// A comparer for <see cref="SeekIndexEntry"/>
    /// </summary>
    internal class SeekIndexEntryComparer : IComparer<SeekIndexEntry>
    {
        /// <inheritdoc />
        public int Compare(SeekIndexEntry x, SeekIndexEntry y) =>
            x.StartTime.Ticks.CompareTo(y.StartTime.Ticks);
    }
}
