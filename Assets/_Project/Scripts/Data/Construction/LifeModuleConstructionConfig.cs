using System;
using _Project.Scripts.Systems.Resources;
using UnityEngine;

namespace _Project.Scripts.Data.Construction
{
    /// <summary>
    /// Defines per-cell resource cost for life-module construction.
    /// </summary>
    [CreateAssetMenu(menuName = "Artemis/Construction/Life Module Construction Config", fileName = "LifeModuleConstructionConfig")]
    public sealed class LifeModuleConstructionConfig : ScriptableObject
    {
        [Serializable]
        public struct PerCellCostItem
        {
            [Tooltip("Resource picked from the shared scene resource list.")]
            public SceneResourceType ResourceType;

            public string ResourceId => ResourceType.GetResourceId();
            public int AmountPerCell;
        }

        [SerializeField] private PerCellCostItem[] _costPerCellItems = Array.Empty<PerCellCostItem>();

        public PerCellCostItem[] CostPerCellItems => _costPerCellItems;
    }
}
