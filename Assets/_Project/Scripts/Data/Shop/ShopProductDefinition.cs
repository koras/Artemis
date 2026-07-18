using _Project.Scripts.Systems.Resources;
using UnityEngine;

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
    [CreateAssetMenu(menuName = "Artemis/Shop/Product Definition", fileName = "ShopProductDefinition")]
    public sealed class ShopProductDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string ProductId = "product-id";
        public string ProductName;
        [TextArea] public string Description;
        public Sprite ProductSprite;
        [Tooltip("Категория для фильтрации в магазине.")]
        public ShopProductCategory Category = ShopProductCategory.Food;

        [Header("Inventory Mapping")]
        [Tooltip("Тип ресурса, который попадет в склад при доставке.")]
        public SceneResourceType ResourceType = SceneResourceType.Iron;

        public string ResourceId => ResourceType.GetResourceId();
    }
}
