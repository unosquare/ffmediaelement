namespace Unosquare.FFME.Platform
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Reflection;
    using System.Windows;
    using Shared;

    internal static class TypeManager
    {
        static TypeManager()
        {
            MediaElementDependencyProperties = new ReadOnlyDictionary<string, DependencyProperty>(
                RetrieveDependencyProperties(typeof(MediaElement)).ToDictionary((p) => p.Name, (p) => p));

            MediaElementProperties = new ReadOnlyDictionary<string, PropertyInfo>(
                RetrieveProperties(typeof(MediaElement)).ToDictionary((p) => p.Name, (p) => p));

            MediaEngineStateProperties = new ReadOnlyDictionary<string, PropertyInfo>(
                RetrieveProperties(typeof(MediaEngineState)).ToDictionary((p) => p.Name, (p) => p));
        }

        public static ReadOnlyDictionary<string, DependencyProperty> MediaElementDependencyProperties { get; }

        public static ReadOnlyDictionary<string, PropertyInfo> MediaElementProperties { get; }

        public static ReadOnlyDictionary<string, PropertyInfo> MediaEngineStateProperties { get; }

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

        private static List<PropertyInfo> RetrieveProperties(Type t)
        {
            var result = new List<PropertyInfo>();
            var propertyInfos = t.GetProperties(BindingFlags.Instance | BindingFlags.Public).ToArray();
            foreach (var propertyInfo in propertyInfos)
                result.Add(propertyInfo);

            return result;
        }
    }
}
