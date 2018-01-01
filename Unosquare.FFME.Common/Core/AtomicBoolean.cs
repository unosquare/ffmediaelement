namespace Unosquare.FFME.Core
{
    using System.Threading;

    /// <summary>
    /// Fast, atomioc boolean combining interlocked to write value and volatile to read values
    /// Idea taken from Memory model and .NET operations in article:
    /// http://igoro.com/archive/volatile-keyword-in-c-memory-model-explained/
    /// </summary>
    internal sealed class AtomicBoolean
    {
        private int m_Value = 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="AtomicBoolean"/> class.
        /// </summary>
        public AtomicBoolean()
        {
            Value = default(bool); // false
        }

        /// <summary>
        /// Gets the latest value written by any of the processors in the machine
        /// Setting
        /// </summary>
        public bool Value
        {
            get => Volatile.Read(ref m_Value) != 0;
            set => Interlocked.Exchange(ref m_Value, value ? 1 : 0);
        }
    }
}
