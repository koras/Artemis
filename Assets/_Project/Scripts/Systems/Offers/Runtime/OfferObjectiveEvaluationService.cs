using System;
using System.Collections.Generic;
using _Project.Scripts.Data.Construction;
using _Project.Scripts.Data.Grid;
using _Project.Scripts.Data.Offers;
using _Project.Scripts.Systems.Construction;
using _Project.Scripts.Systems.Resources;
using _Project.Scripts.Systems.Simulation;
using UnityEngine;

namespace _Project.Scripts.Systems.Offers.Runtime
{
    /// <summary>
    /// Evaluates live offer objectives against inventory, base state, and mission progress.
    /// </summary>
    public sealed class OfferObjectiveEvaluationService
    {
        private readonly GridState _gridState;
        private readonly BuildingManager _buildingManager;
        private readonly ResourceInventoryService _resourceInventoryService;
        private readonly GameTimeService _gameTimeService;
        private readonly OfferReservationService _reservationService;
        private readonly List<BuildingRuntimeEntity> _buildingSnapshotBuffer = new();

        private int _cachedLifeModuleWidthRevision = -1;
        private int _cachedMaxBuiltLifeModuleWidth;

        public OfferObjectiveEvaluationService(
            GridState gridState,
            BuildingManager buildingManager,
            ResourceInventoryService resourceInventoryService,
            GameTimeService gameTimeService,
            OfferReservationService reservationService)
        {
            _gridState = gridState;
            _buildingManager = buildingManager;
            _resourceInventoryService = resourceInventoryService;
            _gameTimeService = gameTimeService;
            _reservationService = reservationService;
        }

        public bool HasStoryStages(OfferDefinition definition)
        {
            return definition != null && definition.HasStages;
        }

        public OfferStageDefinition GetCurrentStage(OfferRuntimeRecord record)
        {
            if (record?.Definition == null || !record.Definition.HasStages)
            {
                return null;
            }

            int index = Mathf.Clamp(record.StageState.CurrentStageIndex, 0, record.Definition.Stages.Length - 1);
            return record.Definition.Stages[index];
        }

        public bool CurrentStageHasDeliverObjectives(OfferRuntimeRecord record)
        {
            OfferStageDefinition stage = GetCurrentStage(record);
            if (stage?.Objectives == null)
            {
                return false;
            }

            for (int i = 0; i < stage.Objectives.Length; i++)
            {
                OfferObjectiveDefinition objective = stage.Objectives[i];
                if (objective != null && objective.ObjectiveType == OfferObjectiveType.DeliverResource)
                {
                    return true;
                }
            }

            return false;
        }

        public OfferResourceAmount[] BuildCurrentStageReservationRequirements(OfferRuntimeRecord record)
        {
            OfferStageDefinition stage = GetCurrentStage(record);
            if (stage?.Objectives == null)
            {
                return Array.Empty<OfferResourceAmount>();
            }

            var result = new List<OfferResourceAmount>();
            for (int i = 0; i < stage.Objectives.Length; i++)
            {
                OfferObjectiveDefinition objective = stage.Objectives[i];
                if (objective == null
                    || objective.ObjectiveType != OfferObjectiveType.DeliverResource
                    || string.IsNullOrWhiteSpace(objective.ResourceId)
                    || objective.RequiredAmount <= 0)
                {
                    continue;
                }

                result.Add(new OfferResourceAmount
                {
                    ResourceId = objective.ResourceId,
                    Amount = objective.RequiredAmount
                });
            }

            return result.ToArray();
        }

        public bool PassesUnlockConditions(OfferDefinition definition)
        {
            if (definition?.ExtraUnlockConditions == null || definition.ExtraUnlockConditions.Length == 0)
            {
                return true;
            }

            for (int i = 0; i < definition.ExtraUnlockConditions.Length; i++)
            {
                OfferUnlockCondition condition = definition.ExtraUnlockConditions[i];
                if (condition?.Objective == null)
                {
                    continue;
                }

                ObjectiveEvaluationResult result = EvaluateObjective(null, condition.Objective, 0, -1, false);
                if (!result.IsCompleted)
                {
                    return false;
                }
            }

            return true;
        }

        public bool HasFailureCondition(OfferRuntimeRecord record)
        {
            if (record?.Definition?.FailureConditions == null)
            {
                return false;
            }

            OfferStageDefinition currentStage = GetCurrentStage(record);
            int currentStageIndex = currentStage != null ? record.StageState.CurrentStageIndex : -1;
            int stableSinceSol = record.StageState?.StageSatisfiedSinceSol ?? -1;

            for (int i = 0; i < record.Definition.FailureConditions.Length; i++)
            {
                OfferFailureCondition condition = record.Definition.FailureConditions[i];
                if (condition?.Objective == null)
                {
                    continue;
                }

                ObjectiveEvaluationResult result = EvaluateObjective(record, condition.Objective, currentStageIndex, stableSinceSol, true);
                if (condition.FailWhenObjectiveIncomplete && !result.IsCompleted)
                {
                    return true;
                }
            }

            return false;
        }

        public OfferStageProgressSnapshot BuildStageSnapshot(OfferRuntimeRecord record)
        {
            if (record?.Definition == null)
            {
                return null;
            }

            if (!record.Definition.HasStages)
            {
                return BuildLegacySnapshot(record);
            }

            OfferStageDefinition stage = GetCurrentStage(record);
            if (stage == null)
            {
                return null;
            }

            int stableSinceSol = record.StageState?.StageSatisfiedSinceSol ?? -1;
            OfferObjectiveProgressSnapshot[] objectives = BuildObjectiveSnapshots(record, stage, stableSinceSol);

            return new OfferStageProgressSnapshot
            {
                Stage = stage,
                StageIndex = record.StageState.CurrentStageIndex,
                TotalStages = record.Definition.Stages.Length,
                IsCompleted = record.ResolutionState == OfferResolutionState.Completed,
                Objectives = objectives
            };
        }

        public OfferStageAdvanceResult EvaluateStageProgress(OfferRuntimeRecord record)
        {
            if (record?.Definition == null || record.ResolutionState != OfferResolutionState.Active)
            {
                return OfferStageAdvanceResult.None;
            }

            if (!record.Definition.HasStages)
            {
                return EvaluateLegacyResourceOffer(record);
            }

            OfferStageDefinition stage = GetCurrentStage(record);
            if (stage == null)
            {
                return OfferStageAdvanceResult.None;
            }

            if (stage.Objectives == null || stage.Objectives.Length == 0)
            {
                return AdvanceStage(record);
            }

            bool prerequisitesCompleted = ArePrerequisitesCompleted(record, stage, record.StageState.StageSatisfiedSinceSol);
            if (prerequisitesCompleted)
            {
                if (record.StageState.StageSatisfiedSinceSol < 0)
                {
                    record.StageState.StageSatisfiedSinceSol = _gameTimeService != null ? _gameTimeService.Sol : 1;
                }
            }
            else
            {
                record.StageState.StageSatisfiedSinceSol = -1;
            }

            OfferObjectiveProgressSnapshot[] snapshots = BuildObjectiveSnapshots(record, stage, record.StageState.StageSatisfiedSinceSol);
            int completedObjectives = CountCompletedObjectives(snapshots);
            record.StageState.CompletedObjectiveCount = completedObjectives;

            if (completedObjectives < snapshots.Length)
            {
                return OfferStageAdvanceResult.None;
            }

            return AdvanceStage(record);
        }

        private OfferStageAdvanceResult EvaluateLegacyResourceOffer(OfferRuntimeRecord record)
        {
            if (record == null || record.Definition == null)
            {
                return OfferStageAdvanceResult.None;
            }

            var requirements = record.Definition.CompletionRequirements;
            if (requirements == null || requirements.Length == 0)
            {
                return OfferStageAdvanceResult.Completed;
            }

            return OfferStageAdvanceResult.None;
        }

        private OfferStageAdvanceResult AdvanceStage(OfferRuntimeRecord record)
        {
            if (record?.Definition == null || !record.Definition.HasStages)
            {
                return OfferStageAdvanceResult.Completed;
            }

            int currentStageIndex = record.StageState.CurrentStageIndex;
            if (currentStageIndex >= record.Definition.Stages.Length - 1)
            {
                return OfferStageAdvanceResult.Completed;
            }

            record.StageState.CurrentStageIndex++;
            record.StageState.StageStartedMissionCount = record.MissionArrivalCount;
            record.StageState.StageSatisfiedSinceSol = -1;
            record.StageState.CompletedObjectiveCount = 0;

            OfferStageDefinition nextStage = GetCurrentStage(record);
            record.StageState.IsInspectionScheduled = nextStage != null && nextStage.ScheduleInspection;
            return OfferStageAdvanceResult.Advanced;
        }

        private OfferStageProgressSnapshot BuildLegacySnapshot(OfferRuntimeRecord record)
        {
            OfferResourceAmount[] requirements = record.Definition.CompletionRequirements ?? Array.Empty<OfferResourceAmount>();
            var objectives = new OfferObjectiveProgressSnapshot[requirements.Length];

            for (int i = 0; i < requirements.Length; i++)
            {
                OfferResourceAmount requirement = requirements[i];
                int reservedAmount = GetReservedAmountForOffer(record, requirement.ResourceId);
                objectives[i] = new OfferObjectiveProgressSnapshot
                {
                    Objective = new OfferObjectiveDefinition
                    {
                        ObjectiveId = $"legacy-{i}",
                        ObjectiveType = OfferObjectiveType.DeliverResource,
                        ResourceId = requirement.ResourceId,
                        RequiredAmount = requirement.Amount
                    },
                    CurrentValue = reservedAmount,
                    TargetValue = requirement.Amount,
                    IsCompleted = reservedAmount >= requirement.Amount
                };
            }

            return new OfferStageProgressSnapshot
            {
                Stage = new OfferStageDefinition
                {
                    StageId = "legacy"
                },
                StageIndex = 0,
                TotalStages = 1,
                IsCompleted = record.ResolutionState == OfferResolutionState.Completed,
                Objectives = objectives
            };
        }

        private OfferObjectiveProgressSnapshot[] BuildObjectiveSnapshots(OfferRuntimeRecord record, OfferStageDefinition stage, int stableSinceSol)
        {
            if (stage?.Objectives == null || stage.Objectives.Length == 0)
            {
                return Array.Empty<OfferObjectiveProgressSnapshot>();
            }

            var result = new OfferObjectiveProgressSnapshot[stage.Objectives.Length];
            for (int i = 0; i < stage.Objectives.Length; i++)
            {
                OfferObjectiveDefinition objective = stage.Objectives[i];
                ObjectiveEvaluationResult evaluation = EvaluateObjective(record, objective, record.StageState.CurrentStageIndex, stableSinceSol, true);
                result[i] = new OfferObjectiveProgressSnapshot
                {
                    Objective = objective,
                    CurrentValue = evaluation.CurrentValue,
                    TargetValue = evaluation.TargetValue,
                    IsCompleted = evaluation.IsCompleted
                };
            }

            return result;
        }

        private ObjectiveEvaluationResult EvaluateObjective(
            OfferRuntimeRecord record,
            OfferObjectiveDefinition objective,
            int currentStageIndex,
            int stableSinceSol,
            bool allowInspectionResolution)
        {
            if (objective == null)
            {
                return ObjectiveEvaluationResult.Empty;
            }

            switch (objective.ObjectiveType)
            {
                case OfferObjectiveType.DeliverResource:
                    return EvaluateDeliverObjective(record, objective);
                case OfferObjectiveType.AccumulateResource:
                    return EvaluateAccumulateObjective(objective);
                case OfferObjectiveType.BuildObjectCount:
                case OfferObjectiveType.BuildSpecificObject:
                    return EvaluateBuildObjectObjective(objective, requireOperational: false);
                case OfferObjectiveType.MaintainOperationalObjectCount:
                    return EvaluateBuildObjectObjective(objective, requireOperational: true, stableSinceSol: stableSinceSol);
                case OfferObjectiveType.BuildLifeModuleWidth:
                    return EvaluateLifeModuleWidthObjective(objective);
                case OfferObjectiveType.ReachStoredGold:
                    return EvaluateGoldObjective(objective);
                case OfferObjectiveType.WaitForMissionArrival:
                    return EvaluateMissionArrivalObjective(record, objective);
                case OfferObjectiveType.KeepDeadlineWithoutFailure:
                    return EvaluateKeepDeadlineObjective(objective, stableSinceSol);
                case OfferObjectiveType.PassInspectionCheck:
                    return EvaluateInspectionObjective(record, currentStageIndex, stableSinceSol, allowInspectionResolution);
                default:
                    return ObjectiveEvaluationResult.Empty;
            }
        }

        private ObjectiveEvaluationResult EvaluateDeliverObjective(OfferRuntimeRecord record, OfferObjectiveDefinition objective)
        {
            int reservedAmount = GetReservedAmountForOffer(record, objective.ResourceId);
            int target = Mathf.Max(1, objective.RequiredAmount);
            return new ObjectiveEvaluationResult(reservedAmount, target, reservedAmount >= target);
        }

        private ObjectiveEvaluationResult EvaluateAccumulateObjective(OfferObjectiveDefinition objective)
        {
            int current = _resourceInventoryService != null ? _resourceInventoryService.GetAmount(objective.ResourceId) : 0;
            int target = Mathf.Max(1, objective.RequiredAmount);
            return new ObjectiveEvaluationResult(current, target, current >= target);
        }

        private ObjectiveEvaluationResult EvaluateBuildObjectObjective(OfferObjectiveDefinition objective, bool requireOperational, int stableSinceSol = -1)
        {
            int count = CountBuildings(objective.BuildObjectType, requireOperational || objective.RequireOperational);
            int target = Mathf.Max(1, objective.RequiredCount);
            bool isCompleted = count >= target;
            int current = count;

            if (requireOperational)
            {
                int stableProgress = CalculateStableSolProgress(stableSinceSol);
                int stableTarget = Mathf.Max(1, objective.RequiredSols);
                current = isCompleted ? stableProgress : 0;
                isCompleted = isCompleted && stableProgress >= stableTarget;
                target = stableTarget;
            }

            return new ObjectiveEvaluationResult(current, target, isCompleted);
        }

        private ObjectiveEvaluationResult EvaluateLifeModuleWidthObjective(OfferObjectiveDefinition objective)
        {
            int width = GetMaxBuiltLifeModuleWidth();
            int target = Mathf.Max(1, objective.RequiredWidth);
            return new ObjectiveEvaluationResult(width, target, width >= target);
        }

        private ObjectiveEvaluationResult EvaluateGoldObjective(OfferObjectiveDefinition objective)
        {
            int current = _resourceInventoryService != null
                ? _resourceInventoryService.GetAmount(ResourceInventoryService.GOLD_RESOURCE_ID)
                : 0;
            int target = Mathf.Max(1, objective.RequiredAmount);
            return new ObjectiveEvaluationResult(current, target, current >= target);
        }

        private ObjectiveEvaluationResult EvaluateMissionArrivalObjective(OfferRuntimeRecord record, OfferObjectiveDefinition objective)
        {
            int baseline = record?.StageState?.StageStartedMissionCount ?? record?.MissionArrivalCountAtAccept ?? 0;
            int current = Mathf.Max(0, (record?.MissionArrivalCount ?? 0) - baseline);
            int target = Mathf.Max(1, objective.RequiredAmount);
            return new ObjectiveEvaluationResult(current, target, current >= target);
        }

        private ObjectiveEvaluationResult EvaluateKeepDeadlineObjective(OfferObjectiveDefinition objective, int stableSinceSol)
        {
            int current = CalculateStableSolProgress(stableSinceSol);
            int target = Mathf.Max(1, objective.RequiredSols);
            return new ObjectiveEvaluationResult(current, target, current >= target);
        }

        private ObjectiveEvaluationResult EvaluateInspectionObjective(
            OfferRuntimeRecord record,
            int currentStageIndex,
            int stableSinceSol,
            bool allowInspectionResolution)
        {
            if (record?.Definition == null || !record.Definition.HasStages || currentStageIndex < 0 || currentStageIndex >= record.Definition.Stages.Length)
            {
                return ObjectiveEvaluationResult.Empty;
            }

            OfferStageDefinition stage = record.Definition.Stages[currentStageIndex];
            bool prerequisitesMet = true;
            if (stage?.Objectives != null)
            {
                for (int i = 0; i < stage.Objectives.Length; i++)
                {
                    OfferObjectiveDefinition objective = stage.Objectives[i];
                    if (objective == null || objective.ObjectiveType == OfferObjectiveType.PassInspectionCheck)
                    {
                        continue;
                    }

                    ObjectiveEvaluationResult result = EvaluateObjective(record, objective, currentStageIndex, stableSinceSol, false);
                    if (!result.IsCompleted)
                    {
                        prerequisitesMet = false;
                        break;
                    }
                }
            }

            bool isCompleted = record.StageState.IsInspectionScheduled && prerequisitesMet && allowInspectionResolution;
            return new ObjectiveEvaluationResult(isCompleted ? 1 : 0, 1, isCompleted);
        }

        private int GetReservedAmountForOffer(OfferRuntimeRecord record, string resourceId)
        {
            if (record == null || string.IsNullOrWhiteSpace(resourceId))
            {
                return 0;
            }

            if (_reservationService == null)
            {
                return 0;
            }

            if (!record.IsReservedForShipment)
            {
                return 0;
            }

            if (!_reservationService.HasFullReservation(record.RuntimeId, BuildCurrentStageReservationRequirements(record)))
            {
                return 0;
            }

            return record.Definition.HasStages
                ? FindCurrentStageReservationAmount(record, resourceId)
                : _reservationService.GetReservedAmount(resourceId);
        }

        private int FindCurrentStageReservationAmount(OfferRuntimeRecord record, string resourceId)
        {
            OfferResourceAmount[] requirements = BuildCurrentStageReservationRequirements(record);
            for (int i = 0; i < requirements.Length; i++)
            {
                if (string.Equals(requirements[i].ResourceId, resourceId, StringComparison.OrdinalIgnoreCase))
                {
                    return requirements[i].Amount;
                }
            }

            return 0;
        }

        private int CountBuildings(BuildObjectType buildObjectType, bool requireOperational)
        {
            if (_buildingManager == null)
            {
                return 0;
            }

            _buildingManager.FillActiveBuildings(_buildingSnapshotBuffer);
            int count = 0;
            for (int i = 0; i < _buildingSnapshotBuffer.Count; i++)
            {
                BuildingRuntimeEntity entity = _buildingSnapshotBuffer[i];
                if (entity?.BuildingDef == null || entity.BuildingDef.ObjectType != buildObjectType)
                {
                    continue;
                }

                if (requireOperational && !entity.IsOperational)
                {
                    continue;
                }

                count++;
            }

            return count;
        }

        private int GetMaxBuiltLifeModuleWidth()
        {
            if (_gridState == null)
            {
                return 0;
            }

            if (_cachedLifeModuleWidthRevision == _gridState.CellRevision)
            {
                return _cachedMaxBuiltLifeModuleWidth;
            }

            var widthByGroupId = new Dictionary<int, int>();
            Span<Cell> cells = _gridState.GetRawCells();
            for (int i = 0; i < cells.Length; i++)
            {
                Cell cell = cells[i];
                if (cell.LifeModuleType != LifeModuleType.Built || !cell.IsLifeModulePartAnchor || cell.LifeModuleGroupId == 0)
                {
                    continue;
                }

                if (!widthByGroupId.TryGetValue(cell.LifeModuleGroupId, out int width))
                {
                    width = 0;
                }

                widthByGroupId[cell.LifeModuleGroupId] = width + Mathf.Max(1, cell.LifeModulePartWidth);
            }

            int maxWidth = 0;
            foreach (KeyValuePair<int, int> pair in widthByGroupId)
            {
                maxWidth = Mathf.Max(maxWidth, pair.Value);
            }

            _cachedLifeModuleWidthRevision = _gridState.CellRevision;
            _cachedMaxBuiltLifeModuleWidth = maxWidth;
            return _cachedMaxBuiltLifeModuleWidth;
        }

        private int CountCompletedObjectives(OfferObjectiveProgressSnapshot[] snapshots)
        {
            if (snapshots == null)
            {
                return 0;
            }

            int completed = 0;
            for (int i = 0; i < snapshots.Length; i++)
            {
                if (snapshots[i] != null && snapshots[i].IsCompleted)
                {
                    completed++;
                }
            }

            return completed;
        }

        private int CalculateStableSolProgress(int stableSinceSol)
        {
            if (stableSinceSol < 0 || _gameTimeService == null)
            {
                return 0;
            }

            return Mathf.Max(0, _gameTimeService.Sol - stableSinceSol + 1);
        }

        private bool ArePrerequisitesCompleted(OfferRuntimeRecord record, OfferStageDefinition stage, int stableSinceSol)
        {
            if (stage?.Objectives == null)
            {
                return true;
            }

            for (int i = 0; i < stage.Objectives.Length; i++)
            {
                OfferObjectiveDefinition objective = stage.Objectives[i];
                if (objective == null)
                {
                    continue;
                }

                if (objective.ObjectiveType == OfferObjectiveType.KeepDeadlineWithoutFailure
                    || objective.ObjectiveType == OfferObjectiveType.PassInspectionCheck)
                {
                    continue;
                }

                if (objective.ObjectiveType == OfferObjectiveType.MaintainOperationalObjectCount)
                {
                    ObjectiveEvaluationResult operationalNow = EvaluateBuildObjectObjective(objective, requireOperational: true, stableSinceSol: _gameTimeService != null ? _gameTimeService.Sol : stableSinceSol);
                    if (operationalNow.CurrentValue <= 0)
                    {
                        return false;
                    }

                    continue;
                }

                ObjectiveEvaluationResult result = EvaluateObjective(record, objective, record.StageState.CurrentStageIndex, stableSinceSol, false);
                if (!result.IsCompleted)
                {
                    return false;
                }
            }

            return true;
        }

        private readonly struct ObjectiveEvaluationResult
        {
            public static ObjectiveEvaluationResult Empty => new ObjectiveEvaluationResult(0, 0, true);

            public ObjectiveEvaluationResult(int currentValue, int targetValue, bool isCompleted)
            {
                CurrentValue = currentValue;
                TargetValue = targetValue;
                IsCompleted = isCompleted;
            }

            public int CurrentValue { get; }
            public int TargetValue { get; }
            public bool IsCompleted { get; }
        }
    }

    public enum OfferStageAdvanceResult
    {
        None = 0,
        Advanced = 1,
        Completed = 2
    }
}