using System;
using System.Collections.Generic;

namespace _Project.Scripts.Systems.Resources
{
    /// <summary>
    /// Builds stable localization keys for inventory resource names.
    /// </summary>
    public static class ResourceLocalizationKeys
    {
        public const string InventoryTitle = "resource.inventory.title";
        public const string DroppedPrefabsId = "Dropped Prefabs";

        public static string Name(string resourceId)
        {
            return resourceId == DroppedPrefabsId
                ? "resource.dropped_prefabs"
                : $"resource.item.{Normalize(resourceId)}";
        }

        public static IEnumerable<string> GetKnownResourceIds()
        {
            foreach (SceneResourceType resourceType in Enum.GetValues(typeof(SceneResourceType)))
            {
                yield return resourceType.GetResourceId();
            }

            yield return DroppedPrefabsId;
        }

        private static string Normalize(string resourceId)
        {
            return resourceId.Trim().ToLowerInvariant().Replace(" ", "_");
        }
    }
}