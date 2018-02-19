namespace Unosquare.FFME.Primitives
{
    using System;

    /// <summary>
    /// Represents an atomically readabl;e or writable integer.
    /// </summary>
    public class AtomicInteger : AtomicTypeBase<int>
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

        /// <summary>
        /// COnverts froma long value to the target type.
        /// </summary>
        /// <param name="backingValue">The backing value.</param>
        /// <returns>
        /// The value converted form a long value
        /// </returns>
        protected override int FromLong(long backingValue)
        {
            return Convert.ToInt32(backingValue);
        }

        /// <summary>
        /// Converts from the target type to a long value
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>
        /// The value converted to a long value
        /// </returns>
        protected override long ToLong(int value)
        {
            return Convert.ToInt64(value);
        }
    }
}
