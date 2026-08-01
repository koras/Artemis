using System;
using System.Collections.Generic;
using _Project.Scripts.Systems.Construction;
using _Project.Scripts.Systems.Resources;
using _Project.Scripts.Data.Tasks;
using _Project.Scripts.Presentation.Character;
using _Project.Scripts.Systems.Units.Orchestrator;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace _Project.Scripts.Systems.Units
{
    public sealed class UnitDiagnosticsService
    {
        private static readonly List<Vector2Int> UnitCellsSnapshotBuffer = new List<Vector2Int>(16);
        private static readonly List<UnitDiagnosticsSnapshot> UnitDiagnosticsSnapshotBuffer = new List<UnitDiagnosticsSnapshot>(16);
        private static readonly List<KeyValuePair<string, int>> FoodPreferenceEntriesBuffer = new List<KeyValuePair<string, int>>(16);
        private static readonly Comparison<KeyValuePair<string, int>> FoodPreferenceEntryComparison = CompareFoodPreferenceEntries;
        private static readonly Dictionary<CharacterActor, FoodPreferenceSummaryCache> FoodPreferenceSummaryByActor = new Dictionary<CharacterActor, FoodPreferenceSummaryCache>(16);
        private static readonly System.Text.StringBuilder FoodPreferenceSummaryBuilder = new System.Text.StringBuilder(128);
        private static readonly List<string> TaskEligibilityPartsBuffer = new List<string>(16);

        private readonly UnitOrchestratorContext _context;

        public UnitDiagnosticsService(UnitOrchestratorContext context)
        {
            _context = context;
        }

        public List<Vector2Int> GetUnitCellsSnapshot(List<int> unitOrder, Dictionary<int, UnitTaskState> statesByUnitId)
        {
            List<Vector2Int> result = UnitCellsSnapshotBuffer;
            result.Clear();
            if (result.Capacity < unitOrder.Count)
            {
                result.Capacity = unitOrder.Count;
            }

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
            List<UnitDiagnosticsSnapshot> result = UnitDiagnosticsSnapshotBuffer;
            result.Clear();
            if (result.Capacity < unitOrder.Count)
            {
                result.Capacity = unitOrder.Count;
            }

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

            List<string> parts = TaskEligibilityPartsBuffer;
            parts.Clear();
            if (parts.Capacity < unitOrder.Count)
            {
                parts.Capacity = unitOrder.Count;
            }

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

            string result = parts.Count == 0 ? "no units" : string.Join(" | ", parts);
            parts.Clear();
            return result;
        }

        private static string BuildFoodPreferencesSummary(CharacterActor actor)
        {
            if (actor == null || actor.FoodPreferences == null || actor.FoodPreferences.Count == 0)
            {
                return "-";
            }

            string localeCode = LocalizationSettings.SelectedLocale.Identifier.Code;
            if (FoodPreferenceSummaryByActor.TryGetValue(actor, out FoodPreferenceSummaryCache cached)
                && cached.Version == actor.FoodPreferencesVersion
                && cached.LocaleCode == localeCode)
            {
                return cached.Summary;
            }

            List<KeyValuePair<string, int>> entries = FoodPreferenceEntriesBuffer;
            entries.Clear();
            if (entries.Capacity < actor.FoodPreferences.Count)
            {
                entries.Capacity = actor.FoodPreferences.Count;
            }

            foreach (KeyValuePair<string, int> preference in actor.FoodPreferences)
            {
                entries.Add(preference);
            }

            entries.Sort(FoodPreferenceEntryComparison);

            System.Text.StringBuilder builder = FoodPreferenceSummaryBuilder;
            builder.Length = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }

                KeyValuePair<string, int> entry = entries[i];
                builder.Append(i + 1);
                builder.Append(". ");
                builder.Append(new LocalizedString("UI", ResourceLocalizationKeys.Name(entry.Key)).GetLocalizedString());
                builder.Append(" (");
                builder.Append(entry.Value);
                builder.Append(')');
            }

            string result = builder.ToString();
            FoodPreferenceSummaryByActor[actor] = new FoodPreferenceSummaryCache(
                actor.FoodPreferencesVersion,
                localeCode,
                result);
            entries.Clear();
            builder.Length = 0;
            return result;
        }

        private static int CompareFoodPreferenceEntries(
            KeyValuePair<string, int> left,
            KeyValuePair<string, int> right)
        {
            int scoreCompare = right.Value.CompareTo(left.Value);
            if (scoreCompare != 0)
            {
                return scoreCompare;
            }

            return string.CompareOrdinal(left.Key, right.Key);
        }

        private readonly struct FoodPreferenceSummaryCache
        {
            public readonly int Version;
            public readonly string LocaleCode;
            public readonly string Summary;

            public FoodPreferenceSummaryCache(int version, string localeCode, string summary)
            {
                Version = version;
                LocaleCode = localeCode;
                Summary = summary;
            }
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