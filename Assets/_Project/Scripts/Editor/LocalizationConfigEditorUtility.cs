using System;
using System.Collections.Generic;
using System.Reflection;
using _Project.Scripts.Data.Localization;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace _Project.Scripts.Editor
{
    /// <summary>
    /// Generic localization UI for config fields marked with localization metadata attributes.
    /// Config-specific fields are rendered by the default Inspector and are not referenced here.
    /// </summary>
    internal static class LocalizationConfigEditorUtility
    {
        public static void DrawLocalizationIdFields(
            UnityEngine.Object target,
            SerializedObject serializedObject)
        {
            List<FieldInfo> idFields = GetFieldsWithAttribute<LocalizationIdAttribute>(target.GetType());
            if (idFields.Count == 0)
            {
                return;
            }

            StringTableCollection collection = GetStringTableCollection();
            LocalizationNamespaceAttribute namespaceAttribute = GetMetadata(target);
            List<string> idOptions = GetScopeIds(collection, namespaceAttribute.NamespaceName);

            EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
            for (int i = 0; i < idFields.Count; i++)
            {
                FieldInfo field = idFields[i];
                LocalizationIdAttribute attribute = field.GetCustomAttribute<LocalizationIdAttribute>();
                SerializedProperty property = serializedObject.FindProperty(field.Name);
                DrawLocalizationIdPopup(attribute.Label, property, idOptions);
            }

            EditorGUILayout.Space(4f);
        }

        public static void DrawLocalizationPanel(
            UnityEngine.Object target,
            SerializedObject serializedObject,
            ref int selectedKeyIndex)
        {
            EditorGUILayout.Space(8f);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Localization", EditorStyles.boldLabel);

                string scope = GetScope(target);

                LocalizationNamespaceAttribute metadata = GetMetadata(target);
                EditorGUILayout.LabelField("Namespace", metadata.NamespaceName);
                EditorGUILayout.LabelField("Scope", scope);

                StringTableCollection collection = GetStringTableCollection();

                List<string> keySuffixes = GetKeySuffixes(collection, scope);

                List<FieldInfo> localizationFields =
                    GetFieldsWithAttribute<LocalizationKeyAttribute>(target.GetType());

                if (localizationFields.Count > 0)
                {
                    DrawLocalizationKeyFields(localizationFields, serializedObject, collection, scope, keySuffixes);

                    return;
                }

                if (keySuffixes.Count == 0)
                {
                    EditorGUILayout.HelpBox(
                        $"No localization keys found under '{scope}'. Run Localization Source import or add the keys first.",
                        MessageType.Info);

                    return;
                }

                selectedKeyIndex = Mathf.Clamp(selectedKeyIndex, 0, keySuffixes.Count - 1);

                selectedKeyIndex = EditorGUILayout.Popup(
                    "Key under scope",
                    selectedKeyIndex,
                    keySuffixes.ToArray());

                string selectedKey = $"{scope}.{keySuffixes[selectedKeyIndex]}";

                EditorGUILayout.SelectableLabel(
                    selectedKey,
                    EditorStyles.textField,
                    GUILayout.Height(EditorGUIUtility.singleLineHeight));

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("Copy Key", GUILayout.Width(90f)))
                    {
                        GUIUtility.systemCopyBuffer = selectedKey;
                    }
                }

                EditorGUILayout.LabelField(
                    $"{keySuffixes.Count} key(s) in this scope",
                    EditorStyles.miniLabel);
            }
        }

        private static void DrawLocalizationIdPopup(
            string fieldLabel,
            SerializedProperty idProperty,
            List<string> idOptions)
        {
            string currentId = GetIdValue(idProperty);
            int selectedIndex = idOptions.IndexOf(currentId);
            bool currentIdMissing = selectedIndex < 0;
            string[] popupOptions = idOptions.ToArray();

            if (currentIdMissing)
            {
                string[] optionsWithCurrent = new string[popupOptions.Length + 1];
                optionsWithCurrent[0] = string.IsNullOrEmpty(currentId)
                    ? "Select ID..."
                    : $"Missing: {currentId}";
                idOptions.CopyTo(optionsWithCurrent, 1);
                popupOptions = optionsWithCurrent;
                selectedIndex = 0;
            }

            if (popupOptions.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    "No localization IDs found under this namespace. Import the localization source first.",
                    MessageType.Warning);

                return;
            }

            int nextIndex = EditorGUILayout.Popup(fieldLabel, selectedIndex, popupOptions);

            if (nextIndex != selectedIndex && !(currentIdMissing && nextIndex == 0))
            {
                SetIdValue(idProperty, popupOptions[nextIndex]);
            }
        }

        private static string GetIdValue(SerializedProperty property)
        {
            if (property.propertyType == SerializedPropertyType.Enum)
            {
                return property.enumNames[property.enumValueIndex].ToLowerInvariant();
            }

            return property.stringValue.Trim();
        }

        private static void SetIdValue(SerializedProperty property, string value)
        {
            if (property.propertyType == SerializedPropertyType.Enum)
            {
                for (int i = 0; i < property.enumNames.Length; i++)
                {
                    if (string.Equals(property.enumNames[i], value, StringComparison.OrdinalIgnoreCase))
                    {
                        property.enumValueIndex = i;
                        return;
                    }
                }

                return;
            }

            property.stringValue = value;
        }

        private static List<string> GetScopeIds(
            StringTableCollection collection,
            string namespaceName)
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            string prefix = $"{namespaceName.Trim('.')}.";

            foreach (var entry in collection.SharedData.Entries)
            {
                if (!entry.Key.StartsWith(prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                string remainder = entry.Key.Substring(prefix.Length);
                int separatorIndex = remainder.IndexOf('.');

                if (separatorIndex > 0)
                {
                    ids.Add(remainder.Substring(0, separatorIndex));
                }
            }

            List<string> result = new List<string>(ids);
            result.Sort(StringComparer.Ordinal);
            return result;
        }

        internal static bool TryGetNestedLocalizationScope(
            SerializedProperty property,
            out string scope)
        {
            scope = string.Empty;
            UnityEngine.Object target = property.serializedObject.targetObject;
            if (target == null)
            {
                return false;
            }

            object[] metadataAttributes = target.GetType().GetCustomAttributes(
                typeof(LocalizationNamespaceAttribute),
                true);
            if (metadataAttributes.Length == 0)
            {
                return false;
            }

            scope = GetScope(target);
            Type currentType = target.GetType();
            string[] pathParts = property.propertyPath.Split('.');
            List<string> pathSegments = new List<string>();

            for (int pathIndex = 0; pathIndex < pathParts.Length; pathIndex++)
            {
                FieldInfo field = FindField(currentType, pathParts[pathIndex]);
                if (field == null)
                {
                    return false;
                }

                if (field.GetCustomAttribute<LocalizationKeyAttribute>() != null)
                {
                    break;
                }

                LocalizationCollectionAttribute collectionAttribute =
                    field.GetCustomAttribute<LocalizationCollectionAttribute>();
                if (collectionAttribute != null)
                {
                    if (pathIndex + 2 >= pathParts.Length
                        || pathParts[pathIndex + 1] != "Array"
                        || !TryGetArrayIndex(pathParts[pathIndex + 2], out int arrayIndex))
                    {
                        return false;
                    }

                    pathSegments.Add(collectionAttribute.Segment);
                    pathSegments.Add(arrayIndex.ToString());
                    currentType = GetElementType(field.FieldType);
                    pathIndex += 2;
                    continue;
                }

                currentType = field.FieldType;
            }

            if (pathSegments.Count > 0)
            {
                scope = string.Concat(scope, ".", string.Join(".", pathSegments));
            }

            return true;
        }

        private static FieldInfo FindField(Type targetType, string fieldName)
        {
            const BindingFlags flags = BindingFlags.Instance |
                                        BindingFlags.Public |
                                        BindingFlags.NonPublic |
                                        BindingFlags.DeclaredOnly;

            for (Type type = targetType; type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(fieldName, flags);
                if (field != null)
                {
                    return field;
                }
            }

            return null;
        }

        private static Type GetElementType(Type collectionType)
        {
            return collectionType.IsArray
                ? collectionType.GetElementType()
                : collectionType.GetGenericArguments()[0];
        }

        private static bool TryGetArrayIndex(string dataToken, out int index)
        {
            const string prefix = "data[";
            index = 0;

            if (!dataToken.StartsWith(prefix, StringComparison.Ordinal)
                || !dataToken.EndsWith("]", StringComparison.Ordinal))
            {
                return false;
            }

            string indexText = dataToken.Substring(prefix.Length, dataToken.Length - prefix.Length - 1);
            return int.TryParse(indexText, out index);
        }

        private static void DrawLocalizationKeyFields(
            List<FieldInfo> localizationFields,
            SerializedObject serializedObject,
            StringTableCollection collection,
            string scope,
            List<string> keySuffixes)
        {
            if (keySuffixes.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No localization keys found under this scope. Run Localization Source import.",
                    MessageType.Warning);

                return;
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Field localization", EditorStyles.boldLabel);

            for (int i = 0; i < localizationFields.Count; i++)
            {
                FieldInfo field = localizationFields[i];
                LocalizationKeyAttribute attribute = field.GetCustomAttribute<LocalizationKeyAttribute>();
                SerializedProperty property = serializedObject.FindProperty(field.Name);

                DrawLocalizationKeyField(
                    attribute.DisplayLabel,
                    property,
                    attribute.DefaultSuffix,
                    scope,
                    collection,
                    keySuffixes);
            }
        }

        private static void DrawLocalizationKeyField(
            string fieldLabel,
            SerializedProperty keyProperty,
            string defaultSuffix,
            string scope,
            StringTableCollection collection,
            List<string> keySuffixes)
        {
            string selectedSuffix = string.IsNullOrWhiteSpace(keyProperty.stringValue)
                ? defaultSuffix
                : keyProperty.stringValue.Trim();

            int selectedIndex = keySuffixes.IndexOf(selectedSuffix);
            bool selectedKeyMissing = selectedIndex < 0;
            string[] popupOptions = keySuffixes.ToArray();

            if (selectedKeyMissing)
            {
                string[] optionsWithMissing = new string[popupOptions.Length + 1];
                optionsWithMissing[0] = $"Missing: {selectedSuffix}";
                keySuffixes.CopyTo(optionsWithMissing, 1);
                popupOptions = optionsWithMissing;
                selectedIndex = 0;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(fieldLabel, GUILayout.Width(170f));
                int nextIndex = EditorGUILayout.Popup(selectedIndex, popupOptions);

                if (nextIndex != selectedIndex && !(selectedKeyMissing && nextIndex == 0))
                {
                    keyProperty.stringValue = popupOptions[nextIndex];
                    selectedSuffix = keyProperty.stringValue;
                }

                string key = $"{scope}.{selectedSuffix}";

                if (GUILayout.Button("Copy", GUILayout.Width(55f)))
                {
                    GUIUtility.systemCopyBuffer = key;
                }
            }

            string selectedKey = $"{scope}.{selectedSuffix}";
            EditorGUILayout.LabelField(selectedKey, EditorStyles.miniLabel);

            if (selectedKeyMissing)
            {
                EditorGUILayout.HelpBox(
                    $"Localization key suffix '{selectedSuffix}' is not present in this scope.",
                    MessageType.Warning);
            }

            StringTable englishTable = collection.GetTable(new LocaleIdentifier("en")) as StringTable;
            StringTable russianTable = collection.GetTable(new LocaleIdentifier("ru")) as StringTable;

            DrawLocaleValue("English", englishTable, selectedKey);
            DrawLocaleValue("Russian", russianTable, selectedKey);
        }

        private static void DrawLocaleValue(string localeLabel, StringTable table, string key)
        {
            StringTableEntry entry = table.GetEntry(key);

            if (entry == null)
            {
                EditorGUILayout.HelpBox($"{localeLabel} entry is missing. Run Localization Source import.",
                    MessageType.Warning);

                return;
            }

            EditorGUILayout.LabelField(localeLabel, EditorStyles.miniLabel);
            EditorGUILayout.LabelField(entry.Value ?? string.Empty, EditorStyles.wordWrappedLabel);
        }

        private static string GetScope(UnityEngine.Object target)
        {
            LocalizationNamespaceAttribute metadata = GetMetadata(target);

            object scopeValue = GetMemberValue(target, metadata.ScopeMemberName);
            string section = scopeValue is Enum
                ? scopeValue.ToString().ToLowerInvariant()
                : scopeValue.ToString();
            return $"{metadata.NamespaceName.Trim('.')}.{section.Trim('.')}";
        }

        private static LocalizationNamespaceAttribute GetMetadata(UnityEngine.Object target)
        {
            object[] attributes = target.GetType().GetCustomAttributes(
                typeof(LocalizationNamespaceAttribute),
                true);

            return (LocalizationNamespaceAttribute)attributes[0];
        }

        private static object GetMemberValue(UnityEngine.Object target, string memberName)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type targetType = target.GetType();

            FieldInfo field = targetType.GetField(memberName, flags);

            if (field != null)
            {
                return field.GetValue(target);
            }

            PropertyInfo property = targetType.GetProperty(memberName, flags);
            return property.GetValue(target);
        }

        private static List<FieldInfo> GetFieldsWithAttribute<TAttribute>(Type targetType)
            where TAttribute : Attribute
        {
            List<FieldInfo> fields = new List<FieldInfo>();

            for (Type type = targetType; type != null; type = type.BaseType)
            {
                const BindingFlags flags = BindingFlags.Instance |
                                            BindingFlags.Public |
                                            BindingFlags.NonPublic |
                                            BindingFlags.DeclaredOnly;

                FieldInfo[] declaredFields = type.GetFields(flags);
                for (int i = 0; i < declaredFields.Length; i++)
                {
                    if (declaredFields[i].GetCustomAttribute<TAttribute>() != null)
                    {
                        fields.Add(declaredFields[i]);
                    }
                }
            }

            fields.Reverse();
            return fields;
        }

        internal static StringTableCollection GetStringTableCollection()
        {
            var collections = LocalizationEditorSettings.GetStringTableCollections();
            return collections[0];
        }

        internal static List<string> GetKeySuffixes(StringTableCollection collection, string scope)
        {
            string prefix = $"{scope}.";
            List<string> keySuffixes = new List<string>();

            foreach (var entry in collection.SharedData.Entries)
            {
                if (entry.Key.StartsWith(prefix, StringComparison.Ordinal))
                {
                    keySuffixes.Add(entry.Key.Substring(prefix.Length));
                }
            }

            keySuffixes.Sort(StringComparer.Ordinal);
            return keySuffixes;
        }
    }
}