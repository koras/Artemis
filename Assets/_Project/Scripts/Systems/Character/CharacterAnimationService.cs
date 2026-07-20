using _Project.Scripts.Data.Tasks;
using _Project.Scripts.Presentation.Character;
using _Project.Scripts.Data.Pathfinding;
using _Project.Scripts.Systems.Grid;
using _Project.Scripts.Systems.Tasks;
using _Project.Scripts.Systems.Units;
using UnityEngine;

namespace _Project.Scripts.Systems.Character
{
    /// <summary>
    /// Maps unit runtime state to animator parameters and work-aim presentation.
    /// </summary>
    public sealed class CharacterAnimationService
    {
        private readonly GlobalTaskBoardService _taskBoard;
        private readonly GridCoordinateConverter _gridCoordinateConverter;

        public CharacterAnimationService(GlobalTaskBoardService taskBoard, GridCoordinateConverter gridCoordinateConverter)
        {
            _taskBoard = taskBoard;
            _gridCoordinateConverter = gridCoordinateConverter;
        }

        /// <summary>
        /// Synchronizes the character view with the current unit runtime state.
        /// </summary>
        public void Refresh(UnitTaskState state)
        {
            if (state == null || state.Actor == null)
            {
                return;
            }

            CharacterActor actor = state.Actor;
            if (actor.SkeletonAnimation == null)
            {
                return;
            }

            bool isMoving = actor.CurrentMoveSpeed > 0.01f || !actor.IsAtMoveTarget();
            bool isEating = state.State == UnitExecutionState.Eating;
            UnitTaskRecord task;
            bool isWorkActive = TryGetCurrentTask(state, out task) && IsWorkTask(task.TaskType);

            if (!isWorkActive)
            {
                actor.ClearWorkPresentation();
                actor.SetAnimationState(ResolveAnimationState(actor, isMoving, isEating));
                return;
            }

            Vector2 targetWorld = _gridCoordinateConverter.CellToWorldCenter(task.TargetCell);
            actor.SetWorkPresentation(targetWorld);
        }

        private bool TryGetCurrentTask(UnitTaskState state, out UnitTaskRecord task)
        {
            task = null;
            if (state.State != UnitExecutionState.Working || state.CurrentTaskId == 0)
            {
                return false;
            }

            return _taskBoard.TryGetTask(state.CurrentTaskId, out task)
                   && task != null;
        }

        private static bool IsWorkTask(UnitTaskType taskType)
        {
            switch (taskType)
            {
                case UnitTaskType.DigCell:
                case UnitTaskType.ClearBuildCell:
                case UnitTaskType.BuildObject:
                case UnitTaskType.DestroyObject:
                case UnitTaskType.BuildCable:
                case UnitTaskType.DestroyCable:
                case UnitTaskType.BuildWater:
                case UnitTaskType.DestroyWater:
                case UnitTaskType.BuildOxygen:
                case UnitTaskType.DestroyOxygen:
                case UnitTaskType.BuildLifeModule:
                    return true;
                default:
                    return false;
            }
        }

        private static CharacterAnimationState ResolveAnimationState(CharacterActor actor, bool isMoving, bool isEating)
        {
            if (isEating)
            {
                return CharacterAnimationState.Eat;
            }

            if (!isMoving || actor == null)
            {
                return CharacterAnimationState.Idle;
            }

            switch (actor.CurrentMovementAnimationAction)
            {
                case MovementActionType.JumpUp1:
                    return CharacterAnimationState.MoveUp;
                case MovementActionType.Fall:
                    return actor.ShouldUseDownAnimationForCurrentMove
                        ? CharacterAnimationState.MoveDown
                        : CharacterAnimationState.Run;
                default:
                    return CharacterAnimationState.Run;
            }
        }
    }
}
