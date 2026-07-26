using _Project.Scripts.Data.Construction;
using _Project.Scripts.Data.Grid;
using UnityEngine;

namespace _Project.Scripts.Systems.Pathfinding
{
    /// <summary>
    /// Shared support/occupancy rules for movement-related systems.
    /// </summary>
    public static class MovementSupportRules
    {
        public static Vector2Int GetDownDirection(Cell cell)
        {
            Vector2Int gravity = cell.GravityVector;
            if (gravity == Vector2Int.zero)
            {
                return Vector2Int.down;
            }

            return Mathf.Abs(gravity.x) > Mathf.Abs(gravity.y)
                ? new Vector2Int(gravity.x > 0 ? 1 : -1, 0)
                : new Vector2Int(0, gravity.y > 0 ? 1 : -1);
        }

        public static bool IsCellStandableForMovement(GridState grid, Vector2Int cellPos, Vector2Int down)
        {
            ref readonly Cell cell = ref grid.GetCell(cellPos.x, cellPos.y);
            if (IsBuiltLifeModuleBottomRowCell(grid, cellPos, down))
            {
                return true;
            }

            CellType effectiveType = GetMovementCellType(cell);
            if (IsLadderCell(cell))
            {
                // Ladder cell is always occupiable for movement, even when it hangs in air.
                return true;
            }

            // Bridge is only a support platform below the unit, not an occupiable cell.
            if (IsBridgeCell(cell))
            {
                return false;
            }

            if (!CellTraversalRules.IsWalkable(effectiveType))
            {
                return false;
            }

            return HasSupportForStanding(grid, cellPos, down);
        }

        public static bool HasSupportForStanding(GridState grid, Vector2Int cellPos, Vector2Int down)
        {
            if (IsBuiltLifeModuleBottomRowCell(grid, cellPos, down))
            {
                return true;
            }

            Vector2Int supportPos = cellPos + down;
            if (!grid.IsInside(supportPos.x, supportPos.y))
            {
                return false;
            }

            ref readonly Cell supportCell = ref grid.GetCell(supportPos.x, supportPos.y);
            return IsLadderCell(supportCell) || IsBridgeCell(supportCell) || !IsAirCell(supportCell);
        }

        public static bool IsBuiltLifeModuleBottomRowCell(GridState grid, Vector2Int cellPos, Vector2Int down)
        {
            ref readonly Cell cell = ref grid.GetCell(cellPos.x, cellPos.y);
            if (cell.LifeModuleType != LifeModuleType.Built)
            {
                return false;
            }

            Vector2Int localDown = GetDownDirection(cell);
            if (localDown != down)
            {
                return false;
            }

            Vector2Int belowModuleCellPos = cellPos + down;
            if (!grid.IsInside(belowModuleCellPos.x, belowModuleCellPos.y))
            {
                return true;
            }

            ref readonly Cell belowModuleCell = ref grid.GetCell(belowModuleCellPos.x, belowModuleCellPos.y);
            return belowModuleCell.LifeModuleType != LifeModuleType.Built
                   || belowModuleCell.LifeModuleGroupId != cell.LifeModuleGroupId;
        }

        public static bool IsAirCell(Cell cell)
        {
            if (IsBridgeCell(cell))
            {
                return false;
            }

            CellType effectiveType = GetMovementCellType(cell);
            return effectiveType == CellType.Empty || effectiveType == CellType.Atmosphere;
        }

        public static CellType GetMovementCellType(Cell cell)
        {
            return cell.IgnoreObstacleForPathfinding ? CellType.Empty : cell.Type;
        }

        public static bool IsLadderCell(Cell cell)
        {
            return cell.BuildObjectType.HasValue && cell.BuildObjectType.Value == BuildObjectType.Ladder;
        }

        public static bool IsBridgeCell(Cell cell)
        {
            return cell.BuildObjectType.HasValue && cell.BuildObjectType.Value == BuildObjectType.Bridge;
        }
    }
}