using System.Collections.Generic;
using _Project.Scripts.Data.ColonyEvents;
using UnityEngine;

namespace _Project.Scripts.Bootstrap.Runtime
{
    internal static class ColonyEventCatalogResolver
    {
        private const string RESOURCE_PATH = "ColonyEvents";

        public static List<ColonyEventDefinition> Resolve(List<ColonyEventDefinition> sceneCatalog)
        {
            if (sceneCatalog != null && sceneCatalog.Count > 0)
            {
                return sceneCatalog;
            }

            // Fallback keeps daily events working in scenes that were not rewired in the Inspector yet.
            ColonyEventDefinition[] fromResources = Resources.LoadAll<ColonyEventDefinition>(RESOURCE_PATH);
            if (fromResources == null || fromResources.Length == 0)
            {
                return new List<ColonyEventDefinition>();
            }

            var result = new List<ColonyEventDefinition>(fromResources.Length);
            for (int i = 0; i < fromResources.Length; i++)
            {
                if (fromResources[i] != null)
                {
                    result.Add(fromResources[i]);
                }
            }

            return result;
        }
    }
}
