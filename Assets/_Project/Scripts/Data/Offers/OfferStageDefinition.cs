using System;

namespace _Project.Scripts.Data.Offers
{
    [Serializable]
    public sealed class OfferStageDefinition
    {
        public string StageId;
        public string Title;
        [UnityEngine.TextArea] public string Description;
        public OfferObjectiveDefinition[] Objectives;
        public bool ScheduleInspection;
        public int BonusGold;
        public int BonusReputation;
    }
}
