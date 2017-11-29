﻿namespace Unosquare.FFME.Core
{
    using System.Threading;

    /// <summary>
    /// Fast, atomioc long combining interlocked to write value and volatile to read values
    /// Idea taken from Memory model and .NET operations in article:
    /// http://igoro.com/archive/volatile-keyword-in-c-memory-model-explained/
    /// </summary>
    internal sealed class AtomicLong
    {
        private long m_Value = 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="AtomicLong"/> class.
        /// </summary>
        public AtomicLong()
        {
            Value = 0;
        }

        /// <summary>
        /// Gets or sets the latest value written by any of the processors in the machine
        /// </summary>
        public long Value
        {
            get { return Volatile.Read(ref m_Value); }
            set { Interlocked.Exchange(ref m_Value, value); }
        }
    }
}
