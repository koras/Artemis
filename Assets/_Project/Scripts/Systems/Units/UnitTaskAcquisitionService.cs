using System.Collections.Generic;
using _Project.Scripts.Data.Tasks;
using _Project.Scripts.Systems.Construction;
using _Project.Scripts.Systems.Tasks;
using UnityEngine;

namespace _Project.Scripts.Systems.Units
{
    /// <summary>
    /// Selects and reserves the best next task for a unit, including build-related subtask priorities.
    /// Выбирает и резервирует следующую лучшую задачу для юнита, включая приоритет подзадач строительства.
    /// </summary>
    public sealed class UnitTaskAcquisitionService
    {
        private const int MAX_SKIPPED_ATTEMPTS_PER_TASK = 5;
        private const float GLOBAL_TASK_RETRY_BREAK_SECONDS = 10f;

        private static readonly Dictionary<int, string> GlobalTaskBreakReasonByTenths = new Dictionary<int, string>(16);
        private static readonly Dictionary<int, string> WaitingDeferredBuildReasonByTaskId = new Dictionary<int, string>(16);
        private static readonly Dictionary<long, string> SkippedTaskReasonByKey = new Dictionary<long, string>(64);
        private static readonly Dictionary<int, string> SkippedTaskBreakReasonByTaskId = new Dictionary<int, string>(16);
        private static readonly Dictionary<ReservedUnreachableReasonKey, string> ReservedUnreachableReasonByKey = new Dictionary<ReservedUnreachableReasonKey, string>(32);

        private readonly GlobalTaskBoardService _taskBoard;
        private readonly BuildingManager _buildingManager;
        private readonly LifeModulePlacementService _lifeModulePlacementService;
        private readonly UnitWorkCellResolver _workCellResolver;
        private readonly bool _enableLogs;
        private readonly System.Func<int, int, bool> _isTaskOnCooldown;
        private readonly System.Action<UnitTaskState, int> _startTaskVisitTracking;
        private readonly List<BuildingManager.StorageDeliveryPoint> _storageDeliveryPointsBuffer = new List<BuildingManager.StorageDeliveryPoint>();
        private readonly List<Vector2Int> _storageDeliveryCellsBuffer = new List<Vector2Int>();
        private readonly System.Func<UnitTaskRecord, bool> _currentTaskAllowedPredicate;
        private readonly System.Func<UnitTaskRecord, bool> _currentTaskReachablePredicate;
        private readonly System.Func<UnitTaskRecord, bool> _currentDeferredClearAllowedPredicate;

        private UnitTaskState _predicateState;
        private int _predicateDeferredBuildTaskId;

        /// <summary>
        /// Creates the task acquisition service and stores all collaborators used during reservation and filtering.
        /// Создаёт сервис подбора задач и сохраняет зависимости, используемые для фильтрации и резервирования.
        /// </summary>
        public UnitTaskAcquisitionService(
            GlobalTaskBoardService taskBoard,
            BuildingManager buildingManager,
            LifeModulePlacementService lifeModulePlacementService,
            UnitWorkCellResolver workCellResolver,
            bool enableLogs,
            System.Func<int, int, bool> isTaskOnCooldown,
            System.Action<UnitTaskState, int> startTaskVisitTracking)
        {
            _taskBoard = taskBoard;
            _buildingManager = buildingManager;
            _lifeModulePlacementService = lifeModulePlacementService;
            _workCellResolver = workCellResolver;
            _enableLogs = enableLogs;
            _isTaskOnCooldown = isTaskOnCooldown;
            _startTaskVisitTracking = startTaskVisitTracking;
            _currentTaskAllowedPredicate = IsCurrentTaskAllowed;
            _currentTaskReachablePredicate = IsCurrentTaskReachable;
            _currentDeferredClearAllowedPredicate = IsCurrentDeferredClearAllowed;
        }
        /// <summary>
        /// Attempts to reserve a valid task for the unit and switch it into movement toward the selected work cell.
        /// Returns a readable block reason when no task can be acquired.
        /// Пытается зарезервировать подходящую задачу для юнита и перевести его в движение к выбранной рабочей клетке.
        /// Возвращает понятную причину блокировки, если задачу взять не удалось.
        /// </summary>
        public bool TryAcquireTask(UnitTaskState state, int currentTick, out string blockReason)
        {
            SetPredicateContext(state);
            blockReason = "none";
            if (state.GlobalTaskRetryBreakRemainingSeconds > 0f)
            {
                blockReason = GetGlobalTaskBreakReason(state.GlobalTaskRetryBreakRemainingSeconds);
                return false;
            }

            if (TryAcquirePendingBuildExcavation(state, currentTick))
            {
                ClearSkippedTaskAttempt(state, state.CurrentTaskId);
                return true;
            }

            if (state.DeferredBuildTaskId != 0)
            {
                if (!_taskBoard.TryGetTask(state.DeferredBuildTaskId, out UnitTaskRecord deferredBuildTask)
                    || deferredBuildTask.Status == UnitTaskStatus.Completed
                    || deferredBuildTask.Status == UnitTaskStatus.Failed)
                {
                    if (_enableLogs)
                    {
            // Debug.Log($"[Task AI] unit {state.UnitId}: deferred build {state.DeferredBuildTaskId} dropped (task already completed or removed).");
                    }
                    state.DeferredBuildTaskId = 0;
                }
            }

            if (state.DeferredBuildTaskId != 0)
            {
                _predicateDeferredBuildTaskId = state.DeferredBuildTaskId;
                bool reservedDeferredClear = _taskBoard.TryReserveBestTaskForUnit(
                    state.UnitId,
                    state.CurrentCell,
                    currentTick,
                    _currentDeferredClearAllowedPredicate,
                    _currentTaskReachablePredicate,
                    out UnitTaskRecord deferredClearTask);

                if (reservedDeferredClear
                    && _workCellResolver.TryFindWorkCellAndBuildPath(
                        state.UnitId,
                        state.CurrentCell,
                        deferredClearTask,
                        out Vector2Int deferredClearWorkCell))
                {
                    state.CurrentTaskId = deferredClearTask.TaskId;
                    state.CurrentTaskTargetCell = deferredClearTask.TargetCell;
                    state.SetMoving(deferredClearWorkCell);
                    state.NoProgressTicks = 0;
                    _startTaskVisitTracking(state, deferredClearTask.TaskId);
                    if (_enableLogs)
                    {
            // Debug.Log($"[Task AI] unit {state.UnitId}: took clear task {deferredClearTask.TaskId}.");
                    }
                    return true;
                }
                else if (reservedDeferredClear)
                {
                    _taskBoard.ReleaseTaskReservation(deferredClearTask.TaskId, state.UnitId, "unreachable");
                    if (_enableLogs)
                    {
            // Debug.Log($"[Task AI] unit {state.UnitId}: cannot start clear task {deferredClearTask.TaskId} - no reachable work cell.");
                    }
                }

                bool reservedDeferredBuild = _taskBoard.TryReserveTaskByIdForUnit(
                    state.DeferredBuildTaskId,
                    state.UnitId,
                    currentTick,
                    _currentTaskAllowedPredicate,
                    _currentTaskReachablePredicate,
                    out UnitTaskRecord deferredBuildTaskById);

                if (reservedDeferredBuild
                    && _workCellResolver.TryFindWorkCellAndBuildPath(
                        state.UnitId,
                        state.CurrentCell,
                        deferredBuildTaskById,
                        out Vector2Int deferredBuildWorkCell))
                {
                    state.CurrentTaskId = deferredBuildTaskById.TaskId;
                    state.CurrentTaskTargetCell = deferredBuildTaskById.TargetCell;
                    state.SetMoving(deferredBuildWorkCell);
                    state.NoProgressTicks = 0;
                    _startTaskVisitTracking(state, deferredBuildTaskById.TaskId);
                    if (_enableLogs)
                    {
            // Debug.Log($"[Task AI] unit {state.UnitId}: took deferred build task {deferredBuildTaskById.TaskId}.");
                    }
                    return true;
                }
                else if (reservedDeferredBuild)
                {
                    _taskBoard.ReleaseTaskReservation(deferredBuildTaskById.TaskId, state.UnitId, "unreachable");
                    if (_enableLogs)
                    {
            // Debug.Log($"[Task AI] unit {state.UnitId}: cannot start build task {deferredBuildTaskById.TaskId} - no reachable work cell.");
                    }
                }

                if (_enableLogs)
                {
            // Debug.Log($"[Task AI] unit {state.UnitId}: waiting on deferred build {state.DeferredBuildTaskId} - no reachable clear/build subtask yet.");
                }

                blockReason = GetWaitingDeferredBuildReason(state.DeferredBuildTaskId);
                return false;
            }

            var orderedTasks = _taskBoard.GetOpenTasksOrderedForUnit(state.CurrentCell, currentTick);
            if (orderedTasks.Count == 0)
            {
                if (_enableLogs)
                {
              //      Debug.Log($"[Task AI] unit {state.UnitId}: no suitable open tasks right now.");
                }
                blockReason = "no suitable open tasks (filtered/reserved/unreachable/out of visibility)";
                return false;
            }

            for (int i = 0; i < orderedTasks.Count; i++)
            {
                UnitTaskRecord candidateTask = orderedTasks[i];
                if (!IsTaskAllowedForUnit(state, candidateTask))
                {
                    if (RegisterSkippedTaskAttempt(state, candidateTask.TaskId, out blockReason))
                    {
                        return false;
                    }

                    continue;
                }

                if (!IsTaskReachableWithDebug(state, candidateTask))
                {
                    if (RegisterSkippedTaskAttempt(state, candidateTask.TaskId, out blockReason))
                    {
                        return false;
                    }

                    continue;
                }

                bool reserved = _taskBoard.TryReserveTaskByIdForUnit(
                    candidateTask.TaskId,
                    state.UnitId,
                    currentTick,
                    _currentTaskAllowedPredicate,
                    _currentTaskReachablePredicate,
                    out UnitTaskRecord reservedTask);

                if (!reserved)
                {
                    continue;
                }

                if (!_workCellResolver.TryFindWorkCellAndBuildPath(
                        state.UnitId,
                        state.CurrentCell,
                        reservedTask,
                        out Vector2Int workCell))
                {
                    _taskBoard.ReleaseTaskReservation(reservedTask.TaskId, state.UnitId, "unreachable");
                    if (RegisterSkippedTaskAttempt(state, reservedTask.TaskId, out blockReason))
                    {
                        return false;
                    }

                    string reason = _workCellResolver.ExplainWhyNoWorkCell(state.UnitId, state.CurrentCell, reservedTask);
                    blockReason = GetReservedUnreachableReason(reservedTask.TaskId, reason);
                    continue;
                }

                state.CurrentTaskId = reservedTask.TaskId;
                state.CurrentTaskTargetCell = reservedTask.TargetCell;
                state.SetMoving(workCell);
                state.NoProgressTicks = 0;
                _startTaskVisitTracking(state, reservedTask.TaskId);
                ClearSkippedTaskAttempt(state, reservedTask.TaskId);

                TryLogBuildTaskNotSelected(state, reservedTask);

                if (_enableLogs)
                {
                // Debug.Log($"[Task AI] unit {state.UnitId}: took task {reservedTask.TaskId} and started moving.");
                }

                blockReason = "task acquired";
                return true;
            }

            blockReason = "no reservable task after full global task pass";
            return false;
        }

        private void SetPredicateContext(UnitTaskState state)
        {
            _predicateState = state;
            _predicateDeferredBuildTaskId = state.DeferredBuildTaskId;
        }

        private bool IsCurrentTaskAllowed(UnitTaskRecord candidateTask)
        {
            return IsTaskAllowedForUnit(_predicateState, candidateTask);
        }

        private bool IsCurrentTaskReachable(UnitTaskRecord candidateTask)
        {
            return IsTaskReachableWithDebug(_predicateState, candidateTask);
        }

        private bool IsCurrentDeferredClearAllowed(UnitTaskRecord candidateTask)
        {
            return IsTaskAllowedForUnit(_predicateState, candidateTask)
                && candidateTask.TaskType == UnitTaskType.ClearBuildCell
                && candidateTask.ParentBuildTaskId == _predicateDeferredBuildTaskId;
        }

        private static string GetGlobalTaskBreakReason(float remainingSeconds)
        {
            int tenths = Mathf.RoundToInt(remainingSeconds * 10f);
            if (GlobalTaskBreakReasonByTenths.TryGetValue(tenths, out string reason))
            {
                return reason;
            }

            reason = $"global task break {tenths / 10f:0.0}s";
            GlobalTaskBreakReasonByTenths[tenths] = reason;
            return reason;
        }

        private static string GetWaitingDeferredBuildReason(int taskId)
        {
            if (WaitingDeferredBuildReasonByTaskId.TryGetValue(taskId, out string reason))
            {
                return reason;
            }

            reason = $"waiting deferred build {taskId}: no reachable clear/build subtask";
            WaitingDeferredBuildReasonByTaskId[taskId] = reason;
            return reason;
        }

        private static string GetSkippedTaskReason(int taskId, int attemptCount)
        {
            long key = ((long)taskId << 32) ^ (uint)attemptCount;
            if (SkippedTaskReasonByKey.TryGetValue(key, out string reason))
            {
                return reason;
            }

            reason = $"task {taskId} skipped {attemptCount}/{MAX_SKIPPED_ATTEMPTS_PER_TASK}";
            SkippedTaskReasonByKey[key] = reason;
            return reason;
        }

        private static string GetSkippedTaskBreakReason(int taskId)
        {
            if (SkippedTaskBreakReasonByTaskId.TryGetValue(taskId, out string reason))
            {
                return reason;
            }

            reason = $"task {taskId} skipped {MAX_SKIPPED_ATTEMPTS_PER_TASK}/{MAX_SKIPPED_ATTEMPTS_PER_TASK}; break for {GLOBAL_TASK_RETRY_BREAK_SECONDS:0}s";
            SkippedTaskBreakReasonByTaskId[taskId] = reason;
            return reason;
        }

        private static string GetReservedUnreachableReason(int taskId, string workCellReason)
        {
            var key = new ReservedUnreachableReasonKey(taskId, workCellReason);
            if (ReservedUnreachableReasonByKey.TryGetValue(key, out string reason))
            {
                return reason;
            }

            reason = $"task {taskId} reserved but unreachable: {workCellReason}";
            ReservedUnreachableReasonByKey[key] = reason;
            return reason;
        }

        private readonly struct ReservedUnreachableReasonKey : System.IEquatable<ReservedUnreachableReasonKey>
        {
            private readonly int _taskId;
            private readonly string _workCellReason;

            public ReservedUnreachableReasonKey(int taskId, string workCellReason)
            {
                _taskId = taskId;
                _workCellReason = workCellReason ?? string.Empty;
            }

            public bool Equals(ReservedUnreachableReasonKey other)
            {
                return _taskId == other._taskId && _workCellReason == other._workCellReason;
            }

            public override bool Equals(object obj)
            {
                return obj is ReservedUnreachableReasonKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (_taskId * 397) ^ _workCellReason.GetHashCode();
                }
            }
        }

        /// <summary>
        /// Prioritizes pending dig/clear subtasks inside build footprints so blocked build tasks can progress.
        /// Приоритизирует ожидающие задачи копки/очистки внутри зоны стройки, чтобы заблокированная стройка могла продвигаться.
        /// </summary>
        private bool TryAcquirePendingBuildExcavation(UnitTaskState state, int currentTick)
        {
            var openTasks = _taskBoard.GetOpenTasksSnapshot();
            for (int i = 0; i < openTasks.Count; i++)
            {
                UnitTaskRecord buildTask = openTasks[i];
                List<Vector2Int> pendingCells = null;

                if (buildTask.TaskType == UnitTaskType.BuildObject && buildTask.BuildPayload != null && buildTask.BuildPayload.RemainingClearSubtasks > 0)
                {
                    pendingCells = _taskBoard.GetPendingDigCellsInBuildFootprint(buildTask.BuildPayload);
                }
                else if (buildTask.TaskType == UnitTaskType.BuildLifeModule
                         && buildTask.LifeModulePayload != null
                         && buildTask.LifeModulePayload.RemainingClearSubtasks > 0)
                {
                    pendingCells = _taskBoard.GetPendingDigCellsInLifeModuleFootprint(buildTask.LifeModulePayload);
                }

                if (pendingCells == null || pendingCells.Count == 0)
                {
                    continue;
                }

                for (int j = 0; j < pendingCells.Count; j++)
                {
                    Vector2Int pendingCell = pendingCells[j];
                    if (!_taskBoard.TryGetTaskByCell(pendingCell, out UnitTaskRecord digOrClearTask) || digOrClearTask == null)
                    {
                        if (_enableLogs)
                        {
            // Debug.Log($"[Task AI] unit {state.UnitId}: build {buildTask.TaskId} waits clear cell ({pendingCell.x},{pendingCell.y}), but no task bound to that cell.");
                        }
                        continue;
                    }

                    if (digOrClearTask.TaskType != UnitTaskType.DigCell && digOrClearTask.TaskType != UnitTaskType.ClearBuildCell)
                    {
                        continue;
                    }

                    bool reserved = _taskBoard.TryReserveTaskByIdForUnit(
                        digOrClearTask.TaskId,
                        state.UnitId,
                        currentTick,
                        _currentTaskAllowedPredicate,
                        _currentTaskReachablePredicate,
                        out UnitTaskRecord reservedTask);

                    if (!reserved)
                    {
                        if (_enableLogs)
                        {
            // Debug.Log($"[Task AI] unit {state.UnitId}: pending clear task {digOrClearTask.TaskId} for build {buildTask.TaskId} not reserved (status={digOrClearTask.Status}, reservedBy={digOrClearTask.ReservedByUnitId}).");
                        }
                        continue;
                    }

                    if (!_workCellResolver.TryFindWorkCellAndBuildPath(
                            state.UnitId,
                            state.CurrentCell,
                            reservedTask,
                            out Vector2Int workCell))
                    {
                        _taskBoard.ReleaseTaskReservation(reservedTask.TaskId, state.UnitId, "pending_clear_unreachable");
                        continue;
                    }

                    state.CurrentTaskId = reservedTask.TaskId;
                    state.CurrentTaskTargetCell = reservedTask.TargetCell;
                    state.SetMoving(workCell);
                    state.NoProgressTicks = 0;
                    _startTaskVisitTracking(state, reservedTask.TaskId);
                    ClearSkippedTaskAttempt(state, reservedTask.TaskId);
                    if (_enableLogs)
                    {
            // Debug.Log($"[Task AI] unit {state.UnitId}: took pending clear task {reservedTask.TaskId} for build {buildTask.TaskId}.");
                    }
                    return true;
                }
            }

            return false;
        }
        /// <summary>
        /// Legacy extension point for deferring build tasks until excavation subtasks are ready.
        /// Currently disabled because subtasks are generated earlier in the build pipeline.
        /// Унаследованная точка расширения для откладывания стройки до готовности подзадач раскопки.
        /// Сейчас отключена, так как подзадачи создаются раньше, на этапе постановки стройки.
        /// </summary>
        public bool TryDeferBuildForExcavation(UnitTaskState state, UnitTaskRecord task, int currentTick)
        {
            // Early pipeline: clear subtasks are created when build is queued, not during runtime acquisition.
            return false;
        }
        /// <summary>
        /// Applies unit-level eligibility rules for a candidate task: cooldown, deferred-build scope, payload validity, excavation gates, and resource checks.
        /// Применяет правила доступности задачи для юнита: кулдаун, ограничения отложенной стройки, валидность payload, условия раскопки и ресурсы.
        /// </summary>
        private bool IsTaskAllowedForUnit(UnitTaskState state, UnitTaskRecord candidateTask)
        {
            if (_isTaskOnCooldown(state.UnitId, candidateTask.TaskId))
            {
                if (_enableLogs)
                {
            // Debug.Log($"[Task AI] unit {state.UnitId}: skip task {candidateTask.TaskId} - cooldown is active.");
                }
                return false;
            }

            if (candidateTask.TaskType == UnitTaskType.ClearBuildCell)
            {
                if (state.DeferredBuildTaskId == 0) return true;
                return candidateTask.ParentBuildTaskId == state.DeferredBuildTaskId;
            }

            if (candidateTask.TaskType == UnitTaskType.BuildLifeModule)
            {
                if (candidateTask.LifeModulePayload == null)
                {
                    return false;
                }

                int pendingLifeModuleClearSubtasks = _taskBoard.CountPendingDigTasksInLifeModuleFootprint(candidateTask.LifeModulePayload);
                candidateTask.LifeModulePayload.RemainingClearSubtasks = pendingLifeModuleClearSubtasks;
                candidateTask.LifeModulePayload.IsExcavatingBeforeBuild = pendingLifeModuleClearSubtasks > 0;
                if (!_lifeModulePlacementService.HasBuildCost(candidateTask.LifeModulePayload))
                {
                    return false;
                }

                return pendingLifeModuleClearSubtasks <= 0;
            }

            if (candidateTask.TaskType != UnitTaskType.BuildObject) return true;
            if (candidateTask.BuildPayload == null)
            {
                if (_enableLogs)
                {
            // Debug.Log($"[Task AI] unit {state.UnitId}: skip build {candidateTask.TaskId} - missing build payload.");
                }
                return false;
            }

            // Keep build gating in sync with real pending dig/clear tasks in footprint.
            int pendingClearSubtasks = _taskBoard.CountPendingDigTasksInBuildFootprint(candidateTask.BuildPayload);
            candidateTask.BuildPayload.RemainingClearSubtasks = pendingClearSubtasks;
            candidateTask.BuildPayload.IsExcavatingBeforeBuild = pendingClearSubtasks > 0;

            if (!_buildingManager.HasBuildCost(candidateTask.BuildPayload))
            {
                if (_enableLogs)
                {
            // Debug.Log($"[Task AI] unit {state.UnitId}: skip build {candidateTask.TaskId} - not enough resources.");
                }
                return false;
            }

            if (pendingClearSubtasks > 0)
            {
                return false;
            }

            return true;
        }
        /// <summary>
        /// Verifies that the unit can reach a usable work cell for the candidate task.
        /// For dropped-resource delivery tasks, also checks that delivery to some storage is possible.
        /// Проверяет, может ли юнит добраться до рабочей клетки для кандидатной задачи.
        /// Для доставки брошенного ресурса дополнительно проверяет, что существует возможность сдать ресурс в склад.
        /// </summary>
        private bool IsTaskReachableWithDebug(UnitTaskState state, UnitTaskRecord candidateTask)
        {
            bool reachable = _workCellResolver.TryFindWorkCell(state.UnitId, state.CurrentCell, candidateTask, out Vector2Int workCell);
            if (reachable && candidateTask != null && candidateTask.TaskType == UnitTaskType.DeliverDroppedResource)
            {
                reachable = CanDeliverDroppedResourceFromWorkCell(state.UnitId, workCell);
            }
            if (!reachable && _enableLogs && candidateTask != null && candidateTask.TaskType == UnitTaskType.BuildObject)
            {
            // Debug.Log($"[Task AI] unit {state.UnitId}: build task {candidateTask.TaskId} unreachable. Reason: {reason}. Neighbors around target: {neighbors}.");
            }

            return reachable;
        }
        /// <summary>
        /// Checks whether at least one storage cell can be served from the selected work cell after pickup.
        /// Проверяет, можно ли из выбранной рабочей клетки обслужить хотя бы один склад после подбора ресурса.
        /// </summary>
        private bool CanDeliverDroppedResourceFromWorkCell(int unitId, Vector2Int workCell)
        {
            _buildingManager.FillActiveStorageDeliveryPoints(_storageDeliveryPointsBuffer);
            _storageDeliveryCellsBuffer.Clear();
            for (int i = 0; i < _storageDeliveryPointsBuffer.Count; i++)
            {
                _storageDeliveryCellsBuffer.Add(_storageDeliveryPointsBuffer[i].DeliveryCell);
            }

            return _workCellResolver.TryFindNearestReachableExactCell(
                unitId,
                workCell,
                _storageDeliveryCellsBuffer,
                out _);
        }
        /// <summary>
        /// Logs diagnostics when a non-build task wins while an eligible build task was also available.
        /// Логирует диагностику, когда выбрана не-строительная задача при наличии доступной строительной задачи.
        /// </summary>
        private void TryLogBuildTaskNotSelected(UnitTaskState state, UnitTaskRecord selectedTask)
        {
            if (!_enableLogs) return;
            if (selectedTask == null) return;
            if (selectedTask.TaskType == UnitTaskType.BuildObject) return;

            var openTasks = _taskBoard.GetOpenTasksSnapshot();
            for (int i = 0; i < openTasks.Count; i++)
            {
                UnitTaskRecord candidate = openTasks[i];
                if (candidate.TaskType != UnitTaskType.BuildObject) continue;
                if (!IsTaskAllowedForUnit(state, candidate)) continue;
                if (!_workCellResolver.TryFindWorkCell(state.UnitId, state.CurrentCell, candidate, out _)) continue;

            // Debug.Log($"[Task AI] unit {state.UnitId}: build {candidate.TaskId} is available, but task {selectedTask.TaskId} won by score.");
                return;
            }
        }

        /// <summary>
        /// Tracks how many full acquisition passes rejected the same task for this unit.
        /// After five rejections the unit clears all counters and pauses global task search for ten seconds.
        /// </summary>
        private bool RegisterSkippedTaskAttempt(UnitTaskState state, int taskId, out string blockReason)
        {
            int attemptCount = 1;
            if (state.SkippedGlobalTaskAttemptsByTaskId.TryGetValue(taskId, out int existingCount))
            {
                attemptCount = existingCount + 1;
            }

            state.SkippedGlobalTaskAttemptsByTaskId[taskId] = attemptCount;
            if (attemptCount < MAX_SKIPPED_ATTEMPTS_PER_TASK)
            {
                blockReason = GetSkippedTaskReason(taskId, attemptCount);
                return false;
            }

            state.SkippedGlobalTaskAttemptsByTaskId.Clear();
            state.GlobalTaskRetryBreakRemainingSeconds = GLOBAL_TASK_RETRY_BREAK_SECONDS;
            blockReason = GetSkippedTaskBreakReason(taskId);
            return true;
        }

        private static void ClearSkippedTaskAttempt(UnitTaskState state, int taskId)
        {
            if (taskId == 0)
            {
                return;
            }

            state.SkippedGlobalTaskAttemptsByTaskId.Remove(taskId);
        }
    }
}