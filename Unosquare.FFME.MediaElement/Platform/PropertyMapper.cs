namespace Unosquare.FFME.Platform
{
    using Common;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// A helper class to map and process synchronization
    /// between media engine state properties and the MediaElement control.
    /// </summary>
    internal static class PropertyMapper
    {
        private static readonly ClassProxy<IMediaEngineState> MediaEngineProxy = new ClassProxy<IMediaEngineState>();
        private static readonly ClassProxy<MediaElement> MediaElementProxy = new ClassProxy<MediaElement>((p) =>
                MediaEngineProxy.PropertyNames.Contains(p.Name));

        /// <summary>
        /// Initializes static members of the <see cref="PropertyMapper"/> class.
        /// </summary>
        /// <exception cref="KeyNotFoundException">When a property exposed by the underlying MediaCore is not mapped.</exception>
        static PropertyMapper()
        {
            MissingPropertyMappings = MediaEngineProxy.PropertyNames
                .Where(p => !MediaElementProxy.PropertyNames.Contains(p))
                .ToArray();
        }

        /// <summary>
        /// Contains the property names found in the Media Engine State type, but not found in the Media Element.
        /// </summary>
        public static IReadOnlyList<string> MissingPropertyMappings { get; }

        /// <summary>
        /// Sets the value for the specified property name on the given instance.
        /// </summary>
        /// <param name="m">The Media Element instance.</param>
        /// <param name="propertyName">Name of the property.</param>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetValue(MediaElement m, string propertyName, object value) =>
            MediaElementProxy[m, propertyName] = value;

        /// <summary>
        /// Detects the properties that have changed since the last snapshot.
        /// </summary>
        /// <param name="m">The m.</param>
        /// <param name="lastSnapshot">The last snapshot.</param>
        /// <returns>A list of property names that have changed.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string[] DetectReadOnlyChanges(this MediaElement m, IDictionary<string, object> lastSnapshot)
        {
            var currentState = m.SnapshotReadOnlyState();
            var result = new List<string>(currentState.Count);

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
        /// Detects which properties (that are both, readable and writable) are out of sync with the Media Engine State properties.
        /// </summary>
        /// <param name="m">The MediaElement.</param>
        /// <returns>A dictionary of controller properties to synchronize along with the current engine values.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Dictionary<string, object> DetectReadWriteChanges(this MediaElement m)
        {
            var properties = MediaElementProxy.ReadWritePropertyNames;
            var result = new Dictionary<string, object>(properties.Count);

            object engineValue;
            object elementValue;

            foreach (var property in properties)
            {
                var engineProperty = MediaEngineProxy[property];
                var elementProperty = MediaElementProxy[property];

                elementValue = elementProperty.GetValue(m);
                var mediaCore = m.MediaCore;

                if (mediaCore != null)
                {
                    // extract the value coming from the media engine state
                    engineValue = engineProperty.GetValue(mediaCore.State);

                    if (engineProperty.Type != elementProperty.Type)
                    {
                        engineValue = engineProperty.Type.IsEnum ?
                            Enum.ToObject(engineProperty.Type, engineValue) :
                            Convert.ChangeType(engineValue, elementProperty.Type, CultureInfo.InvariantCulture);
                    }
                }
                else
                {
                    engineValue = engineProperty.Type.IsValueType
                        ? Activator.CreateInstance(engineProperty.Type)
                        : null;
                }

                if (!Equals(engineValue, elementValue))
                    result[property] = engineValue;
            }

            return result;
        }

        /// <summary>
        /// Snapshots the state of the read only properties as a read-only dictionary.
        /// </summary>
        /// <param name="m">The m.</param>
        /// <returns>The current state of all read-only properties.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static IReadOnlyDictionary<string, object> SnapshotReadOnlyState(this MediaElement m)
        {
            var properties = MediaElementProxy.ReadOnlyPropertyNames;
            var target = new Dictionary<string, object>(properties.Count, StringComparer.Ordinal);
            foreach (var p in properties)
                target[p] = MediaElementProxy[m, p];

            return target;
        }
    }
}
