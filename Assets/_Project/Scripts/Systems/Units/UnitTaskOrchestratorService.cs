using System;
using System.Collections.Generic;
using _Project.Scripts.Data.Grid;
using _Project.Scripts.Data.Pathfinding;
using _Project.Scripts.Data.Tasks;
using _Project.Scripts.Presentation.Character;
using _Project.Scripts.Systems.Character;
using _Project.Scripts.Systems.Grid;
using _Project.Scripts.Systems.Navigation;
using _Project.Scripts.Systems.Resources;
using _Project.Scripts.Systems.Tasks;
using UnityEngine;
using _Project.Scripts.Systems.Construction;
using _Project.Scripts.Data.Construction;
using _Project.Scripts.Systems.Units.Orchestrator;

namespace _Project.Scripts.Systems.Units
{
    /// <summary>
    /// Orchestrates unit behavior: needs, task acquisition, movement, and task execution.
    /// Main state transitions:
    /// Idle -> Moving -> Working -> Idle
    /// Idle/Moving/Working -> NeedOverride -> Idle
    /// Working(Dig) -> DeliveringResource -> Idle
    /// </summary>
    public sealed class UnitTaskOrchestratorService
    {
        private readonly GridState _grid;
        private readonly GlobalTaskBoardService _taskBoard;
        private readonly CharacterNavigationService _navigation;
        private readonly TaskExecutionService _taskExecution;
        private readonly UnitNeedPolicy _needPolicy;
        private readonly CharacterAnimationService _characterAnimationService;
        private readonly GridCoordinateConverter _gridCoordinateConverter;
        private readonly bool _enableLogs;
        private readonly Action<Vector2Int> _onUnitCellChanged;
        private readonly UnitWorkCellResolver _workCellResolver;
        private readonly UnitManualMoveService _manualMoveService;
        private readonly UnitTaskAcquisitionService _taskAcquisitionService;
        private readonly UnitResourceDeliveryService _resourceDeliveryService;
        private readonly UnitOrchestratorContext _orchestratorContext;
        private readonly UnitMovementRuntimeService _movementRuntimeService;
        private readonly UnitDiagnosticsService _diagnosticsService;
        private readonly UnitNeedFlowService _needFlowService;
        private readonly UnitTaskLifecycleService _taskLifecycleService;
        private readonly UnitOrchestratorStateStore _stateStore;
        private readonly UnitOrchestratorTickPipeline _tickPipeline;
        private readonly BuildingManager _buildingManager;
        private readonly LifeModulePlacementService _lifeModulePlacementService;
        private readonly System.Action<BuildTaskPayload> _onBuildCompletedVisual;
        private readonly System.Action<BuildTaskPayload> _onDestroyCompletedVisual;
        private readonly System.Action<Vector2Int> _onCableBuildCompletedVisual;
        private readonly System.Action<Vector2Int> _onCableDestroyCompletedVisual;
        private readonly System.Action<Vector2Int> _onWaterBuildCompletedVisual;
        private readonly System.Action<Vector2Int> _onWaterDestroyCompletedVisual;
        private readonly System.Action<Vector2Int> _onOxygenBuildCompletedVisual;
        private readonly System.Action<Vector2Int> _onOxygenDestroyCompletedVisual;
        private readonly System.Action<LifeModuleTaskPayload> _onLifeModuleBuildCompletedVisual;
        private readonly Action<UnitTaskState> _syncActorPositionBuffered;
        private readonly Action<UnitTaskState, Vector2Int, Vector2Int, MovementActionType> _syncActorStepPositionBuffered;
        // Timeout for manual move when the unit makes no progress.
        private const float MANUAL_MOVE_NO_PROGRESS_TIMEOUT_SECONDS = 1f;
        private const float IDLE_WANDER_MIN_START_DELAY_SECONDS = 20f;
        private const float IDLE_WANDER_MAX_START_DELAY_SECONDS = 45f;
        private const int IDLE_WANDER_RADIUS_CELLS = 8;
        private const float TASK_LOOP_COOLDOWN_SECONDS = 10f;
        private const int TASK_WORK_MAX_DISTANCE = 3;
        private const float GAME_MINUTES_PER_REAL_SECOND = 2f;
        // Oxygen Not Included default schedule:
        // 18 work blocks + 3 downtime/bathtime blocks + 3 bedtime blocks.
        // One ONI cycle is 600 real seconds, which equals 1200 game minutes in this project scale.
        private const float WORK_WINDOW_DURATION_MINUTES = 20f * 60f;
        private const float DAILY_WORK_QUOTA_MINUTES = 15f * 60f;
        private const float BASE_SLEEP_MINUTES = 2.5f * 60f;
        private const float MAX_SLEEP_MINUTES = 2.5f * 60f;
        private const float MEAL_DURATION_MINUTES = 30f;
        private const float DAILY_REST_TARGET_MINUTES = 2.5f * 60f;
        private const int EAT_RESTORE_POINTS = 90;
        private const int REST_SLEEP_RELIEF_PER_HOUR = 6;
        private const int REST_HUNGER_INCREASE_PER_HOUR = 4;
        private readonly System.Random _idleWanderRandom = new System.Random(18473);
        // Tick context used when creating deferred subtasks.
        private int _currentTickContext;
        private float _simulatedGameMinutes;
        
        /// <summary>
        /// Creates orchestrator services and wires dependencies for the unit lifecycle flow.
        /// </summary>
        public UnitTaskOrchestratorService(
            GridState grid,
            GlobalTaskBoardService taskBoard,
            CharacterNavigationService navigation,
            TaskExecutionService taskExecution,
            UnitNeedPolicy needPolicy,
            CharacterAnimationService characterAnimationService,
            GridCoordinateConverter gridCoordinateConverter,
            ResourceInventoryService resourceInventoryService,
            SceneResourceObjectService sceneResourceObjectService,
            BuildingManager buildingManager,
            LifeModulePlacementService lifeModulePlacementService,
            System.Action<BuildTaskPayload> onBuildCompletedVisual,
            System.Action<BuildTaskPayload> onDestroyCompletedVisual,
            System.Action<Vector2Int> onCableBuildCompletedVisual,
            System.Action<Vector2Int> onCableDestroyCompletedVisual,
            System.Action<Vector2Int> onWaterBuildCompletedVisual,
            System.Action<Vector2Int> onWaterDestroyCompletedVisual,
            System.Action<Vector2Int> onOxygenBuildCompletedVisual,
            System.Action<Vector2Int> onOxygenDestroyCompletedVisual,
            System.Action<LifeModuleTaskPayload> onLifeModuleBuildCompletedVisual,
            System.Func<Vector2Int, float> onStorageDeliveryCompletedVisual,
            Action<Vector2Int> onUnitCellChanged,
            IReadOnlyList<string> foodResourceIds,
            bool enableLogs)
        {
            _grid = grid;
            _taskBoard = taskBoard;
            _navigation = navigation;
            _taskExecution = taskExecution;
            _needPolicy = needPolicy;
            _characterAnimationService = characterAnimationService;
            _gridCoordinateConverter = gridCoordinateConverter;
            _buildingManager = buildingManager;
            _lifeModulePlacementService = lifeModulePlacementService;
            _onBuildCompletedVisual = onBuildCompletedVisual;
            _onDestroyCompletedVisual = onDestroyCompletedVisual;
            _onCableBuildCompletedVisual = onCableBuildCompletedVisual;
            _onCableDestroyCompletedVisual = onCableDestroyCompletedVisual;
            _onWaterBuildCompletedVisual = onWaterBuildCompletedVisual;
            _onWaterDestroyCompletedVisual = onWaterDestroyCompletedVisual;
            _onOxygenBuildCompletedVisual = onOxygenBuildCompletedVisual;
            _onOxygenDestroyCompletedVisual = onOxygenDestroyCompletedVisual;
            _onLifeModuleBuildCompletedVisual = onLifeModuleBuildCompletedVisual;
            _onUnitCellChanged = onUnitCellChanged;
            _enableLogs = enableLogs;
            _syncActorPositionBuffered = SyncActorPosition;
            _syncActorStepPositionBuffered = SyncActorStepPosition;
            _stateStore = new UnitOrchestratorStateStore();
            _workCellResolver = new UnitWorkCellResolver(_grid, _navigation, TASK_WORK_MAX_DISTANCE);
            _manualMoveService = new UnitManualMoveService(
                _navigation,
                _workCellResolver,
                _taskBoard,
                MANUAL_MOVE_NO_PROGRESS_TIMEOUT_SECONDS,
                ResetUnitTask,
                ClearManualMoveOrder,
                _syncActorStepPositionBuffered,
                _onUnitCellChanged);
            _taskAcquisitionService = new UnitTaskAcquisitionService(
                _taskBoard,
                _buildingManager,
                _lifeModulePlacementService,
                _workCellResolver,
                _enableLogs,
                IsTaskOnCooldown,
                StartTaskVisitTracking);
            _resourceDeliveryService = new UnitResourceDeliveryService(
                _buildingManager,
                resourceInventoryService,
                sceneResourceObjectService,
                _taskBoard,
                _workCellResolver,
                _enableLogs,
                ResetUnitTask,
                onStorageDeliveryCompletedVisual);
            _orchestratorContext = new UnitOrchestratorContext
            {
                Grid = _grid,
                TaskBoard = _taskBoard,
                Navigation = _navigation,
                GridCoordinateConverter = _gridCoordinateConverter,
                BuildingManager = _buildingManager,
                WorkCellResolver = _workCellResolver,
                OnUnitCellChanged = _onUnitCellChanged,
                ManualMoveNoProgressTimeoutSeconds = MANUAL_MOVE_NO_PROGRESS_TIMEOUT_SECONDS,
                DailyWorkQuotaMinutes = DAILY_WORK_QUOTA_MINUTES,
                DailyRestTargetMinutes = DAILY_REST_TARGET_MINUTES,
                MealDurationMinutes = MEAL_DURATION_MINUTES
            };
            _movementRuntimeService = new UnitMovementRuntimeService(
                _orchestratorContext,
                IsTaskOnCooldown,
                state => ResetUnitTask(state),
                TryResetLoopingTask,
                (unitId, unitCell) =>
                {
                    bool found = TryFindNearestStorageDeliveryCell(unitId, unitCell, out Vector2Int storageCell, out Vector2Int deliveryCell);
                    return (found, storageCell, deliveryCell);
                });
            _diagnosticsService = new UnitDiagnosticsService(_orchestratorContext);
            _taskLifecycleService = new UnitTaskLifecycleService(
                _orchestratorContext,
                _taskAcquisitionService,
                _stateStore.TaskCooldownsByUnitId,
                ClearCarriedResource,
                CanStartWorkFromCell,
                TASK_LOOP_COOLDOWN_SECONDS);
            _needFlowService = new UnitNeedFlowService(
                _orchestratorContext,
                _needPolicy,
                _buildingManager,
                _stateStore.UnitOrder,
                _stateStore.StatesByUnitId,
                state => ResetUnitTask(state),
                _syncActorStepPositionBuffered,
                WORK_WINDOW_DURATION_MINUTES,
                GAME_MINUTES_PER_REAL_SECOND,
                BASE_SLEEP_MINUTES,
                MAX_SLEEP_MINUTES,
                EAT_RESTORE_POINTS,
                REST_SLEEP_RELIEF_PER_HOUR,
                REST_HUNGER_INCREASE_PER_HOUR,
                resourceInventoryService,
                foodResourceIds);
            _tickPipeline = new UnitOrchestratorTickPipeline(ProcessUnitCore);
        }

        /// <summary>
        /// Registers a unit with actor, start cell, and initial runtime state.
        /// </summary>
        public void RegisterUnit(int unitId, CharacterActor actor, Vector2Int startCell, string displayName = null, string characterNameKey = null)
        {
            var state = new UnitTaskState
            {
                UnitId = unitId,
                CharacterNameKey = string.IsNullOrWhiteSpace(characterNameKey) ? $"character_{unitId:0000}" : characterNameKey,
                DisplayName = string.IsNullOrWhiteSpace(displayName)
                    ? (actor != null && !string.IsNullOrWhiteSpace(actor.name) ? actor.name : $"Unit {unitId}")
                    : displayName,
                Actor = actor,
                CurrentTaskId = 0,
                State = UnitExecutionState.Idle,
                CurrentCell = startCell,
                CurrentGoalCell = startCell,
                CurrentTaskTargetCell = startCell,
                RemainingWorkSeconds = 0f,
                StartedDigCellType = CellType.Empty,
                StartedDigResourceAmount = 0,
                NoProgressTicks = 0,
                HasManualMoveOrder = false,
                ManualMoveTargetCell = startCell,
                ManualMoveNoProgressSeconds = 0f,
                MoveNoProgressSeconds = 0f,
                IdleNoTaskSeconds = 0f,
                HasIdleWanderOrder = false,
                IdleWanderTargetCell = startCell,
                IdleWanderPauseRemainingSeconds = PickIdleWanderStartDelaySeconds(),
                GlobalTaskRetryBreakRemainingSeconds = 0f,
                LocalNeedState = UnitLocalNeedState.None,
                IsInLocalNeedFlow = false,
                SleepTotalMinutes = 0f,
                SleepRemainingMinutes = 0f,
                EatTotalMinutes = 0f,
                EatRemainingMinutes = 0f,
                CurrentEatRestorePoints = 0,
                HasLoggedMissingEatRoute = false,
                HasLoggedMissingFoodAtStorage = false,
                RestElapsedMinutes = 0f,
                ForcedWakeupRequested = false,
                HasSleepTarget = false,
                SleepTargetCell = startCell,
                WorkedMinutesWindow = 0f,
                LastGlobalTaskBlockReason = "idle",
                DeferredBuildTaskId = 0,
                CarriedResourceId = null,
                CarriedResourceAmount = 0,
                HasResourceStorageTarget = false,
                CurrentStorageTargetCell = startCell
            };

            _stateStore.StatesByUnitId[unitId] = state;
            _stateStore.UnitOrder.Add(unitId);
            _characterAnimationService.Refresh(state);
        }

        /// <summary>
        /// Returns the id of a unit that currently occupies the specified cell.
        /// </summary>
        public bool TryGetUnitIdAtCell(Vector2Int cell, out int unitId)
        {
            for (int i = 0; i < _stateStore.UnitOrder.Count; i++)
            {
                int candidateUnitId = _stateStore.UnitOrder[i];
                UnitTaskState state = _stateStore.StatesByUnitId[candidateUnitId];
                // Branch rule: if (state.CurrentCell != cell) we take this path; otherwise flow continues.
                if (state.CurrentCell != cell) continue;

                unitId = candidateUnitId;
                return true;
            }

            unitId = 0;
            return false;
        }

        /// <summary>
        /// Issues a manual move order and switches the unit into manual movement mode.
        /// </summary>
        public bool TryIssueManualMoveCommand(int unitId, Vector2Int requestedCell)
        {
            if (!_stateStore.StatesByUnitId.TryGetValue(unitId, out UnitTaskState state)) return false;
            bool issued = _manualMoveService.TryIssueManualMoveCommand(state, requestedCell);
            if (issued)
            {
                _characterAnimationService.Refresh(state);
            }

            return issued;
        }

        /// <summary>
        /// Requests immediate wakeup for a sleeping unit.
        /// </summary>
        public bool RequestForceWakeup(int unitId, string reason)
        {
            if (!_stateStore.StatesByUnitId.TryGetValue(unitId, out UnitTaskState state)) return false;
            state.ForcedWakeupRequested = true;

            if (_enableLogs)
            {
                _ = reason;
            }

            return true;
        }

        /// <summary>
        /// Per-frame movement update: movement progress and no-progress timeouts.
        /// </summary>
        public void TickMovementFrame(float deltaTime)
        {
            for (int i = 0; i < _stateStore.UnitOrder.Count; i++)
            {
                int unitId = _stateStore.UnitOrder[i];
                // Branch rule: if (!_stateStore.StatesByUnitId.TryGetValue(unitId, out UnitTaskState state)) we take this path; otherwise flow continues.
                if (!_stateStore.StatesByUnitId.TryGetValue(unitId, out UnitTaskState state)) continue;
                // Branch rule: if (state.Actor == null) we take this path; otherwise flow continues.
                if (state.Actor == null) continue;

                try
                {
                    // Branch rule: if (TryApplyGravityFall(state)) we take this path; otherwise flow continues.
                    if (TryApplyGravityFall(state))
                    {
                        continue;
                    }

                    // Branch rule: if (state.HasManualMoveOrder) we take this path; otherwise flow continues.
                    if (state.HasManualMoveOrder)
                    {
                        ProcessManualMoveOrder(state, deltaTime);
                        continue;
                    }

                    if (state.HasIdleWanderOrder)
                    {
                        ProcessIdleWanderMoveFrame(state, deltaTime);
                        continue;
                    }

                    // Branch rule: if (state.State != UnitExecutionState.Moving && state.State != UnitExecutionState.DeliveringResource) we take this path; otherwise flow continues.
                    if (state.State != UnitExecutionState.Moving && state.State != UnitExecutionState.DeliveringResource) continue;
                    if (state.State == UnitExecutionState.DeliveringResource)
                    {
                        ProcessDeliveryMoveFrame(state, deltaTime);
                        continue;
                    }

                    // Branch rule: if (state.CurrentTaskId == 0) we take this path; otherwise flow continues.
                    if (state.CurrentTaskId == 0) continue;
                    ProcessTaskMoveFrame(state, deltaTime);
                }
                finally
                {
                    _characterAnimationService.Refresh(state);
                }
            }
        }

        /// <summary>
        /// Tick update for all units using round-robin order for fairness.
        /// </summary>
        public void TickAll(float tickSeconds, int currentTick)
        {
            // Branch rule: if (_stateStore.UnitOrder.Count == 0) we take this path; otherwise flow continues.
            if (_stateStore.UnitOrder.Count == 0) return;
            _currentTickContext = currentTick;
            _simulatedGameMinutes += tickSeconds * GAME_MINUTES_PER_REAL_SECOND;

            int count = _stateStore.UnitOrder.Count;
            for (int i = 0; i < count; i++)
            {
                int index = (_stateStore.RoundRobinStartIndex + i) % count;
                int unitId = _stateStore.UnitOrder[index];
                _tickPipeline.ProcessUnit(_stateStore.StatesByUnitId[unitId], tickSeconds, currentTick);
            }

            _stateStore.RoundRobinStartIndex = (_stateStore.RoundRobinStartIndex + 1) % count;
        }

        /// <summary>
        /// Processes one unit tick: needs, task acquisition, movement, and work execution.
        /// </summary>
        private void ProcessUnitCore(UnitTaskState state, float tickSeconds, int currentTick)
        {
            try
            {
                // 0) Unit must have an actor (it can be destroyed at runtime).
                if (state.Actor == null) return;

                // Manual move has priority over normal task processing and needs.
                if (state.HasManualMoveOrder)
                {
                    state.LastGlobalTaskBlockReason = "manual move active";
                    ProcessManualMoveOrder(state, tickSeconds);
                    return;
                }

                // Update the rolling work window and local needs before normal task processing.
                UpdateWorkWindow(state, tickSeconds);
                _taskLifecycleService.TickGlobalTaskRetryBreak(state, tickSeconds);
                if (ProcessLocalNeedFlow(state, tickSeconds))
                {
                    state.LastGlobalTaskBlockReason = $"local need flow: {state.LocalNeedState}";
                    return;
                }

                UnitLocalNeedState desiredNeed = _needPolicy.DecideLocalNeed(state.Actor, IsWorkQuotaReached(state));
                if (desiredNeed != UnitLocalNeedState.None)
                {
                    ClearIdleWanderState(state);
                    EnterLocalNeedFlow(state, desiredNeed);
                    if (ProcessLocalNeedFlow(state, tickSeconds))
                    {
                        state.LastGlobalTaskBlockReason = $"local need: {desiredNeed}";
                        return;
                    }
                }

                // Backward-compat: drop legacy need override if it remained from older state machine.
                if (state.State == UnitExecutionState.NeedOverride)
                {
                    state.SetIdle();
                }

                // Branch rule: if (state.State == UnitExecutionState.DeliveringResource) we take this path; otherwise flow continues.
                if (state.State == UnitExecutionState.DeliveringResource)
                {
                    state.LastGlobalTaskBlockReason = "delivering resource";
                    ProcessResourceDelivery(state, tickSeconds);
                    return;
                }

                // 2) If no current task, try to reserve/acquire a new one.
                if (state.CurrentTaskId == 0)
                {
                    if (TryAcquireTask(state, currentTick))
                    {
                        ClearIdleWanderState(state);
                        return;
                    }

                    ProcessIdleWander(state, tickSeconds);
                    return;
                }

                state.LastGlobalTaskBlockReason = "already has global task";

                // 3) If task disappeared from board (completed/cancelled), reset state.
                if (!_taskBoard.TryGetTask(state.CurrentTaskId, out UnitTaskRecord task))
                {
                    ResetUnitTask(state);
                    return;
                }

                // 4) If task is terminal, clear current unit task state.
                if (task.Status == UnitTaskStatus.Completed || task.Status == UnitTaskStatus.Failed)
                {
                    ResetUnitTask(state);
                    return;
                }

                // 5) Safety: if reservation owner changed, release/reset this unit task.
                if (task.ReservedByUnitId != state.UnitId)
                {
                    ResetUnitTask(state);
                    return;
                }

                // 6) If unit is in Working, execute work tick for current task.
                // On completion: finalize world change, complete task in board, reset state.
                if (state.State == UnitExecutionState.Working)
                {
                    // Branch rule: if (task.TaskType == UnitTaskType.BuildObject) we take this path; otherwise flow continues.
                    if (task.TaskType == UnitTaskType.BuildObject)
                    {
                        task.BuildPayload.RemainingBuildTicks--;
                        // Branch rule: if (task.BuildPayload.RemainingBuildTicks > 0) we take this path; otherwise flow continues.
                        if (task.BuildPayload.RemainingBuildTicks > 0) return;

                        // TODO: call buildingManager.FinalizeBuild(task.BuildPayload).
                        // After build completion, update world state and visuals.
                        _buildingManager.FinalizeBuild(task.BuildPayload);
                        _taskBoard.TryActivateDigTasksAroundBuildPayload(task.BuildPayload, currentTick);

                        // Build completion visual callback must use the same anchor as preview.
                        _onBuildCompletedVisual?.Invoke(task.BuildPayload);

                        _taskBoard.CompleteTask(task.TaskId, state.UnitId);
                        // Branch rule: if (state.DeferredBuildTaskId == task.TaskId) we take this path; otherwise flow continues.
                        if (state.DeferredBuildTaskId == task.TaskId)
                        {
                            state.DeferredBuildTaskId = 0;
                        }
                        ResetUnitTask(state);
                        return;
                    }

                    // Branch rule: if (task.TaskType == UnitTaskType.DestroyObject) we take this path; otherwise flow continues.
                    if (task.TaskType == UnitTaskType.DestroyObject)
                    {
                        task.BuildPayload.RemainingBuildTicks--;
                        // Branch rule: if (task.BuildPayload.RemainingBuildTicks > 0) we take this path; otherwise flow continues.
                        if (task.BuildPayload.RemainingBuildTicks > 0) return;

                        _buildingManager.FinalizeDestroy(task.BuildPayload);
                        _taskBoard.TryActivateDigTasksAroundBuildPayload(task.BuildPayload, currentTick);
                        _onDestroyCompletedVisual?.Invoke(task.BuildPayload);
                        _taskBoard.CompleteTask(task.TaskId, state.UnitId);
                        ResetUnitTask(state);
                        return;
                    }

                    if (task.TaskType == UnitTaskType.BuildCable)
                    {
                        task.RemainingWorkTicks--;
                        if (task.RemainingWorkTicks > 0) return;

                        _onCableBuildCompletedVisual?.Invoke(task.TargetCell);
                        _taskBoard.CompleteTask(task.TaskId, state.UnitId);
                        ResetUnitTask(state);
                        return;
                    }

                    if (task.TaskType == UnitTaskType.DestroyCable)
                    {
                        task.RemainingWorkTicks--;
                        if (task.RemainingWorkTicks > 0) return;

                        _resourceDeliveryService.AddCableSalvageResource();
                        _onCableDestroyCompletedVisual?.Invoke(task.TargetCell);
                        _taskBoard.CompleteTask(task.TaskId, state.UnitId);
                        ResetUnitTask(state);
                        return;
                    }

                    if (task.TaskType == UnitTaskType.BuildWater)
                    {
                        task.RemainingWorkTicks--;
                        if (task.RemainingWorkTicks > 0) return;

                        _onWaterBuildCompletedVisual?.Invoke(task.TargetCell);
                        _taskBoard.CompleteTask(task.TaskId, state.UnitId);
                        ResetUnitTask(state);
                        return;
                    }

                    if (task.TaskType == UnitTaskType.DestroyWater)
                    {
                        task.RemainingWorkTicks--;
                        if (task.RemainingWorkTicks > 0) return;

                        _resourceDeliveryService.AddWaterSalvageResource();
                        _onWaterDestroyCompletedVisual?.Invoke(task.TargetCell);
                        _taskBoard.CompleteTask(task.TaskId, state.UnitId);
                        ResetUnitTask(state);
                        return;
                    }

                    if (task.TaskType == UnitTaskType.BuildOxygen)
                    {
                        task.RemainingWorkTicks--;
                        if (task.RemainingWorkTicks > 0) return;

                        _onOxygenBuildCompletedVisual?.Invoke(task.TargetCell);
                        _taskBoard.CompleteTask(task.TaskId, state.UnitId);
                        ResetUnitTask(state);
                        return;
                    }

                    if (task.TaskType == UnitTaskType.BuildLifeModule)
                    {
                        task.RemainingWorkTicks--;
                        if (task.RemainingWorkTicks > 0) return;

                        _taskBoard.CompleteTask(task.TaskId, state.UnitId);
                        _onLifeModuleBuildCompletedVisual?.Invoke(task.LifeModulePayload);
                        ResetUnitTask(state);
                        return;
                    }

                    if (task.TaskType == UnitTaskType.DestroyOxygen)
                    {
                        task.RemainingWorkTicks--;
                        if (task.RemainingWorkTicks > 0) return;

                        _resourceDeliveryService.AddOxygenSalvageResource();
                        _onOxygenDestroyCompletedVisual?.Invoke(task.TargetCell);
                        _taskBoard.CompleteTask(task.TaskId, state.UnitId);
                        ResetUnitTask(state);
                        return;
                    }

                    // Dig task execution tick.
                    bool done = _taskExecution.TickDig(ref state, tickSeconds);
                    // Branch rule: if (!done) we take this path; otherwise flow continues.
                    if (!done) return;

                    _taskBoard.TryActivateDigTasksAroundCell(task.TargetCell, currentTick);
                    // Re-check delayed cable plans after dig opens Empty/Atmosphere cells nearby.
                    _taskBoard.TryActivateCableTasksAroundCell(task.TargetCell, currentTick);
                    _taskBoard.TryActivateWaterTasksAroundCell(task.TargetCell, currentTick);
                    _taskBoard.TryActivateOxygenTasksAroundCell(task.TargetCell, currentTick);
                    int minedAmount = state.StartedDigResourceAmount;
                    // Branch rule: if (minedAmount > 0) we take this path; otherwise flow continues.
                    if (minedAmount > 0)
                    {
                        StartResourceDelivery(state, minedAmount);
                        return;
                    }

                    // Branch rule: if (task.TaskType == UnitTaskType.ClearBuildCell) we take this path; otherwise flow continues.
                    if (task.TaskType == UnitTaskType.ClearBuildCell)
                    {
                        _taskBoard.NotifyBuildClearSubtaskCompleted(task);
                    }

                    _taskBoard.CompleteTask(task.TaskId, state.UnitId);
                    ResetUnitTask(state);
                    return;
                }

                // 7) If unit reached a valid work cell, start Working state.
                // Also refresh task status to InProgress and reset movement counters.
                if (CanStartWorkFromCell(state.CurrentCell, task))
                {
                    // Branch rule: if (task.TaskType == UnitTaskType.BuildObject && TryDeferBuildForExcavation(state, task)) we take this path; otherwise flow continues.
                    if (task.TaskType == UnitTaskType.BuildObject && TryDeferBuildForExcavation(state, task))
                    {
                        return;
                    }

                    _taskBoard.MarkInProgress(task.TaskId, state.UnitId);

                    // Build tasks use deferred subtasks before entering Working.
                    // Work progress is tracked in BuildPayload.RemainingBuildTicks.
                    if (task.TaskType == UnitTaskType.BuildObject)
                    {
                        // Branch rule: if (!_buildingManager.TryPayBuildCost(task.BuildPayload)) we take this path; otherwise flow continues.
                        if (!_buildingManager.TryPayBuildCost(task.BuildPayload))
                        {
                            _taskBoard.ReleaseTaskReservation(task.TaskId, state.UnitId, "not-enough-build-resources");
                            ResetUnitTask(state);
                            return;
                        }

                        task.BuildPayload.IsExcavatingBeforeBuild = false;
                        _buildingManager.MarkBuildInProgress(task.BuildPayload);
                        state.State = UnitExecutionState.Working;
                        return;
                    }

                    if (task.TaskType == UnitTaskType.BuildCable)
                    {
                        state.State = UnitExecutionState.Working;
                        return;
                    }

                    if (task.TaskType == UnitTaskType.DestroyCable)
                    {
                        state.State = UnitExecutionState.Working;
                        return;
                    }

                    if (task.TaskType == UnitTaskType.BuildWater)
                    {
                        state.State = UnitExecutionState.Working;
                        return;
                    }

                    if (task.TaskType == UnitTaskType.DestroyWater)
                    {
                        state.State = UnitExecutionState.Working;
                        return;
                    }

                    if (task.TaskType == UnitTaskType.BuildLifeModule)
                    {
                        if (!_lifeModulePlacementService.TryPayBuildCost(task.LifeModulePayload))
                        {
                            _taskBoard.ReleaseTaskReservation(task.TaskId, state.UnitId, "not-enough-life-module-build-resources");
                            ResetUnitTask(state);
                            return;
                        }

                        state.State = UnitExecutionState.Working;
                        return;
                    }

                    if (task.TaskType == UnitTaskType.DeliverDroppedResource)
                    {
                        if (_resourceDeliveryService.TryPickupDroppedResourceAndStartDelivery(state, task))
                        {
                            _taskBoard.CompleteTask(task.TaskId, state.UnitId);
                        }
                        else
                        {
                            _taskBoard.ReleaseTaskReservation(task.TaskId, state.UnitId, "dropped-resource-pickup-failed");
                            ResetUnitTask(state);
                            return;
                        }

                        // Keep unit in delivery state after pickup.
                        return;
                    }

                    // Branch rule: if (task.TaskType == UnitTaskType.DestroyObject) we take this path; otherwise flow continues.
                    if (task.TaskType == UnitTaskType.DestroyObject)
                    {
                        _buildingManager.MarkDestroyInProgress(task.BuildPayload);
                        state.State = UnitExecutionState.Working;
                        return;
                    }

                    // If no valid work cell can be found, release/reset the task.
                    Cell targetCell = _grid.GetCell(task.TargetCell.x, task.TargetCell.y);
                    state.StartedDigCellType = targetCell.Type;
                    state.StartedDigResourceAmount = targetCell.ResourceAmount > 0
                        ? targetCell.ResourceAmount
                        : UnitResourceDeliveryService.GetMinedAmount(state.StartedDigCellType);
                    _taskExecution.TryStartDig(ref state, targetCell.Type);
                    return;
                }

                // 8) Otherwise continue moving toward goal cell.
                // TryStep can both move and report blocked states.
                // Actual movement interpolation is applied in TickMovementFrame().
                return;
            }
            finally
            {
                _characterAnimationService.Refresh(state);
            }
        }

        /// <summary>
        /// Performs high-priority movement refresh for currently moving units.
        /// Called each frame to keep navigation responsive between simulation ticks.
        /// </summary>
        private void ProcessManualMoveOrder(UnitTaskState state, float tickSeconds)
        {
            _manualMoveService.ProcessManualMoveOrder(state, tickSeconds);
        }

        /// <summary>
        /// Handles one movement frame toward current goal cell.
        /// </summary>
        private void ProcessTaskMoveFrame(UnitTaskState state, float deltaTime)
        {
            _movementRuntimeService.ProcessTaskMoveFrame(state, deltaTime, _syncActorStepPositionBuffered);
        }

        /// <summary>
        /// Handles one movement frame for idle wandering without touching the task board.
        /// </summary>
        private void ProcessIdleWanderMoveFrame(UnitTaskState state, float deltaTime)
        {
            if (!state.Actor.IsAtMoveTarget())
            {
                return;
            }

            NavigationStepResult stepResult = _navigation.TryStep(
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
                SyncActorStepPosition(state, fromCell, toCell, actionType);
                _onUnitCellChanged?.Invoke(state.CurrentCell);
                return;
            }

            if (stepResult == NavigationStepResult.Arrived)
            {
                CompleteIdleWander(state);
                return;
            }

            state.MoveNoProgressSeconds += deltaTime;
            if (state.MoveNoProgressSeconds >= MANUAL_MOVE_NO_PROGRESS_TIMEOUT_SECONDS)
            {
                CompleteIdleWander(state);
            }
        }

        /// <summary>
        /// Handles one movement frame for resource delivery flow, independent from task board status.
        /// </summary>
        private void ProcessDeliveryMoveFrame(UnitTaskState state, float deltaTime)
        {
            _movementRuntimeService.ProcessDeliveryMoveFrame(state, deltaTime, _syncActorStepPositionBuffered);
        }

        /// <summary>
        /// Tries to reserve/acquire a task and switch unit into Moving state.
        /// </summary>
        private bool TryAcquireTask(UnitTaskState state, int currentTick)
        {
            bool acquired = _taskLifecycleService.TryAcquireTask(state, currentTick, out string blockReason);
            if (acquired)
            {
                state.LastGlobalTaskBlockReason = "task acquired";
                return true;
            }

            state.LastGlobalTaskBlockReason = string.IsNullOrWhiteSpace(blockReason)
                ? "task not acquired (unknown reason)"
                : blockReason;
            return false;
        }

        private void ProcessIdleWander(UnitTaskState state, float tickSeconds)
        {
            if (state.HasIdleWanderOrder)
            {
                state.LastGlobalTaskBlockReason = "idle wandering";
                return;
            }

            if (state.State != UnitExecutionState.Idle || !state.Actor.IsAtMoveTarget())
            {
                state.LastGlobalTaskBlockReason = "idle wander settle";
                return;
            }

            state.IdleNoTaskSeconds += tickSeconds;
            if (state.IdleWanderPauseRemainingSeconds > 0f)
            {
                state.IdleWanderPauseRemainingSeconds = Mathf.Max(0f, state.IdleWanderPauseRemainingSeconds - tickSeconds);
                state.LastGlobalTaskBlockReason = "idle wander wait";
                return;
            }

            if (TryStartIdleWander(state))
            {
                state.LastGlobalTaskBlockReason = "idle wandering";
                return;
            }

            state.IdleNoTaskSeconds = 0f;
            state.IdleWanderPauseRemainingSeconds = PickIdleWanderStartDelaySeconds();
            state.LastGlobalTaskBlockReason = "idle wander retry pause";
        }

        private void UpdateWorkWindow(UnitTaskState state, float tickSeconds)
        {
            _needFlowService.UpdateWorkWindow(state, tickSeconds, _simulatedGameMinutes);
        }

        private bool IsWorkQuotaReached(UnitTaskState state)
        {
            return _needFlowService.IsWorkQuotaReached(state);
        }

        private void EnterLocalNeedFlow(UnitTaskState state, UnitLocalNeedState nextNeed)
        {
            _needFlowService.EnterLocalNeedFlow(state, nextNeed);
        }

        private bool ProcessLocalNeedFlow(UnitTaskState state, float tickSeconds)
        {
            return _needFlowService.ProcessLocalNeedFlow(state, tickSeconds);
        }

        private bool TryDeferBuildForExcavation(UnitTaskState state, UnitTaskRecord task)
        {
            return _taskLifecycleService.TryDeferBuildForExcavation(state, task, _currentTickContext);
        }

        private void StartResourceDelivery(UnitTaskState state, int minedAmount)
        {
            _resourceDeliveryService.StartResourceDelivery(state, state.StartedDigCellType, minedAmount);
        }

        private void ProcessResourceDelivery(UnitTaskState state, float tickSeconds)
        {
            _resourceDeliveryService.ProcessResourceDelivery(state, tickSeconds);
        }

        private void ClearCarriedResource(UnitTaskState state)
        {
            _resourceDeliveryService.ClearCarriedResource(state);
        }

        private bool TryFindNearestStorageDeliveryCell(
            int unitId,
            Vector2Int unitCell,
            out Vector2Int storageCell,
            out Vector2Int deliveryCell)
        {
            return _resourceDeliveryService.TryFindNearestStorageDeliveryCell(unitId, unitCell, out storageCell, out deliveryCell);
        }

        private void StartTaskVisitTracking(UnitTaskState state, int taskId)
        {
            _taskLifecycleService.StartTaskVisitTracking(state, taskId);
        }

        private void TryResetLoopingTask(UnitTaskState state, UnitTaskRecord task)
        {
            _taskLifecycleService.TryResetLoopingTask(state, task, ResetUnitTask);
        }

        private bool IsTaskOnCooldown(int unitId, int taskId)
        {
            return _taskLifecycleService.IsTaskOnCooldown(unitId, taskId);
        }

        /// <summary>
        /// Clears unit runtime task state and returns unit to Idle.
        /// </summary>
        private void ResetUnitTask(UnitTaskState state, bool clearDeferredBuildTask = true)
        {
            _taskLifecycleService.ResetUnitTask(state, clearDeferredBuildTask);
            ClearIdleWanderState(state);
        }

        /// <summary>
        /// Applies gravity fall if unit is in air and updates goals/reservations after landing.
        /// </summary>
        private bool TryApplyGravityFall(UnitTaskState state)
        {
            return _movementRuntimeService.TryApplyGravityFall(state, _syncActorPositionBuffered);
        }

        /// <summary>
        /// Clears manual move flag and returns unit to idle state.
        /// </summary>
        private static void ClearManualMoveOrder(UnitTaskState state)
        {
            state.HasManualMoveOrder = false;
            state.ManualMoveNoProgressSeconds = 0f;
            state.SetIdle();
        }

        private bool TryStartIdleWander(UnitTaskState state)
        {
            int horizontalSign = _idleWanderRandom.Next(0, 2) == 0 ? -1 : 1;
            Vector2Int candidateCell = BuildIdleWanderCandidateCell(state.CurrentCell, horizontalSign);
            if (!_workCellResolver.TryFindClosestReachableCellWithinRadius(
                    state.UnitId,
                    state.CurrentCell,
                    candidateCell,
                    IDLE_WANDER_RADIUS_CELLS,
                    out Vector2Int reachableCell)
                || reachableCell == state.CurrentCell)
            {
                return false;
            }

            // Idle wandering stays local to the unit and must not reserve global tasks.
            state.HasIdleWanderOrder = true;
            state.IdleWanderTargetCell = reachableCell;
            state.IdleWanderPauseRemainingSeconds = 0f;
            state.IdleNoTaskSeconds = 0f;
            state.CurrentGoalCell = reachableCell;
            state.CurrentTaskTargetCell = candidateCell;
            state.MoveNoProgressSeconds = 0f;
            state.NoProgressTicks = 0;
            state.SetMoving(reachableCell);
            return true;
        }

        private void CompleteIdleWander(UnitTaskState state)
        {
            state.HasIdleWanderOrder = false;
            state.IdleWanderTargetCell = state.CurrentCell;
            state.IdleWanderPauseRemainingSeconds = PickIdleWanderStartDelaySeconds();
            state.IdleNoTaskSeconds = 0f;
            state.CurrentGoalCell = state.CurrentCell;
            state.CurrentTaskTargetCell = state.CurrentCell;
            state.MoveNoProgressSeconds = 0f;
            state.NoProgressTicks = 0;
            state.SetIdle();
            _navigation.ClearPath(state.UnitId);
        }

        private void ClearIdleWanderState(UnitTaskState state)
        {
            state.IdleNoTaskSeconds = 0f;
            state.HasIdleWanderOrder = false;
            state.IdleWanderTargetCell = state.CurrentCell;
            state.IdleWanderPauseRemainingSeconds = PickIdleWanderStartDelaySeconds();
        }

        private float PickIdleWanderStartDelaySeconds()
        {
            double random01 = _idleWanderRandom.NextDouble();
            return IDLE_WANDER_MIN_START_DELAY_SECONDS
                   + (float)random01 * (IDLE_WANDER_MAX_START_DELAY_SECONDS - IDLE_WANDER_MIN_START_DELAY_SECONDS);
        }

        private Vector2Int BuildIdleWanderCandidateCell(Vector2Int originCell, int horizontalSign)
        {
            int offsetX = _idleWanderRandom.Next(1, IDLE_WANDER_RADIUS_CELLS + 1) * horizontalSign;
            int offsetY = _idleWanderRandom.Next(-IDLE_WANDER_RADIUS_CELLS, IDLE_WANDER_RADIUS_CELLS + 1);
            return new Vector2Int(originCell.x + offsetX, originCell.y + offsetY);
        }

        /// <summary>
        /// Synchronizes actor transform with current grid cell center.
        /// </summary>
        private void SyncActorPosition(UnitTaskState state)
        {
            // Branch rule: if (state.Actor == null) we take this path; otherwise flow continues.
            if (state.Actor == null) return;

            Vector2 world = _gridCoordinateConverter.CellToWorldCenter(state.CurrentCell);
            Vector3 targetWorld = new Vector3(world.x, world.y, state.Actor.transform.position.z);
            Vector3 delta = targetWorld - state.Actor.transform.position;

            // Gravity-driven relocation should still mark the current locomotion clip explicitly.
            if (delta.y < -0.001f)
            {
                state.Actor.SetMovementAnimationAction(MovementActionType.Fall, true);
            }
            else if (delta.y > 0.001f)
            {
                state.Actor.SetMovementAnimationAction(MovementActionType.JumpUp1);
            }
            else
            {
                state.Actor.SetMovementAnimationAction(MovementActionType.Walk);
            }

            // Movement target is updated so actor smoothly moves to the new cell center.
            state.Actor.SetMoveTarget(targetWorld);
        }

        private void SyncActorStepPosition(
            UnitTaskState state,
            Vector2Int fromCell,
            Vector2Int toCell,
            MovementActionType actionType)
        {
            if (state.Actor == null)
            {
                return;
            }

            state.Actor.SetMovementAnimationAction(actionType);

            Vector2 targetWorld2 = _gridCoordinateConverter.CellToWorldCenter(toCell);
            Vector3 finalWorldPosition = new Vector3(targetWorld2.x, targetWorld2.y, state.Actor.transform.position.z);

            if (TryBuildStepWaypointWorldPosition(fromCell, toCell, actionType, state.Actor.transform.position.z, out Vector3 waypointWorldPosition))
            {
                state.Actor.SetMoveTargetViaWaypoint(waypointWorldPosition, finalWorldPosition);
                return;
            }

            state.Actor.SetMoveTarget(finalWorldPosition);
        }

        private bool TryBuildStepWaypointWorldPosition(
            Vector2Int fromCell,
            Vector2Int toCell,
            MovementActionType actionType,
            float zPosition,
            out Vector3 waypointWorldPosition)
        {
            waypointWorldPosition = default;

            Vector2Int delta = toCell - fromCell;
            if (Mathf.Abs(delta.x) != 1 || Mathf.Abs(delta.y) != 1)
            {
                return false;
            }

            Vector2 waypointWorld2;
            if (actionType == MovementActionType.JumpUp1)
            {
                waypointWorld2 = _gridCoordinateConverter.CellToWorldCenter(new Vector2Int(fromCell.x, toCell.y));
            }
            else if (actionType == MovementActionType.Fall)
            {
                waypointWorld2 = _gridCoordinateConverter.CellToWorldCenter(new Vector2Int(toCell.x, fromCell.y));
            }
            else
            {
                return false;
            }

            waypointWorldPosition = new Vector3(waypointWorld2.x, waypointWorld2.y, zPosition);
            return true;
        }

        /// <summary>
        /// Checks whether work can start from current unit cell for the given task.
        /// </summary>
        private bool CanStartWorkFromCell(Vector2Int unitCell, UnitTaskRecord task)
        {
            return _workCellResolver.CanStartWorkFromCell(unitCell, task);
        }

        /// <summary>
        /// Checks whether at least one registered unit can reach a valid work cell for a user dig task.
        /// </summary>
        public bool CanAnyUnitReachDigTaskCell(Vector2Int targetCell)
        {
            if (!_grid.IsInside(targetCell.x, targetCell.y))
            {
                return false;
            }

            var digTask = new UnitTaskRecord
            {
                TaskType = UnitTaskType.DigCell,
                TargetCell = targetCell
            };

            for (int i = 0; i < _stateStore.UnitOrder.Count; i++)
            {
                int unitId = _stateStore.UnitOrder[i];
                if (!_stateStore.StatesByUnitId.TryGetValue(unitId, out UnitTaskState state))
                {
                    continue;
                }

                if (state.Actor == null)
                {
                    continue;
                }

                if (_workCellResolver.TryFindWorkCell(state.UnitId, state.CurrentCell, digTask, out _))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns snapshot of unit cells for UI/debug use.
        /// </summary>
        public List<Vector2Int> GetUnitCellsSnapshot()
        {
            return _diagnosticsService.GetUnitCellsSnapshot(_stateStore.UnitOrder, _stateStore.StatesByUnitId);
        }

        /// <summary>
        /// Returns lightweight diagnostics for HUD/debug panels.
        /// </summary>
        public List<UnitDiagnosticsSnapshot> GetUnitDiagnosticsSnapshot()
        {
            return _diagnosticsService.GetUnitDiagnosticsSnapshot(_stateStore.UnitOrder, _stateStore.StatesByUnitId);
        }

        /// <summary>
        /// Returns per-unit eligibility diagnostics for one task id.
        /// Useful for HUD/debug when a task stays open and is not picked.
        /// </summary>
        public string BuildPerUnitTaskEligibilitySummary(int taskId)
        {
            return _diagnosticsService.BuildPerUnitTaskEligibilitySummary(
                taskId,
                _stateStore.UnitOrder,
                _stateStore.StatesByUnitId,
                _workCellResolver,
                _buildingManager,
                IsTaskOnCooldown);
        }


    }

    public readonly struct UnitDiagnosticsSnapshot
    {
        public readonly int UnitId;
        public readonly string CharacterNameKey;
        public readonly string DisplayName;
        public readonly UnitExecutionState ExecutionState;
        public readonly UnitLocalNeedState LocalNeedState;
        public readonly string GlobalTaskBlockReason;
        public readonly string FoodPreferencesSummary;
        public readonly int Hunger;
        public readonly int SleepDesire;
        public readonly int Mood;
        public readonly float WorkedMinutesWindow;
        public readonly float WorkQuotaMinutes;
        public readonly float SleepTotalMinutes;
        public readonly float SleepRemainingMinutes;
        public readonly float EatTotalMinutes;
        public readonly float EatRemainingMinutes;
        public readonly float RestElapsedMinutes;
        public readonly float RestTargetMinutes;
        public readonly float MealTargetMinutes;
        public readonly float CurrentMoveSpeed;
        public readonly float EffectiveMoveSpeed;
        public readonly float MoveLerpSpeed;
        public readonly float SimulationSpeedMultiplier;
        public readonly float MovementAnimationSpeedMultiplier;
        public readonly float MovementAnimationPlaybackSpeed;

        public UnitDiagnosticsSnapshot(
            int unitId,
            string characterNameKey,
            string displayName,
            UnitExecutionState executionState,
            UnitLocalNeedState localNeedState,
            string globalTaskBlockReason,
            string foodPreferencesSummary,
            int hunger,
            int sleepDesire,
            int mood,
            float workedMinutesWindow,
            float workQuotaMinutes,
            float sleepTotalMinutes,
            float sleepRemainingMinutes,
            float eatTotalMinutes,
            float eatRemainingMinutes,
            float restElapsedMinutes,
            float restTargetMinutes,
            float mealTargetMinutes,
            float currentMoveSpeed,
            float effectiveMoveSpeed,
            float moveLerpSpeed,
            float simulationSpeedMultiplier,
            float movementAnimationSpeedMultiplier,
            float movementAnimationPlaybackSpeed)
        {
            UnitId = unitId;
            CharacterNameKey = characterNameKey;
            DisplayName = displayName;
            ExecutionState = executionState;
            LocalNeedState = localNeedState;
            GlobalTaskBlockReason = globalTaskBlockReason;
            FoodPreferencesSummary = string.IsNullOrWhiteSpace(foodPreferencesSummary) ? "-" : foodPreferencesSummary;
            Hunger = hunger;
            SleepDesire = sleepDesire;
            Mood = mood;
            WorkedMinutesWindow = workedMinutesWindow;
            WorkQuotaMinutes = workQuotaMinutes;
            SleepTotalMinutes = sleepTotalMinutes;
            SleepRemainingMinutes = sleepRemainingMinutes;
            EatTotalMinutes = eatTotalMinutes;
            EatRemainingMinutes = eatRemainingMinutes;
            RestElapsedMinutes = restElapsedMinutes;
            RestTargetMinutes = restTargetMinutes;
            MealTargetMinutes = mealTargetMinutes;
            CurrentMoveSpeed = currentMoveSpeed;
            EffectiveMoveSpeed = effectiveMoveSpeed;
            MoveLerpSpeed = moveLerpSpeed;
            SimulationSpeedMultiplier = simulationSpeedMultiplier;
            MovementAnimationSpeedMultiplier = movementAnimationSpeedMultiplier;
            MovementAnimationPlaybackSpeed = movementAnimationPlaybackSpeed;
        }
    }
}