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

                if (definition.Category != OfferCategory.Economic)
                {
                    if (definition.Stages == null || definition.Stages.Length == 0)
                    {
                        Debug.LogError($"[OfferCatalogValidator] Non-economic offer requires stages: {path}");
                        errorCount++;
                    }
                }

                if (definition.Stages != null)
                {
                    for (int stageIndex = 0; stageIndex < definition.Stages.Length; stageIndex++)
                    {
                        OfferStageDefinition stage = definition.Stages[stageIndex];
                        if (stage == null)
                        {
                            Debug.LogError($"[OfferCatalogValidator] Null stage at index {stageIndex}: {path}");
                            errorCount++;
                            continue;
                        }

                        if (stage.Objectives == null || stage.Objectives.Length == 0)
                        {
                            Debug.LogError($"[OfferCatalogValidator] Stage '{stage.StageId}' has no objectives: {path}");
                            errorCount++;
                            continue;
                        }

                        for (int objectiveIndex = 0; objectiveIndex < stage.Objectives.Length; objectiveIndex++)
                        {
                            OfferObjectiveDefinition objective = stage.Objectives[objectiveIndex];
                            if (!ValidateObjective(objective, path, $"stage {stageIndex} objective {objectiveIndex}"))
                            {
                                errorCount++;
                            }
                        }
                    }
                }

                if (definition.ExtraUnlockConditions != null)
                {
                    for (int conditionIndex = 0; conditionIndex < definition.ExtraUnlockConditions.Length; conditionIndex++)
                    {
                        OfferUnlockCondition condition = definition.ExtraUnlockConditions[conditionIndex];
                        if (condition?.Objective == null)
                        {
                            Debug.LogError($"[OfferCatalogValidator] Unlock condition {conditionIndex} is missing objective: {path}");
                            errorCount++;
                            continue;
                        }

                        if (!ValidateObjective(condition.Objective, path, $"unlock condition {conditionIndex}"))
                        {
                            errorCount++;
                        }
                    }
                }

                if (definition.FailureConditions != null)
                {
                    for (int conditionIndex = 0; conditionIndex < definition.FailureConditions.Length; conditionIndex++)
                    {
                        OfferFailureCondition condition = definition.FailureConditions[conditionIndex];
                        if (condition?.Objective == null)
                        {
                            Debug.LogError($"[OfferCatalogValidator] Failure condition {conditionIndex} is missing objective: {path}");
                            errorCount++;
                            continue;
                        }

                        if (!ValidateObjective(condition.Objective, path, $"failure condition {conditionIndex}"))
                        {
                            errorCount++;
                        }
                    }
                }
            }

            if (errorCount > 0)
            {
                Debug.LogError($"[OfferCatalogValidator] Validation failed. Errors: {errorCount}");
                return;
            }

            Debug.Log($"[OfferCatalogValidator] Validation passed. Checked offers: {guids.Length}");
        }

        private static bool ValidateObjective(OfferObjectiveDefinition objective, string path, string label)
        {
            if (objective == null)
            {
                Debug.LogError($"[OfferCatalogValidator] {label} is null: {path}");
                return false;
            }

            switch (objective.ObjectiveType)
            {
                case OfferObjectiveType.DeliverResource:
                case OfferObjectiveType.AccumulateResource:
                    if (string.IsNullOrWhiteSpace(objective.ResourceId) || objective.RequiredAmount <= 0)
                    {
                        Debug.LogError($"[OfferCatalogValidator] {label} requires ResourceId and RequiredAmount > 0: {path}");
                        return false;
                    }
                    return true;
                case OfferObjectiveType.BuildObjectCount:
                case OfferObjectiveType.BuildSpecificObject:
                case OfferObjectiveType.MaintainOperationalObjectCount:
                    if (objective.RequiredCount <= 0)
                    {
                        Debug.LogError($"[OfferCatalogValidator] {label} requires RequiredCount > 0: {path}");
                        return false;
                    }
                    return true;
                case OfferObjectiveType.BuildLifeModuleWidth:
                    if (objective.RequiredWidth <= 0)
                    {
                        Debug.LogError($"[OfferCatalogValidator] {label} requires RequiredWidth > 0: {path}");
                        return false;
                    }
                    return true;
                case OfferObjectiveType.ReachStoredGold:
                case OfferObjectiveType.WaitForMissionArrival:
                    if (objective.RequiredAmount <= 0)
                    {
                        Debug.LogError($"[OfferCatalogValidator] {label} requires RequiredAmount > 0: {path}");
                        return false;
                    }
                    return true;
                case OfferObjectiveType.KeepDeadlineWithoutFailure:
                case OfferObjectiveType.PassInspectionCheck:
                    if (objective.RequiredSols < 0)
                    {
                        Debug.LogError($"[OfferCatalogValidator] {label} has invalid RequiredSols: {path}");
                        return false;
                    }
                    return true;
                default:
                    Debug.LogError($"[OfferCatalogValidator] {label} has unsupported objective type: {path}");
                    return false;
            }
        }
    }
}
