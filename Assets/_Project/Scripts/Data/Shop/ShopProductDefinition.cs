using _Project.Scripts.Data.Localization;
using _Project.Scripts.Systems.Resources;
using UnityEngine;
using UnityEngine.Localization;

namespace _Project.Scripts.Data.Shop
{
    public enum ShopProductCategory
    {
        Food = 0,
        Equipment = 1,
        Personnel = 2
    }

    /// <summary>
    /// Базовое определение товара магазина без привязки к поставщику и правилам появления.
    /// </summary>
    /// <remarks>
    /// Generic custom Inspector: <c>LocalizationConfigEditor</c> in
    /// Assets/_Project/Scripts/Editor/LocalizationConfigEditors.cs.
    /// Localization dropdowns: <c>LocalizationConfigEditorUtility</c> in
    /// Assets/_Project/Scripts/Editor/LocalizationConfigEditorUtility.cs.
    /// </remarks>
    [LocalizationNamespace("shop.product", nameof(ShopProductDefinition.ProductId))]
    [CreateAssetMenu(menuName = "Artemis/Shop/Product Definition", fileName = "ShopProductDefinition")]
    public sealed class ShopProductDefinition : BaseLocalizedConfigDefinition
    {
        private const string NameSuffix = "name";
        private const string DescriptionSuffix = "description";

        [Header("Identity")]
        [LocalizationId("Product Id")]
        [HideInInspector]
        public string ProductId = "product-id";

        [LocalizationKey("Name", NameSuffix)]
        [SerializeField, HideInInspector]
        private string _nameLocalizationKey = NameSuffix;

        [LocalizationKey("Description", DescriptionSuffix)]
        [SerializeField, HideInInspector]
        private string _descriptionLocalizationKey = DescriptionSuffix;

        public Sprite ProductSprite;

        [Tooltip("Категория для фильтрации в магазине.")]
        public ShopProductCategory Category = ShopProductCategory.Food;

        [Header("Inventory Mapping")]
        [Tooltip("Тип ресурса, который попадет в склад при доставке.")]
        public SceneResourceType ResourceType = SceneResourceType.Iron;

        public string ResourceId => ResourceType.GetResourceId();

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
            return $"shop.product.{ProductId}.{suffix}";
        }
    }
}