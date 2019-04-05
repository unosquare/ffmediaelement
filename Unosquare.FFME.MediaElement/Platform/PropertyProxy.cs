#pragma warning disable CA1812
namespace Unosquare.FFME.Platform
{
    using System;
    using System.Reflection;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Defines property metadata and delegate methods to get and set property values.
    /// </summary>
    /// <typeparam name="TClass">The type of the object.</typeparam>
    /// <typeparam name="TProperty">The type of the value.</typeparam>
    internal sealed class PropertyProxy<TClass, TProperty> : IPropertyProxy
        where TClass : class
    {
        private readonly Func<TClass, TProperty> Getter;
        private readonly Action<TClass, TProperty> Setter;

        /// <summary>
        /// Initializes a new instance of the <see cref="PropertyProxy{TClass, TProperty}"/> class.
        /// </summary>
        /// <param name="property">The property.</param>
        public PropertyProxy(PropertyInfo property)
        {
            Name = property.Name;
            Type = property.PropertyType;

            var getterInfo = property.GetGetMethod(false);
            if (getterInfo != null)
            {
                CanRead = true;
                Getter = (Func<TClass, TProperty>)Delegate.CreateDelegate(typeof(Func<TClass, TProperty>), getterInfo);
            }

            var setterInfo = property.GetSetMethod(false);
            if (setterInfo != null)
            {
                CanWrite = true;
                Setter = (Action<TClass, TProperty>)Delegate.CreateDelegate(typeof(Action<TClass, TProperty>), setterInfo);
            }
        }

        /// <inheritdoc />
        public string Name { get; }

        /// <inheritdoc />
        public Type Type { get; }

        /// <inheritdoc />
        public bool CanRead { get; }

        /// <inheritdoc />
        public bool CanWrite { get; }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        object IPropertyProxy.GetValue(object instance) =>
            Getter(instance as TClass);

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void IPropertyProxy.SetValue(object instance, object value) =>
            Setter(instance as TClass, (TProperty)value);
    }
}
#pragma warning restore CA1812