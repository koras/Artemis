using System;

namespace _Project.Scripts.Data.Offers
{
    [Serializable]
    public struct OfferResourceEventCondition
    {
        public string ResourceId;
        public int RequiredDelta;
        public int RequiredTotalAmount;
    }
}
