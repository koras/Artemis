using System.Collections.Generic;
using _Project.Scripts.Data.Construction;
using _Project.Scripts.Data.Grid;
using _Project.Scripts.Data.Pathfinding;
using UnityEngine;

namespace _Project.Scripts.Systems.Pathfinding
{
    /// <summary>
    /// Generates movement edges (core can/cannot move rules).
    /// </summary>
    public sealed class ActionGraphProvider
    {
        private static readonly List<MovementActionEdge> _movementActionEdgesBuffer = new(16);

        private const float WalkCost = 1f;
        private const float JumpGapCost = 1.15f;
        private const float FallCost = 1.1f;
        private const float ClimbCost = 1.2f;
        private const float DigCost = 8f;
        private const float BuildLadderCost = 10f;

        public List<MovementActionEdge> BuildEdges(GridState grid, Vector2Int from, int unitId)
        {
            _movementActionEdgesBuffer.Clear();

            if (!grid.IsInside(from.x, from.y))
            {
                return _movementActionEdgesBuffer;
            }

            GetLocalAxes(grid.GetCell(from.x, from.y), out var up, out var down, out var right, out var left);

            TryWalk(grid, from, right, down, _movementActionEdgesBuffer);
            TryWalk(grid, from, left, down, _movementActionEdgesBuffer);
            TryJumpGap(grid, from, right, down, _movementActionEdgesBuffer);
            TryJumpGap(grid, from, left, down, _movementActionEdgesBuffer);

            TryClimb(grid, from, right, left, up, down, _movementActionEdgesBuffer);
            TryStepUpOntoSupport(grid, from, up, right, down, _movementActionEdgesBuffer);
            TryStepUpOntoSupport(grid, from, up, left, down, _movementActionEdgesBuffer);
            TryStepDownFromSupport(grid, from, right, down, _movementActionEdgesBuffer);
            TryStepDownFromSupport(grid, from, left, down, _movementActionEdgesBuffer);

            TryFall(grid, from, down, _movementActionEdgesBuffer);

            TryDig(grid, from, down, unitId, _movementActionEdgesBuffer);
            TryBuildLadder(grid, from, down, unitId, _movementActionEdgesBuffer);

            return _movementActionEdgesBuffer;
        }

        /// <summary>
        /// Debug helper for movement occupancy.
        /// true = cell is occupiable, false = blocked.
        /// </summary>
        public static bool CanOccupyCellForMovement(GridState grid, Vector2Int cellPos)
        {
            if (grid == null || !grid.IsInside(cellPos.x, cellPos.y))
            {
                return false;
            }

            Vector2Int down = MovementSupportRules.GetDownDirection(grid.GetCell(cellPos.x, cellPos.y));
            return MovementSupportRules.IsCellStandableForMovement(grid, cellPos, down);
        }

        private static void TryWalk(GridState grid, Vector2Int from, Vector2Int side, Vector2Int down,
            List<MovementActionEdge> outEdges)
        {
            Vector2Int to = from + side;
            if (!grid.IsInside(to.x, to.y))
            {
                return;
            }

            if (!MovementSupportRules.IsCellStandableForMovement(grid, to, down))
            {
                return;
            }

            outEdges.Add(new MovementActionEdge(from, to, MovementActionType.Walk, WalkCost));
        }

        private static void TryJumpGap(GridState grid, Vector2Int from, Vector2Int side, Vector2Int down,
            List<MovementActionEdge> outEdges)
        {
            Vector2Int middlePos = from + side;
            Vector2Int targetPos = from + side + side;

            if (!grid.IsInside(middlePos.x, middlePos.y) || !grid.IsInside(targetPos.x, targetPos.y))
            {
                return;
            }

            Cell fromCell = grid.GetCell(from.x, from.y);
            Cell targetCell = grid.GetCell(targetPos.x, targetPos.y);
            if (IsBuiltLifeModuleCell(fromCell) || IsBuiltLifeModuleCell(targetCell))
            {
                return;
            }

            Cell middleCell = grid.GetCell(middlePos.x, middlePos.y);
            if (!IsAirCell(middleCell))
            {
                return;
            }

            if (!MovementSupportRules.IsCellStandableForMovement(grid, targetPos, down))
            {
                return;
            }

            outEdges.Add(new MovementActionEdge(from, targetPos, MovementActionType.JumpGap1, JumpGapCost));
        }

        private static void TryFall(GridState grid, Vector2Int from, Vector2Int down, List<MovementActionEdge> outEdges)
        {
            Vector2Int to = from + down;
            if (!grid.IsInside(to.x, to.y))
            {
                return;
            }

            Cell fromCell = grid.GetCell(from.x, from.y);
            Cell toCell = grid.GetCell(to.x, to.y);
            if (IsBuiltLifeModuleCell(fromCell) || IsBuiltLifeModuleCell(toCell))
            {
                return;
            }

            if (IsLadderCell(fromCell) && IsLadderCell(toCell))
            {
                return;
            }

            if (IsLadderCell(fromCell) && !toCell.BuildObjectType.HasValue)
            {
                return;
            }

            if (!MovementSupportRules.IsCellStandableForMovement(grid, to, down))
            {
                return;
            }

            outEdges.Add(new MovementActionEdge(from, to, MovementActionType.Fall, FallCost));
        }

        private static void TryClimb(GridState grid, Vector2Int from, Vector2Int right, Vector2Int left, Vector2Int up,
            Vector2Int down, List<MovementActionEdge> outEdges)
        {
            Cell fromCell = grid.GetCell(from.x, from.y);
            Vector2Int upPos = from + up;

            if (grid.IsInside(upPos.x, upPos.y))
            {
                Cell upCell = grid.GetCell(upPos.x, upPos.y);
                bool blocksLifeModuleVerticalTransition =
                    IsBuiltLifeModuleCell(fromCell) || IsBuiltLifeModuleCell(upCell);
                if (!blocksLifeModuleVerticalTransition && IsLadderCell(upCell))
                {
                    outEdges.Add(new MovementActionEdge(from, upPos, MovementActionType.ClimbLadder, ClimbCost));
                }
                else if (!blocksLifeModuleVerticalTransition
                         && IsLadderCell(fromCell)
                         && MovementSupportRules.IsCellStandableForMovement(grid, upPos, down))
                {
                    outEdges.Add(new MovementActionEdge(from, upPos, MovementActionType.ClimbLadder, ClimbCost));
                }
            }

            if (IsLadderCell(fromCell))
            {
                Vector2Int downPos = from + down;
                if (grid.IsInside(downPos.x, downPos.y))
                {
                    Cell downCell = grid.GetCell(downPos.x, downPos.y);
                    if (IsBuiltLifeModuleCell(downCell))
                    {
                        return;
                    }

                    if (IsLadderCell(downCell))
                    {
                        outEdges.Add(new MovementActionEdge(from, downPos, MovementActionType.ClimbLadder, ClimbCost));
                    }
                }
            }
        }

        private static void TryStepUpOntoSupport(
            GridState grid,
            Vector2Int from,
            Vector2Int up,
            Vector2Int side,
            Vector2Int down,
            List<MovementActionEdge> outEdges)
        {
            Vector2Int upPos = from + up;
            Vector2Int sidePos = from + side;
            Vector2Int targetPos = from + up + side;

            if (!grid.IsInside(upPos.x, upPos.y) || !grid.IsInside(sidePos.x, sidePos.y) ||
                !grid.IsInside(targetPos.x, targetPos.y))
            {
                return;
            }

            Cell fromCell = grid.GetCell(from.x, from.y);
            Cell upCell = grid.GetCell(upPos.x, upPos.y);
            Cell sideCell = grid.GetCell(sidePos.x, sidePos.y);
            Cell targetCell = grid.GetCell(targetPos.x, targetPos.y);
            if (IsBuiltLifeModuleCell(fromCell)
                || IsBuiltLifeModuleCell(upCell)
                || IsBuiltLifeModuleCell(sideCell)
                || IsBuiltLifeModuleCell(targetCell))
            {
                return;
            }

            // This edge is the narrow "climb out of a one-cell pit / onto one-cell ledge" case.
            // It keeps the old escape behavior without reintroducing free diagonal walking or diagonal falling.
            if (!IsAirCell(upCell) || !IsAirCell(targetCell))
            {
                return;
            }

            if (IsLadderCell(fromCell) || IsLadderCell(sideCell) || IsLadderCell(targetCell))
            {
                return;
            }

            if (IsAirCell(sideCell))
            {
                return;
            }

            if (!MovementSupportRules.IsCellStandableForMovement(grid, targetPos, down))
            {
                return;
            }

            outEdges.Add(new MovementActionEdge(from, targetPos, MovementActionType.JumpUp1, ClimbCost));
        }

        private static void TryStepDownFromSupport(
            GridState grid,
            Vector2Int from,
            Vector2Int side,
            Vector2Int down,
            List<MovementActionEdge> outEdges)
        {
            Vector2Int sidePos = from + side;
            Vector2Int targetPos = from + side + down;
            if (!grid.IsInside(sidePos.x, sidePos.y) || !grid.IsInside(targetPos.x, targetPos.y))
            {
                return;
            }

            Cell fromCell = grid.GetCell(from.x, from.y);
            Cell sideCell = grid.GetCell(sidePos.x, sidePos.y);
            Cell targetCell = grid.GetCell(targetPos.x, targetPos.y);
            if (IsBuiltLifeModuleCell(fromCell)
                || IsBuiltLifeModuleCell(sideCell)
                || IsBuiltLifeModuleCell(targetCell))
            {
                return;
            }

            // This is the narrow "step off a ledge into the first lower corridor cell" case.
            // It restores pathing into multi-cell trenches without reopening free diagonal routing.
            if (IsLadderCell(fromCell) || IsLadderCell(sideCell) || IsLadderCell(targetCell))
            {
                return;
            }

            if (!MovementSupportRules.HasSupportForStanding(grid, from, down))
            {
                return;
            }

            if (!IsAirCell(sideCell))
            {
                return;
            }

            if (!MovementSupportRules.IsCellStandableForMovement(grid, targetPos, down))
            {
                return;
            }

            outEdges.Add(new MovementActionEdge(from, targetPos, MovementActionType.Fall, FallCost));
        }

        private static void TryDig(GridState grid, Vector2Int from, Vector2Int down, int unitId,
            List<MovementActionEdge> outEdges)
        {
            Vector2Int target = from + down;
            if (!grid.IsInside(target.x, target.y))
            {
                return;
            }

            Cell cell = grid.GetCell(target.x, target.y);
            if (!CellTraversalRules.IsDiggable(cell.Type))
            {
                return;
            }

            if (cell.ReservedByUnitId != 0 && cell.ReservedByUnitId != unitId)
            {
                return;
            }

            outEdges.Add(new MovementActionEdge(from, from, MovementActionType.Dig, DigCost));
        }

        private static void TryBuildLadder(GridState grid, Vector2Int from, Vector2Int down, int unitId,
            List<MovementActionEdge> outEdges)
        {
            Vector2Int target = from + down;
            if (!grid.IsInside(target.x, target.y))
            {
                return;
            }

            Cell cell = grid.GetCell(target.x, target.y);
            if (!CellTraversalRules.IsBuildable(cell.Type))
            {
                return;
            }

            if (cell.ReservedByUnitId != 0 && cell.ReservedByUnitId != unitId)
            {
                return;
            }

            outEdges.Add(new MovementActionEdge(from, from, MovementActionType.BuildLadder, BuildLadderCost));
        }

        private static void GetLocalAxes(Cell cell, out Vector2Int up, out Vector2Int down, out Vector2Int right,
            out Vector2Int left)
        {
            down = MovementSupportRules.GetDownDirection(cell);
            up = -down;
            right = new Vector2Int(down.y, -down.x);
            left = -right;
        }

        private static bool IsAirCell(Cell cell)
        {
            return MovementSupportRules.IsAirCell(cell);
        }

        /// <summary>
        /// In pathfinding context, IgnoreObstacleForPathfinding is treated as Empty.
        /// </summary>
        private static CellType GetMovementCellType(Cell cell)
        {
            return MovementSupportRules.GetMovementCellType(cell);
        }

        private static bool IsLadderCell(Cell cell)
        {
            return MovementSupportRules.IsLadderCell(cell);
        }

        private static bool IsBuiltLifeModuleCell(Cell cell)
        {
            return cell.LifeModuleType == LifeModuleType.Built;
        }
    }
}