using _Project.Scripts.Data.Grid;
using _Project.Scripts.Systems.Construction;
using UnityEngine;

namespace _Project.Scripts.Systems.External
{
    /// <summary>
    /// Registers RocketData cells as external storage points in grid state.
    /// </summary>
    public sealed class RocketExternalStorageRegistrationService
    {
        private readonly GridState _gridState;
        private readonly BuildingManager _buildingManager;
        private readonly Transform _externalObjectsRoot;
        private readonly string _externalStorageObjectName;
        private readonly Vector2Int _rocketSpawnCell;

        public RocketExternalStorageRegistrationService(
            GridState gridState,
            BuildingManager buildingManager,
            Transform externalObjectsRoot,
            string externalStorageObjectName,
            Vector2Int rocketSpawnCell)
        {
            _gridState = gridState;
            _buildingManager = buildingManager;
            _externalObjectsRoot = externalObjectsRoot;
            _externalStorageObjectName = externalStorageObjectName;
            _rocketSpawnCell = rocketSpawnCell;
        }

        public void Register()
        {
            if (_buildingManager == null || _gridState == null)
            {
                Debug.LogError($"[RocketExternalStorageRegistrationService] Registration skipped: manager={(_buildingManager != null)}, gridState={(_gridState != null)}, externalRoot={(_externalObjectsRoot != null)}");
                return;
            }

            Transform[] transforms = _externalObjectsRoot != null
                ? _externalObjectsRoot.GetComponentsInChildren<Transform>(true)
                : new Transform[0];
            bool markerFound = false;
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform transformCandidate = transforms[i];
                if (transformCandidate == null) continue;
                if (transformCandidate == _externalObjectsRoot) continue;
                if (!string.Equals(transformCandidate.name, _externalStorageObjectName)) continue;
                markerFound = true;
                RegisterZone(transformCandidate.name, markerFound);
            }

            // Fallback keeps storage zone active even when scene marker is absent.
            if (!markerFound)
            {
                RegisterZone(_externalStorageObjectName, markerFound);
            }
        }

        private void RegisterZone(string storageName, bool markerFound)
        {
            int gridMaxX = _gridState.Width - 1;
            int gridMaxY = _gridState.Height - 1;
            int rocketZoneMax = Mathf.Max(_rocketSpawnCell.x, _rocketSpawnCell.y);
            int minX = Mathf.Clamp((rocketZoneMax / 2) + 3, 0, gridMaxX);
            int maxX = Mathf.Clamp((rocketZoneMax / 2) + 8, 0, gridMaxX);
            int topY = Mathf.Clamp(rocketZoneMax, 0, gridMaxY);
            int bottomY = Mathf.Clamp(rocketZoneMax - 5, 0, gridMaxY);
            int registeredCellsCount = 0;

            for (int y = topY; y >= bottomY; y--)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    _buildingManager.RegisterExternalStorageCell(new Vector2Int(x, y));
                    registeredCellsCount++;
                }
            }

            Debug.Log($"[RocketExternalStorageRegistrationService] Registered external storage '{storageName}' zone: X=[{minX}..{maxX}], Y=[{bottomY}..{topY}], cells={registeredCellsCount}, markerFound={markerFound}, externalRoot={(_externalObjectsRoot != null)}");
        }
    }
}
