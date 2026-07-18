using System;
using System.Collections.Generic;
using _Project.Scripts.Data.Offers;
using _Project.Scripts.Systems.Offers.Runtime;
using _Project.Scripts.Systems.Resources;
using _Project.Scripts.Systems.Simulation;
using UnityEngine;

namespace _Project.Scripts.Systems.Offers
{
    [Serializable]
    public sealed class OfferRuntimeRecordState
    {
        public string RuntimeId;
        public string DefinitionId;
        public int CreatedAtSol;
        public int CreatedAtGameMinutes;
        public int DeadlineSol;
        public bool HasDeadline;
        public OfferTriggerSource Source;
        public bool IsReservedForShipment;
        public int ShipmentMissionTarget;
        public OfferResolutionState ResolutionState;
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
        public List<OfferRuntimeRecordState> AvailableOffers = new List<OfferRuntimeRecordState>();
        public List<OfferRuntimeRecordState> ActiveOffers = new List<OfferRuntimeRecordState>();
        public List<OfferReservedResourceState> ReservedResources = new List<OfferReservedResourceState>();
        public List<OfferCustomerReputationState> Reputation = new List<OfferCustomerReputationState>();
        public List<OfferCooldownState> Cooldowns = new List<OfferCooldownState>();
        public List<OfferGeneratedCountState> GeneratedCounts = new List<OfferGeneratedCountState>();
    }

    /// <summary>
    /// Runtime offer service facade for external systems (UI/bootstrap/shop/save).
    /// All domain logic is delegated to Runtime services, while this class remains the stable entry point.
    /// Maintenance Map:
    /// - Runtime composition and event wiring: OfferSystemService(...) constructor.
    /// - Periodic processing (deadlines/time generation): Tick().
    /// - Offer flow (accept/reject/reserve/resolve): AcceptOffer(), RejectOffer(), TryReserveOfferForNextMission(), ResolveOffersOnMissionArrived().
    /// - Resources/reputation/read models: HasResources(), GetReservedAmount(), GetCustomerReputation(), GetCustomerPortrait().
    /// - Persistence boundary: CaptureState(), RestoreState().
    /// </summary>
    public sealed class OfferSystemService : IDisposable
    {
        private readonly OfferSystemContext _context;
        private readonly OfferGenerationService _generationService;
        private readonly OfferLifecycleService _lifecycleService;
        private readonly OfferReservationService _reservationService;
        private readonly OfferReputationService _reputationService;
        private readonly OfferStateSerializer _stateSerializer;

        /// <summary>
        /// Main composition point: builds Runtime services and subscribes generation to inventory events.
        /// Start here when analyzing dependencies and initialization order.
        /// </summary>
        public OfferSystemService(
            IReadOnlyList<OfferDefinition> catalog,
            ResourceInventoryService resourceInventoryService,
            GameTimeService gameTimeService)
        {
            _context = new OfferSystemContext(catalog, resourceInventoryService, gameTimeService);
            _reservationService = new OfferReservationService(_context);
            _reputationService = new OfferReputationService(_context);
            _generationService = new OfferGenerationService(_context, _reservationService, NotifyStateChanged);
            _lifecycleService = new OfferLifecycleService(_context, _reservationService, _reputationService, NotifyStateChanged);
            _stateSerializer = new OfferStateSerializer(_context, _reputationService);

            if (_context.ResourceInventoryService != null)
            {
                _context.ResourceInventoryService.ResourceAmountChanged += OnResourceAmountChanged;
            }
        }

        /// <summary>
        /// Single event for UI/observers. Any offer state change is routed through it.
        /// </summary>
        public event Action StateChanged;

        /// <summary>
        /// Current player gold. Gold is stored in ResourceInventoryService; this facade keeps existing UI code simple.
        /// </summary>
        public int Gold => _context.ResourceInventoryService != null
            ? _context.ResourceInventoryService.GetAmount(ResourceInventoryService.GOLD_RESOURCE_ID)
            : 0;

        /// <summary>
        /// List of newly generated and available offers.
        /// </summary>
        public IReadOnlyList<OfferRuntimeRecord> AvailableOffers => _context.AvailableOffers;

        /// <summary>
        /// List of active offers already accepted by the player.
        /// </summary>
        public IReadOnlyList<OfferRuntimeRecord> ActiveOffers => _context.ActiveOffers;

        /// <summary>
        /// Releases service subscriptions (important on runtime restart/scene switch).
        /// </summary>
        public void Dispose()
        {
            if (_context.ResourceInventoryService != null)
            {
                _context.ResourceInventoryService.ResourceAmountChanged -= OnResourceAmountChanged;
            }
        }

        /// <summary>
        /// Fast check whether gold amount is sufficient.
        /// </summary>
        public bool HasGold(int amount)
        {
            return _context.ResourceInventoryService != null
                && _context.ResourceInventoryService.Has(ResourceInventoryService.GOLD_RESOURCE_ID, Mathf.Max(0, amount));
        }

        /// <summary>
        /// Attempts to spend gold from inventory and notifies UI on success.
        /// </summary>
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

        /// <summary>
        /// Main tick for the offer system.
        /// For auto-generation/deadline issues, inspect ProcessDeadlines + ProcessTimeOffers.
        /// </summary>
        public void Tick()
        {
            // Process once per game hour to avoid duplicate generation/deadline handling in the same hour.
            if (_context.GameTimeService.Hour == _context.LastProcessedHour)
            {
                return;
            }

            _context.LastProcessedHour = _context.GameTimeService.Hour;
            _lifecycleService.ProcessDeadlines();
            _generationService.ProcessTimeOffers();
        }

        /// <summary>
        /// Debug/manual entry: forces an offer by OfferId.
        /// </summary>
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

        /// <summary>
        /// Debug/manual entry: forces an offer by definition reference.
        /// </summary>
        public bool ForceOffer(OfferDefinition definition, bool ignoreCooldown = false)
        {
            return _generationService.TryCreateOffer(definition, OfferTriggerSource.Manual, ignoreCooldown, null);
        }

        /// <summary>
        /// Reads customer reputation (used by UI and reward/penalty flows).
        /// </summary>
        public int GetCustomerReputation(OfferCustomerDefinition customer)
        {
            return _reputationService.GetCustomerReputation(customer);
        }

        /// <summary>
        /// Accepts an offer: moves it to Active and reserves shipment resources.
        /// </summary>
        public bool AcceptOffer(string runtimeId)
        {
            return _lifecycleService.AcceptOffer(runtimeId);
        }

        /// <summary>
        /// Rejects an offer: removes it from Available and applies reputation penalty.
        /// </summary>
        public bool RejectOffer(string runtimeId)
        {
            return _lifecycleService.RejectOffer(runtimeId);
        }

        /// <summary>
        /// Legacy alias: completion here is equivalent to reserving for the next mission.
        /// </summary>
        public bool TryCompleteOffer(string runtimeId)
        {
            return TryReserveOfferForNextMission(runtimeId, 0);
        }

        /// <summary>
        /// Reserves an active offer for the specified next mission.
        /// </summary>
        public bool TryReserveOfferForNextMission(string runtimeId, int missionCount)
        {
            return _lifecycleService.TryReserveOfferForNextMission(runtimeId, missionCount);
        }

        /// <summary>
        /// Cancels resource reservation for an active offer.
        /// </summary>
        public bool CancelOfferReservation(string runtimeId)
        {
            return _lifecycleService.CancelOfferReservation(runtimeId);
        }

        /// <summary>
        /// Called on mission arrival: completes/fails eligible active offers.
        /// </summary>
        public void ResolveOffersOnMissionArrived(int missionCount)
        {
            _lifecycleService.ResolveOffersOnMissionArrived(missionCount);
        }

        /// <summary>
        /// Checks resource sufficiency for requirements.
        /// </summary>
        public bool HasResources(OfferResourceAmount[] requirements)
        {
            return _reservationService.HasResources(requirements);
        }

        /// <summary>
        /// Returns how much of a resource is already reserved by all offers.
        /// </summary>
        public int GetReservedAmount(string resourceId)
        {
            return _reservationService.GetReservedAmount(resourceId);
        }

        /// <summary>
        /// Selects customer portrait by current reputation.
        /// </summary>
        public Sprite GetCustomerPortrait(OfferCustomerDefinition customer)
        {
            return _reputationService.GetCustomerPortrait(customer);
        }

        /// <summary>
        /// Remaining cooldown in game minutes for a repeatable offer.
        /// </summary>
        public int GetCooldownRemainingMinutes(OfferDefinition definition)
        {
            return _generationService.GetCooldownRemainingMinutes(definition);
        }

        /// <summary>
        /// Captures state snapshot for save/persist layer.
        /// </summary>
        public OfferSystemState CaptureState()
        {
            return _stateSerializer.CaptureState();
        }

        /// <summary>
        /// Restores state from save/persist layer and notifies UI.
        /// </summary>
        public void RestoreState(OfferSystemState state)
        {
            _stateSerializer.RestoreState(state);
            NotifyStateChanged();
        }

        /// <summary>
        /// Centralized StateChanged emit so all updates are dispatched consistently.
        /// </summary>
        private void NotifyStateChanged()
        {
            StateChanged?.Invoke();
        }

        private void OnResourceAmountChanged(ResourceAmountChangedEvent changeEvent)
        {
            _generationService.OnResourceAmountChanged(changeEvent);

            if (string.Equals(changeEvent.ResourceId, ResourceInventoryService.GOLD_RESOURCE_ID, StringComparison.OrdinalIgnoreCase))
            {
                NotifyStateChanged();
            }
        }
    }
}
