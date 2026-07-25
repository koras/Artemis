using System;

namespace _Project.Scripts.Data.Offers
{
    [Serializable]
    public sealed class OfferUnlockCondition
    {
        public string ConditionId;
        public string Description;
        public OfferObjectiveDefinition Objective = new OfferObjectiveDefinition();
    }
}
