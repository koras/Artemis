using System;
using System.Collections.Generic;
using _Project.Scripts.Data.Grid;
using _Project.Scripts.Data.Tasks;
using _Project.Scripts.Systems.Units.Orchestrator;
using UnityEngine;

namespace _Project.Scripts.Systems.Units
{
    public sealed class UnitTaskLifecycleService
    {
        private readonly UnitOrchestratorContext _context;
        private readonly UnitTaskAcquisitionService _taskAcquisitionService;
        private readonly Dictionary<int, Dictionary<int, float>> _taskCooldownsByUnitId;
        private readonly Action<UnitTaskState> _clearCarriedResource;
        private readonly Func<Vector2Int, UnitTaskRecord, bool> _canStartWorkFromCell;
        private readonly float _taskLoopCooldownSeconds;

        public UnitTaskLifecycleService(
            UnitOrchestratorContext context,
            UnitTaskAcquisitionService taskAcquisitionService,
            Dictionary<int, Dictionary<int, float>> taskCooldownsByUnitId,
            Action<UnitTaskState> clearCarriedResource,
            Func<Vector2Int, UnitTaskRecord, bool> canStartWorkFromCell,
            float taskLoopCooldownSeconds)
        {
            _context = context;
            _taskAcquisitionService = taskAcquisitionService;
            _taskCooldownsByUnitId = taskCooldownsByUnitId;
            _clearCarriedResource = clearCarriedResource;
            _canStartWorkFromCell = canStartWorkFromCell;
            _taskLoopCooldownSeconds = taskLoopCooldownSeconds;
        }

        public bool TryAcquireTask(UnitTaskState state, int currentTick, out string blockReason)
        {
            return _taskAcquisitionService.TryAcquireTask(state, currentTick, out blockReason);
        }

        public bool TryDeferBuildForExcavation(UnitTaskState state, UnitTaskRecord task, int currentTickContext)
        {
            return _taskAcquisitionService.TryDeferBuildForExcavation(state, task, currentTickContext);
        }

        public void StartTaskVisitTracking(UnitTaskState state, int taskId)
        {
            state.VisitTrackedTaskId = taskId;
            state.CellVisitsByCurrentTask.Clear();
            state.CellVisitsByCurrentTask[state.CurrentCell] = 1;
        }

        public void TryResetLoopingTask(UnitTaskState state, UnitTaskRecord task, Action<UnitTaskState, bool> resetUnitTask)
        {
            if (state.CurrentTaskId == 0) return;
            if (state.State == UnitExecutionState.Working) return;
            if (_canStartWorkFromCell(state.CurrentCell, task)) return;

            if (state.VisitTrackedTaskId != state.CurrentTaskId)
            {
                StartTaskVisitTracking(state, state.CurrentTaskId);
                return;
            }

            int visitCount = 0;
            state.CellVisitsByCurrentTask.TryGetValue(state.CurrentCell, out visitCount);
            visitCount++;
            state.CellVisitsByCurrentTask[state.CurrentCell] = visitCount;
            if (visitCount < 2) return;

            AddTaskCooldown(state.UnitId, state.CurrentTaskId);
            _context.TaskBoard.ReleaseTaskReservation(state.CurrentTaskId, state.UnitId, "loop-detected");
            resetUnitTask(state, true);
        }

        public bool IsTaskOnCooldown(int unitId, int taskId)
        {
            if (!_taskCooldownsByUnitId.TryGetValue(unitId, out Dictionary<int, float> cooldowns)) return false;
            if (!cooldowns.TryGetValue(taskId, out float cooldownEndTime)) return false;
            if (Time.time < cooldownEndTime) return true;

            cooldowns.Remove(taskId);
            return false;
        }

        public void AddTaskCooldown(int unitId, int taskId)
        {
            if (!_taskCooldownsByUnitId.TryGetValue(unitId, out Dictionary<int, float> cooldowns))
            {
                cooldowns = new Dictionary<int, float>();
                _taskCooldownsByUnitId[unitId] = cooldowns;
            }

            cooldowns[taskId] = Time.time + _taskLoopCooldownSeconds;
        }

        public void ResetUnitTask(UnitTaskState state, bool clearDeferredBuildTask)
        {
            state.CurrentTaskId = 0;
            state.SetIdle();
            state.RemainingWorkSeconds = 0f;
            state.StartedDigCellType = CellType.Empty;
            state.StartedDigResourceAmount = 0;
            state.NoProgressTicks = 0;
            state.MoveNoProgressSeconds = 0f;
            state.VisitTrackedTaskId = 0;
            state.CellVisitsByCurrentTask.Clear();
            state.EatTotalMinutes = 0f;
            state.EatRemainingMinutes = 0f;
            state.CurrentEatRestorePoints = 0;
            state.HasLoggedMissingEatRoute = false;
            state.HasLoggedMissingFoodAtStorage = false;
            _context.Navigation.ClearPath(state.UnitId);
            _clearCarriedResource(state);
            if (clearDeferredBuildTask)
            {
                state.DeferredBuildTaskId = 0;
            }
        }

        public void TickGlobalTaskRetryBreak(UnitTaskState state, float tickSeconds)
        {
            if (state.GlobalTaskRetryBreakRemainingSeconds <= 0f)
            {
                return;
            }

            state.GlobalTaskRetryBreakRemainingSeconds = Mathf.Max(0f, state.GlobalTaskRetryBreakRemainingSeconds - tickSeconds);
        }
    }
}
