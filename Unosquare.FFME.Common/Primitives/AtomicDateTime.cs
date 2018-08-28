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

        /// <inheritdoc />
        protected override DateTime FromLong(long backingValue) => new DateTime(backingValue);

        /// <inheritdoc />
        protected override long ToLong(DateTime value) => value.Ticks;
    }
}
