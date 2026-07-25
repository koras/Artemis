namespace _Project.Scripts.Systems.Offers
{
    public sealed class OfferStageRuntimeState
    {
        public int CurrentStageIndex { get; set; }
        public int StageStartedMissionCount { get; set; }
        public int StageSatisfiedSinceSol { get; set; } = -1;
        public bool IsInspectionScheduled { get; set; }
        public int CompletedObjectiveCount { get; set; }
    }
}
