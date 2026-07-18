using System;
using System.Collections.Generic;
using _Project.Scripts.Data.ColonyEvents;
using _Project.Scripts.Systems.Resources;
using _Project.Scripts.Systems.Simulation;
using UnityEngine;
using _Project.Scripts.Systems.Power;

namespace _Project.Scripts.Systems.ColonyEvents
{
    /// <summary>
    /// Chooses one eligible colony event per Sol and applies its temporary effect.
    /// </summary>
    public sealed class ColonyEventService
    {
        private readonly List<ColonyEventDefinition> _catalog = new List<ColonyEventDefinition>();
        private readonly GameTimeService _gameTimeService;
        private readonly ResourceInventoryService _resourceInventoryService;
        private readonly SolarPowerProductionService _solarPowerProductionService;
        private readonly List<ColonyEventDefinition> _eligibleBuffer = new List<ColonyEventDefinition>();

        private int _lastProcessedSol;

        public ColonyEventService(
            IReadOnlyList<ColonyEventDefinition> catalog,
            GameTimeService gameTimeService,
            ResourceInventoryService resourceInventoryService,
            SolarPowerProductionService solarPowerProductionService)
        {
            _gameTimeService = gameTimeService;
            _resourceInventoryService = resourceInventoryService;
            _solarPowerProductionService = solarPowerProductionService;

            if (catalog == null)
            {
                return;
            }

            for (int i = 0; i < catalog.Count; i++)
            {
                ColonyEventDefinition definition = catalog[i];
                if (definition != null)
                {
                    _catalog.Add(definition);
                }
            }
        }

        public event Action<ColonyEventDefinition> CurrentEventChanged;

        public ColonyEventDefinition CurrentEvent { get; private set; }

        public void Tick()
        {
            if (_gameTimeService == null)
            {
                return;
            }

            int currentSol = _gameTimeService.Sol;
            if (currentSol == _lastProcessedSol)
            {
                return;
            }

            _lastProcessedSol = currentSol;
            SelectEventForCurrentSol();
        }

        private void SelectEventForCurrentSol()
        {
            ClearActiveEffect();
            _eligibleBuffer.Clear();

            for (int i = 0; i < _catalog.Count; i++)
            {
                ColonyEventDefinition definition = _catalog[i];
                if (IsDefinitionEligible(definition))
                {
                    _eligibleBuffer.Add(definition);
                }
            }

            CurrentEvent = _eligibleBuffer.Count > 0
                ? _eligibleBuffer[UnityEngine.Random.Range(0, _eligibleBuffer.Count)]
                : null;

            ApplyActiveEffect();
            CurrentEventChanged?.Invoke(CurrentEvent);
        }

        private bool IsDefinitionEligible(ColonyEventDefinition definition)
        {
            if (definition == null
                || string.IsNullOrWhiteSpace(definition.EventId)
                || string.IsNullOrWhiteSpace(definition.Title))
            {
                return false;
            }

            ColonyEventConditionSet conditions = definition.Conditions;
            if (conditions == null)
            {
                return true;
            }

            if (_gameTimeService.TotalGameMinutes < Mathf.Max(0, conditions.MinGameMinutesToAppear))
            {
                return false;
            }

            if (!PassesPeriodicWindow(conditions.PeriodicWindow, _gameTimeService.Sol))
            {
                return false;
            }

            if (!PassesResourceAmountCondition(conditions.ResourceAmount))
            {
                return false;
            }

            return true;
        }

        private void ApplyActiveEffect()
        {
            if (CurrentEvent == null)
            {
                return;
            }

            switch (CurrentEvent.EffectType)
            {
                case ColonyEventEffectType.SolarGenerationMultiplier:
                    _solarPowerProductionService?.SetGlobalGenerationMultiplier(Mathf.Max(0f, CurrentEvent.EffectMagnitude));
                    break;
                case ColonyEventEffectType.RadiationRiskPlaceholder:
                case ColonyEventEffectType.MoonquakeRiskPlaceholder:
                case ColonyEventEffectType.None:
                default:
                    break;
            }
        }

        private void ClearActiveEffect()
        {
            _solarPowerProductionService?.SetGlobalGenerationMultiplier(1f);
        }

        private bool PassesPeriodicWindow(ColonyEventPeriodicWindowCondition condition, int currentSol)
        {
            if (condition == null || !condition.Enabled)
            {
                return true;
            }

            int interval = Mathf.Max(1, condition.IntervalDays);
            int lifetime = Mathf.Max(1, condition.LifetimeDays);
            int startSol = Mathf.Max(1, condition.StartSol);

            if (currentSol < startSol)
            {
                return false;
            }

            int offset = currentSol - startSol;
            int cycleOffset = offset % interval;
            return cycleOffset < lifetime;
        }

        private bool PassesResourceAmountCondition(ColonyEventResourceAmountCondition condition)
        {
            if (condition == null || !condition.Enabled)
            {
                return true;
            }

            if (_resourceInventoryService == null || string.IsNullOrWhiteSpace(condition.ResourceId))
            {
                return false;
            }

            int amount = _resourceInventoryService.GetAmount(condition.ResourceId);
            return Compare(amount, condition.Comparison, condition.Threshold);
        }

        private static bool Compare(int value, ColonyEventThresholdComparison comparison, int threshold)
        {
            switch (comparison)
            {
                case ColonyEventThresholdComparison.LessOrEqual:
                    return value <= threshold;
                case ColonyEventThresholdComparison.GreaterOrEqual:
                    return value >= threshold;
                case ColonyEventThresholdComparison.LessThan:
                    return value < threshold;
                case ColonyEventThresholdComparison.GreaterThan:
                    return value > threshold;
                default:
                    return false;
            }
        }
    }
}
