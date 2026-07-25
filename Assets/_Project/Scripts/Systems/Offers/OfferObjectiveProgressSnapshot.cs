using _Project.Scripts.Data.Offers;

namespace _Project.Scripts.Systems.Offers
{
    public sealed class OfferObjectiveProgressSnapshot
    {
        public OfferObjectiveDefinition Objective { get; set; }
        public int CurrentValue { get; set; }
        public int TargetValue { get; set; }
        public bool IsCompleted { get; set; }
    }
}
