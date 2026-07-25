using _Project.Scripts.Data.Offers;

namespace _Project.Scripts.Systems.Offers
{
    public sealed class OfferStageProgressSnapshot
    {
        public OfferStageDefinition Stage { get; set; }
        public int StageIndex { get; set; }
        public int TotalStages { get; set; }
        public bool IsCompleted { get; set; }
        public OfferObjectiveProgressSnapshot[] Objectives { get; set; }
    }
}
