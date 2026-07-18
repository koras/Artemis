using System;
using System.Collections.Generic;
using _Project.Scripts.Systems.Construction;
using _Project.Scripts.Data.Tasks;
using _Project.Scripts.Presentation.Character;
using _Project.Scripts.Systems.Units.Orchestrator;
using UnityEngine;

namespace _Project.Scripts.Systems.Units
{
    public sealed class UnitDiagnosticsService
    {
        private readonly UnitOrchestratorContext _context;

        public UnitDiagnosticsService(UnitOrchestratorContext context)
        {
            _context = context;
        }

        public List<Vector2Int> GetUnitCellsSnapshot(List<int> unitOrder, Dictionary<int, UnitTaskState> statesByUnitId)
        {
            var result = new List<Vector2Int>(unitOrder.Count);

            for (int i = 0; i < unitOrder.Count; i++)
            {
                int unitId = unitOrder[i];
                UnitTaskState state = statesByUnitId[unitId];
                result.Add(state.CurrentCell);
            }

            return result;
        }

        public List<UnitDiagnosticsSnapshot> GetUnitDiagnosticsSnapshot(List<int> unitOrder, Dictionary<int, UnitTaskState> statesByUnitId)
        {
            var result = new List<UnitDiagnosticsSnapshot>(unitOrder.Count);

            for (int i = 0; i < unitOrder.Count; i++)
            {
                int unitId = unitOrder[i];
                if (!statesByUnitId.TryGetValue(unitId, out UnitTaskState state) || state == null) continue;

                int hunger = state.Actor != null ? state.Actor.Hunger : 0;
                int sleepDesire = state.Actor != null ? state.Actor.SleepDesire : 0;
                int mood = state.Actor != null ? state.Actor.Mood : 0;
                float currentMoveSpeed = state.Actor != null ? state.Actor.CurrentMoveSpeed : 0f;
                float effectiveMoveSpeed = state.Actor != null ? state.Actor.EffectiveMoveSpeed : 0f;
                float moveLerpSpeed = state.Actor != null ? state.Actor.MoveLerpSpeed : 0f;
                float simulationSpeedMultiplier = state.Actor != null ? state.Actor.GlobalMovementSpeedMultiplier : 0f;
                float movementAnimationSpeedMultiplier = state.Actor != null ? state.Actor.MovementAnimationSpeedMultiplier : 0f;
                float movementAnimationPlaybackSpeed = state.Actor != null ? state.Actor.MovementAnimationPlaybackSpeed : 0f;
                result.Add(new UnitDiagnosticsSnapshot(
                    state.UnitId,
                    state.CharacterNameKey,
                    string.IsNullOrWhiteSpace(state.DisplayName) ? $"Unit {state.UnitId}" : state.DisplayName,
                    state.State,
                    state.LocalNeedState,
                    string.IsNullOrWhiteSpace(state.LastGlobalTaskBlockReason) ? "-" : state.LastGlobalTaskBlockReason,
                    BuildFoodPreferencesSummary(state.Actor),
                    hunger,
                    sleepDesire,
                    mood,
                    state.WorkedMinutesWindow,
                    _context.DailyWorkQuotaMinutes,
                    state.SleepTotalMinutes,
                    state.SleepRemainingMinutes,
                    state.EatTotalMinutes,
                    state.EatRemainingMinutes,
                    state.RestElapsedMinutes,
                    _context.DailyRestTargetMinutes,
                    _context.MealDurationMinutes,
                    currentMoveSpeed,
                    effectiveMoveSpeed,
                    moveLerpSpeed,
                    simulationSpeedMultiplier,
                    movementAnimationSpeedMultiplier,
                    movementAnimationPlaybackSpeed));
            }

            return result;
        }

        public string BuildPerUnitTaskEligibilitySummary(
            int taskId,
            List<int> unitOrder,
            Dictionary<int, UnitTaskState> statesByUnitId,
            UnitWorkCellResolver workCellResolver,
            BuildingManager buildingManager,
            Func<int, int, bool> isTaskOnCooldown)
        {
            if (!_context.TaskBoard.TryGetTask(taskId, out UnitTaskRecord task) || task == null)
            {
                return "task not found";
            }

            var parts = new List<string>(unitOrder.Count);
            for (int i = 0; i < unitOrder.Count; i++)
            {
                int unitId = unitOrder[i];
                if (!statesByUnitId.TryGetValue(unitId, out UnitTaskState state) || state == null)
                {
                    continue;
                }

                string displayName = string.IsNullOrWhiteSpace(state.DisplayName) ? $"Unit {state.UnitId}" : state.DisplayName;
                string reason = ExplainWhyUnitCannotTakeTask(state, task, workCellResolver, buildingManager, isTaskOnCooldown);
                parts.Add($"{displayName}({state.UnitId}): {reason}");
            }

            return parts.Count == 0 ? "no units" : string.Join(" | ", parts);
        }

        private static string BuildFoodPreferencesSummary(CharacterActor actor)
        {
            if (actor == null || actor.FoodPreferences == null || actor.FoodPreferences.Count == 0)
            {
                return "-";
            }

            var entries = new List<KeyValuePair<string, int>>(actor.FoodPreferences);
            entries.Sort((left, right) =>
            {
                int scoreCompare = right.Value.CompareTo(left.Value);
                if (scoreCompare != 0)
                {
                    return scoreCompare;
                }

                return string.CompareOrdinal(left.Key, right.Key);
            });

            var parts = new List<string>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                KeyValuePair<string, int> entry = entries[i];
                parts.Add($"{i + 1}. {entry.Key} ({entry.Value})");
            }

            return string.Join(", ", parts);
        }

        private static string ExplainWhyUnitCannotTakeTask(
            UnitTaskState state,
            UnitTaskRecord task,
            UnitWorkCellResolver workCellResolver,
            BuildingManager buildingManager,
            Func<int, int, bool> isTaskOnCooldown)
        {
            if (state.Actor == null)
            {
                return "no actor";
            }

            if (state.HasManualMoveOrder)
            {
                return "manual move active";
            }

            if (state.IsInLocalNeedFlow)
            {
                return $"local need flow: {state.LocalNeedState}";
            }

            if (state.CurrentTaskId != 0)
            {
                return $"already has task {state.CurrentTaskId}";
            }

            if (isTaskOnCooldown(state.UnitId, task.TaskId))
            {
                return "task cooldown";
            }

            if (task.Status == UnitTaskStatus.Reserved && task.ReservedByUnitId != state.UnitId)
            {
                return $"reserved by unit {task.ReservedByUnitId}";
            }

            if (task.Status == UnitTaskStatus.InProgress)
            {
                return $"in progress by unit {task.ReservedByUnitId}";
            }

            if (task.TaskType == UnitTaskType.BuildObject)
            {
                if (task.BuildPayload == null)
                {
                    return "missing build payload";
                }

                if (!buildingManager.HasBuildCost(task.BuildPayload))
                {
                    return "not enough resources";
                }

                if (task.BuildPayload.RemainingClearSubtasks > 0)
                {
                    return $"waiting for clearing: {task.BuildPayload.RemainingClearSubtasks}";
                }
            }

            if (task.TaskType == UnitTaskType.BuildLifeModule)
            {
                if (task.LifeModulePayload == null)
                {
                    return "missing life-module payload";
                }

                if (task.LifeModulePayload.RemainingClearSubtasks > 0)
                {
                    return $"waiting for clearing: {task.LifeModulePayload.RemainingClearSubtasks}";
                }
            }

            if (!workCellResolver.TryFindWorkCell(state.UnitId, state.CurrentCell, task, out _))
            {
                return $"unreachable: {workCellResolver.ExplainWhyNoWorkCell(state.UnitId, state.CurrentCell, task)}";
            }

            return "can take now";
        }
    }
}
