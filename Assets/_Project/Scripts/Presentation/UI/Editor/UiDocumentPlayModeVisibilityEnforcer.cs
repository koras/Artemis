#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace _Project.Scripts.Presentation.UI.Editor
{
    /// <summary>
    /// Автоматически выключает UIDocument в Edit Mode и включает в Play Mode.
    /// </summary>
    [InitializeOnLoad]
    public static class UiDocumentPlayModeVisibilityEnforcer
    {
        static UiDocumentPlayModeVisibilityEnforcer()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.delayCall += ApplyVisibilitySafe;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // Apply only after mode transition is fully completed to avoid inspector binding races.
            if (state != PlayModeStateChange.EnteredEditMode && state != PlayModeStateChange.EnteredPlayMode)
            {
                return;
            }

            EditorApplication.delayCall += ApplyVisibilitySafe;
        }

        private static void ApplyVisibilitySafe()
        {
            // Skip during script/import refresh where Unity editor object graph is unstable.
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            UIDocument[] documents = Object.FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            bool shouldEnable = EditorApplication.isPlaying;

            for (int i = 0; i < documents.Length; i++)
            {
                UIDocument document = documents[i];
                if (document == null)
                {
                    continue;
                }

                if (document.enabled != shouldEnable)
                {
                    document.enabled = shouldEnable;
                }
            }
        }
    }
}
#endif
