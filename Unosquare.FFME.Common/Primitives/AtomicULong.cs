namespace Unosquare.FFME.Primitives
{
    /// <summary>
    /// Provides an atomic type for an unsigned long.
    /// </summary>
    public sealed class AtomicULong : AtomicTypeBase<ulong>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AtomicULong"/> class.
        /// </summary>
        /// <param name="initialValue">The initial value.</param>
        public AtomicULong(ulong initialValue)
            : base(unchecked((long)initialValue))
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
        protected override ulong FromLong(long backingValue) =>
            unchecked((ulong)backingValue);

        /// <summary>
        /// Converts from the target type to a long value
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>
        /// The value converted to a long value
        /// </returns>
        protected override long ToLong(ulong value) =>
            unchecked((long)value);
    }
}
