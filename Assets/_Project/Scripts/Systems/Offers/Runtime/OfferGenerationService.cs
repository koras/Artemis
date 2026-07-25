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
    public sealed class OfferGenerationService
    {
        private readonly OfferSystemContext _context;
        private readonly OfferReservationService _reservationService;
        private readonly OfferObjectiveEvaluationService _objectiveEvaluationService;
        private readonly Action _stateChanged;

        public OfferGenerationService(
            OfferSystemContext context,
            OfferReservationService reservationService,
            OfferObjectiveEvaluationService objectiveEvaluationService,
            Action stateChanged)
        {
            _context = context;
            _reservationService = reservationService;
            _objectiveEvaluationService = objectiveEvaluationService;
            _stateChanged = stateChanged;
        }

        public void ProcessTimeOffers()
        {
            var eligible = new List<OfferDefinition>();
            var weights = new List<float>();
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
                weights.Add(GetGenerationWeight(definition));
            }

            if (eligible.Count == 0)
            {
                return;
            }

            OfferDefinition selected = SelectWeightedDefinition(eligible, weights);
            TryCreateOffer(selected, OfferTriggerSource.Time, false, null);
        }

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

            if (_context.GameTimeService.TotalGameMinutes < Mathf.Max(0, definition.MinGameMinutesToAppear))
            {
                return false;
            }

            if (_context.GameTimeService.TotalGameMinutes < GetCustomerPenaltyUntilMinutes(definition.Customer))
            {
                return false;
            }

            if (!_objectiveEvaluationService.PassesUnlockConditions(definition))
            {
                return false;
            }

            string definitionId = definition.OfferId.Trim();
            if (ContainsDefinitionId(_context.AvailableOffers, definitionId) || ContainsDefinitionId(_context.ActiveOffers, definitionId))
            {
                return false;
            }

            if (ContainsExclusiveGroupInActiveOffers(definition.ExclusiveGroupId))
            {
                return false;
            }

            if (!PassesChainProgress(definition))
            {
                return false;
            }

            int customerReputation = GetCustomerReputation(definition);
            if (customerReputation < Mathf.Clamp(definition.MinCustomerReputationToAppear, 0, 100))
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

        private static bool HasTrigger(OfferDefinition definition, OfferTriggerType trigger)
        {
            return (definition.TriggerTypes & trigger) == trigger;
        }

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

        private float GetGenerationWeight(OfferDefinition definition)
        {
            float weight = Mathf.Max(0.05f, definition.BaseGenerationWeight);

            switch (definition.Archetype)
            {
                case OfferArchetype.BulkExport:
                    weight *= 1f + GetAverageRequirementSurplus(definition.CompletionRequirements, 80);
                    if (_context.ActiveOffers.Count >= 2)
                    {
                        weight *= 0.65f;
                    }
                    break;
                case OfferArchetype.EmergencyRequest:
                    weight *= _context.ResourceInventoryService.GetAmount(ResourceInventoryService.GOLD_RESOURCE_ID) < 250
                        ? 1.75f
                        : 0.95f;
                    break;
                case OfferArchetype.ProgressiveContract:
                    weight *= 1f + (GetCustomerReputation(definition) / 200f);
                    break;
                case OfferArchetype.OpportunisticDeal:
                    weight *= 1.15f + GetAverageRequirementSurplus(definition.CompletionRequirements, 100);
                    if (_context.ActiveOffers.Count >= 2)
                    {
                        weight *= 1.35f;
                    }
                    break;
            }

            if (definition.Category != OfferCategory.Economic)
            {
                weight *= 0.9f;
            }

            if (!string.IsNullOrWhiteSpace(definition.OfferAffinityTag)
                && string.Equals(definition.OfferAffinityTag, definition.Customer.OfferAffinityTag, StringComparison.OrdinalIgnoreCase))
            {
                weight *= 1.2f;
            }

            int penaltyUntil = GetCustomerPenaltyUntilMinutes(definition.Customer);
            if (_context.GameTimeService.TotalGameMinutes < penaltyUntil)
            {
                weight *= 0.35f;
            }

            return Mathf.Max(0.05f, weight);
        }

        private float GetAverageRequirementSurplus(OfferResourceAmount[] requirements, int baselineAmount)
        {
            if (requirements == null || requirements.Length == 0 || _context.ResourceInventoryService == null)
            {
                return 0f;
            }

            float total = 0f;
            int count = 0;
            for (int i = 0; i < requirements.Length; i++)
            {
                OfferResourceAmount requirement = requirements[i];
                if (string.IsNullOrWhiteSpace(requirement.ResourceId))
                {
                    continue;
                }

                int amount = _context.ResourceInventoryService.GetAmount(requirement.ResourceId);
                float surplus = Mathf.Clamp01((amount - baselineAmount) / (float)Mathf.Max(1, baselineAmount));
                total += surplus;
                count++;
            }

            return count > 0 ? total / count : 0f;
        }

        private OfferDefinition SelectWeightedDefinition(List<OfferDefinition> eligible, List<float> weights)
        {
            if (eligible == null || eligible.Count == 0)
            {
                return null;
            }

            float totalWeight = 0f;
            for (int i = 0; i < weights.Count; i++)
            {
                totalWeight += Mathf.Max(0f, weights[i]);
            }

            if (totalWeight <= 0f)
            {
                return eligible[UnityEngine.Random.Range(0, eligible.Count)];
            }

            float roll = UnityEngine.Random.Range(0f, totalWeight);
            for (int i = 0; i < eligible.Count; i++)
            {
                roll -= Mathf.Max(0f, weights[i]);
                if (roll <= 0f)
                {
                    return eligible[i];
                }
            }

            return eligible[eligible.Count - 1];
        }

        private bool PassesChainProgress(OfferDefinition definition)
        {
            if (definition == null || definition.ChainStep <= 1 || string.IsNullOrWhiteSpace(definition.ChainId))
            {
                return true;
            }

            if (!_context.CompletedChainStepByChainId.TryGetValue(definition.ChainId, out int completedStep))
            {
                return false;
            }

            return completedStep >= definition.ChainStep - 1;
        }

        private bool ContainsExclusiveGroupInActiveOffers(string exclusiveGroupId)
        {
            if (string.IsNullOrWhiteSpace(exclusiveGroupId))
            {
                return false;
            }

            for (int i = 0; i < _context.ActiveOffers.Count; i++)
            {
                OfferRuntimeRecord record = _context.ActiveOffers[i];
                if (record?.Definition == null)
                {
                    continue;
                }

                if (string.Equals(record.Definition.ExclusiveGroupId, exclusiveGroupId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private int GetCustomerReputation(OfferDefinition definition)
        {
            if (definition == null || definition.Customer == null)
            {
                return 50;
            }

            if (_context.ReputationByCustomer.TryGetValue(definition.Customer, out int reputation))
            {
                return reputation;
            }

            return 50;
        }

        private int GetCustomerPenaltyUntilMinutes(OfferCustomerDefinition customer)
        {
            string customerKey = OfferReputationService.GetCustomerKey(customer);
            if (string.IsNullOrWhiteSpace(customerKey))
            {
                return 0;
            }

            if (_context.GenerationPenaltyUntilMinutesByCustomerKey.TryGetValue(customerKey, out int untilMinutes))
            {
                return untilMinutes;
            }

            return 0;
        }
    }
}
