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
            OfferTriggerSource source)
        {
            RuntimeId = runtimeId;
            Definition = definition;
            DefinitionId = definitionId;
            CreatedAtSol = createdAtSol;
            CreatedAtGameMinutes = createdAtGameMinutes;
            DeadlineSol = deadlineSol;
            Source = source;
            IsReservedForShipment = false;
            ShipmentMissionTarget = 0;
            ResolutionState = OfferResolutionState.Active;
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
        public bool IsReservedForShipment { get; set; }
        public int ShipmentMissionTarget { get; set; }
        public OfferResolutionState ResolutionState { get; set; }
    }
}
