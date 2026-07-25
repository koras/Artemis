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
    public sealed class OfferStateSerializer
    {
        private readonly OfferSystemContext _context;
        private readonly OfferReputationService _reputationService;

        public OfferStateSerializer(OfferSystemContext context, OfferReputationService reputationService)
        {
            _context = context;
            _reputationService = reputationService;
        }

        public OfferSystemState CaptureState()
        {
            var state = new OfferSystemState
            {
                Gold = _context.ResourceInventoryService != null
                    ? _context.ResourceInventoryService.GetAmount(ResourceInventoryService.GOLD_RESOURCE_ID)
                    : 0,
                LastProcessedHour = _context.LastProcessedHour,
                MissionArrivalCount = _context.MissionArrivalCount
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

            foreach (KeyValuePair<string, int> pair in _context.CompletedChainStepByChainId)
            {
                state.ChainProgress.Add(new OfferChainProgressState
                {
                    ChainId = pair.Key,
                    CompletedStep = pair.Value
                });
            }

            foreach (KeyValuePair<string, int> pair in _context.GenerationPenaltyUntilMinutesByCustomerKey)
            {
                state.GenerationPenalties.Add(new OfferGenerationPenaltyState
                {
                    CustomerKey = pair.Key,
                    UntilGameMinutes = pair.Value
                });
            }

            return state;
        }

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
            _context.MissionArrivalCount = Mathf.Max(0, state.MissionArrivalCount);
            _context.AvailableOffers.Clear();
            _context.ActiveOffers.Clear();
            _context.ReputationByCustomer.Clear();
            _context.LastGeneratedAtMinutesByDefinition.Clear();
            _context.GeneratedCountByDefinition.Clear();
            _context.ReservedByRuntimeId.Clear();
            _context.CompletedChainStepByChainId.Clear();
            _context.GenerationPenaltyUntilMinutesByCustomerKey.Clear();

            Dictionary<string, OfferCustomerDefinition> customerByKey = _reputationService.BuildCustomerByKey();
            RestoreReputation(state, customerByKey);
            RestoreRuntimeOffers(state.AvailableOffers, _context.AvailableOffers);
            RestoreRuntimeOffers(state.ActiveOffers, _context.ActiveOffers);
            RestoreCooldowns(state.Cooldowns);
            RestoreGeneratedCounts(state.GeneratedCounts);
            RestoreReservedResources(state.ReservedResources);
            RestoreChainProgress(state.ChainProgress);
            RestoreGenerationPenalties(state.GenerationPenalties);
        }

        private static OfferRuntimeRecordState ToState(OfferRuntimeRecord record)
        {
            return new OfferRuntimeRecordState
            {
                RuntimeId = record.RuntimeId,
                DefinitionId = record.DefinitionId,
                CreatedAtSol = record.CreatedAtSol,
                CreatedAtGameMinutes = record.CreatedAtGameMinutes,
                AcceptedAtGameMinutes = record.AcceptedAtGameMinutes,
                ReservedAtGameMinutes = record.ReservedAtGameMinutes,
                FastReserveBonusGranted = record.FastReserveBonusGranted,
                DeadlineSol = record.DeadlineSol.GetValueOrDefault(),
                HasDeadline = record.DeadlineSol.HasValue,
                Source = record.Source,
                IsReservedForShipment = record.IsReservedForShipment,
                ShipmentMissionTarget = record.ShipmentMissionTarget,
                ResolutionState = record.ResolutionState,
                MissionArrivalCountAtAccept = record.MissionArrivalCountAtAccept,
                MissionArrivalCount = record.MissionArrivalCount,
                StageState = new OfferStageRuntimeStateState
                {
                    CurrentStageIndex = record.StageState?.CurrentStageIndex ?? 0,
                    StageStartedMissionCount = record.StageState?.StageStartedMissionCount ?? 0,
                    StageSatisfiedSinceSol = record.StageState?.StageSatisfiedSinceSol ?? -1,
                    IsInspectionScheduled = record.StageState?.IsInspectionScheduled ?? false,
                    CompletedObjectiveCount = record.StageState?.CompletedObjectiveCount ?? 0
                }
            };
        }

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
                    runtimeState.Source,
                    runtimeState.AcceptedAtGameMinutes,
                    runtimeState.ReservedAtGameMinutes,
                    runtimeState.FastReserveBonusGranted,
                    ToStageState(runtimeState.StageState))
                {
                    IsReservedForShipment = runtimeState.IsReservedForShipment,
                    ShipmentMissionTarget = Mathf.Max(0, runtimeState.ShipmentMissionTarget),
                    ResolutionState = runtimeState.ResolutionState,
                    MissionArrivalCountAtAccept = Mathf.Max(0, runtimeState.MissionArrivalCountAtAccept),
                    MissionArrivalCount = Mathf.Max(0, runtimeState.MissionArrivalCount)
                };
                target.Add(record);
            }
        }

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

        private void RestoreChainProgress(List<OfferChainProgressState> states)
        {
            if (states == null)
            {
                return;
            }

            for (int i = 0; i < states.Count; i++)
            {
                OfferChainProgressState chainState = states[i];
                if (chainState == null || string.IsNullOrWhiteSpace(chainState.ChainId))
                {
                    continue;
                }

                _context.CompletedChainStepByChainId[chainState.ChainId] = Mathf.Max(0, chainState.CompletedStep);
            }
        }

        private void RestoreGenerationPenalties(List<OfferGenerationPenaltyState> states)
        {
            if (states == null)
            {
                return;
            }

            for (int i = 0; i < states.Count; i++)
            {
                OfferGenerationPenaltyState penaltyState = states[i];
                if (penaltyState == null || string.IsNullOrWhiteSpace(penaltyState.CustomerKey))
                {
                    continue;
                }

                _context.GenerationPenaltyUntilMinutesByCustomerKey[penaltyState.CustomerKey] = penaltyState.UntilGameMinutes;
            }
        }

        private static OfferStageRuntimeState ToStageState(OfferStageRuntimeStateState state)
        {
            return new OfferStageRuntimeState
            {
                CurrentStageIndex = state?.CurrentStageIndex ?? 0,
                StageStartedMissionCount = state?.StageStartedMissionCount ?? 0,
                StageSatisfiedSinceSol = state?.StageSatisfiedSinceSol ?? -1,
                IsInspectionScheduled = state?.IsInspectionScheduled ?? false,
                CompletedObjectiveCount = state?.CompletedObjectiveCount ?? 0
            };
        }
    }
}
