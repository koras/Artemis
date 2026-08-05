using System;
using _Project.Scripts.Data.Grid;
using _Project.Scripts.Data.Localization;
using UnityEngine;
using UnityEngine.Localization;

namespace _Project.Scripts.Data.Construction
{
    /// <summary>
    /// Тип опоры, необходимой для постройки.
    /// </summary>
    public enum SupportRequirement
    {
        None = 0,
        GroundOrFloor = 1,
        Wall = 2,
        Ceiling = 3
    }

    /// <summary>
    /// Allowed vertical zone inside a 3-cell-high built life-module band.
    /// </summary>
    public enum LifeModulePlacementZone
    {
        Any = 0,
        TopTwo = 1,
        BottomTwo = 2,
        Top = 3,
        Middle = 4,
        Bottom = 5
    }

    /// <summary>
    /// Один ресурс в стоимости постройки.
    /// Сейчас можно держать пустым списком (0 ресурсов для теста).
    /// </summary>
    [Serializable]
    public struct BuildCostItem
    {
        public string ResourceId; // Например: "Titan", "Iron"
        public int Amount;        // Например: 2, 3
    }

    /// <summary>
    /// Дефиниция строящегося объекта (ScriptableObject).
    /// Здесь все игровые атрибуты объекта.
    /// </summary>
    /// <remarks>
    /// Generic custom Inspector: <c>LocalizationConfigEditor</c> in
    /// Assets/_Project/Scripts/Editor/LocalizationConfigEditors.cs.
    /// Localization dropdowns: <c>LocalizationConfigEditorUtility</c> in
    /// Assets/_Project/Scripts/Editor/LocalizationConfigEditorUtility.cs.
    /// </remarks>
    [LocalizationNamespace("building", nameof(BuildingDef.LocalizationId))]
    [CreateAssetMenu(menuName = "Artemis/Construction/Building Def", fileName = "BuildingDef")]
    public class BuildingDef : BaseLocalizedDefinitionConfig
    {
        private const string NameSuffix = "name";
        private const string DescriptionSuffix = "description";

        [Header("Identity")]
        public BuildObjectType ObjectType;

        [LocalizationKey("Name", NameSuffix)]
        [SerializeField, HideInInspector]
        private string _nameLocalizationKey = NameSuffix;

        [LocalizationKey("Description", DescriptionSuffix)]
        [SerializeField, HideInInspector]
        private string _descriptionLocalizationKey = DescriptionSuffix;

        public string LocalizationId => ObjectType.ToString().ToLowerInvariant();

        public string NameLocalizationKey => GetLocalizationKey(_nameLocalizationKey);

        public string DescriptionLocalizationKey => GetLocalizationKey(_descriptionLocalizationKey);

        public LocalizedString GetLocalizedName()
        {
            return new LocalizedString("UI", NameLocalizationKey);
        }

        public LocalizedString GetLocalizedDescription()
        {
            return new LocalizedString("UI", DescriptionLocalizationKey);
        }

        private string GetLocalizationKey(string suffix)
        {
            return $"building.{LocalizationId}.{suffix}";
        }

        [Header("Sprites")]
        public Sprite PreviewSprite; // Превью-спрайт для размещения объекта.
        public Sprite BuiltSprite;   // Финальный спрайт построенного объекта (prefab-based путь).

        [Header("Placement")]
        public int Width = 1;
        public int Height = 1;
        public bool CanRotate;
        public bool IsWalkableAfterBuild; 
        public SupportRequirement SupportRequirement;
        // Placement rule for life-module overlay cells under this building footprint.
        public bool CanBuildOnlyOnBuiltLifeModule;
        // Vertical placement rule used only when CanBuildOnlyOnBuiltLifeModule is enabled.
        public LifeModulePlacementZone AllowedLifeModulePlacementZone = LifeModulePlacementZone.Any;
        // Разрешённые типы базовой клетки под footprint при постановке.
        // Если список пуст, используется текущая legacy-логика CanBuildOnCell.
        public CellType[] AllowedPlacementCellTypes = Array.Empty<CellType>();
        // Для работы постройки: хотя бы одна клетка ниже footprint должна иметь
        // один из этих базовых типов.
        public CellType[] RequiredBelowAnyCellTypes = Array.Empty<CellType>();
        // Для работы постройки: хотя бы одна клетка ниже footprint должна иметь
        // один из этих построенных типов.
        public BuildObjectType[] RequiredBelowAnyBuildObjectTypes = Array.Empty<BuildObjectType>();
        [Header("Interaction")]
        // Relative target cell inside the building footprint used for resource delivery.
        public Vector2Int ResourceDeliveryTargetOffset;
        [Header("Construction")]
        public int BuildTicks = 3; // По задаче: 100 тиков по умолчанию.
        public BuildCostItem[] CostItems = Array.Empty<BuildCostItem>(); // Пока пусто для теста.

        [Header("Simulation")]
        public bool RequiresPower;
        // Ограничения на клетки под строительство (тип основания, допустимый грунт, наличие препятствий).
        public bool UsesPowerNetwork = true;
        public float PowerConsumptionKw;
        public float PowerGenerationKwDay;
        public float BatteryCapacityKwh;
        public float BatteryMaxChargeKw;
        public float BatteryMaxDischargeKw;
        public int PowerPriority;
        public Vector2Int PowerInputOffset;
        public float HeatProduction;
        public float WaterConsumption;


        [Header("Water Simulation")]
        // If false, this building is ignored by water network distribution.
        public bool UsesWaterNetwork;
        // Producer/Consumer role inside water simulation.
        public WaterRole WaterRole;
        // Water pipe port offset from anchor cell, similar to PowerInputOffset.
        public Vector2Int WaterPortOffset;
        // Initial producer switch state after building becomes active.
        public bool IsWaterProducerEnabledByDefault = true;
        // Soft production cap in liters per hour.
        public float WaterProductionLitersPerHour;
        // Rogalite amount consumed for one conversion cycle.
        public int WaterProductionRogalitePerCycle;
        // Water liters produced by one conversion cycle.
        public float WaterProductionLitersPerCycle;
        // Max tank fill speed in liters per hour for consumers.
        public float WaterConsumerFillRateLitersPerHour;
        // Consumer local tank capacity in liters.
        public float WaterConsumerCapacityLiters;
        // Reserved for future distribution policies.
        
        // Storage tank capacity in liters.
        public float WaterStorageCapacityLiters;
        // Max storage fill speed from network in liters per hour.
        public float WaterStorageFillRateLitersPerHour;
        // Max storage discharge speed to network in liters per hour.
        public float WaterStorageDischargeRateLitersPerHour;
public int WaterConsumerPriority;

        [Header("Oxygen Simulation")]
        // If false, this building is ignored by oxygen network distribution.
        public bool UsesOxygenNetwork;
        // Producer/Consumer role inside oxygen simulation.
        public OxygenRole OxygenRole;
        // Oxygen pipe port offset from anchor cell.
        public Vector2Int OxygenPortOffset;
        // Initial producer switch state after building becomes active.
        public bool IsOxygenProducerEnabledByDefault = true;
        // Soft production cap in liters per hour.
        public float OxygenProductionLitersPerHour;
        // Rogalite amount consumed for one conversion cycle.
        public int OxygenProductionRegolithPerCycle;
        // Oxygen liters produced by one conversion cycle.
        public float OxygenProductionLitersPerCycle;
        // Water liters consumed per hour by oxygen production process.
        public float OxygenWaterConsumptionLitersPerHour;
        // Max tank fill speed in liters per hour for consumers.
        public float OxygenConsumerFillRateLitersPerHour;
        // Consumer local tank capacity in liters.
        public float OxygenConsumerCapacityLiters;
        // Reserved for future distribution policies.
        public int OxygenConsumerPriority;
        // Storage tank capacity in liters.
        public float OxygenStorageCapacityLiters;
        // Max storage fill speed from network in liters per hour.
        public float OxygenStorageFillRateLitersPerHour;
        // Max storage discharge speed to network in liters per hour.
        public float OxygenStorageDischargeRateLitersPerHour;
    }
}
