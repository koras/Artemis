using UnityEngine;
using UnityEngine.Localization;
using _Project.Scripts.Data.Localization;

namespace _Project.Scripts.Data.Offers
{
    /// <summary>
    /// Шаблон оффера: условия появления, требования выполнения, награды и штрафы репутации.
    /// </summary>
    [LocalizationNamespace("offer", nameof(OfferDefinition.OfferId))]
    [CreateAssetMenu(menuName = "Artemis/Offers/Offer Definition", fileName = "OfferDefinition")]
    public sealed class OfferDefinition : BaseLocalizedConfigDefinition
    {
        [Header("Identity")]
        [LocalizationId("Offer Id")]
        [HideInInspector] public string OfferId = "offer-id";
        public OfferCustomerDefinition Customer;
        public string Title;
        [TextArea] public string Description;
        public OfferArchetype Archetype = OfferArchetype.BulkExport;
        public OfferCategory Category = OfferCategory.Economic;
        public string OfferAffinityTag;
        [TextArea] public string IntroMessage;
        [TextArea] public string AcceptMessage;
        [TextArea] public string CompleteMessage;
        [TextArea] public string FailMessage;

        [Header("Unlock Conditions")]
        public OfferTriggerType TriggerTypes = OfferTriggerType.Time | OfferTriggerType.Manual;
        public OfferResourceAmount[] AppearanceRequirements;
        public int MinGameMinutesToAppear;
        [Range(0f, 1f)] public float HourlySpawnChance = 1f;
        public OfferResourceEventCondition[] ResourceEventConditions;
        [Min(0f)] public float BaseGenerationWeight = 1f;
        [Range(0, 100)] public int MinCustomerReputationToAppear;
        public OfferUnlockCondition[] ExtraUnlockConditions;

        [Header("Repeatability")]
        public bool IsRepeatable = true;
        [Min(0)] public int CooldownGameMinutes = 120;

        [Header("Branching And Chains")]
        public string ChainId;
        [Min(0)] public int ChainStep;
        public string ExclusiveGroupId;

        [Header("Completion Requirements")]
        public OfferResourceAmount[] CompletionRequirements;
        public OfferStageDefinition[] Stages;
        public OfferFailureCondition[] FailureConditions;

        [Header("Rewards And Reputation")]
        public int GoldReward = 25;
        public int ReputationReward = 10;
        public int ReputationPenaltyOnFail = 10;
        public int ReputationPenaltyOnReject;
        [Min(0)] public int FastReserveBonusGold;
        [Min(0)] public int FastReserveWindowHours = 12;
        public OfferOutcomeDefinition[] Outcomes;

        [Header("Deadline")]
        public bool UseDeadline = true;
        [Min(1)] public int DeadlineDays = 7;

        public bool HasStages => Stages != null && Stages.Length > 0;

        /// <summary>Возвращает ключ локализации для текстового поля оффера.</summary>
        public LocalizedString GetLocalizedField(string field)
        {
            return OfferLocalizationKeys.Localized(OfferLocalizationKeys.Offer(OfferId, field));
        }

        /// <summary>Возвращает локализованные заголовок и описание оффера.</summary>
        public LocalizedString GetLocalizedTitle() => GetLocalizedField("title");
        public LocalizedString GetLocalizedDescription() => GetLocalizedField("description");

        /// <summary>Возвращает локализованные сообщения жизненного цикла оффера.</summary>
        public LocalizedString GetLocalizedIntroMessage() => GetLocalizedField("intro");
        public LocalizedString GetLocalizedAcceptMessage() => GetLocalizedField("accept");
        public LocalizedString GetLocalizedCompleteMessage() => GetLocalizedField("complete");
        public LocalizedString GetLocalizedFailMessage() => GetLocalizedField("fail");

        /// <summary>Возвращает локализованный текст этапа.</summary>
        public LocalizedString GetLocalizedStageField(int stageIndex, string field)
        {
            return OfferLocalizationKeys.Localized(OfferLocalizationKeys.Stage(OfferId, stageIndex, field));
        }

        /// <summary>Возвращает локализованное описание objective этапа.</summary>
        public LocalizedString GetLocalizedObjectiveDescription(int stageIndex, int objectiveIndex)
        {
            return OfferLocalizationKeys.Localized(
                OfferLocalizationKeys.Objective(OfferId, stageIndex, objectiveIndex));
        }

        /// <summary>Возвращает локализованные тексты условий и результата оффера.</summary>
        public LocalizedString GetLocalizedUnlockConditionDescription(int conditionIndex)
        {
            return OfferLocalizationKeys.Localized(
                OfferLocalizationKeys.UnlockCondition(OfferId, conditionIndex));
        }

        public LocalizedString GetLocalizedFailureConditionDescription(int conditionIndex)
        {
            return OfferLocalizationKeys.Localized(
                OfferLocalizationKeys.FailureCondition(OfferId, conditionIndex));
        }

        public LocalizedString GetLocalizedOutcomeDescription(int outcomeIndex)
        {
            return OfferLocalizationKeys.Localized(
                OfferLocalizationKeys.Outcome(OfferId, outcomeIndex));
        }
    }
}