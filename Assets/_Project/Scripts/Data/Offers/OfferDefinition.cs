using UnityEngine;

namespace _Project.Scripts.Data.Offers
{
    /// <summary>
    /// Шаблон оффера: условия появления, требования выполнения, награды и штрафы репутации.
    /// </summary>
    [CreateAssetMenu(menuName = "Artemis/Offers/Offer Definition", fileName = "OfferDefinition")]
    public sealed class OfferDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string OfferId = "offer-id";
        public OfferCustomerDefinition Customer;
        public string Title;
        [TextArea] public string Description;

        [Header("Unlock Conditions")]
        public OfferTriggerType TriggerTypes = OfferTriggerType.Time | OfferTriggerType.Manual;
        public OfferResourceAmount[] AppearanceRequirements;
        public int MinGameMinutesToAppear;
        [Range(0f, 1f)] public float HourlySpawnChance = 1f;
        public OfferResourceEventCondition[] ResourceEventConditions;

        [Header("Repeatability")]
        public bool IsRepeatable = true;
        [Min(0)] public int CooldownGameMinutes = 120;

        [Header("Completion Requirements")]
        public OfferResourceAmount[] CompletionRequirements;

        [Header("Rewards And Reputation")]
        public int GoldReward = 25;
        public int ReputationReward = 10;
        public int ReputationPenaltyOnFail = 10;
        public int ReputationPenaltyOnReject;

        [Header("Deadline")]
        public bool UseDeadline = true;
        [Min(1)] public int DeadlineDays = 7;
    }
}
