namespace Unosquare.FFME.Diagnostics
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Contains benchmark summary data.
    /// </summary>
    internal sealed class BenchmarkResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BenchmarkResult" /> class.
        /// </summary>
        /// <param name="identifier">The identifier.</param>
        /// <param name="measures">The measures.</param>
        internal BenchmarkResult(string identifier, List<TimeSpan> measures)
        {
            Identifier = identifier;
            Count = measures.Count;
            Average = measures.Average(t => t.TotalMilliseconds);
            Min = measures.Min(t => t.TotalMilliseconds);
            Max = measures.Max(t => t.TotalMilliseconds);
        }

        /// <summary>
        /// Gets the benchmark identifier.
        /// </summary>
        public string Identifier { get; }

        /// <summary>
        /// Gets the measure count
        /// </summary>
        public int Count { get; }

        /// <summary>
        /// Gets the average time in milliseconds.
        /// </summary>
        public double Average { get; }

        /// <summary>
        /// Gets the minimum time in milliseconds.
        /// </summary>
        public double Min { get; }

        /// <summary>
        /// Gets the maximum time in milliseconds.
        /// </summary>
        public double Max { get; }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"BID: {Identifier,-30} | CNT: {Count,6} | " +
                $"AVG: {Average,8:0.000} ms. | " +
                $"MAX: {Max,8:0.000} ms. | " +
                $"MIN: {Min,8:0.000} ms. | ";
        }
    }
}
