using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace _Project.Scripts.Editor
{
    /// <summary>
    /// Repairs URP default renderer links after Unity/package upgrades.
    /// </summary>
    [InitializeOnLoad]
    public static class UrpRendererRepair
    {
        private const string AutoRepairSessionKey = "Artemis.UrpRendererRepair.RanThisSession";
        private const string UniversalRpAssetPath = "Assets/_Project/Settings/UniversalRP.asset";
        private const string Renderer2DAssetPath = "Assets/_Project/Settings/Renderer2D.asset";
        private const string UrpPackagePath = "Packages/com.unity.render-pipelines.universal";
        private const string DefaultPostProcessDataAssetPath = UrpPackagePath + "/Runtime/Data/PostProcessData.asset";

        static UrpRendererRepair()
        {
            EditorApplication.delayCall += AutoRepairOncePerSession;
        }

        [MenuItem("Artemis/Validation/Repair URP Renderer")]
        public static void RepairFromMenu()
        {
            Repair(forceLog: true);
        }

        private static void AutoRepairOncePerSession()
        {
            if (SessionState.GetBool(AutoRepairSessionKey, false))
            {
                return;
            }

            SessionState.SetBool(AutoRepairSessionKey, true);
            Repair(forceLog: false);
        }

        private static void Repair(bool forceLog)
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += () => Repair(forceLog);
                return;
            }

            var pipelineAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(UniversalRpAssetPath);
            if (pipelineAsset == null)
            {
                return;
            }

            Renderer2DData renderer2DData = GetOrCreateRenderer2DData();
            if (renderer2DData == null)
            {
                Debug.LogError("[UrpRendererRepair] Failed to load or create Renderer2D.asset.");
                return;
            }

            bool changed = false;

            changed |= ReloadRendererResources(renderer2DData);
            changed |= EnsurePostProcessData(renderer2DData);
            changed |= BindRendererToPipeline(pipelineAsset, renderer2DData);

            if (!changed)
            {
                if (forceLog)
                {
                    Debug.Log("[UrpRendererRepair] URP renderer configuration is already valid.");
                }

                return;
            }

            EditorUtility.SetDirty(renderer2DData);
            EditorUtility.SetDirty(pipelineAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(Renderer2DAssetPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(UniversalRpAssetPath, ImportAssetOptions.ForceUpdate);

            Debug.Log("[UrpRendererRepair] Repaired URP default renderer configuration.");
        }

        private static Renderer2DData GetOrCreateRenderer2DData()
        {
            Renderer2DData renderer2DData = AssetDatabase.LoadAssetAtPath<Renderer2DData>(Renderer2DAssetPath);
            if (renderer2DData != null)
            {
                return renderer2DData;
            }

            Object existingAsset = AssetDatabase.LoadMainAssetAtPath(Renderer2DAssetPath);
            if (existingAsset != null)
            {
                string backupPath = AssetDatabase.GenerateUniqueAssetPath("Assets/_Project/Settings/Renderer2D_BrokenBackup.asset");
                string moveError = AssetDatabase.MoveAsset(Renderer2DAssetPath, backupPath);
                if (!string.IsNullOrEmpty(moveError))
                {
                    Debug.LogWarning($"[UrpRendererRepair] Failed to back up invalid Renderer2D asset: {moveError}");
                    AssetDatabase.DeleteAsset(Renderer2DAssetPath);
                }
            }

            renderer2DData = ScriptableObject.CreateInstance<Renderer2DData>();
            AssetDatabase.CreateAsset(renderer2DData, Renderer2DAssetPath);
            return renderer2DData;
        }

        private static bool ReloadRendererResources(Renderer2DData renderer2DData)
        {
            return ResourceReloader.ReloadAllNullIn(renderer2DData, UniversalRenderPipelineAsset.packagePath);
        }

        private static bool EnsurePostProcessData(Renderer2DData renderer2DData)
        {
            PostProcessData postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(DefaultPostProcessDataAssetPath);
            if (postProcessData == null)
            {
                return false;
            }

            var serializedRendererData = new SerializedObject(renderer2DData);
            SerializedProperty postProcessProperty = serializedRendererData.FindProperty("m_PostProcessData");
            if (postProcessProperty == null || postProcessProperty.objectReferenceValue == postProcessData)
            {
                return false;
            }

            postProcessProperty.objectReferenceValue = postProcessData;
            serializedRendererData.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static bool BindRendererToPipeline(UniversalRenderPipelineAsset pipelineAsset, Renderer2DData renderer2DData)
        {
            var serializedPipelineAsset = new SerializedObject(pipelineAsset);
            SerializedProperty rendererTypeProperty = serializedPipelineAsset.FindProperty("m_RendererType");
            SerializedProperty rendererDataProperty = serializedPipelineAsset.FindProperty("m_RendererData");
            SerializedProperty rendererDataListProperty = serializedPipelineAsset.FindProperty("m_RendererDataList");
            SerializedProperty defaultRendererIndexProperty = serializedPipelineAsset.FindProperty("m_DefaultRendererIndex");

            bool changed = false;

            if (rendererTypeProperty != null && rendererTypeProperty.intValue != 2)
            {
                rendererTypeProperty.intValue = 2;
                changed = true;
            }

            if (rendererDataProperty != null && rendererDataProperty.objectReferenceValue != renderer2DData)
            {
                rendererDataProperty.objectReferenceValue = renderer2DData;
                changed = true;
            }

            if (rendererDataListProperty != null)
            {
                if (rendererDataListProperty.arraySize != 1)
                {
                    rendererDataListProperty.arraySize = 1;
                    changed = true;
                }

                SerializedProperty defaultRendererProperty = rendererDataListProperty.GetArrayElementAtIndex(0);
                if (defaultRendererProperty.objectReferenceValue != renderer2DData)
                {
                    defaultRendererProperty.objectReferenceValue = renderer2DData;
                    changed = true;
                }
            }

            if (defaultRendererIndexProperty != null && defaultRendererIndexProperty.intValue != 0)
            {
                defaultRendererIndexProperty.intValue = 0;
                changed = true;
            }

            if (!changed)
            {
                return false;
            }

            serializedPipelineAsset.ApplyModifiedPropertiesWithoutUndo();
            GraphicsSettings.defaultRenderPipeline = pipelineAsset;
            QualitySettings.renderPipeline = pipelineAsset;
            return true;
        }
    }
}
