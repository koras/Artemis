using System;
using System.Collections.Generic;
using _Project.Scripts.Data.Construction;
using _Project.Scripts.Data.Grid;
using _Project.Scripts.Data.Tasks;
using _Project.Scripts.Systems.Grid;
using _Project.Scripts.Systems.Tasks;
using DG.Tweening;
using UnityEngine;

namespace _Project.Scripts.Systems.Resources
{
    /// <summary>
    /// Manages resource drop objects in the world: spawn, pickup, stacking and falling.
    /// </summary>
    public sealed class SceneResourceObjectService
    {
        private readonly GridState _gridState;
        private readonly GlobalTaskBoardService _taskBoard;
        private readonly GridCoordinateConverter _gridCoordinateConverter;
        private readonly Transform _resourceRoot;
        private readonly Dictionary<SceneResourceType, GameObject> _prefabByType;
        private readonly HashSet<CellType> _supportCellTypes;
        private readonly HashSet<BuildObjectType> _supportBuildObjectTypes;
        private readonly float _fallStepDurationSeconds;
        private readonly float _horizontalJitterFactor = 0.28f;

        private readonly Dictionary<Vector2Int, List<SpawnedResourceRecord>> _resourcesByCell =
            new Dictionary<Vector2Int, List<SpawnedResourceRecord>>();

        private readonly List<Vector2Int> _iterationBuffer = new List<Vector2Int>();

        public SceneResourceObjectService(
            GridState gridState,
            GlobalTaskBoardService taskBoard,
            GridCoordinateConverter gridCoordinateConverter,
            Transform resourceRoot,
            IDictionary<SceneResourceType, GameObject> prefabByType,
            IList<CellType> supportCellTypes,
            IList<BuildObjectType> supportBuildObjectTypes,
            float fallStepDurationSeconds)
        {
            _gridState = gridState ?? throw new ArgumentNullException(nameof(gridState));
            _taskBoard = taskBoard ?? throw new ArgumentNullException(nameof(taskBoard));
            _gridCoordinateConverter = gridCoordinateConverter ?? throw new ArgumentNullException(nameof(gridCoordinateConverter));
            _resourceRoot = resourceRoot;
            _prefabByType = new Dictionary<SceneResourceType, GameObject>(prefabByType
                                                                           ?? throw new ArgumentNullException(nameof(prefabByType)));
            _supportCellTypes = supportCellTypes != null ? new HashSet<CellType>(supportCellTypes) : new HashSet<CellType>();
            _supportBuildObjectTypes = supportBuildObjectTypes != null ? new HashSet<BuildObjectType>(supportBuildObjectTypes) : new HashSet<BuildObjectType>();
            _fallStepDurationSeconds = Mathf.Max(0.05f, fallStepDurationSeconds);
        }

        /// <summary>
        /// Spawns resource drop in the requested cell.
        /// For same-type drops in the same cell it stacks amount instead of creating a new object.
        /// For different types it tries to place a new drop in a nearby free cell.
        /// </summary>
        public bool TrySpawnAtCell(
            SceneResourceType resourceType,
            string resourceId,
            int amount,
            Vector2Int cell,
            out GameObject spawnedObject,
            out Vector2Int spawnedCell)
        {
            spawnedObject = null;
            spawnedCell = cell;
            if (string.IsNullOrWhiteSpace(resourceId) || amount <= 0)
            {
                return false;
            }

            if (!_gridState.IsInside(cell.x, cell.y))
            {
                return false;
            }

            if (_resourcesByCell.TryGetValue(cell, out List<SpawnedResourceRecord> existingRecords))
            {
                for (int i = 0; i < existingRecords.Count; i++)
                {
                    SpawnedResourceRecord existingRecord = existingRecords[i];
                    if (!CanStack(existingRecord, resourceType, resourceId))
                    {
                        continue;
                    }

                    int mergedAmount = existingRecord.Amount + amount;
                    var existingView = existingRecord.ResourceObject != null
                        ? existingRecord.ResourceObject.GetComponent<SceneResourceDropView>()
                        : null;
                    existingView?.AddAmount(amount);
                    existingRecords[i] = new SpawnedResourceRecord(
                        existingRecord.ResourceType,
                        existingRecord.ResourceId,
                        mergedAmount,
                        existingRecord.ResourceObject,
                        existingRecord.FallTween,
                        existingRecord.JitterX);
                    spawnedObject = existingRecord.ResourceObject;
                    return true;
                }
            }

            if (!_prefabByType.TryGetValue(resourceType, out GameObject prefab) || prefab == null)
            {
                return false;
            }

            spawnedObject = UnityEngine.Object.Instantiate(prefab, Vector3.zero, Quaternion.identity, _resourceRoot);
            var dropView = spawnedObject.GetComponent<SceneResourceDropView>();
            if (dropView == null)
            {
                UnityEngine.Object.Destroy(spawnedObject);
                spawnedObject = null;
                return false;
            }

            float jitterX = CalculateStableJitterX(cell, resourceType, resourceId);
            if (!_resourcesByCell.TryGetValue(cell, out List<SpawnedResourceRecord> recordsAtCell))
            {
                recordsAtCell = new List<SpawnedResourceRecord>();
                _resourcesByCell[cell] = recordsAtCell;
            }

            dropView.Initialize(resourceId, amount);
            recordsAtCell.Add(new SpawnedResourceRecord(resourceType, resourceId, amount, spawnedObject, null, jitterX));
            RepositionCellObjects(cell);
            return true;
        }

        public bool TryTakeFromCell(Vector2Int cell, out SceneResourceType resourceType, out string resourceId, out int amount)
        {
            resourceType = SceneResourceType.Iron;
            resourceId = null;
            amount = 0;

            if (!_resourcesByCell.TryGetValue(cell, out List<SpawnedResourceRecord> recordsAtCell) || recordsAtCell.Count == 0)
            {
                return false;
            }

            int takeIndex = recordsAtCell.Count - 1;
            SpawnedResourceRecord record = recordsAtCell[takeIndex];
            resourceType = record.ResourceType;
            resourceId = record.ResourceId;
            amount = record.Amount;
            recordsAtCell.RemoveAt(takeIndex);
            if (recordsAtCell.Count == 0)
            {
                _resourcesByCell.Remove(cell);
            }
            else
            {
                RepositionCellObjects(cell);
            }

            if (record.ResourceObject != null)
            {
                record.FallTween?.Kill();
                UnityEngine.Object.Destroy(record.ResourceObject);
            }

            return true;
        }

        public bool HasResourceAtCell(Vector2Int cell)
        {
            return _resourcesByCell.TryGetValue(cell, out List<SpawnedResourceRecord> records) && records.Count > 0;
        }

        public bool TryGetResourceTypeAtCell(Vector2Int cell, out SceneResourceType resourceType)
        {
            resourceType = SceneResourceType.Iron;
            if (!_resourcesByCell.TryGetValue(cell, out List<SpawnedResourceRecord> records) || records.Count == 0)
            {
                return false;
            }

            SpawnedResourceRecord record = records[records.Count - 1];
            resourceType = record.ResourceType;
            return true;
        }

        public bool TryPeekResourceAtCell(Vector2Int cell, out string resourceId, out int amount)
        {
            resourceId = null;
            amount = 0;
            if (!_resourcesByCell.TryGetValue(cell, out List<SpawnedResourceRecord> records) || records.Count == 0)
            {
                return false;
            }

            SpawnedResourceRecord record = records[records.Count - 1];
            resourceId = record.ResourceId;
            amount = record.Amount;
            return true;
        }

        /// <summary>
        /// Builds a per-cell snapshot string with dropped prefab counts grouped by resource id.
        /// Example: "Iron x2, Rogalite x1".
        /// </summary>
        public bool TryBuildCellPrefabSummary(Vector2Int cell, out int prefabCount, out string summary)
        {
            prefabCount = 0;
            summary = "none";
            if (!_resourcesByCell.TryGetValue(cell, out List<SpawnedResourceRecord> records) || records == null || records.Count == 0)
            {
                return false;
            }

            var countByResourceId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            prefabCount = records.Count;
            for (int i = 0; i < records.Count; i++)
            {
                string resourceId = records[i].ResourceId;
                if (string.IsNullOrWhiteSpace(resourceId))
                {
                    resourceId = records[i].ResourceType.GetResourceId();
                }

                countByResourceId.TryGetValue(resourceId, out int current);
                countByResourceId[resourceId] = current + 1;
            }

            var parts = new List<string>(countByResourceId.Count);
            foreach (KeyValuePair<string, int> pair in countByResourceId)
            {
                parts.Add($"{pair.Key} x{pair.Value}");
            }

            summary = string.Join(", ", parts);
            return true;
        }

        /// <summary>
        /// Returns current count of dropped prefab instances on the scene.
        /// </summary>
        public int GetTotalDroppedPrefabCount()
        {
            int total = 0;
            foreach (KeyValuePair<Vector2Int, List<SpawnedResourceRecord>> pair in _resourcesByCell)
            {
                if (pair.Value == null) continue;
                total += pair.Value.Count;
            }

            return total;
        }

        public void ClearAll()
        {
            foreach (KeyValuePair<Vector2Int, List<SpawnedResourceRecord>> pair in _resourcesByCell)
            {
                for (int i = 0; i < pair.Value.Count; i++)
                {
                    SpawnedResourceRecord record = pair.Value[i];
                    if (record.ResourceObject == null) continue;
                    record.FallTween?.Kill();
                    UnityEngine.Object.Destroy(record.ResourceObject);
                }
            }

            _resourcesByCell.Clear();
        }

        public void TickFalling(float tickSeconds)
        {
            _iterationBuffer.Clear();
            foreach (KeyValuePair<Vector2Int, List<SpawnedResourceRecord>> pair in _resourcesByCell)
            {
                _iterationBuffer.Add(pair.Key);
            }

            for (int i = 0; i < _iterationBuffer.Count; i++)
            {
                Vector2Int cell = _iterationBuffer[i];
                if (!_resourcesByCell.TryGetValue(cell, out List<SpawnedResourceRecord> recordsAtCell) || recordsAtCell.Count == 0)
                {
                    continue;
                }

                int movingIndex = recordsAtCell.Count - 1;
                SpawnedResourceRecord record = recordsAtCell[movingIndex];
                if (record.ResourceObject == null)
                {
                    recordsAtCell.RemoveAt(movingIndex);
                    if (recordsAtCell.Count == 0) _resourcesByCell.Remove(cell);
                    continue;
                }

                if (record.FallTween != null && record.FallTween.IsActive() && record.FallTween.IsPlaying())
                {
                    continue;
                }

                Vector2Int belowCell = new Vector2Int(cell.x, cell.y - 1);
                if (!_gridState.IsInside(belowCell.x, belowCell.y))
                {
                    continue;
                }

                if (TryStackIntoCell(cell, belowCell, record))
                {
                    continue;
                }

                if (!CanFallToCell(belowCell))
                {
                    continue;
                }

                UnitTaskRecord dropTask;
                if (_taskBoard.TryGetTaskByCell(cell, out dropTask)
                    && dropTask != null
                    && dropTask.TaskType == UnitTaskType.DeliverDroppedResource)
                {
                    bool movedTask = _taskBoard.TryMoveDroppedResourceTaskCell(cell, belowCell);
                    if (!movedTask)
                    {
                        _taskBoard.CancelTaskByCell(cell, out _, out _);
                    }
                }

                recordsAtCell.RemoveAt(movingIndex);
                if (recordsAtCell.Count == 0)
                {
                    _resourcesByCell.Remove(cell);
                }
                else
                {
                    RepositionCellObjects(cell);
                }

                Vector2 world2D = GetCellRestWorldPosition(record.ResourceObject, belowCell, record.JitterX);
                Tween fallTween = record.ResourceObject.transform
                    .DOMove(new Vector3(world2D.x, world2D.y, record.ResourceObject.transform.position.z), _fallStepDurationSeconds)
                    .SetEase(Ease.InQuad);

                if (!_resourcesByCell.TryGetValue(belowCell, out List<SpawnedResourceRecord> recordsBelow))
                {
                    recordsBelow = new List<SpawnedResourceRecord>();
                    _resourcesByCell[belowCell] = recordsBelow;
                }

                recordsBelow.Add(new SpawnedResourceRecord(
                    record.ResourceType,
                    record.ResourceId,
                    record.Amount,
                    record.ResourceObject,
                    fallTween,
                    record.JitterX));
                RepositionCellObjects(belowCell);
            }

            EnsureDeliveryTasksForAllDropCells();
        }

        public void SetFallingPaused(bool isPaused)
        {
            foreach (KeyValuePair<Vector2Int, List<SpawnedResourceRecord>> pair in _resourcesByCell)
            {
                List<SpawnedResourceRecord> records = pair.Value;
                if (records == null)
                {
                    continue;
                }

                for (int i = 0; i < records.Count; i++)
                {
                    Tween fallTween = records[i].FallTween;
                    if (fallTween == null || !fallTween.IsActive())
                    {
                        continue;
                    }

                    if (isPaused)
                    {
                        fallTween.Pause();
                        continue;
                    }

                    fallTween.Play();
                }
            }
        }

        /// <summary>
        /// Immediate fall check after a cell is turned into Empty (for example after digging).
        /// </summary>
        public void NotifyCellBecameEmpty(Vector2Int emptiedCell)
        {
            if (!_gridState.IsInside(emptiedCell.x, emptiedCell.y))
            {
                return;
            }

            // Try to propagate falling in the same column right away.
            // This keeps the behavior responsive exactly at dig completion.
            bool movedAny;
            do
            {
                movedAny = false;
                for (int y = emptiedCell.y + 1; y < _gridState.Height; y++)
                {
                    Vector2Int current = new Vector2Int(emptiedCell.x, y);
                    if (!_resourcesByCell.TryGetValue(current, out List<SpawnedResourceRecord> recordsAtCurrent) || recordsAtCurrent.Count == 0)
                    {
                        continue;
                    }

                    int movingIndex = recordsAtCurrent.Count - 1;
                    SpawnedResourceRecord record = recordsAtCurrent[movingIndex];
                    if (record.ResourceObject == null)
                    {
                        recordsAtCurrent.RemoveAt(movingIndex);
                        if (recordsAtCurrent.Count == 0) _resourcesByCell.Remove(current);
                        continue;
                    }

                    if (record.FallTween != null && record.FallTween.IsActive() && record.FallTween.IsPlaying())
                    {
                        continue;
                    }

                    Vector2Int belowCell = new Vector2Int(current.x, current.y - 1);
                    if (!_gridState.IsInside(belowCell.x, belowCell.y))
                    {
                        continue;
                    }

                    if (TryStackIntoCell(current, belowCell, record))
                    {
                        movedAny = true;
                        continue;
                    }

                    if (!CanFallToCell(belowCell))
                    {
                        continue;
                    }

                    UnitTaskRecord dropTask;
                    if (_taskBoard.TryGetTaskByCell(current, out dropTask)
                        && dropTask != null
                        && dropTask.TaskType == UnitTaskType.DeliverDroppedResource)
                    {
                        bool movedTask = _taskBoard.TryMoveDroppedResourceTaskCell(current, belowCell);
                        if (!movedTask)
                        {
                            _taskBoard.CancelTaskByCell(current, out _, out _);
                        }
                    }

                    recordsAtCurrent.RemoveAt(movingIndex);
                    if (recordsAtCurrent.Count == 0)
                    {
                        _resourcesByCell.Remove(current);
                    }
                    else
                    {
                        RepositionCellObjects(current);
                    }

                    Vector2 world2D = GetCellRestWorldPosition(record.ResourceObject, belowCell, record.JitterX);
                    Tween fallTween = record.ResourceObject.transform
                        .DOMove(new Vector3(world2D.x, world2D.y, record.ResourceObject.transform.position.z), _fallStepDurationSeconds)
                        .SetEase(Ease.InQuad);

                    if (!_resourcesByCell.TryGetValue(belowCell, out List<SpawnedResourceRecord> recordsBelow))
                    {
                        recordsBelow = new List<SpawnedResourceRecord>();
                        _resourcesByCell[belowCell] = recordsBelow;
                    }

                    recordsBelow.Add(new SpawnedResourceRecord(
                        record.ResourceType,
                        record.ResourceId,
                        record.Amount,
                        record.ResourceObject,
                        fallTween,
                        record.JitterX));
                    RepositionCellObjects(belowCell);
                    movedAny = true;
                }
            } while (movedAny);

            EnsureDeliveryTasksForAllDropCells();
        }

        /// <summary>
        /// Restores missing dropped-resource delivery tasks for all cells that currently contain drops.
        /// This protects against edge cases where task mapping was canceled during falls/merges.
        /// </summary>
        private void EnsureDeliveryTasksForAllDropCells()
        {
            foreach (KeyValuePair<Vector2Int, List<SpawnedResourceRecord>> pair in _resourcesByCell)
            {
                Vector2Int cell = pair.Key;
                List<SpawnedResourceRecord> records = pair.Value;
                if (records == null || records.Count == 0)
                {
                    continue;
                }

                SpawnedResourceRecord top = records[records.Count - 1];
                if (string.IsNullOrWhiteSpace(top.ResourceId) || top.Amount <= 0)
                {
                    continue;
                }

                _taskBoard.TryEnsureDroppedResourceDeliveryTask(
                    cell,
                    top.ResourceId,
                    top.Amount,
                    Time.frameCount);
            }
        }

        private bool CanFallToCell(Vector2Int targetCell)
        {
            if (!_gridState.IsInside(targetCell.x, targetCell.y))
            {
                return false;
            }

            Cell target = _gridState.GetCell(targetCell.x, targetCell.y);
            if (_supportCellTypes.Contains(target.Type))
            {
                return false;
            }

            if (target.BuildObjectType.HasValue && _supportBuildObjectTypes.Contains(target.BuildObjectType.Value))
            {
                return false;
            }

            return true;
        }

        private bool TryFindNearbyFreeCell(Vector2Int origin, out Vector2Int freeCell)
        {
            Vector2Int[] offsets =
            {
                new Vector2Int(1, 0),
                new Vector2Int(-1, 0),
                new Vector2Int(0, 1),
                new Vector2Int(1, 1),
                new Vector2Int(-1, 1),
                new Vector2Int(2, 0),
                new Vector2Int(-2, 0),
                new Vector2Int(0, 2)
            };

            for (int i = 0; i < offsets.Length; i++)
            {
                Vector2Int candidate = origin + offsets[i];
                if (!_gridState.IsInside(candidate.x, candidate.y))
                {
                    continue;
                }

                freeCell = candidate;
                return true;
            }

            freeCell = origin;
            return false;
        }

        private void MoveObjectToCellRestPosition(GameObject resourceObject, Vector2Int cell, float jitterX)
        {
            Vector2 restPos = GetCellRestWorldPosition(resourceObject, cell, jitterX);
            resourceObject.transform.position = new Vector3(restPos.x, restPos.y, resourceObject.transform.position.z);
        }

        private Vector2 GetCellRestWorldPosition(GameObject resourceObject, Vector2Int cell, float jitterX)
        {
            Vector2 center = _gridCoordinateConverter.CellToWorldCenter(cell);
            float bottomY = center.y - (_gridState.CellSize * 0.5f);
            float halfHeight = GetObjectHalfHeight(resourceObject);
            return new Vector2(center.x + jitterX, bottomY + halfHeight);
        }

        private static float GetObjectHalfHeight(GameObject resourceObject)
        {
            if (resourceObject == null)
            {
                return 0f;
            }

            CircleCollider2D circle = resourceObject.GetComponent<CircleCollider2D>();
            if (circle != null)
            {
                float scaleY = Mathf.Abs(circle.transform.lossyScale.y);
                return circle.radius * Mathf.Max(0.0001f, scaleY);
            }

            Renderer renderer = resourceObject.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                return renderer.bounds.extents.y;
            }

            return 0f;
        }

        private bool TryStackIntoCell(Vector2Int fromCell, Vector2Int toCell, SpawnedResourceRecord movingRecord)
        {
            if (!_resourcesByCell.TryGetValue(toCell, out List<SpawnedResourceRecord> recordsAtTarget) || recordsAtTarget.Count == 0)
            {
                return false;
            }

            SpawnedResourceRecord targetRecord = recordsAtTarget[recordsAtTarget.Count - 1];
            if (!CanStack(targetRecord, movingRecord.ResourceType, movingRecord.ResourceId))
            {
                return false;
            }

            int mergedAmount = targetRecord.Amount + movingRecord.Amount;
            var targetView = targetRecord.ResourceObject != null
                ? targetRecord.ResourceObject.GetComponent<SceneResourceDropView>()
                : null;
            targetView?.AddAmount(movingRecord.Amount);
            recordsAtTarget[recordsAtTarget.Count - 1] = new SpawnedResourceRecord(
                targetRecord.ResourceType,
                targetRecord.ResourceId,
                mergedAmount,
                targetRecord.ResourceObject,
                targetRecord.FallTween,
                targetRecord.JitterX);

            if (movingRecord.ResourceObject != null)
            {
                movingRecord.FallTween?.Kill();
                UnityEngine.Object.Destroy(movingRecord.ResourceObject);
            }

            if (_resourcesByCell.TryGetValue(fromCell, out List<SpawnedResourceRecord> fromRecords))
            {
                for (int i = fromRecords.Count - 1; i >= 0; i--)
                {
                    if (fromRecords[i].ResourceObject == movingRecord.ResourceObject)
                    {
                        fromRecords.RemoveAt(i);
                        break;
                    }
                }

                if (fromRecords.Count == 0)
                {
                    _resourcesByCell.Remove(fromCell);
                }
                else
                {
                    RepositionCellObjects(fromCell);
                }
            }

            _taskBoard.TryGetTaskByCell(fromCell, out UnitTaskRecord fromTask);
            if (fromTask != null && fromTask.TaskType == UnitTaskType.DeliverDroppedResource)
            {
                _taskBoard.CancelTaskByCell(fromCell, out _, out _);
            }

            return true;
        }

        private void RepositionCellObjects(Vector2Int cell)
        {
            if (!_resourcesByCell.TryGetValue(cell, out List<SpawnedResourceRecord> records) || records.Count == 0)
            {
                return;
            }

            Vector2 center = _gridCoordinateConverter.CellToWorldCenter(cell);
            float bottomY = center.y - (_gridState.CellSize * 0.5f);
            float slotX = _gridState.CellSize * 0.18f;
            float slotY = _gridState.CellSize * 0.28f;
            float extraStackGap = _gridState.CellSize * 0.08f;

            for (int i = 0; i < records.Count; i++)
            {
                SpawnedResourceRecord record = records[i];
                if (record.ResourceObject == null) continue;

                float halfHeight = GetObjectHalfHeight(record.ResourceObject);
                float x;
                float y;
                float jitter = record.JitterX * 0.15f;

                // First 4 objects are placed in a 2x2 local layout within the same cell.
                if (i < 4)
                {
                    switch (i)
                    {
                        case 0:
                            x = center.x - slotX + jitter;
                            y = bottomY + halfHeight;
                            break;
                        case 1:
                            x = center.x + slotX + jitter;
                            y = bottomY + halfHeight;
                            break;
                        case 2:
                            x = center.x - slotX + jitter;
                            y = bottomY + halfHeight + slotY;
                            break;
                        default:
                            x = center.x + slotX + jitter;
                            y = bottomY + halfHeight + slotY;
                            break;
                    }
                }
                else
                {
                    // Additional objects are stacked above the 2x2 block.
                    int extraIndex = i - 4;
                    int extraRow = extraIndex / 2;
                    bool left = (extraIndex % 2) == 0;
                    x = center.x + (left ? -slotX : slotX) + jitter;
                    y = bottomY + halfHeight + (slotY * 2f) + (extraRow * ((halfHeight * 2f) + extraStackGap));
                }

                record.ResourceObject.transform.position = new Vector3(x, y, record.ResourceObject.transform.position.z);
            }
        }

        private static bool CanStack(SpawnedResourceRecord existingRecord, SceneResourceType resourceType, string resourceId)
        {
            return existingRecord.ResourceType == resourceType
                   && string.Equals(existingRecord.ResourceId, resourceId, StringComparison.OrdinalIgnoreCase);
        }

        private float CalculateStableJitterX(Vector2Int cell, SceneResourceType resourceType, string resourceId)
        {
            int hash = cell.x * 73856093 ^ cell.y * 19349663 ^ (int)resourceType * 83492791;
            if (!string.IsNullOrWhiteSpace(resourceId))
            {
                hash ^= resourceId.GetHashCode();
            }

            float normalized = Mathf.Abs(hash % 1000) / 1000f;
            float signed = (normalized * 2f) - 1f;
            float maxOffset = _gridState.CellSize * _horizontalJitterFactor;
            return signed * maxOffset;
        }

        private struct SpawnedResourceRecord
        {
            public readonly SceneResourceType ResourceType;
            public readonly string ResourceId;
            public readonly int Amount;
            public readonly GameObject ResourceObject;
            public readonly Tween FallTween;
            public readonly float JitterX;

            public SpawnedResourceRecord(
                SceneResourceType resourceType,
                string resourceId,
                int amount,
                GameObject resourceObject,
                Tween fallTween,
                float jitterX)
            {
                ResourceType = resourceType;
                ResourceId = resourceId;
                Amount = amount;
                ResourceObject = resourceObject;
                FallTween = fallTween;
                JitterX = jitterX;
            }
        }
    }
}
