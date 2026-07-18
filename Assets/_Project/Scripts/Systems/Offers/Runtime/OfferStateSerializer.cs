using System;
using System.Collections.Generic;
using _Project.Scripts.Data.Offers;
using _Project.Scripts.Systems.Resources;
using UnityEngine;

namespace _Project.Scripts.Systems.Offers.Runtime
{
    /// <summary>
    /// Serializes and restores Offer runtime state snapshot used by save/load flows.
    /// </summary>
    internal sealed class OfferStateSerializer
    {
        private readonly OfferSystemContext _context;
        private readonly OfferReputationService _reputationService;

        /// <summary>
        /// Creates serializer bound to shared context and reputation helpers.
        /// </summary>
        public OfferStateSerializer(OfferSystemContext context, OfferReputationService reputationService)
        {
            _context = context;
            _reputationService = reputationService;
        }

        /// <summary>
        /// Captures full runtime state into transferable DTO snapshot.
        /// </summary>
        public OfferSystemState CaptureState()
        {
            var state = new OfferSystemState
            {
                Gold = _context.ResourceInventoryService != null
                    ? _context.ResourceInventoryService.GetAmount(ResourceInventoryService.GOLD_RESOURCE_ID)
                    : 0,
                LastProcessedHour = _context.LastProcessedHour
            };

            for (int i = 0; i < _context.AvailableOffers.Count; i++)
            {
                state.AvailableOffers.Add(ToState(_context.AvailableOffers[i]));
            }

            for (int i = 0; i < _context.ActiveOffers.Count; i++)
            {
                state.ActiveOffers.Add(ToState(_context.ActiveOffers[i]));
            }

            foreach (KeyValuePair<string, Dictionary<string, int>> pair in _context.ReservedByRuntimeId)
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value == null)
                {
                    continue;
                }

                foreach (KeyValuePair<string, int> resourcePair in pair.Value)
                {
                    if (string.IsNullOrWhiteSpace(resourcePair.Key) || resourcePair.Value <= 0)
                    {
                        continue;
                    }

                    state.ReservedResources.Add(new OfferReservedResourceState
                    {
                        RuntimeId = pair.Key,
                        ResourceId = resourcePair.Key,
                        Amount = resourcePair.Value
                    });
                }
            }

            foreach (KeyValuePair<OfferCustomerDefinition, int> pair in _context.ReputationByCustomer)
            {
                if (pair.Key == null)
                {
                    continue;
                }

                string customerKey = OfferReputationService.GetCustomerKey(pair.Key);
                if (string.IsNullOrWhiteSpace(customerKey))
                {
                    continue;
                }

                state.Reputation.Add(new OfferCustomerReputationState
                {
                    CustomerKey = customerKey,
                    Reputation = pair.Value
                });
            }

            foreach (KeyValuePair<string, int> pair in _context.LastGeneratedAtMinutesByDefinition)
            {
                state.Cooldowns.Add(new OfferCooldownState
                {
                    DefinitionId = pair.Key,
                    LastGeneratedAtGameMinutes = pair.Value
                });
            }

            foreach (KeyValuePair<string, int> pair in _context.GeneratedCountByDefinition)
            {
                state.GeneratedCounts.Add(new OfferGeneratedCountState
                {
                    DefinitionId = pair.Key,
                    Count = pair.Value
                });
            }

            return state;
        }

        /// <summary>
        /// Restores runtime state from snapshot and rebuilds all runtime collections.
        /// </summary>
        public void RestoreState(OfferSystemState state)
        {
            if (state == null)
            {
                return;
            }

            if (_context.ResourceInventoryService != null)
            {
                _context.ResourceInventoryService.SetAmount(ResourceInventoryService.GOLD_RESOURCE_ID, Mathf.Max(0, state.Gold));
            }

            _context.LastProcessedHour = state.LastProcessedHour;
            _context.AvailableOffers.Clear();
            _context.ActiveOffers.Clear();
            _context.ReputationByCustomer.Clear();
            _context.LastGeneratedAtMinutesByDefinition.Clear();
            _context.GeneratedCountByDefinition.Clear();
            _context.ReservedByRuntimeId.Clear();

            Dictionary<string, OfferCustomerDefinition> customerByKey = _reputationService.BuildCustomerByKey();
            RestoreReputation(state, customerByKey);
            RestoreRuntimeOffers(state.AvailableOffers, _context.AvailableOffers);
            RestoreRuntimeOffers(state.ActiveOffers, _context.ActiveOffers);
            RestoreCooldowns(state.Cooldowns);
            RestoreGeneratedCounts(state.GeneratedCounts);
            RestoreReservedResources(state.ReservedResources);
        }

        /// <summary>
        /// Converts runtime record into serializable record DTO.
        /// </summary>
        private static OfferRuntimeRecordState ToState(OfferRuntimeRecord record)
        {
            return new OfferRuntimeRecordState
            {
                RuntimeId = record.RuntimeId,
                DefinitionId = record.DefinitionId,
                CreatedAtSol = record.CreatedAtSol,
                CreatedAtGameMinutes = record.CreatedAtGameMinutes,
                DeadlineSol = record.DeadlineSol.GetValueOrDefault(),
                HasDeadline = record.DeadlineSol.HasValue,
                Source = record.Source,
                IsReservedForShipment = record.IsReservedForShipment,
                ShipmentMissionTarget = record.ShipmentMissionTarget,
                ResolutionState = record.ResolutionState
            };
        }

        /// <summary>
        /// Restores persisted customer reputation values.
        /// </summary>
        private void RestoreReputation(OfferSystemState state, Dictionary<string, OfferCustomerDefinition> customerByKey)
        {
            if (state.Reputation == null)
            {
                return;
            }

            for (int i = 0; i < state.Reputation.Count; i++)
            {
                OfferCustomerReputationState reputationState = state.Reputation[i];
                if (reputationState == null || string.IsNullOrWhiteSpace(reputationState.CustomerKey))
                {
                    continue;
                }

                if (!customerByKey.TryGetValue(reputationState.CustomerKey, out OfferCustomerDefinition customer))
                {
                    continue;
                }

                _context.ReputationByCustomer[customer] = Mathf.Clamp(reputationState.Reputation, 0, 100);
            }
        }

        /// <summary>
        /// Restores runtime offer records into target list.
        /// </summary>
        private void RestoreRuntimeOffers(List<OfferRuntimeRecordState> states, List<OfferRuntimeRecord> target)
        {
            if (states == null)
            {
                return;
            }

            for (int i = 0; i < states.Count; i++)
            {
                OfferRuntimeRecordState runtimeState = states[i];
                if (runtimeState == null || string.IsNullOrWhiteSpace(runtimeState.DefinitionId))
                {
                    continue;
                }

                if (!_context.DefinitionById.TryGetValue(runtimeState.DefinitionId, out OfferDefinition definition))
                {
                    continue;
                }

                int? deadline = runtimeState.HasDeadline ? runtimeState.DeadlineSol : (int?)null;
                string runtimeId = string.IsNullOrWhiteSpace(runtimeState.RuntimeId) ? Guid.NewGuid().ToString("N") : runtimeState.RuntimeId;
                var record = new OfferRuntimeRecord(
                    runtimeId,
                    definition,
                    runtimeState.DefinitionId,
                    runtimeState.CreatedAtSol,
                    runtimeState.CreatedAtGameMinutes,
                    deadline,
                    runtimeState.Source)
                {
                    IsReservedForShipment = runtimeState.IsReservedForShipment,
                    ShipmentMissionTarget = Mathf.Max(0, runtimeState.ShipmentMissionTarget),
                    ResolutionState = runtimeState.ResolutionState
                };
                target.Add(record);
            }
        }

        /// <summary>
        /// Restores per-definition cooldown timestamps.
        /// </summary>
        private void RestoreCooldowns(List<OfferCooldownState> states)
        {
            if (states == null)
            {
                return;
            }

            for (int i = 0; i < states.Count; i++)
            {
                OfferCooldownState cooldownState = states[i];
                if (cooldownState == null || string.IsNullOrWhiteSpace(cooldownState.DefinitionId))
                {
                    continue;
                }

                _context.LastGeneratedAtMinutesByDefinition[cooldownState.DefinitionId] = cooldownState.LastGeneratedAtGameMinutes;
            }
        }

        /// <summary>
        /// Restores per-definition generated counters.
        /// </summary>
        private void RestoreGeneratedCounts(List<OfferGeneratedCountState> states)
        {
            if (states == null)
            {
                return;
            }

            for (int i = 0; i < states.Count; i++)
            {
                OfferGeneratedCountState generatedState = states[i];
                if (generatedState == null || string.IsNullOrWhiteSpace(generatedState.DefinitionId))
                {
                    continue;
                }

                _context.GeneratedCountByDefinition[generatedState.DefinitionId] = Mathf.Max(0, generatedState.Count);
            }
        }

        /// <summary>
        /// Restores reserved resource map grouped by runtime offer id.
        /// </summary>
        private void RestoreReservedResources(List<OfferReservedResourceState> states)
        {
            if (states == null)
            {
                return;
            }

            for (int i = 0; i < states.Count; i++)
            {
                OfferReservedResourceState reservedState = states[i];
                if (reservedState == null || string.IsNullOrWhiteSpace(reservedState.RuntimeId) || string.IsNullOrWhiteSpace(reservedState.ResourceId))
                {
                    continue;
                }

                int amount = Mathf.Max(0, reservedState.Amount);
                if (amount <= 0)
                {
                    continue;
                }

                if (!_context.ReservedByRuntimeId.TryGetValue(reservedState.RuntimeId, out Dictionary<string, int> map))
                {
                    map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    _context.ReservedByRuntimeId[reservedState.RuntimeId] = map;
                }

                map.TryGetValue(reservedState.ResourceId, out int currentAmount);
                map[reservedState.ResourceId] = currentAmount + amount;
            }
        }
    }
}
