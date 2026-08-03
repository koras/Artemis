using System;
using _Project.Scripts.Data.Localization;

namespace _Project.Scripts.Data.Offers
{
    [Serializable]
    public sealed class OfferStageDefinition
    {
        public string StageId;

        [LocalizationCollection("objective")]
        public OfferObjectiveDefinition[] Objectives;
        public bool ScheduleInspection;
        public int BonusGold;
        public int BonusReputation;
    }
}