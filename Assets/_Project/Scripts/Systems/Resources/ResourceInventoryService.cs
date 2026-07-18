using System;
using System.Collections.Generic;

namespace _Project.Scripts.Systems.Resources
{
    /// <summary>
    /// Event payload for a single resource amount change.
    /// </summary>
    public readonly struct ResourceAmountChangedEvent
    {
        public ResourceAmountChangedEvent(string resourceId, int delta, int totalAmount)
        {
            ResourceId = resourceId;
            Delta = delta;
            TotalAmount = totalAmount;
        }

        public string ResourceId { get; }
        public int Delta { get; }
        public int TotalAmount { get; }
    }

    /// <summary>
    /// Runtime base resource inventory (resourceId -> amount).
    /// </summary>
    public sealed class ResourceInventoryService
    {
        public const string GOLD_RESOURCE_ID = "Gold";

        private const int DEFAULT_STARTING_RESOURCE_AMOUNT = 100;
        private const int DEFAULT_STARTING_WATER_PIPE_AMOUNT = 150;
        private const int DEFAULT_STARTING_OXYGEN_PIPE_AMOUNT = 150;
        private const int DEFAULT_STARTING_RESOURCE_LADDER = 150;
        private const int DEFAULT_STARTING_RESOURCE_FOOD = 500;
        private const int DEFAULT_STARTING_RESOURCE_BEER = 50;
        private const int DEFAULT_STARTING_GOLD_AMOUNT = 10000;

        private readonly Dictionary<string, int> _amountByResourceId = new Dictionary<string, int>();

        // Fired on any inventory change (add/remove), useful for full HUD refresh.
        public event Action InventoryChanged;
        // Fired for a specific resource change with delta and resulting total.
        public event Action<ResourceAmountChangedEvent> ResourceAmountChanged;

        public void Add(string resourceId, int amount)
        {
            if (string.IsNullOrWhiteSpace(resourceId)) return;
            if (amount <= 0) return;

            _amountByResourceId.TryGetValue(resourceId, out int currentAmount);
            int nextAmount = currentAmount + amount;
            _amountByResourceId[resourceId] = nextAmount;
            InventoryChanged?.Invoke();
            ResourceAmountChanged?.Invoke(new ResourceAmountChangedEvent(resourceId, amount, nextAmount));
        }

        /// <summary>
        /// Grants default starting resources for a new gameplay session.
        /// </summary>
        public void AddDefaultStartingResources()
        {
            Add(GOLD_RESOURCE_ID, DEFAULT_STARTING_GOLD_AMOUNT);
            Add("Cable", DEFAULT_STARTING_RESOURCE_AMOUNT);
            Add("Water Pipe", DEFAULT_STARTING_WATER_PIPE_AMOUNT);
            Add("Oxygen Pipe", DEFAULT_STARTING_OXYGEN_PIPE_AMOUNT);
            Add("Iron", DEFAULT_STARTING_RESOURCE_AMOUNT);
            Add("Titan", DEFAULT_STARTING_RESOURCE_AMOUNT);
            Add("aluminium", DEFAULT_STARTING_RESOURCE_AMOUNT);
            Add("Rogalite", DEFAULT_STARTING_RESOURCE_AMOUNT);
            Add("Ladder", DEFAULT_STARTING_RESOURCE_LADDER); 
            Add("Tree", DEFAULT_STARTING_RESOURCE_LADDER); 
            Add("Fish", DEFAULT_STARTING_RESOURCE_FOOD); 
            Add("Beer", DEFAULT_STARTING_RESOURCE_BEER); 
            Add("Meet", DEFAULT_STARTING_RESOURCE_FOOD);
            Add("Vegetables", DEFAULT_STARTING_RESOURCE_FOOD); 
            Add("Medicines", DEFAULT_STARTING_RESOURCE_LADDER); 
        }

        public bool Has(string resourceId, int amount)
        {
            if (string.IsNullOrWhiteSpace(resourceId)) return false;
            if (amount <= 0) return true;

            return GetAmount(resourceId) >= amount;
        }

        public bool TryRemove(string resourceId, int amount)
        {
            if (!Has(resourceId, amount)) return false;
            if (amount <= 0) return true;

            int nextAmount = _amountByResourceId[resourceId] - amount;
            _amountByResourceId[resourceId] = nextAmount;
            InventoryChanged?.Invoke();
            ResourceAmountChanged?.Invoke(new ResourceAmountChangedEvent(resourceId, -amount, nextAmount));
            return true;
        }

        public void SetAmount(string resourceId, int amount)
        {
            if (string.IsNullOrWhiteSpace(resourceId)) return;

            int nextAmount = Math.Max(0, amount);
            _amountByResourceId.TryGetValue(resourceId, out int currentAmount);
            if (currentAmount == nextAmount) return;

            _amountByResourceId[resourceId] = nextAmount;
            InventoryChanged?.Invoke();
            ResourceAmountChanged?.Invoke(new ResourceAmountChangedEvent(resourceId, nextAmount - currentAmount, nextAmount));
        }

        public int GetAmount(string resourceId)
        {
            if (string.IsNullOrWhiteSpace(resourceId)) return 0;

            _amountByResourceId.TryGetValue(resourceId, out int amount);
            return amount;
        }

        /// <summary>
        /// Returns a copy to prevent external mutation of internal state.
        /// </summary>
        public Dictionary<string, int> GetAmountsSnapshot()
        {
            return new Dictionary<string, int>(_amountByResourceId);
        }
    }
}
