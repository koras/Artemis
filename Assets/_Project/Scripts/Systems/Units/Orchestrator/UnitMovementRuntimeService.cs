using System;
using _Project.Scripts.Data.Construction;
using _Project.Scripts.Data.Grid;
using _Project.Scripts.Data.Pathfinding;
using _Project.Scripts.Data.Tasks;
using _Project.Scripts.Systems.Navigation;
using _Project.Scripts.Systems.Pathfinding;
using _Project.Scripts.Systems.Units.Orchestrator;
using UnityEngine;

namespace _Project.Scripts.Systems.Units
{
    public sealed class UnitMovementRuntimeService
    {
        private readonly UnitOrchestratorContext _context;
        private readonly Func<int, int, bool> _isTaskOnCooldown;
        private readonly Action<UnitTaskState> _resetUnitTask;
        private readonly Action<UnitTaskState, UnitTaskRecord> _tryResetLoopingTask;
        private readonly Func<int, Vector2Int, (bool found, Vector2Int storageCell, Vector2Int deliveryCell)> _tryFindNearestStorageDeliveryCell;

        public UnitMovementRuntimeService(
            UnitOrchestratorContext context,
            Func<int, int, bool> isTaskOnCooldown,
            Action<UnitTaskState> resetUnitTask,
            Action<UnitTaskState, UnitTaskRecord> tryResetLoopingTask,
            Func<int, Vector2Int, (bool found, Vector2Int storageCell, Vector2Int deliveryCell)> tryFindNearestStorageDeliveryCell)
        {
            _context = context;
            _isTaskOnCooldown = isTaskOnCooldown;
            _resetUnitTask = resetUnitTask;
            _tryResetLoopingTask = tryResetLoopingTask;
            _tryFindNearestStorageDeliveryCell = tryFindNearestStorageDeliveryCell;
        }

        public void ProcessTaskMoveFrame(
            UnitTaskState state,
            float deltaTime,
            Action<UnitTaskState, Vector2Int, Vector2Int, MovementActionType> syncActorStepPosition)
        {
            if (!_context.TaskBoard.TryGetTask(state.CurrentTaskId, out UnitTaskRecord task))
            {
                _resetUnitTask(state);
                return;
            }

            if (task.Status == UnitTaskStatus.Completed || task.Status == UnitTaskStatus.Failed)
            {
                _resetUnitTask(state);
                return;
            }

            if (task.ReservedByUnitId != state.UnitId)
            {
                _resetUnitTask(state);
                return;
            }

            if (!state.Actor.IsAtMoveTarget())
            {
                return;
            }

            NavigationStepResult stepResult = _context.Navigation.TryStep(
                state.UnitId,
                ref state.CurrentCell,
                state.CurrentGoalCell,
                out Vector2Int fromCell,
                out Vector2Int toCell,
                out MovementActionType actionType);

            if (stepResult == NavigationStepResult.Stepped)
            {
                state.MoveNoProgressSeconds = 0f;
                state.NoProgressTicks = 0;

                Vector2Int moveDirection = toCell - fromCell;
                state.Actor.SetFacing(moveDirection);
                syncActorStepPosition(state, fromCell, toCell, actionType);
                _context.OnUnitCellChanged?.Invoke(state.CurrentCell);
                if (state.State != UnitExecutionState.DeliveringResource)
                {
                    _tryResetLoopingTask(state, task);
                }
                return;
            }

            if (stepResult == NavigationStepResult.Arrived)
            {
                state.MoveNoProgressSeconds = 0f;
                state.NoProgressTicks = 0;
                return;
            }

            state.MoveNoProgressSeconds += deltaTime;
            if (state.MoveNoProgressSeconds >= _context.ManualMoveNoProgressTimeoutSeconds)
            {
                _context.TaskBoard.ReleaseTaskReservation(state.CurrentTaskId, state.UnitId, "no-progress");
                _resetUnitTask(state);
            }
        }

        public void ProcessDeliveryMoveFrame(
            UnitTaskState state,
            float deltaTime,
            Action<UnitTaskState, Vector2Int, Vector2Int, MovementActionType> syncActorStepPosition)
        {
            if (!state.Actor.IsAtMoveTarget())
            {
                return;
            }

            NavigationStepResult stepResult = _context.Navigation.TryStep(
                state.UnitId,
                ref state.CurrentCell,
                state.CurrentGoalCell,
                out Vector2Int fromCell,
                out Vector2Int toCell,
                out MovementActionType actionType);

            if (stepResult == NavigationStepResult.Stepped)
            {
                state.MoveNoProgressSeconds = 0f;
                state.NoProgressTicks = 0;

                Vector2Int moveDirection = toCell - fromCell;
                state.Actor.SetFacing(moveDirection);
                syncActorStepPosition(state, fromCell, toCell, actionType);
                _context.OnUnitCellChanged?.Invoke(state.CurrentCell);
                return;
            }

            if (stepResult == NavigationStepResult.Arrived)
            {
                state.MoveNoProgressSeconds = 0f;
                state.NoProgressTicks = 0;
                return;
            }

            state.MoveNoProgressSeconds += deltaTime;
            if (state.MoveNoProgressSeconds < _context.ManualMoveNoProgressTimeoutSeconds)
            {
                return;
            }

            state.MoveNoProgressSeconds = 0f;
            RefreshDeliveryGoalAfterFall(state);
        }

        public bool TryApplyGravityFall(UnitTaskState state, Action<UnitTaskState> syncActorPosition)
        {
            if (!_context.Grid.IsInside(state.CurrentCell.x, state.CurrentCell.y)) return false;

            ref readonly Cell currentCell = ref _context.Grid.GetCell(state.CurrentCell.x, state.CurrentCell.y);
            if (IsLadderCell(currentCell)) return false;
            if (!IsAirCell(currentCell)) return false;

            Vector2Int down = MovementSupportRules.GetDownDirection(currentCell);
            if (MovementSupportRules.HasSupportForStanding(_context.Grid, state.CurrentCell, down))
            {
                return false;
            }

            Vector2Int landingCell = state.CurrentCell;

            while (true)
            {
                if (MovementSupportRules.HasSupportForStanding(_context.Grid, landingCell, down))
                {
                    break;
                }

                Vector2Int belowCellPos = landingCell + down;
                if (!_context.Grid.IsInside(belowCellPos.x, belowCellPos.y)) break;

                ref readonly Cell belowCell = ref _context.Grid.GetCell(belowCellPos.x, belowCellPos.y);
                if (IsLadderCell(belowCell)) break;
                if (IsBridgeCell(belowCell)) break;
                if (!IsAirCell(belowCell)) break;

                landingCell = belowCellPos;
            }

            if (landingCell == state.CurrentCell) return false;

            state.CurrentCell = landingCell;
            state.MoveNoProgressSeconds = 0f;
            state.ManualMoveNoProgressSeconds = 0f;
            _context.Navigation.ClearPath(state.UnitId);

            if (state.CurrentTaskId != 0)
            {
                if (state.State == UnitExecutionState.DeliveringResource)
                {
                    RefreshDeliveryGoalAfterFall(state);
                }
                else
                {
                    RefreshTaskGoalAfterFall(state);
                    if (state.CurrentTaskId != 0 && _context.TaskBoard.TryGetTask(state.CurrentTaskId, out UnitTaskRecord task))
                    {
                        _tryResetLoopingTask(state, task);
                    }
                }
            }

            syncActorPosition(state);
            _context.OnUnitCellChanged?.Invoke(state.CurrentCell);
            return true;
        }

        public bool IsTaskOnCooldown(int unitId, int taskId)
        {
            return _isTaskOnCooldown(unitId, taskId);
        }

        private void RefreshTaskGoalAfterFall(UnitTaskState state)
        {
            if (!_context.TaskBoard.TryGetTask(state.CurrentTaskId, out UnitTaskRecord task))
            {
                _resetUnitTask(state);
                return;
            }

            if (!_context.WorkCellResolver.TryFindWorkCell(state.UnitId, state.CurrentCell, task, out Vector2Int workCell))
            {
                _context.TaskBoard.ReleaseTaskReservation(state.CurrentTaskId, state.UnitId, "fall-unreachable");
                _resetUnitTask(state);
                return;
            }

            state.CurrentGoalCell = workCell;
            state.CurrentTaskTargetCell = task.TargetCell;
            state.SetMoving(workCell);
        }

        private void RefreshDeliveryGoalAfterFall(UnitTaskState state)
        {
            var storageSearch = _tryFindNearestStorageDeliveryCell(state.UnitId, state.CurrentCell);
            if (storageSearch.found)
            {
                state.HasResourceStorageTarget = true;
                state.CurrentStorageTargetCell = storageSearch.storageCell;
                state.CurrentGoalCell = storageSearch.deliveryCell;
                state.State = UnitExecutionState.DeliveringResource;
                return;
            }

            state.HasResourceStorageTarget = false;
            state.CurrentStorageTargetCell = state.CurrentCell;
            state.CurrentGoalCell = state.CurrentCell;
            state.State = UnitExecutionState.DeliveringResource;
        }

        private static bool IsAirCell(Cell cell)
        {
            return UnitWorkCellResolver.IsAirCell(cell);
        }

        private static bool IsLadderCell(Cell cell)
        {
            return cell.BuildObjectType.HasValue && cell.BuildObjectType.Value == BuildObjectType.Ladder;
        }

        private static bool IsBridgeCell(Cell cell)
        {
            return cell.BuildObjectType.HasValue && cell.BuildObjectType.Value == BuildObjectType.Bridge;
        }
    }
}