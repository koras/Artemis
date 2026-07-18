using System;
using System.Collections.Generic;
using _Project.Scripts.Data.Offers;
using _Project.Scripts.Data.Shop;
using _Project.Scripts.Systems.Offers;
using _Project.Scripts.Systems.Resources;
using _Project.Scripts.Systems.External;
using _Project.Scripts.Systems.Simulation;
using UnityEngine;

namespace _Project.Scripts.Systems.Shop
{
    /// <summary>
    /// Runtime-сервис магазина: применяет условия definitions, устраняет коллизии и оформляет заказы.
    /// </summary>
    public sealed class ShopSystemService
    {
        private readonly List<ShopOfferDefinition> _catalog;
        private readonly ResourceInventoryService _resourceInventoryService;
        private readonly OfferSystemService _offerSystemService;
        private readonly GameTimeService _gameTimeService;
        private readonly IronRocketArrivalService _ironRocketArrivalService;
        private readonly List<ShopRuntimeEntry> _availableEntries = new List<ShopRuntimeEntry>();
        private readonly List<PendingShopOrder> _pendingOrders = new List<PendingShopOrder>();
        private readonly Dictionary<string, int> _pendingAmountByKey = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _selectedAmountByKey = new Dictionary<string, int>();

        private int _lastProcessedHour = -1;

        public event Action StateChanged;

        public ShopSystemService(
            IReadOnlyList<ShopOfferDefinition> catalog,
            ResourceInventoryService resourceInventoryService,
            OfferSystemService offerSystemService,
            GameTimeService gameTimeService,
            IronRocketArrivalService ironRocketArrivalService)
        {
            _resourceInventoryService = resourceInventoryService;
            _offerSystemService = offerSystemService;
            _gameTimeService = gameTimeService;
            _ironRocketArrivalService = ironRocketArrivalService;
            _catalog = new List<ShopOfferDefinition>();

            if (catalog == null)
            {
                return;
            }

            for (int i = 0; i < catalog.Count; i++)
            {
                ShopOfferDefinition definition = catalog[i];
                if (definition != null)
                {
                    _catalog.Add(definition);
                }
            }
        }

        public IReadOnlyList<ShopRuntimeEntry> AvailableEntries => _availableEntries;
        public IReadOnlyList<PendingShopOrder> PendingOrders => _pendingOrders;
        public int Gold => _resourceInventoryService != null
            ? _resourceInventoryService.GetAmount(ResourceInventoryService.GOLD_RESOURCE_ID)
            : 0;

        public void Tick()
        {
            if (_gameTimeService.Hour == _lastProcessedHour)
            {
                return;
            }

            _lastProcessedHour = _gameTimeService.Hour;
            RebuildAvailability();
        }

        public int GetSelectedAmount(string entryKey)
        {
            if (string.IsNullOrWhiteSpace(entryKey))
            {
                return 0;
            }

            return _selectedAmountByKey.TryGetValue(entryKey, out int selectedAmount) ? selectedAmount : 0;
        }

        public int GetPendingAmount(string entryKey)
        {
            if (string.IsNullOrWhiteSpace(entryKey))
            {
                return 0;
            }

            return _pendingAmountByKey.TryGetValue(entryKey, out int pendingAmount) ? pendingAmount : 0;
        }

        public void ChangeSelectedAmount(string entryKey, int delta)
        {
            int current = GetSelectedAmount(entryKey);
            int maxSelectableAmount = GetMaxSelectableAmount(entryKey);
            // Selection is capped by the supplier limit minus already pending orders for the same offer.
            _selectedAmountByKey[entryKey] = Mathf.Clamp(current + delta, 0, maxSelectableAmount);
            StateChanged?.Invoke();
        }

        public int GetRemainingOrderCapacity(string entryKey)
        {
            if (!TryFindEntry(entryKey, out ShopRuntimeEntry entry))
            {
                return 0;
            }

            int pendingAmount = GetPendingAmount(entryKey);
            return Mathf.Max(0, entry.MaxPurchaseAmount - pendingAmount);
        }

        public bool PlaceOrder(string entryKey)
        {
            if (!TryFindEntry(entryKey, out ShopRuntimeEntry entry))
            {
                return false;
            }

            int amount = GetSelectedAmount(entryKey);
            if (amount <= 0)
            {
                return false;
            }

            if (amount > GetRemainingOrderCapacity(entryKey))
            {
                return false;
            }

            int totalPrice = amount * entry.UnitPrice;
            if (!_resourceInventoryService.Has(ResourceInventoryService.GOLD_RESOURCE_ID, totalPrice)
                || !_resourceInventoryService.TryRemove(ResourceInventoryService.GOLD_RESOURCE_ID, totalPrice))
            {
                return false;
            }

            // Store each purchase as its own pending order so UI can cancel it precisely.
            _pendingOrders.Add(new PendingShopOrder(
                Guid.NewGuid().ToString("N"),
                entryKey,
                entry.Product,
                entry.Product.ResourceId,
                amount,
                entry.UnitPrice,
                totalPrice,
                _gameTimeService.Sol,
                GetNextMissionTarget()));
            AddPendingAmount(entryKey, amount);
            // Reset selection after purchase so the player must make a new choice before ordering again.
            _selectedAmountByKey[entryKey] = 0;
            StateChanged?.Invoke();
            return true;
        }

        public bool CancelOrder(string orderId)
        {
            if (string.IsNullOrWhiteSpace(orderId))
            {
                return false;
            }

            for (int i = 0; i < _pendingOrders.Count; i++)
            {
                PendingShopOrder order = _pendingOrders[i];
                if (!string.Equals(order.OrderId, orderId, StringComparison.Ordinal))
                {
                    continue;
                }

                _pendingOrders.RemoveAt(i);
                _resourceInventoryService.Add(ResourceInventoryService.GOLD_RESOURCE_ID, order.TotalPrice);
                RemovePendingAmount(order.EntryKey, order.Amount);
                StateChanged?.Invoke();
                return true;
            }

            return false;
        }

        public int GetRemainingDeliveryDays(PendingShopOrder order)
        {
            if (_ironRocketArrivalService == null)
            {
                return 0;
            }

            int remainingMissions = Mathf.Max(0, order.TargetScheduledMissionIndex - _ironRocketArrivalService.ScheduledMissionIndex);
            if (remainingMissions == 0)
            {
                return 0;
            }

            int remainingHours = _ironRocketArrivalService.RemainingHoursToNextArrival;
            if (remainingMissions > 1)
            {
                remainingHours += (remainingMissions - 1) * _ironRocketArrivalService.CadenceHours;
            }

            return Mathf.Max(0, Mathf.CeilToInt(remainingHours / 24f));
        }

        public void OnRocketMissionResolved(IronRocketArrivalService.RocketMissionResult missionResult)
        {
            if (_pendingOrders.Count == 0)
            {
                return;
            }

            bool changed = false;
            for (int i = _pendingOrders.Count - 1; i >= 0; i--)
            {
                PendingShopOrder order = _pendingOrders[i];
                if (missionResult.ScheduledMissionIndex < order.TargetScheduledMissionIndex)
                {
                    continue;
                }

                if (missionResult.IsSuccess)
                {
                    // Delivery should use the product's current inventory mapping so fixed shop assets
                    // immediately affect already pending orders too.
                    string deliveredResourceId = order.Product != null
                        ? order.Product.ResourceId
                        : order.ResourceId;
                    _resourceInventoryService.Add(deliveredResourceId, order.Amount);
                }

                RemovePendingAmount(order.EntryKey, order.Amount);
                _pendingOrders.RemoveAt(i);
                changed = true;
            }

            if (changed)
            {
                StateChanged?.Invoke();
            }
        }

        private bool TryFindEntry(string entryKey, out ShopRuntimeEntry entry)
        {
            for (int i = 0; i < _availableEntries.Count; i++)
            {
                if (_availableEntries[i].EntryKey == entryKey)
                {
                    entry = _availableEntries[i];
                    return true;
                }
            }

            entry = default;
            return false;
        }

        private int GetNextMissionTarget()
        {
            return _ironRocketArrivalService != null ? _ironRocketArrivalService.ScheduledMissionIndex + 1 : 1;
        }

        private int GetMaxSelectableAmount(string entryKey)
        {
            return Mathf.Max(0, GetRemainingOrderCapacity(entryKey));
        }

        private void AddPendingAmount(string entryKey, int amount)
        {
            _pendingAmountByKey.TryGetValue(entryKey, out int currentPendingAmount);
            _pendingAmountByKey[entryKey] = currentPendingAmount + Mathf.Max(0, amount);
        }

        private void RemovePendingAmount(string entryKey, int amount)
        {
            if (!_pendingAmountByKey.TryGetValue(entryKey, out int currentPendingAmount))
            {
                return;
            }

            int nextAmount = Mathf.Max(0, currentPendingAmount - Mathf.Max(0, amount));
            if (nextAmount == 0)
            {
                _pendingAmountByKey.Remove(entryKey);
            }
            else
            {
                _pendingAmountByKey[entryKey] = nextAmount;
            }
        }

        private void RebuildAvailability()
        {
            _availableEntries.Clear();
            var winnerByCollisionKey = new Dictionary<string, ShopRuntimeEntry>();

            for (int i = 0; i < _catalog.Count; i++)
            {
                ShopOfferDefinition definition = _catalog[i];
                if (!IsDefinitionEligible(definition))
                {
                    continue;
                }

                ShopRuntimeEntry entry = BuildRuntimeEntry(definition);
                string collisionKey = BuildCollisionKey(definition, _gameTimeService.Sol);

                if (winnerByCollisionKey.TryGetValue(collisionKey, out ShopRuntimeEntry currentWinner))
                {
                    if (!ShouldReplaceWinner(entry, currentWinner))
                    {
                        continue;
                    }
                }

                winnerByCollisionKey[collisionKey] = entry;
            }

            foreach (ShopRuntimeEntry entry in winnerByCollisionKey.Values)
            {
                _availableEntries.Add(entry);
            }

            StateChanged?.Invoke();
        }

        private bool IsDefinitionEligible(ShopOfferDefinition definition)
        {
            if (definition == null || definition.Product == null || definition.Supplier == null)
            {
                return false;
            }

            if (definition.BaseUnitPrice < 1)
            {
                return false;
            }

            ShopDefinitionConditionSet conditions = definition.Conditions;
            if (conditions == null)
            {
                return true;
            }

            if (_gameTimeService.TotalGameMinutes < Mathf.Max(0, conditions.MinGameMinutesToAppear))
            {
                return false;
            }

            float chance = Mathf.Clamp01(conditions.HourlySpawnChance);
            if (UnityEngine.Random.value > chance)
            {
                return false;
            }

            if (!PassesPeriodicWindow(conditions.PeriodicWindow, _gameTimeService.Sol))
            {
                return false;
            }

            if (!PassesReputationCondition(conditions.SupplierReputation, definition.Supplier))
            {
                return false;
            }

            if (!PassesResourceAmountCondition(conditions.ResourceAmount))
            {
                return false;
            }

            return true;
        }

        private bool PassesPeriodicWindow(ShopPeriodicWindowCondition condition, int currentSol)
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

        private bool PassesReputationCondition(ShopSupplierReputationCondition condition, OfferCustomerDefinition supplier)
        {
            if (condition == null || !condition.Enabled)
            {
                return true;
            }

            int reputation = _offerSystemService.GetCustomerReputation(supplier);
            return Compare(reputation, condition.Comparison, condition.Threshold);
        }

        private bool PassesResourceAmountCondition(ShopResourceAmountCondition condition)
        {
            if (condition == null || !condition.Enabled)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(condition.ResourceId))
            {
                return false;
            }

            int amount = _resourceInventoryService.GetAmount(condition.ResourceId);
            return Compare(amount, condition.Comparison, condition.Threshold);
        }

        private static bool Compare(int value, ShopThresholdComparison comparison, int threshold)
        {
            switch (comparison)
            {
                case ShopThresholdComparison.LessOrEqual:
                    return value <= threshold;
                case ShopThresholdComparison.GreaterOrEqual:
                    return value >= threshold;
                case ShopThresholdComparison.LessThan:
                    return value < threshold;
                case ShopThresholdComparison.GreaterThan:
                    return value > threshold;
                default:
                    return false;
            }
        }

        private ShopRuntimeEntry BuildRuntimeEntry(ShopOfferDefinition definition)
        {
            int reputation = _offerSystemService.GetCustomerReputation(definition.Supplier);
            int unitPrice = CalculateUnitPrice(definition, reputation);
            string entryKey = BuildEntryKey(definition, _gameTimeService.Sol);
            int maxPurchaseAmount = Mathf.Max(1, definition.MaxPurchaseAmount);
            return new ShopRuntimeEntry(entryKey, definition, definition.Product, definition.Supplier, reputation, unitPrice, maxPurchaseAmount);
        }

        private int CalculateUnitPrice(ShopOfferDefinition definition, int reputation)
        {
            // Цена зависит от репутации поставщика и базовой цены рыночной позиции.
            float reputationFactor = Mathf.Lerp(1.5f, 0.8f, Mathf.Clamp01(reputation / 100f));
            float multiplier = definition.UsePriceMultiplierOverride ? Mathf.Max(0.1f, definition.PriceMultiplierOverride) : 1f;
            float finalPrice = definition.BaseUnitPrice * multiplier * reputationFactor;
            return Mathf.Max(1, Mathf.RoundToInt(finalPrice));
        }

        private static bool ShouldReplaceWinner(ShopRuntimeEntry candidate, ShopRuntimeEntry winner)
        {
            if (candidate.Definition.Priority != winner.Definition.Priority)
            {
                return candidate.Definition.Priority > winner.Definition.Priority;
            }

            return string.CompareOrdinal(candidate.Definition.DefinitionId, winner.Definition.DefinitionId) < 0;
        }

        private static string BuildCollisionKey(ShopOfferDefinition definition, int currentSol)
        {
            string supplierId = definition.Supplier != null ? definition.Supplier.name : "missing-supplier";
            return $"{definition.Product.ProductId}_{supplierId}_{currentSol}";
        }

        private static string BuildEntryKey(ShopOfferDefinition definition, int currentSol)
        {
            return $"{definition.DefinitionId}_{currentSol}";
        }

        public readonly struct ShopRuntimeEntry
        {
            public readonly string EntryKey;
            public readonly ShopOfferDefinition Definition;
            public readonly ShopProductDefinition Product;
            public readonly OfferCustomerDefinition Supplier;
            public readonly int SupplierReputation;
            public readonly int UnitPrice;
            public readonly int MaxPurchaseAmount;

            public ShopRuntimeEntry(
                string entryKey,
                ShopOfferDefinition definition,
                ShopProductDefinition product,
                OfferCustomerDefinition supplier,
                int supplierReputation,
                int unitPrice,
                int maxPurchaseAmount)
            {
                EntryKey = entryKey;
                Definition = definition;
                Product = product;
                Supplier = supplier;
                SupplierReputation = supplierReputation;
                UnitPrice = unitPrice;
                MaxPurchaseAmount = maxPurchaseAmount;
            }
        }

        public readonly struct PendingShopOrder
        {
            public readonly string OrderId;
            public readonly string EntryKey;
            public readonly ShopProductDefinition Product;
            public readonly string ResourceId;
            public readonly int Amount;
            public readonly int UnitPrice;
            public readonly int TotalPrice;
            public readonly int CreatedAtSol;
            public readonly int TargetScheduledMissionIndex;

            public PendingShopOrder(
                string orderId,
                string entryKey,
                ShopProductDefinition product,
                string resourceId,
                int amount,
                int unitPrice,
                int totalPrice,
                int createdAtSol,
                int targetScheduledMissionIndex)
            {
                OrderId = orderId;
                EntryKey = entryKey;
                Product = product;
                ResourceId = resourceId;
                Amount = amount;
                UnitPrice = unitPrice;
                TotalPrice = totalPrice;
                CreatedAtSol = createdAtSol;
                TargetScheduledMissionIndex = targetScheduledMissionIndex;
            }
        }
    }
}
