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

        /// <inheritdoc />
        protected override ulong FromLong(long backingValue) =>
            unchecked((ulong)backingValue);

        /// <inheritdoc />
        protected override long ToLong(ulong value) =>
            unchecked((long)value);
    }
}
