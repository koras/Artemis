using System;
using System.Collections.Generic;
using _Project.Scripts.Data.Grid;
using _Project.Scripts.Data.Offers;
using _Project.Scripts.Systems.Construction;
using _Project.Scripts.Systems.Offers.Runtime;
using _Project.Scripts.Systems.Resources;
using _Project.Scripts.Systems.Simulation;
using UnityEngine;

namespace _Project.Scripts.Systems.Offers
{
    [Serializable]
    public sealed class OfferStageRuntimeStateState
    {
        public int CurrentStageIndex;
        public int StageStartedMissionCount;
        public int StageSatisfiedSinceSol = -1;
        public bool IsInspectionScheduled;
        public int CompletedObjectiveCount;
    }

    [Serializable]
    public sealed class OfferRuntimeRecordState
    {
        public string RuntimeId;
        public string DefinitionId;
        public int CreatedAtSol;
        public int CreatedAtGameMinutes;
        public int AcceptedAtGameMinutes;
        public int ReservedAtGameMinutes;
        public bool FastReserveBonusGranted;
        public int DeadlineSol;
        public bool HasDeadline;
        public OfferTriggerSource Source;
        public bool IsReservedForShipment;
        public int ShipmentMissionTarget;
        public OfferResolutionState ResolutionState;
        public int MissionArrivalCountAtAccept;
        public int MissionArrivalCount;
        public OfferStageRuntimeStateState StageState = new OfferStageRuntimeStateState();
    }

    [Serializable]
    public sealed class OfferReservedResourceState
    {
        public string RuntimeId;
        public string ResourceId;
        public int Amount;
    }

    [Serializable]
    public sealed class OfferCustomerReputationState
    {
        public string CustomerKey;
        public int Reputation;
    }

    [Serializable]
    public sealed class OfferCooldownState
    {
        public string DefinitionId;
        public int LastGeneratedAtGameMinutes;
    }

    [Serializable]
    public sealed class OfferChainProgressState
    {
        public string ChainId;
        public int CompletedStep;
    }

    [Serializable]
    public sealed class OfferGenerationPenaltyState
    {
        public string CustomerKey;
        public int UntilGameMinutes;
    }

    [Serializable]
    public sealed class OfferGeneratedCountState
    {
        public string DefinitionId;
        public int Count;
    }

    [Serializable]
    public sealed class OfferSystemState
    {
        public int Gold;
        public int LastProcessedHour = -1;
        public int MissionArrivalCount;
        public List<OfferRuntimeRecordState> AvailableOffers = new List<OfferRuntimeRecordState>();
        public List<OfferRuntimeRecordState> ActiveOffers = new List<OfferRuntimeRecordState>();
        public List<OfferReservedResourceState> ReservedResources = new List<OfferReservedResourceState>();
        public List<OfferCustomerReputationState> Reputation = new List<OfferCustomerReputationState>();
        public List<OfferCooldownState> Cooldowns = new List<OfferCooldownState>();
        public List<OfferGeneratedCountState> GeneratedCounts = new List<OfferGeneratedCountState>();
        public List<OfferChainProgressState> ChainProgress = new List<OfferChainProgressState>();
        public List<OfferGenerationPenaltyState> GenerationPenalties = new List<OfferGenerationPenaltyState>();
    }

    /// <summary>
    /// Runtime offer service facade for external systems (UI/bootstrap/shop/save).
    /// </summary>
    public sealed class OfferSystemService : IDisposable
    {
        private readonly OfferSystemContext _context;
        private readonly OfferGenerationService _generationService;
        private readonly OfferLifecycleService _lifecycleService;
        private readonly OfferReservationService _reservationService;
        private readonly OfferReputationService _reputationService;
        private readonly OfferStateSerializer _stateSerializer;
        private readonly OfferObjectiveEvaluationService _objectiveEvaluationService;

        public OfferSystemService(
            IReadOnlyList<OfferDefinition> catalog,
            GridState gridState,
            BuildingManager buildingManager,
            ResourceInventoryService resourceInventoryService,
            GameTimeService gameTimeService)
        {
            _context = new OfferSystemContext(catalog, gridState, buildingManager, resourceInventoryService, gameTimeService);
            _reservationService = new OfferReservationService(_context);
            _reputationService = new OfferReputationService(_context);
            _objectiveEvaluationService = new OfferObjectiveEvaluationService(
                gridState,
                buildingManager,
                resourceInventoryService,
                gameTimeService,
                _reservationService);
            _generationService = new OfferGenerationService(_context, _reservationService, _objectiveEvaluationService, NotifyStateChanged);
            _lifecycleService = new OfferLifecycleService(
                _context,
                _reservationService,
                _reputationService,
                _objectiveEvaluationService,
                NotifyStateChanged);
            _stateSerializer = new OfferStateSerializer(_context, _reputationService);

            if (_context.ResourceInventoryService != null)
            {
                _context.ResourceInventoryService.ResourceAmountChanged += OnResourceAmountChanged;
            }
        }

        public event Action StateChanged;

        public int Gold => _context.ResourceInventoryService != null
            ? _context.ResourceInventoryService.GetAmount(ResourceInventoryService.GOLD_RESOURCE_ID)
            : 0;

        public IReadOnlyList<OfferRuntimeRecord> AvailableOffers => _context.AvailableOffers;

        public IReadOnlyList<OfferRuntimeRecord> ActiveOffers => _context.ActiveOffers;

        public void Dispose()
        {
            if (_context.ResourceInventoryService != null)
            {
                _context.ResourceInventoryService.ResourceAmountChanged -= OnResourceAmountChanged;
            }

            StateChanged = null;
        }

        public bool HasGold(int amount)
        {
            return _context.ResourceInventoryService != null
                && _context.ResourceInventoryService.Has(ResourceInventoryService.GOLD_RESOURCE_ID, Mathf.Max(0, amount));
        }

        public bool TrySpendGold(int amount)
        {
            int sanitizedAmount = Mathf.Max(0, amount);
            if (_context.ResourceInventoryService == null
                || !_context.ResourceInventoryService.TryRemove(ResourceInventoryService.GOLD_RESOURCE_ID, sanitizedAmount))
            {
                return false;
            }

            NotifyStateChanged();
            return true;
        }

        public void Tick()
        {
            if (_context.GameTimeService.Hour == _context.LastProcessedHour)
            {
                return;
            }

            _context.LastProcessedHour = _context.GameTimeService.Hour;
            _lifecycleService.ProcessDeadlines();
            _lifecycleService.ProcessActiveOfferProgress();
            _generationService.ProcessTimeOffers();
        }

        public bool ForceOffer(string offerId, bool ignoreCooldown = false)
        {
            if (string.IsNullOrWhiteSpace(offerId))
            {
                return false;
            }

            if (!_context.DefinitionById.TryGetValue(offerId, out OfferDefinition definition))
            {
                return false;
            }

            return ForceOffer(definition, ignoreCooldown);
        }

        public bool ForceOffer(OfferDefinition definition, bool ignoreCooldown = false)
        {
            return _generationService.TryCreateOffer(definition, OfferTriggerSource.Manual, ignoreCooldown, null);
        }

        public int GetCustomerReputation(OfferCustomerDefinition customer)
        {
            return _reputationService.GetCustomerReputation(customer);
        }

        public bool AcceptOffer(string runtimeId)
        {
            return _lifecycleService.AcceptOffer(runtimeId);
        }

        public bool RejectOffer(string runtimeId)
        {
            return _lifecycleService.RejectOffer(runtimeId);
        }

        public bool TryCompleteOffer(string runtimeId)
        {
            return TryReserveOfferForNextMission(runtimeId, 0);
        }

        public bool TryReserveOfferForNextMission(string runtimeId, int missionCount)
        {
            return _lifecycleService.TryReserveOfferForNextMission(runtimeId, missionCount);
        }

        public bool CancelOfferReservation(string runtimeId)
        {
            return _lifecycleService.CancelOfferReservation(runtimeId);
        }

        public void ResolveOffersOnMissionArrived(int missionCount)
        {
            _context.MissionArrivalCount = Mathf.Max(_context.MissionArrivalCount, missionCount);
            _lifecycleService.ResolveOffersOnMissionArrived(missionCount);
        }

        public bool HasResources(OfferResourceAmount[] requirements)
        {
            return _reservationService.HasResources(requirements);
        }

        public int GetReservedAmount(string resourceId)
        {
            return _reservationService.GetReservedAmount(resourceId);
        }

        public Sprite GetCustomerPortrait(OfferCustomerDefinition customer)
        {
            return _reputationService.GetCustomerPortrait(customer);
        }

        public int GetCooldownRemainingMinutes(OfferDefinition definition)
        {
            return _generationService.GetCooldownRemainingMinutes(definition);
        }

        public int GetGoldReward(OfferRuntimeRecord record)
        {
            return _lifecycleService.GetGoldReward(record);
        }

        public bool IsFastReserveWindowOpen(OfferRuntimeRecord record)
        {
            return _lifecycleService.IsFastReserveWindowOpen(record);
        }

        public OfferSystemState CaptureState()
        {
            return _stateSerializer.CaptureState();
        }

        public void RestoreState(OfferSystemState state)
        {
            _stateSerializer.RestoreState(state);
            NotifyStateChanged();
        }

        public OfferStageProgressSnapshot GetStageProgress(OfferRuntimeRecord record)
        {
            return _objectiveEvaluationService.BuildStageSnapshot(record);
        }

        public bool CurrentStageHasDeliverObjectives(OfferRuntimeRecord record)
        {
            return _objectiveEvaluationService.CurrentStageHasDeliverObjectives(record);
        }

        private void NotifyStateChanged()
        {
            StateChanged?.Invoke();
        }

        private void OnResourceAmountChanged(ResourceAmountChangedEvent changeEvent)
        {
            _generationService.OnResourceAmountChanged(changeEvent);
            _lifecycleService.ProcessActiveOfferProgress();

            if (string.Equals(changeEvent.ResourceId, ResourceInventoryService.GOLD_RESOURCE_ID, StringComparison.OrdinalIgnoreCase))
            {
                NotifyStateChanged();
            }
        }
    }
}