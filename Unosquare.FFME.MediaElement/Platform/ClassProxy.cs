namespace Unosquare.FFME.Platform
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Reflection;

    /// <summary>
    /// Represents a proxy for the properties exposed by the gicen type.
    /// </summary>
    /// <typeparam name="T">The type that this proxy represents.</typeparam>
    internal sealed class ClassProxy<T>
        where T : class
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ClassProxy{T}"/> class.
        /// </summary>
        public ClassProxy()
            : this((p) => true)
        {
            // placeholder
        }

        /// <summary>Initializes a new instance of the <see cref="ClassProxy{T}"/> class.</summary>
        /// <param name="matchClause">The match clause.</param>
        public ClassProxy(Func<PropertyInfo, bool> matchClause)
        {
            var properties = RetrieveProperties().OrderBy(p => p.Name).ToArray();
            var proxies = new Dictionary<string, IPropertyProxy>(properties.Length, StringComparer.Ordinal);
            foreach (var property in properties)
            {
                if (!matchClause(property))
                    continue;

                var proxy = CreatePropertyProxy(property);
                proxies[property.Name] = proxy;
            }

            Properties = new ReadOnlyDictionary<string, IPropertyProxy>(proxies);
            PropertyNames = new ReadOnlyCollection<string>(Properties.Keys.ToArray());
            ReadOnlyPropertyNames = new ReadOnlyCollection<string>(Properties
                .Where(kvp => kvp.Value.CanRead && !kvp.Value.CanWrite)
                .Select(kvp => kvp.Key).OrderBy(s => s).ToArray());
            ReadWritePropertyNames = new ReadOnlyCollection<string>(Properties
                .Where(kvp => kvp.Value.CanRead && kvp.Value.CanWrite)
                .Select(kvp => kvp.Key).OrderBy(s => s).ToArray());
        }

        /// <summary>
        /// Gets the property proxied for this class.
        /// </summary>
        public IReadOnlyDictionary<string, IPropertyProxy> Properties { get; }

        /// <summary>
        /// Gets the registered property names.
        /// </summary>
        public IReadOnlyList<string> PropertyNames { get; }

        /// <summary>
        /// Gets the read only property names.
        /// </summary>
        public IReadOnlyList<string> ReadOnlyPropertyNames { get; }

        /// <summary>
        /// Gets the property names that are both, readable and writable.
        /// </summary>
        public IReadOnlyList<string> ReadWritePropertyNames { get; }

        /// <summary>
        /// Gets or sets the value of the specified property, with the specified instance.
        /// </summary>
        /// <value>
        /// The value to set.
        /// </value>
        /// <param name="instance">The instance.</param>
        /// <param name="propertyName">Name of the property.</param>
        /// <returns>The value of the property</returns>
        public object this[T instance, string propertyName]
        {
            get => Properties[propertyName].GetValue(instance);
            set => Properties[propertyName].SetValue(instance, value);
        }

        /// <summary>
        /// Gets the <see cref="IPropertyProxy"/> for the specified property name.
        /// </summary>
        /// <value>
        /// The <see cref="IPropertyProxy"/>.
        /// </value>
        /// <param name="propertyName">Name of the property.</param>
        /// <returns>The property proxy</returns>
        public IPropertyProxy this[string propertyName] => Properties[propertyName];

        /// <summary>
        /// Retrieves the property information for the properties of the specified type.
        /// </summary>
        /// <returns>A collection of property information objects.</returns>
        public static IReadOnlyList<PropertyInfo> RetrieveProperties()
        {
            var flags = BindingFlags.Instance | BindingFlags.Public;
            var declaredOnly = typeof(T).IsInterface;
            if (declaredOnly) flags |= BindingFlags.DeclaredOnly;

            var result = new List<PropertyInfo>(64);
            var propertyInfos = typeof(T).GetProperties(flags).ToArray();
            foreach (var propertyInfo in propertyInfos)
                result.Add(propertyInfo);

            return result;
        }

        /// <summary>
        /// Creates an instance of the property proxy without the need to specify type argument explicitly.
        /// </summary>
        /// <param name="propertyInfo">The property information.</param>
        /// <returns>The property proxy containing metadata a nd getter and setter delegates.</returns>
        private static IPropertyProxy CreatePropertyProxy(PropertyInfo propertyInfo)
        {
            var genericType = typeof(PropertyProxy<,>)
                .MakeGenericType(propertyInfo.DeclaringType, propertyInfo.PropertyType);
            return Activator.CreateInstance(genericType, propertyInfo) as IPropertyProxy;
        }
    }
}
