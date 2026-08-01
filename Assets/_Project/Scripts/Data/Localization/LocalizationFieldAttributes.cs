using System;

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
    public sealed class LocalizationKeyAttribute : Attribute
    {
        public string Label { get; }
        public string DefaultSuffix { get; }

        public LocalizationKeyAttribute(string label, string defaultSuffix)
        {
            Label = label;
            DefaultSuffix = defaultSuffix;
        }
    }
}