using System;
using UnityEngine;

namespace _Project.Scripts.Data.Localization
{
    /// <summary>
    /// Marks a serialized field as the identifier segment of a localization scope.
    /// The localization custom editor renders its value as a scope-aware dropdown.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class LocalizationIdAttribute : Attribute
    {
        public string Label { get; }

        public LocalizationIdAttribute(string label)
        {
            Label = label;
        }
    }

    /// <summary>
    /// Marks a serialized suffix field for generic localization key selection.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class LocalizationKeyAttribute : PropertyAttribute
    {
        public string Label { get; }
        public string DefaultSuffix { get; }
        public string DisplayLabel => $"{Label} (Localization Key)";

        public LocalizationKeyAttribute(string label, string defaultSuffix)
        {
            Label = label;
            DefaultSuffix = defaultSuffix;
        }
    }

    /// <summary>
    /// Marks a serialized collection as a localization key path segment.
    /// The editor adds segment and either the collection index or the element ID
    /// to the parent localization scope.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class LocalizationCollectionAttribute : Attribute
    {
        public string Segment { get; }
        public string IdMemberName { get; }

        public LocalizationCollectionAttribute(string segment, string idMemberName = null)
        {
            Segment = segment;
            IdMemberName = idMemberName;
        }
    }
}