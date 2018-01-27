namespace Unosquare.FFME.Platform
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Windows;
    using Shared;

    /// <summary>
    /// A helper class to map and process synchronization
    /// between media engine state properties and the MediaElement control.
    /// </summary>
    internal static class PropertyMapper
    {
        public const int PropertyMaxCount = 64;

        /// <summary>
        /// Initializes static members of the <see cref="PropertyMapper"/> class.
        /// </summary>
        /// <exception cref="KeyNotFoundException">When a property exposed by the underlying MediaCore is not mapped.</exception>
        static PropertyMapper()
        {
            MediaEngineStateProperties = new ReadOnlyDictionary<string, PropertyInfo>(
                RetrieveProperties(typeof(MediaEngineState), false).ToDictionary((p) => p.Name, (p) => p));

            var enginePropertyNames = MediaEngineStateProperties.Keys.ToArray();

            MediaElementDependencyProperties = new ReadOnlyDictionary<string, DependencyProperty>(
                RetrieveDependencyProperties(typeof(MediaElement))
                    .Where(p => enginePropertyNames.Contains(p.Name))
                    .ToDictionary((p) => p.Name, (p) => p));

            var dependencyPropertyNames = MediaElementDependencyProperties.Keys.ToArray();

            MediaElementNotificationProperties = new ReadOnlyDictionary<string, PropertyInfo>(
                RetrieveProperties(typeof(MediaElement), false)
                    .Where(p => enginePropertyNames.Contains(p.Name)
                        && dependencyPropertyNames.Contains(p.Name) == false
                        && p.CanRead == true && p.CanWrite == false)
                    .ToDictionary((p) => p.Name, (p) => p));

            var allMediaElementPropertyNames = dependencyPropertyNames.Union(MediaElementNotificationProperties.Keys.ToArray()).ToArray();
            var missingMediaElementPropertyNames = MediaEngineStateProperties.Keys
                .Where(p => allMediaElementPropertyNames.Contains(p) == false)
                .ToArray();

            if (missingMediaElementPropertyNames.Length > 0)
            {
                throw new KeyNotFoundException($"{nameof(MediaElement)} is missing properties exposed by {nameof(MediaEngineState)}. " +
                    $"Missing properties are: {string.Join(",", missingMediaElementPropertyNames)}");
            }
        }

        /// <summary>
        /// Gets the media element dependency properties.
        /// </summary>
        public static ReadOnlyDictionary<string, DependencyProperty> MediaElementDependencyProperties { get; }

        /// <summary>
        /// Gets the media element notification properties.
        /// </summary>
        public static ReadOnlyDictionary<string, PropertyInfo> MediaElementNotificationProperties { get; }

        /// <summary>
        /// Gets the media engine state properties.
        /// </summary>
        public static ReadOnlyDictionary<string, PropertyInfo> MediaEngineStateProperties { get; }

        /// <summary>
        /// Detects the properties that have changed since the last snapshot.
        /// </summary>
        /// <param name="m">The m.</param>
        /// <param name="lastSnapshot">The last snapshot.</param>
        /// <returns>A list of property names that have changed.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string[] DetectNotificationPropertyChanges(this MediaElement m, Dictionary<string, object> lastSnapshot)
        {
            var result = new List<string>(PropertyMaxCount);
            var currentState = new Dictionary<string, object>(PropertyMaxCount);
            m.SnapshotNotifications(currentState);
            var initLastSnapshot = currentState.Count != lastSnapshot.Count;

            foreach (var kvp in currentState)
            {
                if (initLastSnapshot || Equals(lastSnapshot[kvp.Key], kvp.Value) == false)
                {
                    result.Add(kvp.Key);
                    lastSnapshot[kvp.Key] = kvp.Value;
                }
            }

            return result.ToArray();
        }

        /// <summary>
        /// Detects which dependendy properties are out of sync with the Media Engine State properties
        /// </summary>
        /// <param name="m">The m.</param>
        /// <returns>A dictionary of dependency properties to synchonize along with the engine values.</returns>
        public static Dictionary<DependencyProperty, object> DetectDependencyPropertyChanges(this MediaElement m)
        {
            var result = new Dictionary<DependencyProperty, object>(PropertyMaxCount);
            object engineValue = null; // The current value of the media engine state property
            object propertyValue = null; // The current value of the dependency property

            foreach (var targetProperty in MediaElementDependencyProperties)
            {
                engineValue = MediaEngineStateProperties[targetProperty.Key].GetValue(m.MediaCore.State);
                propertyValue = m.GetValue(targetProperty.Value);

                if (targetProperty.Value.PropertyType != MediaEngineStateProperties[targetProperty.Key].PropertyType)
                {
                    if (targetProperty.Value.PropertyType.IsEnum)
                        engineValue = Enum.ToObject(targetProperty.Value.PropertyType, engineValue);
                    else
                        engineValue = Convert.ChangeType(engineValue, targetProperty.Value.PropertyType);
                }

                if (Equals(engineValue, propertyValue) == false)
                    result[targetProperty.Value] = engineValue;
            }

            return result;
        }

        /// <summary>
        /// Compiles the state into the target dictionary of property names and property values
        /// </summary>
        /// <param name="m">The m.</param>
        /// <param name="target">The target.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SnapshotNotifications(this MediaElement m, Dictionary<string, object> target)
        {
            foreach (var p in MediaElementNotificationProperties)
                target[p.Key] = p.Value.GetValue(m);
        }

        /// <summary>
        /// Retrieves the dependency properties.
        /// </summary>
        /// <param name="t">The t.</param>
        /// <returns>A list of dependency properties</returns>
        private static List<DependencyProperty> RetrieveDependencyProperties(Type t)
        {
            var result = new List<DependencyProperty>();
            var fieldInfos = t.GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(x => x.FieldType == typeof(DependencyProperty))
                .ToArray();

            foreach (var fieldInfo in fieldInfos)
            {
                if (fieldInfo.GetValue(null) is DependencyProperty property)
                    result.Add(property);
            }

            return result;
        }

        /// <summary>
        /// Retrieves the properties.
        /// </summary>
        /// <param name="t">The t.</param>
        /// <param name="declaredOnly">if set to <c>true</c> [declared only].</param>
        /// <returns>A list of properties</returns>
        private static List<PropertyInfo> RetrieveProperties(Type t, bool declaredOnly)
        {
            var flags = BindingFlags.Instance | BindingFlags.Public;
            if (declaredOnly) flags |= BindingFlags.DeclaredOnly;

            var result = new List<PropertyInfo>();
            var propertyInfos = t.GetProperties(flags).ToArray();
            foreach (var propertyInfo in propertyInfos)
                result.Add(propertyInfo);

            return result;
        }
    }
}
