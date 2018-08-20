namespace Unosquare.FFME.Primitives
{
    using System;
    using System.Threading;

    /// <summary>
    /// Provides a generic implementation of an Atomic (interlocked) type
    /// </summary>
    /// <typeparam name="T">The structure type backed by a 64-bit value</typeparam>
    public abstract class AtomicTypeBase<T> : IComparable, IComparable<T>, IComparable<AtomicTypeBase<T>>, IEquatable<T>, IEquatable<AtomicTypeBase<T>>
        where T : struct, IComparable, IComparable<T>, IEquatable<T>
    {
        private long backingValue;

        /// <summary>
        /// Initializes a new instance of the <see cref="AtomicTypeBase{T}"/> class.
        /// </summary>
        /// <param name="initialValue">The initial value.</param>
        protected AtomicTypeBase(long initialValue)
        {
            BackingValue = initialValue;
        }

        /// <summary>
        /// Gets or sets the value.
        /// </summary>
        public T Value
        {
            get => FromLong(BackingValue);
            set => BackingValue = ToLong(value);
        }

        /// <summary>
        /// Gets or sets the backing value.
        /// </summary>
        protected long BackingValue
        {
            get => Interlocked.Read(ref backingValue);
            set => Interlocked.Exchange(ref backingValue, value);
        }

        /// <summary>
        /// Implements the operator ==.
        /// </summary>
        /// <param name="a">a.</param>
        /// <param name="b">The b.</param>
        /// <returns>
        /// The result of the operator.
        /// </returns>
        public static bool operator ==(AtomicTypeBase<T> a, T b)
        {
            return a.Equals(b);
        }

        /// <summary>
        /// Implements the operator !=.
        /// </summary>
        /// <param name="a">a.</param>
        /// <param name="b">The b.</param>
        /// <returns>
        /// The result of the operator.
        /// </returns>
        public static bool operator !=(AtomicTypeBase<T> a, T b)
        {
            return a.Equals(b) == false;
        }

        /// <summary>
        /// Implements the operator &gt;.
        /// </summary>
        /// <param name="a">a.</param>
        /// <param name="b">The b.</param>
        /// <returns>
        /// The result of the operator.
        /// </returns>
        public static bool operator >(AtomicTypeBase<T> a, T b)
        {
            return a.CompareTo(b) > 0;
        }

        /// <summary>
        /// Implements the operator &lt;.
        /// </summary>
        /// <param name="a">a.</param>
        /// <param name="b">The b.</param>
        /// <returns>
        /// The result of the operator.
        /// </returns>
        public static bool operator <(AtomicTypeBase<T> a, T b)
        {
            return a.CompareTo(b) < 0;
        }

        /// <summary>
        /// Implements the operator &gt;=.
        /// </summary>
        /// <param name="a">a.</param>
        /// <param name="b">The b.</param>
        /// <returns>
        /// The result of the operator.
        /// </returns>
        public static bool operator >=(AtomicTypeBase<T> a, T b)
        {
            return a.CompareTo(b) >= 0;
        }

        /// <summary>
        /// Implements the operator &lt;=.
        /// </summary>
        /// <param name="a">a.</param>
        /// <param name="b">The b.</param>
        /// <returns>
        /// The result of the operator.
        /// </returns>
        public static bool operator <=(AtomicTypeBase<T> a, T b)
        {
            return a.CompareTo(b) <= 0;
        }

        /// <summary>
        /// Implements the operator ++.
        /// </summary>
        /// <param name="instance">The instance.</param>
        /// <returns>
        /// The result of the operator.
        /// </returns>
        public static AtomicTypeBase<T> operator ++(AtomicTypeBase<T> instance)
        {
            Interlocked.Increment(ref instance.backingValue);
            return instance;
        }

        /// <summary>
        /// Implements the operator --.
        /// </summary>
        /// <param name="instance">The instance.</param>
        /// <returns>
        /// The result of the operator.
        /// </returns>
        public static AtomicTypeBase<T> operator --(AtomicTypeBase<T> instance)
        {
            Interlocked.Decrement(ref instance.backingValue);
            return instance;
        }

        /// <summary>
        /// Implements the operator -&lt;.
        /// </summary>
        /// <param name="instance">The instance.</param>
        /// <param name="operand">The operand.</param>
        /// <returns>
        /// The result of the operator.
        /// </returns>
        public static AtomicTypeBase<T> operator +(AtomicTypeBase<T> instance, long operand)
        {
            instance.BackingValue = instance.BackingValue + operand;
            return instance;
        }

        /// <summary>
        /// Implements the operator -.
        /// </summary>
        /// <param name="instance">The instance.</param>
        /// <param name="operand">The operand.</param>
        /// <returns>
        /// The result of the operator.
        /// </returns>
        public static AtomicTypeBase<T> operator -(AtomicTypeBase<T> instance, long operand)
        {
            instance.BackingValue = instance.BackingValue - operand;
            return instance;
        }

        /// <summary>
        /// Compares the value to the other instance
        /// </summary>
        /// <param name="other">The other instance.</param>
        /// <returns>0 if equal, 1 if this instance is greater, -1 if this instance is less than</returns>
        /// <exception cref="ArgumentException">When types are incompatible</exception>
        public int CompareTo(object other)
        {
            if (other == null)
                return 1;

            if (other is AtomicTypeBase<T>)
                return BackingValue.CompareTo((other as AtomicTypeBase<T>).BackingValue);

            if (other is T)
                return Value.CompareTo((T)other);

            throw new ArgumentException($"Incompatible comparison types");
        }

        /// <summary>
        /// Compares the value to the other instance
        /// </summary>
        /// <param name="other">The other instance.</param>
        /// <returns>0 if equal, 1 if this instance is greater, -1 if this instance is less than</returns>
        public int CompareTo(T other) => Value.CompareTo(other);

        /// <summary>
        /// Compares the value to the other instance
        /// </summary>
        /// <param name="other">The other instance.</param>
        /// <returns>0 if equal, 1 if this instance is greater, -1 if this instance is less than</returns>
        public int CompareTo(AtomicTypeBase<T> other) =>
            BackingValue.CompareTo(other?.BackingValue ?? default);

        /// <summary>
        /// Determines whether the specified <see cref="object" />, is equal to this instance.
        /// </summary>
        /// <param name="other">The <see cref="object" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="object" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        public override bool Equals(object other)
        {
            if (other is AtomicTypeBase<T>) return Equals(other as AtomicTypeBase<T>);
            if (other is T) return Equals((T)other);

            return false;
        }

        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <returns>
        /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table.
        /// </returns>
        public override int GetHashCode() => BackingValue.GetHashCode();

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <param name="other">An object to compare with this object.</param>
        /// <returns>
        /// true if the current object is equal to the <paramref name="other">other</paramref> parameter; otherwise, false.
        /// </returns>
        public bool Equals(AtomicTypeBase<T> other) =>
            BackingValue == (other?.BackingValue ?? default);

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <param name="other">An object to compare with this object.</param>
        /// <returns>
        /// true if the current object is equal to the <paramref name="other">other</paramref> parameter; otherwise, false.
        /// </returns>
        public bool Equals(T other) => Equals(Value, other);

        /// <summary>
        /// Converts froma long value to the target type.
        /// </summary>
        /// <param name="backingValue">The backing value.</param>
        /// <returns>The value converted form a long value</returns>
        protected abstract T FromLong(long backingValue);

        /// <summary>
        /// Converts from the target type to a long value
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The value converted to a long value</returns>
        protected abstract long ToLong(T value);
    }
}
