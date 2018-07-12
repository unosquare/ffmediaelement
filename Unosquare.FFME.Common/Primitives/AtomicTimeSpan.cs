namespace Unosquare.FFME.Primitives
{
    using System;

    /// <summary>
    /// Represents an atomic TimeSpan type
    /// </summary>
    public sealed class AtomicTimeSpan : AtomicTypeBase<TimeSpan>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AtomicTimeSpan" /> class.
        /// </summary>
        /// <param name="initialValue">The initial value.</param>
        public AtomicTimeSpan(TimeSpan initialValue)
                    : base(initialValue.Ticks)
        {
            // placeholder
        }

        /// <summary>
        /// Converts froma long value to the target type.
        /// </summary>
        /// <param name="backingValue">The backing value.</param>
        /// <returns>
        /// The value converted form a long value
        /// </returns>
        protected override TimeSpan FromLong(long backingValue) => TimeSpan.FromTicks(backingValue);

        /// <summary>
        /// Converts from the target type to a long value
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>
        /// The value converted to a long value
        /// </returns>
        protected override long ToLong(TimeSpan value) => value.Ticks;
    }
}
