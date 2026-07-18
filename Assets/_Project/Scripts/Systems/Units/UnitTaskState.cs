using System.Collections.Generic;
using _Project.Scripts.Presentation.Character;
using _Project.Scripts.Data.Grid;
using UnityEngine;

namespace _Project.Scripts.Systems.Units
{
    /// <summary>
    /// Состояние выполнения задач конкретным юнитом.
    /// </summary>
    public sealed class UnitTaskState
    {
        public int UnitId;
        public string CharacterNameKey;
        public string DisplayName;
        public CharacterActor Actor;

        public int CurrentTaskId;
        public UnitExecutionState State;
        public Vector2Int CurrentCell;
        public Vector2Int CurrentGoalCell;
        public Vector2Int CurrentTaskTargetCell;
        public float RemainingWorkSeconds;
        public CellType StartedDigCellType;
        public int StartedDigResourceAmount;
        public int NoProgressTicks;
        public int VisitTrackedTaskId;
        public Dictionary<Vector2Int, int> CellVisitsByCurrentTask = new Dictionary<Vector2Int, int>();
        public int DeferredBuildTaskId;
        public string CarriedResourceId;
        public int CarriedResourceAmount;
        public bool HasResourceStorageTarget;
        public Vector2Int CurrentStorageTargetCell;
        // True after resources were already handed to storage and the unit is only waiting for the door animation.
        public bool IsWaitingForStorageInteraction;
        // Remaining wait time while the storage open-close animation finishes.
        public float StorageInteractionWaitRemainingSeconds;
        public int BuildPipelineTaskId;
        public List<string> BuildPipelineSteps = new List<string>();

        // Локальная ручная задача перемещения, поставленная кликом игрока.
        public bool HasManualMoveOrder;
        public Vector2Int ManualMoveTargetCell;
        public float ManualMoveNoProgressSeconds;
        public float MoveNoProgressSeconds;
        public float IdleNoTaskSeconds;
        public bool HasIdleWanderOrder;
        public Vector2Int IdleWanderTargetCell;
        public float IdleWanderPauseRemainingSeconds;
        public float GlobalTaskRetryBreakRemainingSeconds;
        public Dictionary<int, int> SkippedGlobalTaskAttemptsByTaskId = new Dictionary<int, int>();

        // Локальный режим жизненного цикла (сон/еда/отдых).
        public UnitLocalNeedState LocalNeedState;
        public bool IsInLocalNeedFlow;
        public float SleepTotalMinutes;
        public float SleepRemainingMinutes;
        public float EatTotalMinutes;
        public float EatRemainingMinutes;
        public int CurrentEatRestorePoints;
        public bool HasLoggedMissingEatRoute;
        public bool HasLoggedMissingFoodAtStorage;
        public float RestElapsedMinutes;
        public bool ForcedWakeupRequested;
        public bool HasSleepTarget;
        public Vector2Int SleepTargetCell;

        // Скользящее окно отработанного времени за последние 24 игровых часа.
        public float WorkedMinutesWindow;
        public Queue<WorkWindowEntry> WorkHistory = new Queue<WorkWindowEntry>();
        public string LastGlobalTaskBlockReason;

        /// <summary>
        /// Устанавливает состояние простоя без изменения остальных полей runtime-контекста.
        /// </summary>
        // Method SetIdle: executes the SetIdle workflow.
        public void SetIdle()
        {
            State = UnitExecutionState.Idle;
        }

        /// <summary>
        /// Переводит юнита в режим движения к указанной клетке цели.
        /// </summary>
        // Method SetMoving: executes the SetMoving workflow.
        public void SetMoving(Vector2Int goalCell)
        {
            State = UnitExecutionState.Moving;
            CurrentGoalCell = goalCell;
        }
    }

    /// <summary>
    /// Runtime-состояние исполнения у юнита.
    /// </summary>
    public enum UnitExecutionState
    {
        Idle = 0,
        Moving = 1,
        Working = 2,
        NeedOverride = 3,
        DeliveringResource = 4,
        Sleeping = 5,
        Eating = 6,
        Resting = 7
    }

    public enum UnitLocalNeedState
    {
        None = 0,
        Sleep = 1,
        Eat = 2,
        Rest = 3
    }

    public readonly struct WorkWindowEntry
    {
        public readonly float TimestampMinutes;
        public readonly float WorkedMinutes;

        // Method WorkWindowEntry: executes the WorkWindowEntry workflow.
        public WorkWindowEntry(float timestampMinutes, float workedMinutes)
        {
            TimestampMinutes = timestampMinutes;
            WorkedMinutes = workedMinutes;
        }
    }
}
