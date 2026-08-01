using System;

namespace _Project.Scripts.Data.Localization
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public sealed class LocalizationNamespaceAttribute : Attribute
    {
        public string NamespaceName { get; }
        public string ScopeMemberName { get; }

        public LocalizationNamespaceAttribute(string namespaceName, string scopeMemberName)
        {
            NamespaceName = namespaceName;
            ScopeMemberName = scopeMemberName;
        }
    }
}