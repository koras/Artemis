using System;
using System.Collections.Generic;
using _Project.Scripts.Data.Offers;
using _Project.Scripts.Systems.Resources;
using UnityEngine;

namespace _Project.Scripts.Systems.Offers.Runtime
{
    /// <summary>
    /// Controls offer state transitions: accept/reject/reserve/resolve/fail by deadlines.
    /// </summary>
    public sealed class OfferLifecycleService
    {
        private const int DEFAULT_REJECT_PENALTY_MINUTES = 180;

        private readonly OfferSystemContext _context;
        private readonly OfferReservationService _reservationService;
        private readonly OfferReputationService _reputationService;
        private readonly OfferObjectiveEvaluationService _objectiveEvaluationService;
        private readonly Action _stateChanged;
        private readonly Action<OfferDefinition> _offerCompleted;

        public OfferLifecycleService(
            OfferSystemContext context,
            OfferReservationService reservationService,
            OfferReputationService reputationService,
            OfferObjectiveEvaluationService objectiveEvaluationService,
            Action stateChanged,
            Action<OfferDefinition> offerCompleted)
        {
            _context = context;
            _reservationService = reservationService;
            _reputationService = reputationService;
            _objectiveEvaluationService = objectiveEvaluationService;
            _stateChanged = stateChanged;
            _offerCompleted = offerCompleted;
        }

        public bool AcceptOffer(string runtimeId)
        {
            OfferRuntimeRecord record = FindByRuntimeId(_context.AvailableOffers, runtimeId);
            if (record == null)
            {
                return false;
            }

            _context.AvailableOffers.Remove(record);
            ResolveExclusiveGroup(record);
            record.ResolutionState = OfferResolutionState.Active;
            record.AcceptedAtGameMinutes = _context.GameTimeService.TotalGameMinutes;
            record.MissionArrivalCountAtAccept = _context.MissionArrivalCount;
            record.MissionArrivalCount = _context.MissionArrivalCount;
            record.StageState.StageStartedMissionCount = _context.MissionArrivalCount;
            record.StageState.IsInspectionScheduled = _objectiveEvaluationService.GetCurrentStage(record)?.ScheduleInspection ?? false;
            _context.ActiveOffers.Add(record);

            ProcessActiveOfferProgress();
            _stateChanged?.Invoke();
            return true;
        }

        public bool RejectOffer(string runtimeId)
        {
            OfferRuntimeRecord record = FindByRuntimeId(_context.AvailableOffers, runtimeId);
            if (record == null)
            {
                return false;
            }

            _context.AvailableOffers.Remove(record);
            _reputationService.ApplyReputation(record.Definition.Customer, -Mathf.Abs(record.Definition.ReputationPenaltyOnReject));
            ApplyRejectGenerationPenalty(record.Definition.Customer, record.Definition.CooldownGameMinutes);
            _stateChanged?.Invoke();
            return true;
        }

        public bool TryReserveOfferForNextMission(string runtimeId, int missionCount)
        {
            OfferRuntimeRecord record = FindByRuntimeId(_context.ActiveOffers, runtimeId);
            if (record == null || record.ResolutionState != OfferResolutionState.Active)
            {
                return false;
            }

            OfferResourceAmount[] requirements = GetReservationRequirements(record);
            if (requirements.Length == 0)
            {
                return false;
            }

            if (record.IsReservedForShipment)
            {
                return true;
            }

            if (!_reservationService.HasResources(requirements))
            {
                return false;
            }

            Dictionary<string, int> reservedResources = OfferReservationService.BuildReservationMap(requirements);
            if (!_reservationService.ConsumeResources(reservedResources))
            {
                return false;
            }

            _context.ReservedByRuntimeId[record.RuntimeId] = reservedResources;
            record.IsReservedForShipment = true;
            if (record.ReservedAtGameMinutes < 0)
            {
                record.ReservedAtGameMinutes = _context.GameTimeService.TotalGameMinutes;
            }

            record.FastReserveBonusGranted = IsFastReserveWindowOpen(record);
            record.ShipmentMissionTarget = Mathf.Max(0, missionCount + 1);
            ProcessActiveOfferProgress();
            _stateChanged?.Invoke();
            return true;
        }

        public bool CancelOfferReservation(string runtimeId)
        {
            OfferRuntimeRecord record = FindByRuntimeId(_context.ActiveOffers, runtimeId);
            if (record == null || !record.IsReservedForShipment)
            {
                return false;
            }

            _reservationService.ReleaseReservation(record.RuntimeId);
            record.IsReservedForShipment = false;
            record.ShipmentMissionTarget = 0;
            _stateChanged?.Invoke();
            return true;
        }

        public void ResolveOffersOnMissionArrived(int missionCount)
        {
            bool hasStateChanges = false;

            for (int i = _context.ActiveOffers.Count - 1; i >= 0; i--)
            {
                OfferRuntimeRecord record = _context.ActiveOffers[i];
                if (record.ResolutionState != OfferResolutionState.Active)
                {
                    continue;
                }

                record.MissionArrivalCount = Mathf.Max(record.MissionArrivalCount, missionCount);

                if (!record.IsReservedForShipment)
                {
                    continue;
                }

                if (record.ShipmentMissionTarget > 0 && missionCount < record.ShipmentMissionTarget)
                {
                    continue;
                }

                OfferResourceAmount[] requirements = GetReservationRequirements(record);
                if (requirements.Length == 0)
                {
                    continue;
                }

                if (_reservationService.HasFullReservation(record.RuntimeId, requirements))
                {
                    if (!record.Definition.HasStages)
                    {
                        CompleteRecord(record);
                        hasStateChanges = true;
                        continue;
                    }

                    OfferStageAdvanceResult result = _objectiveEvaluationService.EvaluateStageProgress(record);
                    hasStateChanges |= HandleStageAdvanceResult(record, result, shouldReleaseReservation: true);
                    continue;
                }

                _reservationService.ReleaseReservation(record.RuntimeId);
                record.IsReservedForShipment = false;
                record.ShipmentMissionTarget = 0;
                FailRecord(record, releaseReservation: false);
                hasStateChanges = true;
            }

            hasStateChanges |= ProcessActiveOfferProgressInternal();

            if (hasStateChanges)
            {
                _stateChanged?.Invoke();
            }
        }

        public void ProcessDeadlines()
        {
            bool hasStateChanges = false;
            for (int i = _context.ActiveOffers.Count - 1; i >= 0; i--)
            {
                OfferRuntimeRecord offer = _context.ActiveOffers[i];
                if (!offer.DeadlineSol.HasValue)
                {
                    continue;
                }

                if (_context.GameTimeService.Sol <= offer.DeadlineSol.Value)
                {
                    continue;
                }

                FailRecord(offer, releaseReservation: true);
                hasStateChanges = true;
            }

            if (hasStateChanges)
            {
                _stateChanged?.Invoke();
            }
        }

        public void ProcessActiveOfferProgress()
        {
            if (ProcessActiveOfferProgressInternal())
            {
                _stateChanged?.Invoke();
            }
        }

        public int GetGoldReward(OfferRuntimeRecord record)
        {
            if (record?.Definition == null)
            {
                return 0;
            }

            int reward = Mathf.Max(0, record.Definition.GoldReward);
            float rewardMultiplier = _reputationService.GetRewardMultiplier(record.Definition.Customer);

            switch (record.Definition.Archetype)
            {
                case OfferArchetype.BulkExport:
                    rewardMultiplier += 0.1f;
                    break;
                case OfferArchetype.EmergencyRequest:
                    rewardMultiplier += 0.2f;
                    break;
                case OfferArchetype.ProgressiveContract:
                    rewardMultiplier += 0.15f * Mathf.Max(0, record.Definition.ChainStep - 1);
                    break;
                case OfferArchetype.OpportunisticDeal:
                    rewardMultiplier += 0.05f;
                    break;
            }

            reward = Mathf.RoundToInt(reward * rewardMultiplier);
            if (record.FastReserveBonusGranted)
            {
                reward += Mathf.Max(0, record.Definition.FastReserveBonusGold);
            }

            return Mathf.Max(0, reward);
        }

        public bool IsFastReserveWindowOpen(OfferRuntimeRecord record)
        {
            if (record?.Definition == null || record.Definition.FastReserveBonusGold <= 0)
            {
                return false;
            }

            int fastWindowMinutes = Mathf.Max(0, record.Definition.FastReserveWindowHours) * 60;
            if (fastWindowMinutes <= 0)
            {
                return false;
            }

            return _context.GameTimeService.TotalGameMinutes - record.CreatedAtGameMinutes <= fastWindowMinutes;
        }

        private bool ProcessActiveOfferProgressInternal()
        {
            bool hasStateChanges = false;

            for (int i = _context.ActiveOffers.Count - 1; i >= 0; i--)
            {
                OfferRuntimeRecord record = _context.ActiveOffers[i];
                if (record == null || record.ResolutionState != OfferResolutionState.Active)
                {
                    continue;
                }

                record.MissionArrivalCount = Mathf.Max(record.MissionArrivalCount, _context.MissionArrivalCount);
                if (_objectiveEvaluationService.HasFailureCondition(record))
                {
                    FailRecord(record, releaseReservation: true);
                    hasStateChanges = true;
                    continue;
                }

                OfferStageAdvanceResult result = _objectiveEvaluationService.EvaluateStageProgress(record);
                hasStateChanges |= HandleStageAdvanceResult(record, result, shouldReleaseReservation: true);
            }

            return hasStateChanges;
        }

        private bool HandleStageAdvanceResult(OfferRuntimeRecord record, OfferStageAdvanceResult result, bool shouldReleaseReservation)
        {
            if (result == OfferStageAdvanceResult.None || record == null)
            {
                return false;
            }

            if (shouldReleaseReservation && record.IsReservedForShipment)
            {
                _context.ReservedByRuntimeId.Remove(record.RuntimeId);
                record.IsReservedForShipment = false;
                record.ShipmentMissionTarget = 0;
            }

            if (result == OfferStageAdvanceResult.Advanced)
            {
                ApplyStageBonus(record);
                return true;
            }

            CompleteRecord(record);
            ApplyCurrentStageCompletionBonus(record);
            return true;
        }

        private void ApplyStageBonus(OfferRuntimeRecord record)
        {
            if (record?.Definition == null || !record.Definition.HasStages)
            {
                return;
            }

            int completedStageIndex = Mathf.Clamp(record.StageState.CurrentStageIndex - 1, 0, record.Definition.Stages.Length - 1);
            OfferStageDefinition stage = record.Definition.Stages[completedStageIndex];
            if (stage == null)
            {
                return;
            }

            if (stage.BonusGold > 0)
            {
                _context.ResourceInventoryService.Add(ResourceInventoryService.GOLD_RESOURCE_ID, stage.BonusGold);
            }

            if (stage.BonusReputation != 0)
            {
                _reputationService.ApplyReputation(record.Definition.Customer, stage.BonusReputation);
            }
        }

        private void ApplyCurrentStageCompletionBonus(OfferRuntimeRecord record)
        {
            if (record?.Definition == null || !record.Definition.HasStages)
            {
                return;
            }

            int stageIndex = Mathf.Clamp(record.StageState.CurrentStageIndex, 0, record.Definition.Stages.Length - 1);
            OfferStageDefinition stage = record.Definition.Stages[stageIndex];
            if (stage == null)
            {
                return;
            }

            if (stage.BonusGold > 0)
            {
                _context.ResourceInventoryService.Add(ResourceInventoryService.GOLD_RESOURCE_ID, stage.BonusGold);
            }

            if (stage.BonusReputation != 0)
            {
                _reputationService.ApplyReputation(record.Definition.Customer, stage.BonusReputation);
            }
        }

        private void CompleteRecord(OfferRuntimeRecord record)
        {
            if (record == null)
            {
                return;
            }

            _context.ActiveOffers.Remove(record);
            record.ResolutionState = OfferResolutionState.Completed;
            record.IsReservedForShipment = false;
            record.ShipmentMissionTarget = 0;
            _context.ReservedByRuntimeId.Remove(record.RuntimeId);

            _context.ResourceInventoryService.Add(ResourceInventoryService.GOLD_RESOURCE_ID, GetGoldReward(record));
            _reputationService.ApplyReputation(record.Definition.Customer, Mathf.Max(0, record.Definition.ReputationReward));
            ApplyOutcomes(record);
            RegisterCompletedChainStep(record);
            _offerCompleted?.Invoke(record.Definition);
        }

        private void ApplyOutcomes(OfferRuntimeRecord record)
        {
            if (record?.Definition?.Outcomes == null)
            {
                return;
            }

            for (int i = 0; i < record.Definition.Outcomes.Length; i++)
            {
                OfferOutcomeDefinition outcome = record.Definition.Outcomes[i];
                if (outcome == null)
                {
                    continue;
                }

                if (outcome.GoldDelta != 0)
                {
                    _context.ResourceInventoryService.Add(ResourceInventoryService.GOLD_RESOURCE_ID, outcome.GoldDelta);
                }

                if (outcome.ReputationDelta != 0)
                {
                    _reputationService.ApplyReputation(record.Definition.Customer, outcome.ReputationDelta);
                }
            }
        }

        private void FailRecord(OfferRuntimeRecord record, bool releaseReservation)
        {
            if (record == null)
            {
                return;
            }

            if (releaseReservation && record.IsReservedForShipment)
            {
                _reservationService.ReleaseReservation(record.RuntimeId);
            }

            _context.ActiveOffers.Remove(record);
            record.ResolutionState = OfferResolutionState.Failed;
            record.IsReservedForShipment = false;
            record.ShipmentMissionTarget = 0;
            _context.ReservedByRuntimeId.Remove(record.RuntimeId);
            _reputationService.ApplyReputation(record.Definition.Customer, -GetFailPenalty(record));
        }

        private OfferResourceAmount[] GetReservationRequirements(OfferRuntimeRecord record)
        {
            if (record?.Definition == null)
            {
                return Array.Empty<OfferResourceAmount>();
            }

            if (record.Definition.HasStages)
            {
                return _objectiveEvaluationService.BuildCurrentStageReservationRequirements(record);
            }

            return record.Definition.CompletionRequirements ?? Array.Empty<OfferResourceAmount>();
        }

        private static OfferRuntimeRecord FindByRuntimeId(List<OfferRuntimeRecord> list, string runtimeId)
        {
            if (string.IsNullOrWhiteSpace(runtimeId))
            {
                return null;
            }

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].RuntimeId == runtimeId)
                {
                    return list[i];
                }
            }

            return null;
        }

        private int GetFailPenalty(OfferRuntimeRecord record)
        {
            if (record?.Definition == null)
            {
                return 0;
            }

            int penalty = Mathf.Abs(record.Definition.ReputationPenaltyOnFail);
            if (record.Definition.Archetype == OfferArchetype.EmergencyRequest)
            {
                penalty += 5;
            }

            return penalty;
        }

        private void RegisterCompletedChainStep(OfferRuntimeRecord record)
        {
            if (record?.Definition == null
                || string.IsNullOrWhiteSpace(record.Definition.ChainId)
                || record.Definition.ChainStep <= 0)
            {
                return;
            }

            _context.CompletedChainStepByChainId.TryGetValue(record.Definition.ChainId, out int currentStep);
            if (record.Definition.ChainStep > currentStep)
            {
                _context.CompletedChainStepByChainId[record.Definition.ChainId] = record.Definition.ChainStep;
            }
        }

        private void ResolveExclusiveGroup(OfferRuntimeRecord acceptedRecord)
        {
            if (acceptedRecord?.Definition == null || string.IsNullOrWhiteSpace(acceptedRecord.Definition.ExclusiveGroupId))
            {
                return;
            }

            string exclusiveGroupId = acceptedRecord.Definition.ExclusiveGroupId;
            for (int i = _context.AvailableOffers.Count - 1; i >= 0; i--)
            {
                OfferRuntimeRecord record = _context.AvailableOffers[i];
                if (record == null || record.RuntimeId == acceptedRecord.RuntimeId || record.Definition == null)
                {
                    continue;
                }

                if (!string.Equals(record.Definition.ExclusiveGroupId, exclusiveGroupId, StringComparison.Ordinal))
                {
                    continue;
                }

                _context.AvailableOffers.RemoveAt(i);
            }
        }

        private void ApplyRejectGenerationPenalty(OfferCustomerDefinition customer, int fallbackMinutes)
        {
            string customerKey = OfferReputationService.GetCustomerKey(customer);
            if (string.IsNullOrWhiteSpace(customerKey))
            {
                return;
            }

            int penaltyMinutes = Mathf.Max(DEFAULT_REJECT_PENALTY_MINUTES, Mathf.Max(0, fallbackMinutes));
            _context.GenerationPenaltyUntilMinutesByCustomerKey[customerKey] = _context.GameTimeService.TotalGameMinutes + penaltyMinutes;
        }
    }
}
