using System.Collections.Generic;
using _Project.Scripts.Data.Offers;
using UnityEditor;
using UnityEngine;

namespace _Project.Scripts.Editor
{
    public static class OfferCatalogValidator
    {
        [MenuItem("Artemis/Validation/Validate Offer Catalog")]
        public static void Validate()
        {
            string[] guids = AssetDatabase.FindAssets("t:OfferDefinition");
            var usedIds = new HashSet<string>();
            int errorCount = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                OfferDefinition definition = AssetDatabase.LoadAssetAtPath<OfferDefinition>(path);
                if (definition == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(definition.OfferId))
                {
                    Debug.LogError($"[OfferCatalogValidator] Empty OfferId: {path}");
                    errorCount++;
                }
                else if (!usedIds.Add(definition.OfferId))
                {
                    Debug.LogError($"[OfferCatalogValidator] Duplicate OfferId '{definition.OfferId}': {path}");
                    errorCount++;
                }

                if (definition.Customer == null)
                {
                    Debug.LogError($"[OfferCatalogValidator] Missing Customer: {path}");
                    errorCount++;
                }

                bool hasResourceEvent = (definition.TriggerTypes & OfferTriggerType.ResourceEvent) == OfferTriggerType.ResourceEvent;
                if (hasResourceEvent && (definition.ResourceEventConditions == null || definition.ResourceEventConditions.Length == 0))
                {
                    Debug.LogError($"[OfferCatalogValidator] ResourceEvent trigger has no conditions: {path}");
                    errorCount++;
                }

                if (!definition.IsRepeatable && definition.CooldownGameMinutes > 0)
                {
                    Debug.LogWarning($"[OfferCatalogValidator] Cooldown is ignored for non-repeatable offer: {path}");
                }

                if (definition.UseDeadline && definition.DeadlineDays <= 0)
                {
                    Debug.LogError($"[OfferCatalogValidator] DeadlineDays must be > 0: {path}");
                    errorCount++;
                }
            }

            if (errorCount > 0)
            {
                Debug.LogError($"[OfferCatalogValidator] Validation failed. Errors: {errorCount}");
                return;
            }

            Debug.Log($"[OfferCatalogValidator] Validation passed. Checked offers: {guids.Length}");
        }
    }
}
