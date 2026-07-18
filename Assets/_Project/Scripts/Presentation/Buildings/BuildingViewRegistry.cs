using System;
using System.Collections.Generic;
using _Project.Scripts.Data.Construction;
using UnityEngine;

namespace _Project.Scripts.Presentation.Buildings
{
    /// <summary>
    /// Реестр runtime view-префабов зданий.
    /// Ключ (BuildingDef) берётся из самого prefab-компонента BuildingViewBase.
    /// </summary>
    public sealed class BuildingViewRegistry : MonoBehaviour
    {
        [Header("Mappings")]
        [SerializeField] private BuildingViewBase[] _viewPrefabs = Array.Empty<BuildingViewBase>();

        private readonly Dictionary<BuildingDef, BuildingViewBase> _prefabsByDef = new Dictionary<BuildingDef, BuildingViewBase>();
        private bool _isBuilt;

        /// <summary>
        /// Возвращает view-префаб для переданного дефа.
        /// </summary>
        public bool TryGetViewPrefab(BuildingDef buildingDef, out BuildingViewBase viewPrefab)
        {
            EnsureBuilt();
            return _prefabsByDef.TryGetValue(buildingDef, out viewPrefab);
        }

        private void EnsureBuilt()
        {
            if (_isBuilt) return;
            _prefabsByDef.Clear();

            for (int i = 0; i < _viewPrefabs.Length; i++)
            {
                BuildingViewBase prefab = _viewPrefabs[i];
                if (prefab == null) continue;
                if (prefab.BuildingDef == null)
                {
            // Debug.LogWarning($"[Build] BuildingViewRegistry: prefab '{prefab.name}' has no BuildingDef.");
                    continue;
                }

                _prefabsByDef[prefab.BuildingDef] = prefab;
            }

            _isBuilt = true;
        }
    }
}
