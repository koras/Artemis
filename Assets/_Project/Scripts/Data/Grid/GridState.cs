using System;
using UnityEngine;

namespace _Project.Scripts.Data.Grid
{
    /// <summary>
    /// Состояние всей сетки.
    /// </summary>
    public sealed class GridState
    {
        public event Action<Vector2Int, Cell, Cell> CellChanged;

        public int Width { get; }
        public int Height { get; }

        /// <summary>
        /// Changes only when cell data used by movement graph generation changes.
        /// </summary>
        public int NavigationRevision { get; private set; }

        /// <summary>
        /// Changes when any cell data changes, including presentation-only life-module fields.
        /// </summary>
        public int CellRevision { get; private set; }

        public  int CellSize { get; } // размер одной ячейки (например 1f)
        
        
        // Храним в 1D-массиве для скорости и простой памяти.
        private readonly Cell[] _cells;

        public GridState(int width, int height,  int cellSize)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));

            CellSize = cellSize;
            Width = width;
            Height = height;
            _cells = new Cell[width * height];
        }

        public int GetIndex(int x, int y)
        {
            return y * Width + x;
        }

        public bool IsInside(int x, int y)
        {
            return x >= 0 && x < Width && y >= 0 && y < Height;
        }

        public Cell GetCell(int x, int y)
        {
            if (!IsInside(x, y))
            {
                throw new ArgumentOutOfRangeException($"Cell ({x},{y}) out of bounds.");
            }

            return _cells[GetIndex(x, y)];
        }

        public void SetCell(int x, int y, Cell cell)
        {
            if (!IsInside(x, y))
            {
                throw new ArgumentOutOfRangeException($"Cell ({x},{y}) out of bounds.");
            }

            int index = GetIndex(x, y);
            Cell previousCell = _cells[index];
            _cells[index] = cell;

            if (!AreNavigationCellsEqual(previousCell, cell))
            {
                NavigationRevision++;
            }

            if (!AreCellsEqual(previousCell, cell))
            {
                CellRevision++;
                CellChanged?.Invoke(new Vector2Int(x, y), previousCell, cell);
            }
        }

        public Span<Cell> GetRawCells()
        {
            // Даёт быстрый доступ для системного пересчёта.
            return _cells;
        }

        private static bool AreCellsEqual(Cell left, Cell right)
        {
            return left.IsDigMarked == right.IsDigMarked
                   && left.Type == right.Type
                   && left.ResourceAmount == right.ResourceAmount
                   && left.BuildObjectType == right.BuildObjectType
                   && left.IsOccupiedByBuilding == right.IsOccupiedByBuilding
                   && left.Temperature.Equals(right.Temperature)
                   && left.IgnoreObstacleForPathfinding == right.IgnoreObstacleForPathfinding
                   && left.GravityVector == right.GravityVector
                   && left.GravityMagnitude.Equals(right.GravityMagnitude)
                   && left.ReservedByUnitId == right.ReservedByUnitId
                   && left.IsCableMarked == right.IsCableMarked
                   && left.HasCable == right.HasCable
                   && left.CableMask4 == right.CableMask4
                   && left.CableNetworkId == right.CableNetworkId
                   && left.CableVisualShapeId == right.CableVisualShapeId
                   && left.CableRotationZ.Equals(right.CableRotationZ)
                   && left.CableBuiltShapeId == right.CableBuiltShapeId
                   && left.CableBuiltRotationZ.Equals(right.CableBuiltRotationZ)
                   && left.IsCablePreviewVisible == right.IsCablePreviewVisible
                   && left.CablePreviewMask4 == right.CablePreviewMask4
                   && left.CablePreviewShapeId == right.CablePreviewShapeId
                   && left.CablePreviewRotationZ.Equals(right.CablePreviewRotationZ)
                   && left.IsWaterMarked == right.IsWaterMarked
                   && left.HasWater == right.HasWater
                   && left.WaterMask4 == right.WaterMask4
                   && left.WaterNetworkId == right.WaterNetworkId
                   && left.WaterVisualShapeId == right.WaterVisualShapeId
                   && left.WaterRotationZ.Equals(right.WaterRotationZ)
                   && left.WaterBuiltShapeId == right.WaterBuiltShapeId
                   && left.WaterBuiltRotationZ.Equals(right.WaterBuiltRotationZ)
                   && left.IsWaterPreviewVisible == right.IsWaterPreviewVisible
                   && left.WaterPreviewMask4 == right.WaterPreviewMask4
                   && left.WaterPreviewShapeId == right.WaterPreviewShapeId
                   && left.WaterPreviewRotationZ.Equals(right.WaterPreviewRotationZ)
                   && left.IsOxygenMarked == right.IsOxygenMarked
                   && left.HasOxygen == right.HasOxygen
                   && left.OxygenMask4 == right.OxygenMask4
                   && left.OxygenNetworkId == right.OxygenNetworkId
                   && left.OxygenVisualShapeId == right.OxygenVisualShapeId
                   && left.OxygenRotationZ.Equals(right.OxygenRotationZ)
                   && left.OxygenBuiltShapeId == right.OxygenBuiltShapeId
                   && left.OxygenBuiltRotationZ.Equals(right.OxygenBuiltRotationZ)
                   && left.IsOxygenPreviewVisible == right.IsOxygenPreviewVisible
                   && left.OxygenPreviewMask4 == right.OxygenPreviewMask4
                   && left.OxygenPreviewShapeId == right.OxygenPreviewShapeId
                   && left.OxygenPreviewRotationZ.Equals(right.OxygenPreviewRotationZ)
                   && left.LifeModuleType == right.LifeModuleType
                   && left.LifeModulePartType == right.LifeModulePartType
                   && left.LifeModuleGroupId == right.LifeModuleGroupId
                   && left.LifeModulePartWidth == right.LifeModulePartWidth
                   && left.LifeModulePartOrder == right.LifeModulePartOrder
                   && left.IsLifeModulePartAnchor == right.IsLifeModulePartAnchor;
        }

        private static bool AreNavigationCellsEqual(Cell left, Cell right)
        {
            return left.Type == right.Type
                   && left.BuildObjectType == right.BuildObjectType
                   && left.IsOccupiedByBuilding == right.IsOccupiedByBuilding
                   && left.IgnoreObstacleForPathfinding == right.IgnoreObstacleForPathfinding
                   && left.GravityVector == right.GravityVector
                   && left.LifeModuleType == right.LifeModuleType
                   && left.LifeModuleGroupId == right.LifeModuleGroupId;
        }
    }
}