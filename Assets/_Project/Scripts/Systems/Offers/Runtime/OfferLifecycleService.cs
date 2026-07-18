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
    internal sealed class OfferLifecycleService
    {
        private readonly OfferSystemContext _context;
        private readonly OfferReservationService _reservationService;
        private readonly OfferReputationService _reputationService;
        private readonly Action _stateChanged;

        /// <summary>
        /// Creates lifecycle service over shared context and collaborators.
        /// </summary>
        public OfferLifecycleService(
            OfferSystemContext context,
            OfferReservationService reservationService,
            OfferReputationService reputationService,
            Action stateChanged)
        {
            _context = context;
            _reservationService = reservationService;
            _reputationService = reputationService;
            _stateChanged = stateChanged;
        }

        /// <summary>
        /// Moves an available offer into active state and reserves completion resources.
        /// </summary>
        public bool AcceptOffer(string runtimeId)
        {
            OfferRuntimeRecord record = FindByRuntimeId(_context.AvailableOffers, runtimeId);
            if (record == null)
            {
                return false;
            }

            OfferResourceAmount[] requirements = record.Definition.CompletionRequirements;
            if (!_reservationService.HasResources(requirements))
            {
                return false;
            }

            Dictionary<string, int> reservedResources = OfferReservationService.BuildReservationMap(requirements);
            if (!_reservationService.ConsumeResources(reservedResources))
            {
                return false;
            }

            _context.AvailableOffers.Remove(record);
            record.ResolutionState = OfferResolutionState.Active;
            record.IsReservedForShipment = true;
            record.ShipmentMissionTarget = 0;
            _context.ReservedByRuntimeId[record.RuntimeId] = reservedResources;
            _context.ActiveOffers.Add(record);
            _stateChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Rejects available offer and applies customer reputation penalty.
        /// </summary>
        public bool RejectOffer(string runtimeId)
        {
            OfferRuntimeRecord record = FindByRuntimeId(_context.AvailableOffers, runtimeId);
            if (record == null)
            {
                return false;
            }

            _context.AvailableOffers.Remove(record);
            _reputationService.ApplyReputation(record.Definition.Customer, -Mathf.Abs(record.Definition.ReputationPenaltyOnReject));
            _stateChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Reserves resources for shipment and binds offer completion to target mission index.
        /// </summary>
        public bool TryReserveOfferForNextMission(string runtimeId, int missionCount)
        {
            OfferRuntimeRecord record = FindByRuntimeId(_context.ActiveOffers, runtimeId);
            if (record == null || record.ResolutionState != OfferResolutionState.Active)
            {
                return false;
            }

            if (record.IsReservedForShipment)
            {
                return true;
            }

            OfferResourceAmount[] requirements = record.Definition.CompletionRequirements;
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
            record.ShipmentMissionTarget = Mathf.Max(0, missionCount + 1);
            _stateChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Cancels previously reserved shipment resources for active offer.
        /// </summary>
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

        /// <summary>
        /// Resolves active offers when mission arrives: complete when fully reserved, otherwise fail.
        /// </summary>
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

                if (record.ShipmentMissionTarget > 0 && missionCount < record.ShipmentMissionTarget)
                {
                    continue;
                }

                if (record.IsReservedForShipment && _reservationService.HasFullReservation(record.RuntimeId, record.Definition.CompletionRequirements))
                {
                    _context.ActiveOffers.RemoveAt(i);
                    record.ResolutionState = OfferResolutionState.Completed;
                    record.IsReservedForShipment = false;
                    record.ShipmentMissionTarget = 0;
                    _context.ReservedByRuntimeId.Remove(record.RuntimeId);

                    _context.ResourceInventoryService.Add(ResourceInventoryService.GOLD_RESOURCE_ID, Mathf.Max(0, record.Definition.GoldReward));
                    _reputationService.ApplyReputation(record.Definition.Customer, Mathf.Max(0, record.Definition.ReputationReward));
                    hasStateChanges = true;
                    continue;
                }

                if (record.IsReservedForShipment)
                {
                    _reservationService.ReleaseReservation(record.RuntimeId);
                }

                _context.ActiveOffers.RemoveAt(i);
                record.ResolutionState = OfferResolutionState.Failed;
                record.IsReservedForShipment = false;
                record.ShipmentMissionTarget = 0;
                _reputationService.ApplyReputation(record.Definition.Customer, -Mathf.Abs(record.Definition.ReputationPenaltyOnFail));
                hasStateChanges = true;
            }

            if (hasStateChanges)
            {
                _stateChanged?.Invoke();
            }
        }

        /// <summary>
        /// Fails offers that exceeded deadline and releases their reservations.
        /// </summary>
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

                if (offer.IsReservedForShipment)
                {
                    _reservationService.ReleaseReservation(offer.RuntimeId);
                }

                _context.ActiveOffers.RemoveAt(i);
                offer.ResolutionState = OfferResolutionState.Failed;
                offer.IsReservedForShipment = false;
                offer.ShipmentMissionTarget = 0;
                _reputationService.ApplyReputation(offer.Definition.Customer, -Mathf.Abs(offer.Definition.ReputationPenaltyOnFail));
                hasStateChanges = true;
            }

            if (hasStateChanges)
            {
                _stateChanged?.Invoke();
            }
        }

        /// <summary>
        /// Finds runtime record in a list by runtime id.
        /// </summary>
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
    }
}
