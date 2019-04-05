namespace Unosquare.FFME.Platform
{
    using System;

    /// <summary>
    /// Defines methods and properties for a proxy related to a property
    /// containing getter and setter delegates along with property metadata.
    /// </summary>
    internal interface IPropertyProxy
    {
        /// <summary>
        /// Gets the property name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the property type.
        /// </summary>
        Type Type { get; }

        /// <summary>
        /// Gets a value indicating whether this property has a getter.
        /// </summary>
        bool CanRead { get; }

        /// <summary>
        /// Gets a value indicating whether this property has a setter.
        /// </summary>
        bool CanWrite { get; }

        /// <summary>
        /// Calls the getter method for the property on the specified instance.
        /// </summary>
        /// <param name="instance">The instance.</param>
        /// <returns>The value of the property.</returns>
        object GetValue(object instance);

        /// <summary>
        /// Calls the setter method for the property on the specified instance.
        /// </summary>
        /// <param name="instance">The instance.</param>
        /// <param name="value">The value.</param>
        void SetValue(object instance, object value);
    }
}
