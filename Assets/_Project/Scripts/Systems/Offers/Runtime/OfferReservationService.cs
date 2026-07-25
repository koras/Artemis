using System;
using System.Collections.Generic;
using _Project.Scripts.Data.Offers;
using UnityEngine;

namespace _Project.Scripts.Systems.Offers.Runtime
{
    /// <summary>
    /// Handles resource checks, reservation consume/release, and reservation integrity validation.
    /// </summary>
    public sealed class OfferReservationService
    {
        private readonly OfferSystemContext _context;

        /// <summary>
        /// Creates reservation service over shared runtime context.
        /// </summary>
        public OfferReservationService(OfferSystemContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Returns true when all requirement amounts are currently present in inventory.
        /// </summary>
        public bool HasResources(OfferResourceAmount[] requirements)
        {
            if (requirements == null || requirements.Length == 0)
            {
                return true;
            }

            for (int i = 0; i < requirements.Length; i++)
            {
                OfferResourceAmount requirement = requirements[i];
                if (string.IsNullOrWhiteSpace(requirement.ResourceId) || requirement.Amount <= 0)
                {
                    continue;
                }

                if (!_context.ResourceInventoryService.Has(requirement.ResourceId, requirement.Amount))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Calculates total amount reserved for a resource across all active reservations.
        /// </summary>
        public int GetReservedAmount(string resourceId)
        {
            if (string.IsNullOrWhiteSpace(resourceId))
            {
                return 0;
            }

            int total = 0;
            foreach (KeyValuePair<string, Dictionary<string, int>> pair in _context.ReservedByRuntimeId)
            {
                Dictionary<string, int> map = pair.Value;
                if (map == null)
                {
                    continue;
                }

                if (!map.TryGetValue(resourceId, out int amount))
                {
                    continue;
                }

                total += Mathf.Max(0, amount);
            }

            return total;
        }

        /// <summary>
        /// Aggregates requirement array into a per-resource reservation map.
        /// </summary>
        public static Dictionary<string, int> BuildReservationMap(OfferResourceAmount[] requirements)
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (requirements == null)
            {
                return result;
            }

            for (int i = 0; i < requirements.Length; i++)
            {
                OfferResourceAmount requirement = requirements[i];
                if (string.IsNullOrWhiteSpace(requirement.ResourceId) || requirement.Amount <= 0)
                {
                    continue;
                }

                result.TryGetValue(requirement.ResourceId, out int currentAmount);
                result[requirement.ResourceId] = currentAmount + requirement.Amount;
            }

            return result;
        }

        /// <summary>
        /// Atomically validates and then removes resources from inventory for reservation.
        /// </summary>
        public bool ConsumeResources(Dictionary<string, int> requirements)
        {
            if (requirements == null || requirements.Count == 0)
            {
                return true;
            }

            foreach (KeyValuePair<string, int> pair in requirements)
            {
                if (!_context.ResourceInventoryService.Has(pair.Key, pair.Value))
                {
                    return false;
                }
            }

            foreach (KeyValuePair<string, int> pair in requirements)
            {
                if (!_context.ResourceInventoryService.TryRemove(pair.Key, pair.Value))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Returns reserved resources back to inventory and removes reservation entry.
        /// </summary>
        public void ReleaseReservation(string runtimeId)
        {
            if (string.IsNullOrWhiteSpace(runtimeId))
            {
                return;
            }

            if (!_context.ReservedByRuntimeId.TryGetValue(runtimeId, out Dictionary<string, int> reservedResources) || reservedResources == null)
            {
                return;
            }

            foreach (KeyValuePair<string, int> pair in reservedResources)
            {
                if (pair.Value <= 0)
                {
                    continue;
                }

                _context.ResourceInventoryService.Add(pair.Key, pair.Value);
            }

            _context.ReservedByRuntimeId.Remove(runtimeId);
        }

        /// <summary>
        /// Checks whether reservation fully covers expected completion requirements.
        /// </summary>
        public bool HasFullReservation(string runtimeId, OfferResourceAmount[] requirements)
        {
            if (string.IsNullOrWhiteSpace(runtimeId))
            {
                return false;
            }

            if (!_context.ReservedByRuntimeId.TryGetValue(runtimeId, out Dictionary<string, int> reservedResources) || reservedResources == null)
            {
                return false;
            }

            Dictionary<string, int> expected = BuildReservationMap(requirements);
            foreach (KeyValuePair<string, int> pair in expected)
            {
                if (!reservedResources.TryGetValue(pair.Key, out int reservedAmount))
                {
                    return false;
                }

                if (reservedAmount < pair.Value)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
