using System.Collections.Generic;
using _Project.Scripts.Data.Grid;
using _Project.Scripts.Data.Tasks;
using _Project.Scripts.Presentation.Grid;
using _Project.Scripts.Systems.Tasks;
using UnityEngine;

namespace _Project.Scripts.Systems.Oxygen
{
    /// <summary>
    /// Отвечает за пересчёт и отрисовку preview-кабелей (mask/shape/rotation).
    /// </summary>
    public sealed class OxygenPreviewRefreshService
    {
        private readonly GridState _gridState;
        private readonly GridTileVisualService _gridTileVisualService;
        private readonly GlobalTaskBoardService _globalTaskBoardService;

        public OxygenPreviewRefreshService(
            GridState gridState,
            GridTileVisualService gridTileVisualService,
            GlobalTaskBoardService globalTaskBoardService)
        {
            _gridState = gridState;
            _gridTileVisualService = gridTileVisualService;
            _globalTaskBoardService = globalTaskBoardService;
        }

        public void RefreshAround(Vector2Int center, HashSet<Vector2Int> stagedPreviewCells)
        {
            RefreshAt(center, stagedPreviewCells);
            RefreshAt(center + Vector2Int.up, stagedPreviewCells);
            RefreshAt(center + Vector2Int.right, stagedPreviewCells);
            RefreshAt(center + Vector2Int.down, stagedPreviewCells);
            RefreshAt(center + Vector2Int.left, stagedPreviewCells);
            RefreshAt(center + Vector2Int.up + Vector2Int.right, stagedPreviewCells);
            RefreshAt(center + Vector2Int.up + Vector2Int.left, stagedPreviewCells);
            RefreshAt(center + Vector2Int.down + Vector2Int.right, stagedPreviewCells);
            RefreshAt(center + Vector2Int.down + Vector2Int.left, stagedPreviewCells);
        }

        public void RefreshAt(Vector2Int cellPos, HashSet<Vector2Int> stagedPreviewCells)
        {
            if (!_gridState.IsInside(cellPos.x, cellPos.y)) return;
            if (!HasPlannedOxygenAt(cellPos, stagedPreviewCells)) return;

            byte mask = ComputePreviewOxygenMask(cellPos, stagedPreviewCells);
            Cell cell = _gridState.GetCell(cellPos.x, cellPos.y);
            OxygenVisualResolver.ResolveVisualDebug(mask, out OxygenVisualShapeId shapeId, out float rotationZ, out _);
            cell.OxygenVisualShapeId = (byte)shapeId;
            cell.OxygenRotationZ = rotationZ;
            cell.IsOxygenPreviewVisible = true;
            cell.OxygenPreviewMask4 = mask;
            cell.OxygenPreviewShapeId = (byte)shapeId;
            cell.OxygenPreviewRotationZ = rotationZ;
            _gridState.SetCell(cellPos.x, cellPos.y, cell);
            _gridTileVisualService.SetOxygenPreview(cellPos, true, mask);
        }

        public void HideIfNotPlanned(Vector2Int cellPos, HashSet<Vector2Int> stagedPreviewCells)
        {
            if (!_gridState.IsInside(cellPos.x, cellPos.y)) return;

            if (HasPlannedOxygenAt(cellPos, stagedPreviewCells))
            {
                RefreshAt(cellPos, stagedPreviewCells);
                return;
            }

            ClearCellState(cellPos);
            _gridTileVisualService.SetOxygenPreview(cellPos, false, 0);
        }

        public void ClearCellState(Vector2Int cellPos)
        {
            if (!_gridState.IsInside(cellPos.x, cellPos.y)) return;

            Cell cell = _gridState.GetCell(cellPos.x, cellPos.y);
            cell.IsOxygenPreviewVisible = false;
            cell.OxygenPreviewMask4 = 0;
            cell.OxygenPreviewShapeId = 0;
            cell.OxygenPreviewRotationZ = 0f;
            _gridState.SetCell(cellPos.x, cellPos.y, cell);
        }

        public void RebuildAllPlanned(HashSet<Vector2Int> stagedPreviewCells)
        {
            var stagedSnapshot = new HashSet<Vector2Int>(stagedPreviewCells);
            _gridTileVisualService.ClearOxygenPreview();
            stagedPreviewCells.Clear();

            foreach (Vector2Int stagedCell in stagedSnapshot)
            {
                if (!_gridState.IsInside(stagedCell.x, stagedCell.y)) continue;
                Cell stagedGridCell = _gridState.GetCell(stagedCell.x, stagedCell.y);
                if (stagedGridCell.HasOxygen) continue;
                stagedPreviewCells.Add(stagedCell);
            }

            for (int y = 0; y < _gridState.Height; y++)
            {
                for (int x = 0; x < _gridState.Width; x++)
                {
                    Vector2Int cellPos = new Vector2Int(x, y);
                    Cell markedCell = _gridState.GetCell(x, y);
                    if (!markedCell.IsOxygenMarked || markedCell.HasOxygen)
                    {
                        continue;
                    }

                    stagedPreviewCells.Add(cellPos);
                }
            }

            List<UnitTaskRecord> tasks = _globalTaskBoardService.GetActiveTasksSnapshot();
            for (int i = 0; i < tasks.Count; i++)
            {
                UnitTaskRecord task = tasks[i];
                if (task == null || task.TaskType != UnitTaskType.BuildOxygen) continue;

                Vector2Int cell = task.TargetCell;
                if (!_gridState.IsInside(cell.x, cell.y)) continue;
                Cell taskCell = _gridState.GetCell(cell.x, cell.y);
                if (taskCell.HasOxygen) continue;
                stagedPreviewCells.Add(cell);
            }

            foreach (Vector2Int cell in stagedPreviewCells)
            {
                RefreshAround(cell, stagedPreviewCells);
            }
        }

        public void ReconcileAllPlannedFromTaskBoard()
        {
            var emptyStagedSet = new HashSet<Vector2Int>();
            RebuildAllPlanned(emptyStagedSet);
        }

        public void RefreshBuiltAround(Vector2Int center)
        {
            RefreshBuiltAt(center);
            RefreshBuiltAt(center + Vector2Int.up);
            RefreshBuiltAt(center + Vector2Int.right);
            RefreshBuiltAt(center + Vector2Int.down);
            RefreshBuiltAt(center + Vector2Int.left);
            RefreshBuiltAt(center + Vector2Int.up + Vector2Int.right);
            RefreshBuiltAt(center + Vector2Int.up + Vector2Int.left);
            RefreshBuiltAt(center + Vector2Int.down + Vector2Int.right);
            RefreshBuiltAt(center + Vector2Int.down + Vector2Int.left);
        }

        public void RefreshBuiltAt(Vector2Int cellPos)
        {
            if (!_gridState.IsInside(cellPos.x, cellPos.y)) return;

            Cell cell = _gridState.GetCell(cellPos.x, cellPos.y);
            if (!cell.HasOxygen)
            {
                cell.OxygenMask4 = 0;
                cell.OxygenVisualShapeId = 0;
                cell.OxygenRotationZ = 0f;
                cell.OxygenBuiltShapeId = 0;
                cell.OxygenBuiltRotationZ = 0f;
                cell.OxygenPreviewMask4 = 0;
                _gridState.SetCell(cellPos.x, cellPos.y, cell);
                _gridTileVisualService.SetOxygenBuilt(cellPos, false, 0);
                return;
            }

            byte mask = ComputeBuiltOxygenMask(cellPos);
            OxygenVisualResolver.ResolveVisualDebug(mask, out OxygenVisualShapeId shapeId, out float rotationZ, out _);
            cell.OxygenMask4 = mask;
            cell.OxygenVisualShapeId = (byte)shapeId;
            cell.OxygenRotationZ = rotationZ;
            cell.OxygenBuiltShapeId = (byte)shapeId;
            cell.OxygenBuiltRotationZ = rotationZ;
            _gridState.SetCell(cellPos.x, cellPos.y, cell);
            _gridTileVisualService.SetOxygenBuilt(cellPos, true, mask);
        }

        public void ReconcileAllBuilt()
        {
            for (int y = 0; y < _gridState.Height; y++)
            {
                for (int x = 0; x < _gridState.Width; x++)
                {
                    RefreshBuiltAt(new Vector2Int(x, y));
                }
            }
        }

        public byte ComputePreviewOxygenMask(Vector2Int cellPos, HashSet<Vector2Int> stagedPreviewCells)
        {
            byte mask = 0;
            if (HasOxygenOrPlannedAt(cellPos + Vector2Int.up, stagedPreviewCells)) mask |= 1;
            if (HasOxygenOrPlannedAt(cellPos + Vector2Int.right, stagedPreviewCells)) mask |= 2;
            if (HasOxygenOrPlannedAt(cellPos + Vector2Int.down, stagedPreviewCells)) mask |= 4;
            if (HasOxygenOrPlannedAt(cellPos + Vector2Int.left, stagedPreviewCells)) mask |= 8;
            return mask;
        }

        public bool HasPlannedOxygenAt(Vector2Int cellPos, HashSet<Vector2Int> stagedPreviewCells)
        {
            if (!_gridState.IsInside(cellPos.x, cellPos.y)) return false;
            Cell cell = _gridState.GetCell(cellPos.x, cellPos.y);
            if (cell.HasOxygen) return false;
            if (stagedPreviewCells.Contains(cellPos)) return true;
            if (cell.IsOxygenMarked) return true;

            return _globalTaskBoardService.TryGetOxygenTaskByCell(cellPos, out UnitTaskRecord task)
                   && task != null
                   && task.TaskType == UnitTaskType.BuildOxygen
                   && task.Status != UnitTaskStatus.Completed
                   && task.Status != UnitTaskStatus.Failed;
        }

        private bool HasOxygenOrPlannedAt(Vector2Int cellPos, HashSet<Vector2Int> stagedPreviewCells)
        {
            if (!_gridState.IsInside(cellPos.x, cellPos.y)) return false;
            Cell cell = _gridState.GetCell(cellPos.x, cellPos.y);
            if (cell.HasOxygen) return true;
            return HasPlannedOxygenAt(cellPos, stagedPreviewCells);
        }

        private byte ComputeBuiltOxygenMask(Vector2Int cellPos)
        {
            byte mask = 0;
            if (HasBuiltOxygenAt(cellPos + Vector2Int.up)) mask |= 1;
            if (HasBuiltOxygenAt(cellPos + Vector2Int.right)) mask |= 2;
            if (HasBuiltOxygenAt(cellPos + Vector2Int.down)) mask |= 4;
            if (HasBuiltOxygenAt(cellPos + Vector2Int.left)) mask |= 8;
            return mask;
        }

        private bool HasBuiltOxygenAt(Vector2Int cellPos)
        {
            if (!_gridState.IsInside(cellPos.x, cellPos.y)) return false;
            return _gridState.GetCell(cellPos.x, cellPos.y).HasOxygen;
        }
    }
}
