﻿namespace Unosquare.FFME.Primitives
{
    /// <summary>
    /// Fast, atomic boolean combining interlocked to write value and volatile to read values
    /// Idea taken from Memory model and .NET operations in article:
    /// http://igoro.com/archive/volatile-keyword-in-c-memory-model-explained/.
    /// </summary>
    internal sealed class AtomicBoolean : AtomicTypeBase<bool>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AtomicBoolean"/> class.
        /// </summary>
        /// <param name="initialValue">if set to <c>true</c> [initial value].</param>
        public AtomicBoolean(bool initialValue)
            : base(initialValue ? 1 : 0)
        {
            // placeholder
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AtomicBoolean"/> class.
        /// </summary>
        public AtomicBoolean()
            : base(0)
        {
            // placeholder
        }

        /// <inheritdoc />
        protected override bool FromLong(long backingValue) => backingValue != 0;

        /// <inheritdoc />
        protected override long ToLong(bool value) => value ? 1 : 0;
    }
}
