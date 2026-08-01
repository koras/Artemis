using System.Collections.Generic;
using _Project.Scripts.Data.Localization;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;

namespace _Project.Scripts.Editor
{
    /// <summary>
    /// Generic custom Inspector entry point for localized config definitions.
    /// Localization controls are driven by LocalizationIdAttribute and LocalizationKeyAttribute.
    /// </summary>
    [CustomEditor(typeof(BaseLocalizedDefinitionConfig), true)]
    public sealed class LocalizationConfigEditor : UnityEditor.Editor
    {
        private int _selectedKeyIndex;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            LocalizationConfigEditorUtility.DrawLocalizationIdFields(target, serializedObject);

            // Scope preview reads the target object, so commit an ID selected in this frame first.
            serializedObject.ApplyModifiedProperties();
            serializedObject.Update();

            DrawDefaultInspector();
            LocalizationConfigEditorUtility.DrawLocalizationPanel(target, serializedObject, ref _selectedKeyIndex);

            serializedObject.ApplyModifiedProperties();
        }
    }

    [CustomPropertyDrawer(typeof(LocalizationKeyAttribute))]
    internal sealed class LocalizationKeyPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(
            Rect position,
            SerializedProperty property,
            GUIContent label)
        {
            LocalizationKeyAttribute localizationAttribute = (LocalizationKeyAttribute)attribute;
            string displayLabel = localizationAttribute.DisplayLabel;

            EditorGUI.BeginProperty(position, label, property);

            if (!LocalizationConfigEditorUtility.TryGetNestedLocalizationScope(
                    property,
                    out string scope))
            {
                EditorGUI.LabelField(position, displayLabel, "Localization scope unavailable");
                EditorGUI.EndProperty();
                return;
            }

            StringTableCollection collection = LocalizationConfigEditorUtility.GetStringTableCollection();
            List<string> keySuffixes = LocalizationConfigEditorUtility.GetKeySuffixes(collection, scope);
            string selectedSuffix = string.IsNullOrWhiteSpace(property.stringValue)
                ? localizationAttribute.DefaultSuffix
                : property.stringValue.Trim();

            int selectedIndex = keySuffixes.IndexOf(selectedSuffix);
            bool selectedKeyMissing = selectedIndex < 0;
            if (selectedKeyMissing)
            {
                keySuffixes.Insert(0, "Missing: " + selectedSuffix);
                selectedIndex = 0;
            }

            if (keySuffixes.Count == 0)
            {
                EditorGUI.LabelField(position, displayLabel, "No localization keys");
                EditorGUI.EndProperty();
                return;
            }

            int nextIndex = EditorGUI.Popup(
                position,
                displayLabel,
                selectedIndex,
                keySuffixes.ToArray());

            if (nextIndex != selectedIndex && !(selectedKeyMissing && nextIndex == 0))
            {
                property.stringValue = keySuffixes[nextIndex];
            }

            EditorGUI.EndProperty();
        }
    }
}