using _Project.Scripts.Systems.Navigation;
using _Project.Scripts.Systems.Tasks;
using _Project.Scripts.Data.Pathfinding;
using UnityEngine;

namespace _Project.Scripts.Systems.Units
{
    /// <summary>
    /// Обрабатывает ручные команды перемещения юнитов.
    /// </summary>
    public sealed class UnitManualMoveService
    {
        private readonly CharacterNavigationService _navigation;
        private readonly UnitWorkCellResolver _workCellResolver;
        private readonly GlobalTaskBoardService _taskBoard;
        private readonly float _noProgressTimeoutSeconds;
        private readonly System.Action<UnitTaskState, bool> _resetUnitTask;
        private readonly System.Action<UnitTaskState> _clearManualMoveOrder;
        private readonly System.Action<UnitTaskState, Vector2Int, Vector2Int, MovementActionType> _syncActorStepPosition;
        private readonly System.Action<Vector2Int> _onUnitCellChanged;

        /// <summary>
        /// Создаёт сервис ручного перемещения и принимает зависимости для движения и сброса состояния.
        /// </summary>
        // Method UnitManualMoveService: executes the UnitManualMoveService workflow.
        public UnitManualMoveService(
            CharacterNavigationService navigation,
            UnitWorkCellResolver workCellResolver,
            GlobalTaskBoardService taskBoard,
            float noProgressTimeoutSeconds,
            System.Action<UnitTaskState, bool> resetUnitTask,
            System.Action<UnitTaskState> clearManualMoveOrder,
            System.Action<UnitTaskState, Vector2Int, Vector2Int, MovementActionType> syncActorStepPosition,
            System.Action<Vector2Int> onUnitCellChanged)
        {
            _navigation = navigation;
            _workCellResolver = workCellResolver;
            _taskBoard = taskBoard;
            _noProgressTimeoutSeconds = noProgressTimeoutSeconds;
            _resetUnitTask = resetUnitTask;
            _clearManualMoveOrder = clearManualMoveOrder;
            _syncActorStepPosition = syncActorStepPosition;
            _onUnitCellChanged = onUnitCellChanged;
        }

        /// <summary>
        /// Пытается выдать юниту ручную команду перемещения к ближайшей достижимой клетке от requestedCell.
        /// </summary>
        // Method TryIssueManualMoveCommand: executes the TryIssueManualMoveCommand workflow.
        public bool TryIssueManualMoveCommand(UnitTaskState state, Vector2Int requestedCell)
        {
            if (state.Actor == null) return false;
            if (!state.Actor.IsAtMoveTarget()) return false;
            if (!_workCellResolver.TryFindNearestReachableCell(state.UnitId, state.CurrentCell, requestedCell, out Vector2Int reachableCell)) return false;

            if (state.CurrentTaskId != 0)
            {
                _taskBoard.ReleaseTaskReservation(state.CurrentTaskId, state.UnitId, "manual-move");
            }

            _resetUnitTask(state, true);
            state.HasManualMoveOrder = true;
            state.ManualMoveTargetCell = reachableCell;
            state.ManualMoveNoProgressSeconds = 0f;
            state.CurrentGoalCell = reachableCell;
            state.CurrentTaskTargetCell = requestedCell;
            state.State = UnitExecutionState.Moving;
            state.NoProgressTicks = 0;
            state.MoveNoProgressSeconds = 0f;
            return true;
        }

        /// <summary>
        /// Обрабатывает выполнение ручной команды: шаг движения, завершение по достижению и таймаут без прогресса.
        /// </summary>
        // Method ProcessManualMoveOrder: executes the ProcessManualMoveOrder workflow.
        public void ProcessManualMoveOrder(UnitTaskState state, float tickSeconds)
        {
            if (state.CurrentCell == state.ManualMoveTargetCell)
            {
                _clearManualMoveOrder(state);
                return;
            }

            if (!state.Actor.IsAtMoveTarget())
            {
                return;
            }

            NavigationStepResult stepResult = _navigation.TryStep(
                state.UnitId,
                ref state.CurrentCell,
                state.ManualMoveTargetCell,
                out Vector2Int fromCell,
                out Vector2Int toCell,
                out MovementActionType actionType);

            if (stepResult == NavigationStepResult.Stepped)
            {
                state.ManualMoveNoProgressSeconds = 0f;
                state.NoProgressTicks = 0;

                Vector2Int moveDirection = toCell - fromCell;
                state.Actor.SetFacing(moveDirection);
                _syncActorStepPosition(state, fromCell, toCell, actionType);
                _onUnitCellChanged?.Invoke(state.CurrentCell);
                return;
            }

            if (stepResult == NavigationStepResult.Arrived)
            {
                _clearManualMoveOrder(state);
                return;
            }

            state.ManualMoveNoProgressSeconds += tickSeconds;
            if (state.ManualMoveNoProgressSeconds >= _noProgressTimeoutSeconds)
            {
                _clearManualMoveOrder(state);
            }
        }
    }
}
