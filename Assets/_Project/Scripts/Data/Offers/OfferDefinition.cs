using UnityEngine;
using UnityEngine.Localization;
using _Project.Scripts.Data.Localization;

namespace _Project.Scripts.Data.Offers
{
    /// <summary>
    /// Шаблон оффера: условия появления, требования выполнения, награды и штрафы репутации.
    /// </summary>
    /// <remarks>
    /// Generic custom Inspector: <c>LocalizationConfigEditor</c> in
    /// Assets/_Project/Scripts/Editor/LocalizationConfigEditors.cs.
    /// Localization dropdowns: <c>LocalizationConfigEditorUtility</c> in
    /// Assets/_Project/Scripts/Editor/LocalizationConfigEditorUtility.cs.
    /// </remarks>
    [LocalizationNamespace("offer", nameof(OfferDefinition.OfferId))]
    [CreateAssetMenu(menuName = "Artemis/Offers/Offer Definition", fileName = "OfferDefinition")]
    public sealed class OfferDefinition : BaseLocalizedConfigDefinition
    {
        private const string TitleSuffix = "title";
        private const string DescriptionSuffix = "description";
        private const string IntroSuffix = "intro";
        private const string AcceptSuffix = "accept";
        private const string CompleteSuffix = "complete";
        private const string FailSuffix = "fail";

        [Header("Identity")]
        [LocalizationId("Offer Id")]
        [HideInInspector] public string OfferId = "offer-id";

        public OfferCustomerDefinition Customer;

        [LocalizationKey("Title", TitleSuffix)]
        [SerializeField, HideInInspector]
        private string _titleLocalizationKey = TitleSuffix;

        [LocalizationKey("Description", DescriptionSuffix)]
        [SerializeField, HideInInspector]
        private string _descriptionLocalizationKey = DescriptionSuffix;

        [LocalizationKey("Intro Message", IntroSuffix)]
        [SerializeField, HideInInspector]
        private string _introLocalizationKey = IntroSuffix;

        [LocalizationKey("Accept Message", AcceptSuffix)]
        [SerializeField, HideInInspector]
        private string _acceptLocalizationKey = AcceptSuffix;

        [LocalizationKey("Complete Message", CompleteSuffix)]
        [SerializeField, HideInInspector]
        private string _completeLocalizationKey = CompleteSuffix;

        [LocalizationKey("Fail Message", FailSuffix)]
        [SerializeField, HideInInspector]
        private string _failLocalizationKey = FailSuffix;

        public OfferArchetype Archetype = OfferArchetype.BulkExport;
        public OfferCategory Category = OfferCategory.Economic;
        public string OfferAffinityTag;

        [Header("Unlock Conditions")]
        public OfferTriggerType TriggerTypes = OfferTriggerType.Time | OfferTriggerType.Manual;

        public OfferResourceAmount[] AppearanceRequirements;
        public int MinGameMinutesToAppear;
        [Range(0f, 1f)] public float HourlySpawnChance = 1f;
        public OfferResourceEventCondition[] ResourceEventConditions;
        [Min(0f)] public float BaseGenerationWeight = 1f;
        [Range(0, 100)] public int MinCustomerReputationToAppear;

        [LocalizationCollection("unlock")]
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

        [LocalizationCollection("stage")]
        public OfferStageDefinition[] Stages;

        [LocalizationCollection("failure")]
        public OfferFailureCondition[] FailureConditions;

        [Header("Rewards And Reputation")]
        public int GoldReward = 25;

        public int ReputationReward = 10;
        public int ReputationPenaltyOnFail = 10;
        public int ReputationPenaltyOnReject;
        [Min(0)] public int FastReserveBonusGold;
        [Min(0)] public int FastReserveWindowHours = 12;

        [LocalizationCollection("outcome")]
        public OfferOutcomeDefinition[] Outcomes;

        [Header("Deadline")]
        public bool UseDeadline = true;

        [Min(1)] public int DeadlineDays = 7;

        public bool HasStages => Stages != null && Stages.Length > 0;

        public string TitleLocalizationKey => OfferLocalizationKeys.Offer(OfferId, _titleLocalizationKey);

        public string DescriptionLocalizationKey =>
            OfferLocalizationKeys.Offer(OfferId, _descriptionLocalizationKey);

        public string IntroLocalizationKey => OfferLocalizationKeys.Offer(OfferId, _introLocalizationKey);

        public string AcceptLocalizationKey => OfferLocalizationKeys.Offer(OfferId, _acceptLocalizationKey);

        public string CompleteLocalizationKey => OfferLocalizationKeys.Offer(OfferId, _completeLocalizationKey);

        public string FailLocalizationKey => OfferLocalizationKeys.Offer(OfferId, _failLocalizationKey);

        /// <summary>Возвращает локализованные заголовок и описание оффера.</summary>
        public LocalizedString GetLocalizedTitle() => OfferLocalizationKeys.Localized(TitleLocalizationKey);

        public LocalizedString GetLocalizedDescription() =>
            OfferLocalizationKeys.Localized(DescriptionLocalizationKey);

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
    }
}