using System;
using System.Collections.Generic;
using _Project.Scripts.Data.Grid;
using _Project.Scripts.Data.Offers;
using _Project.Scripts.Systems.Construction;
using _Project.Scripts.Systems.Resources;
using _Project.Scripts.Systems.Simulation;

namespace _Project.Scripts.Systems.Offers.Runtime
{
    /// <summary>
    /// Shared runtime state container for Offer subservices.
    /// </summary>
    public sealed class OfferSystemContext
    {
        public readonly List<OfferDefinition> Catalog;
        public readonly GridState GridState;
        public readonly BuildingManager BuildingManager;
        public readonly ResourceInventoryService ResourceInventoryService;
        public readonly GameTimeService GameTimeService;
        public readonly List<OfferRuntimeRecord> AvailableOffers = new List<OfferRuntimeRecord>();
        public readonly List<OfferRuntimeRecord> ActiveOffers = new List<OfferRuntimeRecord>();
        public readonly Dictionary<OfferCustomerDefinition, int> ReputationByCustomer = new Dictionary<OfferCustomerDefinition, int>();
        public readonly Dictionary<string, OfferDefinition> DefinitionById = new Dictionary<string, OfferDefinition>();
        public readonly Dictionary<string, int> LastGeneratedAtMinutesByDefinition = new Dictionary<string, int>();
        public readonly Dictionary<string, int> GeneratedCountByDefinition = new Dictionary<string, int>();
        public readonly Dictionary<string, Dictionary<string, int>> ReservedByRuntimeId = new Dictionary<string, Dictionary<string, int>>();
        public readonly Dictionary<string, int> CompletedChainStepByChainId = new Dictionary<string, int>(StringComparer.Ordinal);
        public readonly Dictionary<string, int> GenerationPenaltyUntilMinutesByCustomerKey = new Dictionary<string, int>(StringComparer.Ordinal);
        public int MissionArrivalCount;

        public int LastProcessedHour = -1;

        /// <summary>
        /// Builds initial runtime indexes from catalog and shared services.
        /// </summary>
        public OfferSystemContext(
            IReadOnlyList<OfferDefinition> catalog,
            GridState gridState,
            BuildingManager buildingManager,
            ResourceInventoryService resourceInventoryService,
            GameTimeService gameTimeService)
        {
            GridState = gridState;
            BuildingManager = buildingManager;
            ResourceInventoryService = resourceInventoryService;
            GameTimeService = gameTimeService;
            Catalog = new List<OfferDefinition>();

            if (catalog == null)
            {
                return;
            }

            for (int i = 0; i < catalog.Count; i++)
            {
                OfferDefinition definition = catalog[i];
                if (definition == null)
                {
                    continue;
                }

                Catalog.Add(definition);
                if (!string.IsNullOrWhiteSpace(definition.OfferId) && !DefinitionById.ContainsKey(definition.OfferId))
                {
                    DefinitionById.Add(definition.OfferId, definition);
                }
            }
        }
    }
}
