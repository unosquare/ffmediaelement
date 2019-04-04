namespace Unosquare.FFME.Platform
{
    using Common;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;

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
                RetrieveProperties(typeof(IMediaEngineState), false).ToDictionary(p => p.Name, p => p));

            var enginePropertyNames = MediaEngineStateProperties.Keys.ToArray();

            MediaElementControllerProperties = new ReadOnlyDictionary<string, PropertyInfo>(
                RetrieveProperties(typeof(MediaElement), false)
                    .Where(p => enginePropertyNames.Contains(p.Name)
                        && p.CanRead && p.CanWrite)
                    .ToDictionary(p => p.Name, p => p));

            var controllerPropertyNames = MediaElementControllerProperties.Keys.ToArray();

            MediaElementInfoProperties = new ReadOnlyDictionary<string, PropertyInfo>(
                RetrieveProperties(typeof(MediaElement), false)
                    .Where(p => enginePropertyNames.Contains(p.Name)
                        && controllerPropertyNames.Contains(p.Name) == false
                        && p.CanRead && p.CanWrite == false)
                    .ToDictionary(p => p.Name, p => p));

            var allMediaElementPropertyNames = controllerPropertyNames.Union(MediaElementInfoProperties.Keys.ToArray()).ToArray();
            var missingMediaElementPropertyNames = MediaEngineStateProperties.Keys
                .Where(p => allMediaElementPropertyNames.Contains(p) == false)
                .ToArray();

            MissingPropertyMappings = new ReadOnlyCollection<string>(missingMediaElementPropertyNames);
        }

        private interface IPropertyWrapper
        {
            string Name { get; }

            Type Type { get; }

            bool CanRead { get; }

            bool CanWrite { get; }

            object GetValue();

            void SetValue(object value);
        }

        /// <summary>
        /// Contains the property names found in the Media Engine State type, but not found in the Media Element.
        /// </summary>
        public static ReadOnlyCollection<string> MissingPropertyMappings { get; }

        /// <summary>
        /// Gets the media element properties that can be read and written to.
        /// </summary>
        public static ReadOnlyDictionary<string, PropertyInfo> MediaElementControllerProperties { get; }

        /// <summary>
        /// Gets the media element properties that can only be read from.
        /// </summary>
        public static ReadOnlyDictionary<string, PropertyInfo> MediaElementInfoProperties { get; }

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
        public static string[] DetectInfoPropertyChanges(this MediaElement m, Dictionary<string, object> lastSnapshot)
        {
            var result = new List<string>(PropertyMaxCount);
            var currentState = new Dictionary<string, object>(PropertyMaxCount);
            m.SnapshotNotifications(currentState);
            var initLastSnapshot = currentState.Count != lastSnapshot.Count;

            foreach (var kvp in currentState)
            {
                if (!initLastSnapshot && Equals(lastSnapshot[kvp.Key], kvp.Value))
                    continue;

                result.Add(kvp.Key);
                lastSnapshot[kvp.Key] = kvp.Value;
            }

            return result.ToArray();
        }

        /// <summary>
        /// Detects which controller properties are out of sync with the Media Engine State properties.
        /// </summary>
        /// <param name="m">The m.</param>
        /// <returns>A dictionary of controller properties to synchronize along with the current engine values.</returns>
        public static Dictionary<PropertyInfo, object> DetectControllerPropertyChanges(this MediaElement m)
        {
            var result = new Dictionary<PropertyInfo, object>(PropertyMaxCount);
            object engineValue; // The current value of the media engine state property
            object propertyValue; // The current value of the controller property

            foreach (var targetProperty in MediaElementControllerProperties)
            {
                propertyValue = targetProperty.Value.GetValue(m);

                if (m.MediaCore != null)
                {
                    // extract the value coming from the media engine state
                    engineValue = MediaEngineStateProperties[targetProperty.Key].GetValue(m.MediaCore.State);

                    if (targetProperty.Value.PropertyType != MediaEngineStateProperties[targetProperty.Key].PropertyType)
                    {
                        engineValue = targetProperty.Value.PropertyType.IsEnum ?
                            Enum.ToObject(targetProperty.Value.PropertyType, engineValue) :
                            Convert.ChangeType(engineValue, targetProperty.Value.PropertyType, CultureInfo.InvariantCulture);
                    }
                }
                else
                {
                    engineValue = targetProperty.Value.PropertyType.IsValueType
                        ? Activator.CreateInstance(targetProperty.Value.PropertyType)
                        : null;
                }

                if (Equals(engineValue, propertyValue) == false)
                    result[targetProperty.Value] = engineValue;
            }

            return result;
        }

        /// <summary>
        /// Compiles the state into the target dictionary of property names and property values.
        /// </summary>
        /// <param name="m">The m.</param>
        /// <param name="target">The target.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SnapshotNotifications(this MediaElement m, Dictionary<string, object> target)
        {
            foreach (var p in MediaElementInfoProperties)
                target[p.Key] = p.Value.GetValue(m);
        }

        /// <summary>
        /// Retrieves the properties.
        /// </summary>
        /// <param name="t">The t.</param>
        /// <param name="declaredOnly">if set to <c>true</c> [declared only].</param>
        /// <returns>A list of properties.</returns>
        private static List<PropertyInfo> RetrieveProperties(Type t, bool declaredOnly)
        {
            var flags = BindingFlags.Instance | BindingFlags.Public;
            if (declaredOnly) flags |= BindingFlags.DeclaredOnly;

            var result = new List<PropertyInfo>(64);
            var propertyInfos = t.GetProperties(flags).ToArray();
            foreach (var propertyInfo in propertyInfos)
                result.Add(propertyInfo);

            return result;
        }

        private sealed class ObjectProxy<T>
            where T : class
        {
            public ObjectProxy(T instance)
            {
                Instance = instance;
                var properties = RetrieveProperties(typeof(T)).OrderBy(p => p.Name).ToArray();
                var proxies = new Dictionary<string, IPropertyWrapper>(properties.Length);
                foreach (var property in properties)
                {
                    var proxy = CreatePropertyWrapper(property, instance);
                    proxies[property.Name] = proxy;
                }

                Properties = new ReadOnlyDictionary<string, IPropertyWrapper>(proxies);
                PropertyNames = new ReadOnlyCollection<string>(Properties.Keys.ToArray());
                ReadOnlyPropertyNames = new ReadOnlyCollection<string>(Properties
                    .Where(kvp => kvp.Value.CanRead && !kvp.Value.CanWrite)
                    .Select(kvp => kvp.Key).OrderBy(s => s).ToArray());
                ReadWritePropertyNames = new ReadOnlyCollection<string>(Properties
                    .Where(kvp => kvp.Value.CanRead && kvp.Value.CanWrite)
                    .Select(kvp => kvp.Key).OrderBy(s => s).ToArray());
            }

            public T Instance { get; }

            public IReadOnlyDictionary<string, IPropertyWrapper> Properties { get; }

            public IReadOnlyList<string> PropertyNames { get; }

            public IReadOnlyList<string> ReadOnlyPropertyNames { get; }

            public IReadOnlyList<string> ReadWritePropertyNames { get; }

            public object this[string propertyName]
            {
                get => Properties[propertyName].GetValue();
                set => Properties[propertyName].SetValue(value);
            }

            private static List<PropertyInfo> RetrieveProperties(Type t)
            {
                var flags = BindingFlags.Instance | BindingFlags.Public;
                var result = new List<PropertyInfo>(64);
                var propertyInfos = t.GetProperties(flags).ToArray();
                foreach (var propertyInfo in propertyInfos)
                    result.Add(propertyInfo);

                return result;
            }

            private static IPropertyWrapper CreatePropertyWrapper(PropertyInfo propertyInfo, object instance)
            {
                var genericType = typeof(PropertyWrapper<,>)
                    .MakeGenericType(propertyInfo.DeclaringType, propertyInfo.PropertyType);
                return Activator.CreateInstance(genericType, propertyInfo, instance) as IPropertyWrapper;
            }
        }

#pragma warning disable CA1812
        private sealed class PropertyWrapper<TObject, TValue> : IPropertyWrapper
            where TObject : class
        {
            private readonly TObject Instance;
            private readonly Func<TObject, TValue> Getter;
            private readonly Action<TObject, TValue> Setter;

            public PropertyWrapper(PropertyInfo property, TObject instance)
            {
                Instance = instance;
                Name = property.Name;
                Type = property.PropertyType;

                var getterInfo = property.GetGetMethod(false);
                if (getterInfo != null)
                {
                    CanRead = true;
                    Getter = (Func<TObject, TValue>)Delegate.CreateDelegate(typeof(Func<TObject, TValue>), getterInfo);
                }

                var setterInfo = property.GetSetMethod(false);
                if (setterInfo != null)
                {
                    CanWrite = true;
                    Setter = (Action<TObject, TValue>)Delegate.CreateDelegate(typeof(Action<TObject, TValue>), setterInfo);
                }
            }

            public string Name { get; }

            public Type Type { get; }

            public bool CanRead { get; }

            public bool CanWrite { get; }

            object IPropertyWrapper.GetValue() =>
                Getter(Instance);

            void IPropertyWrapper.SetValue(object value) =>
                Setter(Instance, (TValue)value);
        }
    }
}
