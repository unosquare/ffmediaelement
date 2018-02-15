namespace Unosquare.FFME.Primitives
{
    using System.Threading;

    /// <summary>
    /// Fast, atomioc boolean combining interlocked to write value and volatile to read values
    /// Idea taken from Memory model and .NET operations in article:
    /// http://igoro.com/archive/volatile-keyword-in-c-memory-model-explained/
    /// </summary>
    public sealed class AtomicBoolean
    {
        private long m_Value = 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="AtomicBoolean"/> class.
        /// </summary>
        public AtomicBoolean()
        {
            Value = default(bool); // false
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AtomicBoolean"/> class.
        /// </summary>
        /// <param name="initialValue">if set to <c>true</c> [initial value].</param>
        public AtomicBoolean(bool initialValue)
        {
            Value = initialValue;
        }

        /// <summary>
        /// Gets the latest value written by any of the processors in the machine
        /// Setting
        /// </summary>
        public bool Value
        {
            get => Interlocked.Read(ref m_Value) != 0;
            set => Interlocked.Exchange(ref m_Value, value ? 1 : 0);
        }
    }
}
