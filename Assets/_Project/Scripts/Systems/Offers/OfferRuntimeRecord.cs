using System;
using _Project.Scripts.Data.Offers;

namespace _Project.Scripts.Systems.Offers
{
    public enum OfferResolutionState
    {
        Active = 0,
        Completed = 1,
        Failed = 2
    }

    /// <summary>
    /// Runtime-состояние конкретного оффера.
    /// </summary>
    public sealed class OfferRuntimeRecord
    {
        public OfferRuntimeRecord(
            string runtimeId,
            OfferDefinition definition,
            string definitionId,
            int createdAtSol,
            int createdAtGameMinutes,
            int? deadlineSol,
            OfferTriggerSource source,
            int acceptedAtGameMinutes = -1,
            int reservedAtGameMinutes = -1,
            bool fastReserveBonusGranted = false,
            OfferStageRuntimeState stageState = null)
        {
            RuntimeId = runtimeId;
            Definition = definition;
            DefinitionId = definitionId;
            CreatedAtSol = createdAtSol;
            CreatedAtGameMinutes = createdAtGameMinutes;
            DeadlineSol = deadlineSol;
            Source = source;
            AcceptedAtGameMinutes = acceptedAtGameMinutes;
            ReservedAtGameMinutes = reservedAtGameMinutes;
            FastReserveBonusGranted = fastReserveBonusGranted;
            IsReservedForShipment = false;
            ShipmentMissionTarget = 0;
            ResolutionState = OfferResolutionState.Active;
            StageState = stageState ?? CreateInitialStageState(definition);
        }

        public OfferRuntimeRecord(
            OfferDefinition definition,
            string definitionId,
            int createdAtSol,
            int createdAtGameMinutes,
            int? deadlineSol,
            OfferTriggerSource source)
            : this(Guid.NewGuid().ToString("N"), definition, definitionId, createdAtSol, createdAtGameMinutes, deadlineSol, source)
        {
        }

        public string RuntimeId { get; }
        public OfferDefinition Definition { get; }
        public string DefinitionId { get; }
        public int CreatedAtSol { get; }
        public int CreatedAtGameMinutes { get; }
        public int? DeadlineSol { get; }
        public OfferTriggerSource Source { get; }
        public int AcceptedAtGameMinutes { get; set; }
        public int ReservedAtGameMinutes { get; set; }
        public bool FastReserveBonusGranted { get; set; }
        public bool IsReservedForShipment { get; set; }
        public int ShipmentMissionTarget { get; set; }
        public OfferResolutionState ResolutionState { get; set; }
        public OfferStageRuntimeState StageState { get; }
        public int MissionArrivalCountAtAccept { get; set; }
        public int MissionArrivalCount { get; set; }

        private static OfferStageRuntimeState CreateInitialStageState(OfferDefinition definition)
        {
            var result = new OfferStageRuntimeState
            {
                CurrentStageIndex = 0
            };

            if (definition != null
                && definition.HasStages
                && definition.Stages[0] != null)
            {
                result.IsInspectionScheduled = definition.Stages[0].ScheduleInspection;
            }

            return result;
        }
    }
}
