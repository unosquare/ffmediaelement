namespace Unosquare.FFME.Primitives
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// A simple benchmarking class.
    /// </summary>
    public static class Benchmark
    {
        private static readonly object SyncLock = new object();
        private static readonly Dictionary<string, List<TimeSpan>> Measures = new Dictionary<string, List<TimeSpan>>();

        /// <summary>
        /// Starts measuring with the given identifier.
        /// </summary>
        /// <param name="identifier">The identifier.</param>
        /// <returns>A disposable object that when disposed, adds a benchmark result.</returns>
        public static IDisposable Start(string identifier)
        {
            return new BenchmarkUnit(identifier);
        }

        /// <summary>
        /// Outputs the benchmark statistics.
        /// </summary>
        /// <returns>A string containing human-readable statistics</returns>
        public static string Dump()
        {
            lock (SyncLock)
            {
                var builder = new StringBuilder();
                foreach (var kvp in Measures)
                {
                    builder.AppendLine($"BID: {kvp.Key,-30} | CNT: {kvp.Value.Count,6} | " +
                        $"AVG: {kvp.Value.Average(t => t.TotalMilliseconds),8:0.000} ms. | " +
                        $"MAX: {kvp.Value.Max(t => t.TotalMilliseconds),8:0.000} ms. | " +
                        $"MIN: {kvp.Value.Min(t => t.TotalMilliseconds),8:0.000} ms. | ");
                }

                return builder.ToString().TrimEnd();
            }
        }

        /// <summary>
        /// Adds the specified result to the given identifier.
        /// </summary>
        /// <param name="identifier">The identifier.</param>
        /// <param name="elapsed">The elapsed.</param>
        private static void Add(string identifier, TimeSpan elapsed)
        {
            lock (SyncLock)
            {
                if (Measures.ContainsKey(identifier) == false)
                    Measures[identifier] = new List<TimeSpan>(1024 * 1024);
            }

            // ReSharper disable once InconsistentlySynchronizedField
            Measures[identifier].Add(elapsed);
        }

        /// <summary>
        /// Represents a disposable benchmark unit.
        /// </summary>
        /// <seealso cref="IDisposable" />
        private sealed class BenchmarkUnit : IDisposable
        {
            private readonly string Identifier;
            private readonly AtomicBoolean IsDisposed = new AtomicBoolean(false); // To detect redundant calls
            private readonly Stopwatch Stopwatch = new Stopwatch();

            /// <summary>
            /// Initializes a new instance of the <see cref="BenchmarkUnit" /> class.
            /// </summary>
            /// <param name="identifier">The identifier.</param>
            public BenchmarkUnit(string identifier)
            {
                Identifier = identifier;
                Stopwatch.Start();
            }

            /// <inheritdoc />
            public void Dispose() => Dispose(true);

            /// <summary>
            /// Releases unmanaged and - optionally - managed resources.
            /// </summary>
            /// <param name="alsoManaged"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
            private void Dispose(bool alsoManaged)
            {
                if (IsDisposed == true) return;
                IsDisposed.Value = true;
                if (!alsoManaged) return;

                Add(Identifier, Stopwatch.Elapsed);
                Stopwatch.Stop();
            }
        }
    }
}
