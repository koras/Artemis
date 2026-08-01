using UnityEngine.Localization;
using _Project.Scripts.Data.Localization;
using _Project.Scripts.Systems.Units;

namespace _Project.Scripts.Presentation.UI
{
    internal static class CharacterDiagnosticsLocalizationKeys
    {
        public const string Title = "character.diagnostics.title";
        public const string NoCharacters = "character.diagnostics.no_characters";
        public const string NameKey = "character.diagnostics.name_key";
        public const string StateLabel = "character.diagnostics.state";
        public const string LocalStateLabel = "character.diagnostics.local";
        public const string WorkDecisionLabel = "character.diagnostics.work_decision";
        public const string TaskBlockReasonLabel = "character.diagnostics.task_block_reason";
        public const string FoodPreferencesLabel = "character.diagnostics.food_preferences";
        public const string Movement = "character.diagnostics.movement";
        public const string Animation = "character.diagnostics.animation";
        public const string Hunger = "character.diagnostics.hunger";
        public const string SleepDesire = "character.diagnostics.sleep_desire";
        public const string Mood = "character.diagnostics.mood";
        public const string SleepCycle = "character.diagnostics.sleep_cycle";
        public const string EatCycle = "character.diagnostics.eat_cycle";
        public const string Work24Hours = "character.diagnostics.work_24h";
        public const string RestCycle = "character.diagnostics.rest_cycle";
        public const string CycleDoneLeftTotal = "character.diagnostics.cycle.done_left_total";
        public const string CycleDoneLeftCurrentEat = "character.diagnostics.cycle.done_left_current_eat";
        public const string CycleDoneLeftTarget = "character.diagnostics.cycle.done_left_target";
        public const string NamePrefix = "character.name.prefix";
        public const string NameSuffix = "character.name.suffix";

        public const string StatePrefix = "character.diagnostics.state";
        public const string LocalStatePrefix = "character.diagnostics.local";

        public const string WorkDecisionYesAlready = "character.diagnostics.work_decision.yes_already";
        public const string WorkDecisionNoLocalNeed = "character.diagnostics.work_decision.no_local_need";
        public const string WorkDecisionNoQuota = "character.diagnostics.work_decision.no_quota";
        public const string WorkDecisionYesWillTake = "character.diagnostics.work_decision.yes_will_take";

        public const string TaskBlockReasonIdle = "character.diagnostics.reason.idle";
        public const string TaskBlockReasonManualMove = "character.diagnostics.reason.manual_move";
        public const string TaskBlockReasonLocalNeedFlow = "character.diagnostics.reason.local_need_flow";
        public const string TaskBlockReasonLocalNeed = "character.diagnostics.reason.local_need";
        public const string TaskBlockReasonDeliveringResource = "character.diagnostics.reason.delivering_resource";
        public const string TaskBlockReasonAlreadyHasTask = "character.diagnostics.reason.already_has_task";
        public const string TaskBlockReasonTaskAcquired = "character.diagnostics.reason.task_acquired";
        public const string TaskBlockReasonUnknown = "character.diagnostics.reason.unknown";
        public const string TaskBlockReasonIdleWandering = "character.diagnostics.reason.idle_wandering";
        public const string TaskBlockReasonIdleWanderSettle = "character.diagnostics.reason.idle_wander_settle";
        public const string TaskBlockReasonIdleWanderWait = "character.diagnostics.reason.idle_wander_wait";
        public const string TaskBlockReasonIdleWanderRetryPause = "character.diagnostics.reason.idle_wander_retry_pause";

        public static string State(UnitExecutionState state)
        {
            return LocalizationKeyBuilder.FromEnum(StatePrefix, state);
        }

        public static string LocalState(UnitLocalNeedState state)
        {
            return LocalizationKeyBuilder.FromEnum(LocalStatePrefix, state);
        }

        public static string Prefix(string value)
        {
            return $"{NamePrefix}.{value.Trim().ToLowerInvariant()}";
        }

        public static string Suffix(string value)
        {
            return $"{NameSuffix}.{value.Trim().ToLowerInvariant()}";
        }

        public static LocalizedString Localized(string key)
        {
            return new LocalizedString("UI", key);
        }
    }
}