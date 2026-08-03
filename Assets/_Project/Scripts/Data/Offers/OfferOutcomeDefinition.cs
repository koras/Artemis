using System;

namespace _Project.Scripts.Data.Offers
{
    [Serializable]
    public sealed class OfferOutcomeDefinition
    {
        public string OutcomeId;

        public int GoldDelta;
        public int ReputationDelta;
        public string UnlockTag;
    }
}