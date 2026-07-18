using System;
using System.Collections.Generic;
using _Project.Scripts.Data.Offers;
using _Project.Scripts.Systems.Resources;
using UnityEngine;

namespace _Project.Scripts.Systems.Offers.Runtime
{
    /// <summary>
    /// Generates offers from time/resource triggers and validates appearance eligibility.
    /// </summary>
    internal sealed class OfferGenerationService
    {
        private readonly OfferSystemContext _context;
        private readonly OfferReservationService _reservationService;
        private readonly Action _stateChanged;

        /// <summary>
        /// Creates generation service over shared context and state-change callback.
        /// </summary>
        public OfferGenerationService(OfferSystemContext context, OfferReservationService reservationService, Action stateChanged)
        {
            _context = context;
            _reservationService = reservationService;
            _stateChanged = stateChanged;
        }

        /// <summary>
        /// Processes hourly time-triggered offer generation.
        /// </summary>
        public void ProcessTimeOffers()
        {
            var eligible = new List<OfferDefinition>();
            for (int i = 0; i < _context.Catalog.Count; i++)
            {
                OfferDefinition definition = _context.Catalog[i];
                if (definition == null)
                {
                    continue;
                }

                if (!HasTrigger(definition, OfferTriggerType.Time) || !IsEligible(definition, OfferTriggerSource.Time, false))
                {
                    continue;
                }

                eligible.Add(definition);
            }

            if (eligible.Count == 0)
            {
                return;
            }

            OfferDefinition selected = eligible[UnityEngine.Random.Range(0, eligible.Count)];
            TryCreateOffer(selected, OfferTriggerSource.Time, false, null);
        }

        /// <summary>
        /// Handles inventory change events and tries to generate matching resource-triggered offers.
        /// </summary>
        public void OnResourceAmountChanged(ResourceAmountChangedEvent changedEvent)
        {
            for (int i = 0; i < _context.Catalog.Count; i++)
            {
                OfferDefinition definition = _context.Catalog[i];
                if (definition == null)
                {
                    continue;
                }

                if (!HasTrigger(definition, OfferTriggerType.ResourceEvent) || !MatchesResourceEvent(definition, changedEvent))
                {
                    continue;
                }

                TryCreateOffer(definition, OfferTriggerSource.ResourceEvent, false, changedEvent);
            }
        }

        /// <summary>
        /// Tries to create a new runtime offer record for a trigger source.
        /// </summary>
        public bool TryCreateOffer(OfferDefinition definition, OfferTriggerSource source, bool ignoreCooldown, ResourceAmountChangedEvent? changedEvent)
        {
            if (!IsEligible(definition, source, ignoreCooldown))
            {
                return false;
            }

            if (source == OfferTriggerSource.Time)
            {
                float chance = Mathf.Clamp01(definition.HourlySpawnChance);
                if (UnityEngine.Random.value > chance)
                {
                    return false;
                }
            }

            if (source == OfferTriggerSource.ResourceEvent && !MatchesResourceEvent(definition, changedEvent))
            {
                return false;
            }

            int? deadlineSol = null;
            if (definition.UseDeadline)
            {
                deadlineSol = _context.GameTimeService.Sol + Mathf.Max(1, definition.DeadlineDays);
            }

            string definitionId = definition.OfferId.Trim();
            var record = new OfferRuntimeRecord(
                definition,
                definitionId,
                _context.GameTimeService.Sol,
                _context.GameTimeService.TotalGameMinutes,
                deadlineSol,
                source);
            _context.AvailableOffers.Add(record);

            _context.LastGeneratedAtMinutesByDefinition[definitionId] = _context.GameTimeService.TotalGameMinutes;
            _context.GeneratedCountByDefinition.TryGetValue(definitionId, out int generatedCount);
            _context.GeneratedCountByDefinition[definitionId] = generatedCount + 1;

            _stateChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Checks full eligibility pipeline before offer creation.
        /// </summary>
        public bool IsEligible(OfferDefinition definition, OfferTriggerSource source, bool ignoreCooldown)
        {
            if (definition == null || definition.Customer == null || string.IsNullOrWhiteSpace(definition.OfferId))
            {
                return false;
            }

            OfferTriggerType requiredTrigger = ToTriggerType(source);
            if (!HasTrigger(definition, requiredTrigger))
            {
                return false;
            }

            if (!_reservationService.HasResources(definition.AppearanceRequirements))
            {
                return false;
            }

            if (source == OfferTriggerSource.Time && _context.GameTimeService.TotalGameMinutes < Mathf.Max(0, definition.MinGameMinutesToAppear))
            {
                return false;
            }

            string definitionId = definition.OfferId.Trim();
            if (ContainsDefinitionId(_context.AvailableOffers, definitionId) || ContainsDefinitionId(_context.ActiveOffers, definitionId))
            {
                return false;
            }

            if (!definition.IsRepeatable && _context.GeneratedCountByDefinition.TryGetValue(definitionId, out int generatedCount) && generatedCount > 0)
            {
                return false;
            }

            if (!ignoreCooldown && definition.IsRepeatable && definition.CooldownGameMinutes > 0)
            {
                if (_context.LastGeneratedAtMinutesByDefinition.TryGetValue(definitionId, out int lastGeneratedAtMinutes))
                {
                    int elapsed = _context.GameTimeService.TotalGameMinutes - lastGeneratedAtMinutes;
                    if (elapsed < definition.CooldownGameMinutes)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Returns remaining cooldown in game minutes for repeatable definition.
        /// </summary>
        public int GetCooldownRemainingMinutes(OfferDefinition definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.OfferId) || !definition.IsRepeatable)
            {
                return 0;
            }

            if (!_context.LastGeneratedAtMinutesByDefinition.TryGetValue(definition.OfferId, out int lastGeneratedAtMinutes))
            {
                return 0;
            }

            int cooldown = Mathf.Max(0, definition.CooldownGameMinutes);
            int elapsed = _context.GameTimeService.TotalGameMinutes - lastGeneratedAtMinutes;
            return Mathf.Max(0, cooldown - elapsed);
        }

        /// <summary>
        /// Validates whether changed resource payload satisfies any resource-event condition.
        /// </summary>
        public static bool MatchesResourceEvent(OfferDefinition definition, ResourceAmountChangedEvent? changedEvent)
        {
            if (!changedEvent.HasValue)
            {
                return false;
            }

            OfferResourceEventCondition[] conditions = definition.ResourceEventConditions;
            if (conditions == null || conditions.Length == 0)
            {
                return false;
            }

            ResourceAmountChangedEvent payload = changedEvent.Value;
            for (int i = 0; i < conditions.Length; i++)
            {
                OfferResourceEventCondition condition = conditions[i];
                if (string.IsNullOrWhiteSpace(condition.ResourceId))
                {
                    continue;
                }

                if (!string.Equals(condition.ResourceId, payload.ResourceId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                bool isDeltaSatisfied = condition.RequiredDelta <= 0 || payload.Delta >= condition.RequiredDelta;
                bool isTotalSatisfied = condition.RequiredTotalAmount <= 0 || payload.TotalAmount >= condition.RequiredTotalAmount;
                if (isDeltaSatisfied && isTotalSatisfied)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Maps runtime trigger source to definition trigger type mask.
        /// </summary>
        private static OfferTriggerType ToTriggerType(OfferTriggerSource source)
        {
            if (source == OfferTriggerSource.Time)
            {
                return OfferTriggerType.Time;
            }

            if (source == OfferTriggerSource.ResourceEvent)
            {
                return OfferTriggerType.ResourceEvent;
            }

            return OfferTriggerType.Manual;
        }

        /// <summary>
        /// Checks if definition contains required trigger flag.
        /// </summary>
        private static bool HasTrigger(OfferDefinition definition, OfferTriggerType trigger)
        {
            return (definition.TriggerTypes & trigger) == trigger;
        }

        /// <summary>
        /// Checks whether list already contains an offer instance with same definition id.
        /// </summary>
        private static bool ContainsDefinitionId(List<OfferRuntimeRecord> list, string definitionId)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i].DefinitionId, definitionId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
