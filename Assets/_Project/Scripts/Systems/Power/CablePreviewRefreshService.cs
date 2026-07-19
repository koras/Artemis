using System.Collections.Generic;
using _Project.Scripts.Data.Grid;
using _Project.Scripts.Data.Tasks;
using _Project.Scripts.Presentation.Grid;
using _Project.Scripts.Systems.Tasks;
using UnityEngine;

namespace _Project.Scripts.Systems.Power
{
    /// <summary>
    /// Отвечает за пересчёт и отрисовку preview-кабелей (mask/shape/rotation).
    /// </summary>
    public sealed class CablePreviewRefreshService
    {
        private readonly GridState _gridState;
        private readonly GridTileVisualService _gridTileVisualService;
        private readonly GlobalTaskBoardService _globalTaskBoardService;

        public CablePreviewRefreshService(
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
            if (!HasPlannedCableAt(cellPos, stagedPreviewCells)) return;

            byte mask = ComputePreviewCableMask(cellPos, stagedPreviewCells);
            Cell cell = _gridState.GetCell(cellPos.x, cellPos.y);
            CableVisualResolver.ResolveVisualDebug(mask, out CableVisualShapeId shapeId, out float rotationZ, out _);
            cell.CableVisualShapeId = (byte)shapeId;
            cell.CableRotationZ = rotationZ;
            cell.IsCablePreviewVisible = true;
            cell.CablePreviewMask4 = mask;
            cell.CablePreviewShapeId = (byte)shapeId;
            cell.CablePreviewRotationZ = rotationZ;
            _gridState.SetCell(cellPos.x, cellPos.y, cell);
            _gridTileVisualService.SetCablePreview(cellPos, true, mask);
        }

        public void HideIfNotPlanned(Vector2Int cellPos, HashSet<Vector2Int> stagedPreviewCells)
        {
            if (!_gridState.IsInside(cellPos.x, cellPos.y)) return;

            if (HasPlannedCableAt(cellPos, stagedPreviewCells))
            {
                RefreshAt(cellPos, stagedPreviewCells);
                return;
            }

            ClearCellState(cellPos);
            _gridTileVisualService.SetCablePreview(cellPos, false, 0);
        }

        public void ClearCellState(Vector2Int cellPos)
        {
            if (!_gridState.IsInside(cellPos.x, cellPos.y)) return;

            Cell cell = _gridState.GetCell(cellPos.x, cellPos.y);
            cell.IsCablePreviewVisible = false;
            cell.CablePreviewMask4 = 0;
            cell.CablePreviewShapeId = 0;
            cell.CablePreviewRotationZ = 0f;
            _gridState.SetCell(cellPos.x, cellPos.y, cell);
        }

        public void RebuildAllPlanned(HashSet<Vector2Int> stagedPreviewCells)
        {
            var stagedSnapshot = new HashSet<Vector2Int>(stagedPreviewCells);
            _gridTileVisualService.ClearCablePreview();
            stagedPreviewCells.Clear();

            foreach (Vector2Int stagedCell in stagedSnapshot)
            {
                if (!_gridState.IsInside(stagedCell.x, stagedCell.y)) continue;
                Cell stagedGridCell = _gridState.GetCell(stagedCell.x, stagedCell.y);
                if (stagedGridCell.HasCable) continue;
                stagedPreviewCells.Add(stagedCell);
            }

            for (int y = 0; y < _gridState.Height; y++)
            {
                for (int x = 0; x < _gridState.Width; x++)
                {
                    Vector2Int cellPos = new Vector2Int(x, y);
                    Cell markedCell = _gridState.GetCell(x, y);
                    if (!markedCell.IsCableMarked || markedCell.HasCable)
                    {
                        continue;
                    }

                    stagedPreviewCells.Add(cellPos);
                }
            }

            var tasks = _globalTaskBoardService.GetActiveTasksSnapshot();
            for (int i = 0; i < tasks.Count; i++)
            {
                UnitTaskRecord task = tasks[i];
                if (task == null || task.TaskType != UnitTaskType.BuildCable) continue;

                Vector2Int cell = task.TargetCell;
                if (!_gridState.IsInside(cell.x, cell.y)) continue;
                Cell taskCell = _gridState.GetCell(cell.x, cell.y);
                if (taskCell.HasCable) continue;
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
            if (!cell.HasCable)
            {
                cell.CableMask4 = 0;
                cell.CableVisualShapeId = 0;
                cell.CableRotationZ = 0f;
                cell.CableBuiltShapeId = 0;
                cell.CableBuiltRotationZ = 0f;
                _gridState.SetCell(cellPos.x, cellPos.y, cell);
                _gridTileVisualService.SetCableBuilt(cellPos, false, 0);
                return;
            }

            byte mask = ComputeBuiltCableMask(cellPos);
            CableVisualResolver.ResolveVisualDebug(mask, out CableVisualShapeId shapeId, out float rotationZ, out _);
            cell.CableMask4 = mask;
            cell.CableVisualShapeId = (byte)shapeId;
            cell.CableRotationZ = rotationZ;
            cell.CableBuiltShapeId = (byte)shapeId;
            cell.CableBuiltRotationZ = rotationZ;
            _gridState.SetCell(cellPos.x, cellPos.y, cell);
            _gridTileVisualService.SetCableBuilt(cellPos, true, mask);
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

        public byte ComputePreviewCableMask(Vector2Int cellPos, HashSet<Vector2Int> stagedPreviewCells)
        {
            byte mask = 0;
            if (HasCableOrPlannedAt(cellPos + Vector2Int.up, stagedPreviewCells)) mask |= 1;
            if (HasCableOrPlannedAt(cellPos + Vector2Int.right, stagedPreviewCells)) mask |= 2;
            if (HasCableOrPlannedAt(cellPos + Vector2Int.down, stagedPreviewCells)) mask |= 4;
            if (HasCableOrPlannedAt(cellPos + Vector2Int.left, stagedPreviewCells)) mask |= 8;
            return mask;
        }

        public bool HasPlannedCableAt(Vector2Int cellPos, HashSet<Vector2Int> stagedPreviewCells)
        {
            if (!_gridState.IsInside(cellPos.x, cellPos.y)) return false;
            Cell cell = _gridState.GetCell(cellPos.x, cellPos.y);
            if (cell.HasCable) return false;
            if (stagedPreviewCells.Contains(cellPos)) return true;
            if (cell.IsCableMarked) return true;

            return _globalTaskBoardService.TryGetCableTaskByCell(cellPos, out UnitTaskRecord task)
                   && task != null
                   && task.TaskType == UnitTaskType.BuildCable
                   && task.Status != UnitTaskStatus.Completed
                   && task.Status != UnitTaskStatus.Failed;
        }

        private bool HasCableOrPlannedAt(Vector2Int cellPos, HashSet<Vector2Int> stagedPreviewCells)
        {
            if (!_gridState.IsInside(cellPos.x, cellPos.y)) return false;
            Cell cell = _gridState.GetCell(cellPos.x, cellPos.y);
            if (cell.HasCable) return true;
            return HasPlannedCableAt(cellPos, stagedPreviewCells);
        }

        private byte ComputeBuiltCableMask(Vector2Int cellPos)
        {
            byte mask = 0;
            if (HasBuiltCableAt(cellPos + Vector2Int.up)) mask |= 1;
            if (HasBuiltCableAt(cellPos + Vector2Int.right)) mask |= 2;
            if (HasBuiltCableAt(cellPos + Vector2Int.down)) mask |= 4;
            if (HasBuiltCableAt(cellPos + Vector2Int.left)) mask |= 8;
            return mask;
        }

        private bool HasBuiltCableAt(Vector2Int cellPos)
        {
            if (!_gridState.IsInside(cellPos.x, cellPos.y)) return false;
            return _gridState.GetCell(cellPos.x, cellPos.y).HasCable;
        }
    }
}