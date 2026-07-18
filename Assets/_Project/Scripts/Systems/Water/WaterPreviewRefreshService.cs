using System.Collections.Generic;
using _Project.Scripts.Data.Grid;
using _Project.Scripts.Data.Tasks;
using _Project.Scripts.Presentation.Grid;
using _Project.Scripts.Systems.Tasks;
using UnityEngine;

namespace _Project.Scripts.Systems.Water
{
    /// <summary>
    /// Отвечает за пересчёт и отрисовку preview-кабелей (mask/shape/rotation).
    /// </summary>
    public sealed class WaterPreviewRefreshService
    {
        private readonly GridState _gridState;
        private readonly GridTileVisualService _gridTileVisualService;
        private readonly GlobalTaskBoardService _globalTaskBoardService;

        public WaterPreviewRefreshService(
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
            if (!HasPlannedWaterAt(cellPos, stagedPreviewCells)) return;

            byte mask = ComputePreviewWaterMask(cellPos, stagedPreviewCells);
            Cell cell = _gridState.GetCell(cellPos.x, cellPos.y);
            WaterVisualResolver.ResolveVisualDebug(mask, out WaterVisualShapeId shapeId, out float rotationZ, out _);
            cell.WaterVisualShapeId = (byte)shapeId;
            cell.WaterRotationZ = rotationZ;
            cell.IsWaterPreviewVisible = true;
            cell.WaterPreviewMask4 = mask;
            cell.WaterPreviewShapeId = (byte)shapeId;
            cell.WaterPreviewRotationZ = rotationZ;
            _gridState.SetCell(cellPos.x, cellPos.y, cell);
            _gridTileVisualService.SetWaterPreview(cellPos, true, mask);
        }

        public void HideIfNotPlanned(Vector2Int cellPos, HashSet<Vector2Int> stagedPreviewCells)
        {
            if (!_gridState.IsInside(cellPos.x, cellPos.y)) return;

            if (HasPlannedWaterAt(cellPos, stagedPreviewCells))
            {
                RefreshAt(cellPos, stagedPreviewCells);
                return;
            }

            ClearCellState(cellPos);
            _gridTileVisualService.SetWaterPreview(cellPos, false, 0);
        }

        public void ClearCellState(Vector2Int cellPos)
        {
            if (!_gridState.IsInside(cellPos.x, cellPos.y)) return;

            Cell cell = _gridState.GetCell(cellPos.x, cellPos.y);
            cell.IsWaterPreviewVisible = false;
            cell.WaterPreviewMask4 = 0;
            cell.WaterPreviewShapeId = 0;
            cell.WaterPreviewRotationZ = 0f;
            _gridState.SetCell(cellPos.x, cellPos.y, cell);
        }

        public void RebuildAllPlanned(HashSet<Vector2Int> stagedPreviewCells)
        {
            var stagedSnapshot = new HashSet<Vector2Int>(stagedPreviewCells);
            _gridTileVisualService.ClearWaterPreview();
            stagedPreviewCells.Clear();

            foreach (Vector2Int stagedCell in stagedSnapshot)
            {
                if (!_gridState.IsInside(stagedCell.x, stagedCell.y)) continue;
                Cell stagedGridCell = _gridState.GetCell(stagedCell.x, stagedCell.y);
                if (stagedGridCell.HasWater) continue;
                stagedPreviewCells.Add(stagedCell);
            }

            for (int y = 0; y < _gridState.Height; y++)
            {
                for (int x = 0; x < _gridState.Width; x++)
                {
                    Vector2Int cellPos = new Vector2Int(x, y);
                    Cell markedCell = _gridState.GetCell(x, y);
                    if (!markedCell.IsWaterMarked || markedCell.HasWater)
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
                if (task == null || task.TaskType != UnitTaskType.BuildWater) continue;

                Vector2Int cell = task.TargetCell;
                if (!_gridState.IsInside(cell.x, cell.y)) continue;
                Cell taskCell = _gridState.GetCell(cell.x, cell.y);
                if (taskCell.HasWater) continue;
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
            if (!cell.HasWater)
            {
                cell.WaterMask4 = 0;
                cell.WaterVisualShapeId = 0;
                cell.WaterRotationZ = 0f;
                cell.WaterBuiltShapeId = 0;
                cell.WaterBuiltRotationZ = 0f;
                cell.WaterPreviewMask4 = 0;
                _gridState.SetCell(cellPos.x, cellPos.y, cell);
                _gridTileVisualService.SetWaterBuilt(cellPos, false, 0);
                return;
            }

            byte mask = ComputeBuiltWaterMask(cellPos);
            WaterVisualResolver.ResolveVisualDebug(mask, out WaterVisualShapeId shapeId, out float rotationZ, out _);
            cell.WaterMask4 = mask;
            cell.WaterVisualShapeId = (byte)shapeId;
            cell.WaterRotationZ = rotationZ;
            cell.WaterBuiltShapeId = (byte)shapeId;
            cell.WaterBuiltRotationZ = rotationZ;
            _gridState.SetCell(cellPos.x, cellPos.y, cell);
            _gridTileVisualService.SetWaterBuilt(cellPos, true, mask);
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

        public byte ComputePreviewWaterMask(Vector2Int cellPos, HashSet<Vector2Int> stagedPreviewCells)
        {
            byte mask = 0;
            if (HasWaterOrPlannedAt(cellPos + Vector2Int.up, stagedPreviewCells)) mask |= 1;
            if (HasWaterOrPlannedAt(cellPos + Vector2Int.right, stagedPreviewCells)) mask |= 2;
            if (HasWaterOrPlannedAt(cellPos + Vector2Int.down, stagedPreviewCells)) mask |= 4;
            if (HasWaterOrPlannedAt(cellPos + Vector2Int.left, stagedPreviewCells)) mask |= 8;
            return mask;
        }

        public bool HasPlannedWaterAt(Vector2Int cellPos, HashSet<Vector2Int> stagedPreviewCells)
        {
            if (!_gridState.IsInside(cellPos.x, cellPos.y)) return false;
            Cell cell = _gridState.GetCell(cellPos.x, cellPos.y);
            if (cell.HasWater) return false;
            if (stagedPreviewCells.Contains(cellPos)) return true;
            if (cell.IsWaterMarked) return true;

            return _globalTaskBoardService.TryGetWaterTaskByCell(cellPos, out UnitTaskRecord task)
                   && task != null
                   && task.TaskType == UnitTaskType.BuildWater
                   && task.Status != UnitTaskStatus.Completed
                   && task.Status != UnitTaskStatus.Failed;
        }

        private bool HasWaterOrPlannedAt(Vector2Int cellPos, HashSet<Vector2Int> stagedPreviewCells)
        {
            if (!_gridState.IsInside(cellPos.x, cellPos.y)) return false;
            Cell cell = _gridState.GetCell(cellPos.x, cellPos.y);
            if (cell.HasWater) return true;
            return HasPlannedWaterAt(cellPos, stagedPreviewCells);
        }

        private byte ComputeBuiltWaterMask(Vector2Int cellPos)
        {
            byte mask = 0;
            if (HasBuiltWaterAt(cellPos + Vector2Int.up)) mask |= 1;
            if (HasBuiltWaterAt(cellPos + Vector2Int.right)) mask |= 2;
            if (HasBuiltWaterAt(cellPos + Vector2Int.down)) mask |= 4;
            if (HasBuiltWaterAt(cellPos + Vector2Int.left)) mask |= 8;
            return mask;
        }

        private bool HasBuiltWaterAt(Vector2Int cellPos)
        {
            if (!_gridState.IsInside(cellPos.x, cellPos.y)) return false;
            return _gridState.GetCell(cellPos.x, cellPos.y).HasWater;
        }
    }
}
