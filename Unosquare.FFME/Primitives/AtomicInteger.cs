namespace Unosquare.FFME.Primitives
{
    using System;

    /// <summary>
    /// Represents an atomically readable or writable integer.
    /// </summary>
    internal sealed class AtomicInteger : AtomicTypeBase<int>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AtomicInteger"/> class.
        /// </summary>
        /// <param name="initialValue">if set to <c>true</c> [initial value].</param>
        public AtomicInteger(int initialValue)
            : base(Convert.ToInt64(initialValue))
        {
            // placeholder
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AtomicInteger"/> class.
        /// </summary>
        public AtomicInteger()
            : base(0)
        {
            // placeholder
        }

        /// <inheritdoc />
        protected override int FromLong(long backingValue) => Convert.ToInt32(backingValue);

        /// <inheritdoc />
        protected override long ToLong(int value) => Convert.ToInt64(value);
    }
}
