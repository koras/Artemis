using _Project.Scripts.Data.Localization;
using UnityEditor;

namespace _Project.Scripts.Editor
{
    /// <summary>
    /// Generic custom Inspector entry point for localized config definitions.
    /// Localization controls are driven by LocalizationIdAttribute and LocalizationKeyAttribute.
    /// </summary>
    [CustomEditor(typeof(BaseLocalizedConfigDefinition), true)]
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
}