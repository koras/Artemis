using _Project.Scripts.Data.Offers;
using UnityEngine;

namespace _Project.Scripts.Data.Shop
{
    public enum ShopThresholdComparison
    {
        LessOrEqual = 0,
        GreaterOrEqual = 1,
        LessThan = 2,
        GreaterThan = 3
    }

    [System.Serializable]
    public sealed class ShopPeriodicWindowCondition
    {
        public bool Enabled;
        [Min(1)] public int IntervalDays = 8;
        [Min(1)] public int LifetimeDays = 2;
        [Min(1)] public int StartSol = 1;
    }

    [System.Serializable]
    public sealed class ShopSupplierReputationCondition
    {
        public bool Enabled;
        public ShopThresholdComparison Comparison = ShopThresholdComparison.LessOrEqual;
        [Range(0, 100)] public int Threshold = 30;
    }

    [System.Serializable]
    public sealed class ShopResourceAmountCondition
    {
        public bool Enabled;
        public string ResourceId = "Iron";
        public ShopThresholdComparison Comparison = ShopThresholdComparison.LessThan;
        [Min(0)] public int Threshold = 10;
    }

    [System.Serializable]
    public sealed class ShopDefinitionConditionSet
    {
        [Header("Base")]
        [Min(0)] public int MinGameMinutesToAppear;
        [Range(0f, 1f)] public float HourlySpawnChance = 1f;

        [Header("Optional Conditions")]
        public ShopPeriodicWindowCondition PeriodicWindow = new ShopPeriodicWindowCondition();
        public ShopSupplierReputationCondition SupplierReputation = new ShopSupplierReputationCondition();
        public ShopResourceAmountCondition ResourceAmount = new ShopResourceAmountCondition();
    }

    /// <summary>
    /// Правила публикации товара поставщика в магазине.
    /// </summary>
    [CreateAssetMenu(menuName = "Artemis/Shop/Shop Offer Definition", fileName = "ShopOfferDefinition")]
    public sealed class ShopOfferDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string DefinitionId = "shop-offer-definition-id";
        public string OfferId = "shop-offer-id";
        [Min(0)] public int Priority;

        [Header("Links")]
        public ShopProductDefinition Product;
        public OfferCustomerDefinition Supplier;

        [Header("Pricing")]
        [Min(1)] public int BaseUnitPrice = 5;
        [Min(1)] public int MaxPurchaseAmount = 10;
        [Min(0.1f)] public float PriceMultiplierOverride = 1f;
        public bool UsePriceMultiplierOverride;

        [Header("Conditions")]
        public ShopDefinitionConditionSet Conditions = new ShopDefinitionConditionSet();
    }
}
