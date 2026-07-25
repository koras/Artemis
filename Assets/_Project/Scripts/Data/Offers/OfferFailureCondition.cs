using System;

namespace _Project.Scripts.Data.Offers
{
    [Serializable]
    public sealed class OfferFailureCondition
    {
        public string ConditionId;
        public string Description;
        public OfferObjectiveDefinition Objective = new OfferObjectiveDefinition();
        public bool FailWhenObjectiveIncomplete = true;
    }
}
