namespace Unosquare.FFME.Primitives
{
    using System;

    /// <summary>
    /// Defines an atomic DateTime
    /// </summary>
    public sealed class AtomicDateTime : AtomicTypeBase<DateTime>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AtomicDateTime"/> class.
        /// </summary>
        /// <param name="initialValue">The initial value.</param>
        public AtomicDateTime(DateTime initialValue)
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
        protected override DateTime FromLong(long backingValue) => new DateTime(backingValue);

        /// <summary>
        /// Converts from the target type to a long value
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>
        /// The value converted to a long value
        /// </returns>
        protected override long ToLong(DateTime value) => value.Ticks;
    }
}
