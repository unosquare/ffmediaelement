namespace Unosquare.FFME.Playlists
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// A set of extensions to allow for easy notification property implementations backed by
    /// attributes
    /// </summary>
    public static class IAttributeContainerExtensions
    {
        private static readonly Dictionary<Type, Dictionary<string, string>> PropertyMaps = new Dictionary<Type, Dictionary<string, string>>();

        /// <summary>
        /// Registers the property mapping.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="propertyName">Name of the property.</param>
        /// <param name="attributeName">Name of the attribute.</param>
        public static void RegisterPropertyMapping(this Type type, string propertyName, string attributeName)
        {
            if (PropertyMaps.ContainsKey(type) == false)
                PropertyMaps[type] = new Dictionary<string, string>();

            PropertyMaps[type][propertyName] = attributeName;
        }

        /// <summary>
        /// Determines whether [contains attribute for] [the specified property name].
        /// </summary>
        /// <param name="instance">The instance.</param>
        /// <param name="propertyName">Name of the property.</param>
        /// <returns>
        ///   <c>true</c> if [contains attribute for] [the specified property name]; otherwise, <c>false</c>.
        /// </returns>
        public static bool ContainsAttributeFor(this IAttributeContainer instance, [CallerMemberName] string propertyName = null)
        {
            var attributeName = instance.GetAttributeNameFor(propertyName);
            return instance.Attributes.ContainsKey(attributeName);
        }

        /// <summary>
        /// Gets the attribute name for the given property.
        /// </summary>
        /// <param name="instance">The instance.</param>
        /// <param name="propertyName">Name of the property.</param>
        /// <returns>
        /// The matching attribute name
        /// </returns>
        public static string GetAttributeNameFor(this IAttributeContainer instance, [CallerMemberName] string propertyName = null)
        {
            return PropertyMaps[instance.GetType()][propertyName];
        }

        /// <summary>
        /// Gets the attribute value.
        /// </summary>
        /// <param name="instance">The instance.</param>
        /// <param name="propertyName">Name of the property.</param>
        /// <returns>
        /// The string value. Null if key does not exist
        /// </returns>
        public static string GetAttributeValue(this IAttributeContainer instance, [CallerMemberName] string propertyName = null)
        {
            var attributeName = instance.GetAttributeNameFor(propertyName);
            if (instance.ContainsAttributeFor(propertyName) == false)
                return null;

            return instance.Attributes[attributeName];
        }

        /// <summary>
        /// Sets the attribute value.
        /// </summary>
        /// <param name="instance">The instance.</param>
        /// <param name="value">The value.</param>
        /// <param name="propertyName">Name of the property.</param>
        /// <returns>
        /// Sets the string value for the given property
        /// </returns>
        public static bool SetAttributeValue(this IAttributeContainer instance, string value, [CallerMemberName] string propertyName = null)
        {
            var attributeName = instance.GetAttributeNameFor(propertyName);
            var currentValue = instance.GetAttributeValue(propertyName);

            if (currentValue != null && value == null)
            {
                instance.Attributes.Remove(attributeName);
                instance.NotifyAttributeChangedFor(propertyName);
                return true;
            }

            if (Equals(currentValue, value))
                return false;

            instance.Attributes[attributeName] = value;
            instance.NotifyAttributeChangedFor(propertyName);
            return true;
        }
    }
}
