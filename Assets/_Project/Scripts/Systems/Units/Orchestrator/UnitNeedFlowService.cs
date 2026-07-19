using System;
using System.Collections.Generic;
using _Project.Scripts.Data.Construction;
using _Project.Scripts.Data.Pathfinding;
using _Project.Scripts.Data.Tasks;
using _Project.Scripts.Presentation.Character;
using _Project.Scripts.Systems.Construction;
using _Project.Scripts.Systems.Navigation;
using _Project.Scripts.Systems.Resources;
using _Project.Scripts.Systems.Units.Orchestrator;
using UnityEngine;

namespace _Project.Scripts.Systems.Units
{
    public sealed class UnitNeedFlowService
    {
        private const float EAT_RESOURCE_DURATION_MINUTES = 10f;
        private readonly UnitOrchestratorContext _context;
        private readonly UnitNeedPolicy _needPolicy;
        private readonly BuildingManager _buildingManager;
        private readonly List<int> _unitOrder;
        private readonly Dictionary<int, UnitTaskState> _statesByUnitId;
        private readonly Action<UnitTaskState> _resetUnitTask;
        private readonly Action<UnitTaskState, Vector2Int, Vector2Int, MovementActionType> _syncActorStepPosition;
        private readonly float _workWindowDurationMinutes;
        private readonly float _gameMinutesPerRealSecond;
        private readonly float _baseSleepMinutes;
        private readonly float _maxSleepMinutes;
        private readonly int _eatRestorePoints;
        private readonly int _restSleepReliefPerHour;
        private readonly int _restHungerIncreasePerHour;
        private readonly ResourceInventoryService _resourceInventoryService;
        private readonly List<string> _foodResourceIds;
        private readonly List<BuildingManager.StorageDeliveryPoint> _storageDeliveryPointsBuffer = new List<BuildingManager.StorageDeliveryPoint>();
        private readonly List<Vector2Int> _storageDeliveryCellsBuffer = new List<Vector2Int>();
        private readonly List<BuildingRuntimeEntity> _activeBuildingsBuffer = new List<BuildingRuntimeEntity>();
        private const string BEER_RESOURCE_ID = "Beer";

        public UnitNeedFlowService(
            UnitOrchestratorContext context,
            UnitNeedPolicy needPolicy,
            BuildingManager buildingManager,
            List<int> unitOrder,
            Dictionary<int, UnitTaskState> statesByUnitId,
            Action<UnitTaskState> resetUnitTask,
            Action<UnitTaskState, Vector2Int, Vector2Int, MovementActionType> syncActorStepPosition,
            float workWindowDurationMinutes,
            float gameMinutesPerRealSecond,
            float baseSleepMinutes,
            float maxSleepMinutes,
            int eatRestorePoints,
            int restSleepReliefPerHour,
            int restHungerIncreasePerHour,
            ResourceInventoryService resourceInventoryService,
            IReadOnlyList<string> foodResourceIds)
        {
            _context = context;
            _needPolicy = needPolicy;
            _buildingManager = buildingManager;
            _unitOrder = unitOrder;
            _statesByUnitId = statesByUnitId;
            _resetUnitTask = resetUnitTask;
            _syncActorStepPosition = syncActorStepPosition;
            _workWindowDurationMinutes = workWindowDurationMinutes;
            _gameMinutesPerRealSecond = gameMinutesPerRealSecond;
            _baseSleepMinutes = baseSleepMinutes;
            _maxSleepMinutes = maxSleepMinutes;
            _eatRestorePoints = eatRestorePoints;
            _restSleepReliefPerHour = restSleepReliefPerHour;
            _restHungerIncreasePerHour = restHungerIncreasePerHour;
            _resourceInventoryService = resourceInventoryService;
            _foodResourceIds = foodResourceIds != null
                ? new List<string>(foodResourceIds)
                : new List<string>();
        }

        public void UpdateWorkWindow(UnitTaskState state, float tickSeconds, float nowMinutes)
        {
            while (state.WorkHistory.Count > 0)
            {
                WorkWindowEntry head = state.WorkHistory.Peek();
                if (nowMinutes - head.TimestampMinutes <= _workWindowDurationMinutes) break;
                state.WorkHistory.Dequeue();
                state.WorkedMinutesWindow = Mathf.Max(0f, state.WorkedMinutesWindow - head.WorkedMinutes);
            }

            if (state.IsInLocalNeedFlow) return;
            if (state.CurrentTaskId == 0) return;
            if (state.State != UnitExecutionState.Moving
                && state.State != UnitExecutionState.Working
                && state.State != UnitExecutionState.DeliveringResource) return;

            float workedMinutes = tickSeconds * _gameMinutesPerRealSecond;
            if (workedMinutes <= 0f) return;

            state.WorkedMinutesWindow += workedMinutes;
            state.WorkHistory.Enqueue(new WorkWindowEntry(nowMinutes, workedMinutes));
        }

        public bool IsWorkQuotaReached(UnitTaskState state)
        {
            return state.WorkedMinutesWindow >= _context.DailyWorkQuotaMinutes - 0.001f;
        }

        public void EnterLocalNeedFlow(UnitTaskState state, UnitLocalNeedState nextNeed)
        {
            if (state.CurrentTaskId != 0)
            {
                _context.TaskBoard.ReleaseTaskReservation(state.CurrentTaskId, state.UnitId, "local-need");
            }

            _resetUnitTask(state);
            state.IsInLocalNeedFlow = true;
            state.LocalNeedState = nextNeed;

            if (nextNeed == UnitLocalNeedState.Sleep)
            {
                float overcapSleep = Mathf.Max(0f, state.Actor.SleepDesire - 200f);
                float extraSleepMinutes = Mathf.Min(240f, overcapSleep * 2f);
                state.SleepRemainingMinutes = Mathf.Clamp(_baseSleepMinutes + extraSleepMinutes, _baseSleepMinutes, _maxSleepMinutes);
                state.SleepTotalMinutes = state.SleepRemainingMinutes;
                state.RestElapsedMinutes = 0f;
                state.EatRemainingMinutes = 0f;
                state.EatTotalMinutes = 0f;
                state.HasSleepTarget = false;
                state.SleepTargetCell = state.CurrentCell;
                state.State = UnitExecutionState.Idle;
                return;
            }

            if (nextNeed == UnitLocalNeedState.Eat)
            {
                state.SleepRemainingMinutes = 0f;
                state.SleepTotalMinutes = 0f;
                state.EatTotalMinutes = 0f;
                state.EatRemainingMinutes = 0f;
                state.CurrentEatRestorePoints = 0;
                state.HasLoggedMissingEatRoute = false;
                state.HasLoggedMissingFoodAtStorage = false;
                state.RestElapsedMinutes = 0f;
                state.HasSleepTarget = false;
                state.HasResourceStorageTarget = false;
                state.CurrentStorageTargetCell = state.CurrentCell;
                state.CurrentGoalCell = state.CurrentCell;
                state.State = UnitExecutionState.Idle;
                return;
            }

            state.SleepRemainingMinutes = 0f;
            state.SleepTotalMinutes = 0f;
            state.EatTotalMinutes = 0f;
            state.EatRemainingMinutes = 0f;
            state.RestElapsedMinutes = 0f;
            state.HasSleepTarget = false;
            state.State = UnitExecutionState.Resting;
        }

        public bool ProcessLocalNeedFlow(UnitTaskState state, float tickSeconds)
        {
            if (!state.IsInLocalNeedFlow) return false;

            float tickMinutes = tickSeconds * _gameMinutesPerRealSecond;

            if (state.LocalNeedState == UnitLocalNeedState.Sleep)
            {
                if (state.ForcedWakeupRequested)
                {
                    state.ForcedWakeupRequested = false;
                    state.LocalNeedState = UnitLocalNeedState.Rest;
                    state.State = UnitExecutionState.Resting;
                    state.RestElapsedMinutes = 0f;
                    return true;
                }

                if (state.Actor != null && state.Actor.Hunger >= 260)
                {
                    state.LocalNeedState = UnitLocalNeedState.Eat;
                    state.State = UnitExecutionState.Idle;
                    state.EatTotalMinutes = 0f;
                    state.EatRemainingMinutes = 0f;
                    state.CurrentEatRestorePoints = 0;
                    state.HasResourceStorageTarget = false;
                    state.CurrentStorageTargetCell = state.CurrentCell;
                    return true;
                }

                TrySleepAtNearestModule(state, tickMinutes);
                return true;
            }

            if (state.LocalNeedState == UnitLocalNeedState.Eat)
            {
                if (ProcessEatingAtStorage(state, tickMinutes))
                {
                    return true;
                }

                state.LocalNeedState = UnitLocalNeedState.Rest;
                state.State = UnitExecutionState.Resting;
                state.RestElapsedMinutes = 0f;
                return true;
            }

            if (state.LocalNeedState == UnitLocalNeedState.Rest)
            {
                state.RestElapsedMinutes += tickMinutes;
                int sleepRelief = Mathf.RoundToInt((tickMinutes / 60f) * _restSleepReliefPerHour);
                int hungerIncrease = Mathf.RoundToInt((tickMinutes / 60f) * _restHungerIncreasePerHour);
                if (state.Actor != null)
                {
                    state.Actor.SetSleepDesire(state.Actor.SleepDesire - sleepRelief);
                    state.Actor.SetHunger(state.Actor.Hunger + hungerIncrease);
                }

                if (!IsWorkQuotaReached(state))
                {
                    UnitLocalNeedState nextNeed = _needPolicy.DecideLocalNeed(state.Actor, false);
                    if (nextNeed == UnitLocalNeedState.None)
                    {
                        state.IsInLocalNeedFlow = false;
                        state.LocalNeedState = UnitLocalNeedState.None;
                        state.SetIdle();
                        return false;
                    }

                    if (nextNeed != UnitLocalNeedState.Rest)
                    {
                        EnterLocalNeedFlow(state, nextNeed);
                    }
                }

                return true;
            }

            state.IsInLocalNeedFlow = false;
            state.LocalNeedState = UnitLocalNeedState.None;
            return false;
        }

        private bool ProcessEatingAtStorage(UnitTaskState state, float tickMinutes)
        {
            CharacterActor actor = state.Actor;
            if (actor == null)
            {
                return false;
            }

            if (actor.Hunger <= 0)
            {
                return false;
            }

            if (state.EatRemainingMinutes > 0f && state.CurrentEatRestorePoints > 0)
            {
                state.EatRemainingMinutes = Mathf.Max(0f, state.EatRemainingMinutes - tickMinutes);
                state.State = UnitExecutionState.Eating;
                if (state.EatRemainingMinutes > 0f)
                {
                    return true;
                }

                actor.SetHunger(actor.Hunger - state.CurrentEatRestorePoints);
                state.EatTotalMinutes = 0f;
                state.EatRemainingMinutes = 0f;
                state.CurrentEatRestorePoints = 0;
                // Re-evaluate the need after each consumed item so units do not chain-eat
                // another resource when the previous one already brought hunger below the eat threshold.
                UnitLocalNeedState nextNeed = _needPolicy.DecideLocalNeed(actor, IsWorkQuotaReached(state));
                if (nextNeed != UnitLocalNeedState.Eat)
                {
                    state.HasResourceStorageTarget = false;
                    state.CurrentStorageTargetCell = state.CurrentCell;

                    if (nextNeed == UnitLocalNeedState.None)
                    {
                        state.IsInLocalNeedFlow = false;
                        state.LocalNeedState = UnitLocalNeedState.None;
                        state.State = UnitExecutionState.Idle;
                    }
                    else
                    {
                        EnterLocalNeedFlow(state, nextNeed);
                    }

                    return true;
                }
            }

            if (!EnsureEatStorageRoute(state))
            {
                return true;
            }

            if (state.CurrentCell != state.CurrentGoalCell)
            {
                state.State = UnitExecutionState.DeliveringResource;
                return true;
            }

            if (TryConsumePreferredFood(actor, actor.Hunger, out int restorePoints))
            {
                state.EatTotalMinutes = EAT_RESOURCE_DURATION_MINUTES;
                state.EatRemainingMinutes = EAT_RESOURCE_DURATION_MINUTES;
                state.CurrentEatRestorePoints = restorePoints;
                state.HasLoggedMissingFoodAtStorage = false;
                state.State = UnitExecutionState.Eating;
                return true;
            }

            if (!state.HasLoggedMissingFoodAtStorage)
            {
                Debug.LogWarning($"[UnitNeedFlowService] Unit {state.UnitId} cannot eat: no suitable food is available in storage.");
                state.HasLoggedMissingFoodAtStorage = true;
            }

            state.EatTotalMinutes = 0f;
            state.EatRemainingMinutes = 0f;
            state.CurrentEatRestorePoints = 0;
            state.State = UnitExecutionState.Idle;
            return true;
        }

        private bool EnsureEatStorageRoute(UnitTaskState state)
        {
            if (state.HasResourceStorageTarget)
            {
                bool hasActiveTarget = _buildingManager.IsActiveStorageDeliveryPoint(
                    state.CurrentStorageTargetCell,
                    state.CurrentGoalCell);
                if (hasActiveTarget)
                {
                    state.HasLoggedMissingEatRoute = false;
                    return true;
                }

                state.HasResourceStorageTarget = false;
                state.CurrentStorageTargetCell = state.CurrentCell;
                state.CurrentGoalCell = state.CurrentCell;
                _context.Navigation.ClearPath(state.UnitId);
            }

            if (TryFindNearestStorageCell(state.UnitId, state.CurrentCell, out Vector2Int storageCell, out Vector2Int deliveryCell))
            {
                state.HasResourceStorageTarget = true;
                state.CurrentStorageTargetCell = storageCell;
                state.CurrentGoalCell = deliveryCell;
                state.MoveNoProgressSeconds = 0f;
                state.HasLoggedMissingEatRoute = false;
                state.State = UnitExecutionState.DeliveringResource;
                return true;
            }

            state.HasResourceStorageTarget = false;
            state.CurrentStorageTargetCell = state.CurrentCell;
            state.CurrentGoalCell = state.CurrentCell;
            state.State = UnitExecutionState.Idle;
            if (!state.HasLoggedMissingEatRoute)
            {
                Debug.LogWarning($"[UnitNeedFlowService] Unit {state.UnitId} cannot path to any storage for eating and is waiting for a route.");
                state.HasLoggedMissingEatRoute = true;
            }

            return false;
        }

        private bool TryConsumePreferredFood(CharacterActor actor, int hungerToRestore, out int restorePoints)
        {
            restorePoints = 0;
            if (actor == null || _resourceInventoryService == null)
            {
                return false;
            }

            if (!TryGetPreferredAvailableFoodResourceId(actor, hungerToRestore, out string resourceId, out restorePoints))
            {
                return false;
            }

            return _resourceInventoryService.TryRemove(resourceId, 1);
        }

        private bool TryGetPreferredAvailableFoodResourceId(
            CharacterActor actor,
            int hungerToRestore,
            out string resourceId,
            out int restorePoints)
        {
            resourceId = null;
            restorePoints = 0;
            if (actor == null || _foodResourceIds.Count == 0 || _resourceInventoryService == null)
            {
                return false;
            }

            int bestFitRestorePoints = -1;
            string bestFitResourceId = null;
            int bestOverallRestorePoints = -1;
            string bestOverallResourceId = null;

            for (int i = 0; i < _foodResourceIds.Count; i++)
            {
                string candidateResourceId = _foodResourceIds[i];
                if (string.IsNullOrWhiteSpace(candidateResourceId))
                {
                    continue;
                }

                if (!_resourceInventoryService.Has(candidateResourceId, 1))
                {
                    continue;
                }

                int candidateRestorePoints = CalculateRestorePoints(actor, candidateResourceId);
                if (candidateRestorePoints > bestOverallRestorePoints)
                {
                    bestOverallRestorePoints = candidateRestorePoints;
                    bestOverallResourceId = candidateResourceId;
                }

                if (candidateRestorePoints <= hungerToRestore && candidateRestorePoints > bestFitRestorePoints)
                {
                    bestFitRestorePoints = candidateRestorePoints;
                    bestFitResourceId = candidateResourceId;
                }
            }

            if (!string.IsNullOrWhiteSpace(bestFitResourceId))
            {
                resourceId = bestFitResourceId;
                restorePoints = bestFitRestorePoints;
                return true;
            }

            if (!string.IsNullOrWhiteSpace(bestOverallResourceId))
            {
                resourceId = bestOverallResourceId;
                restorePoints = bestOverallRestorePoints;
                return true;
            }

            return false;
        }

        private int CalculateRestorePoints(CharacterActor actor, string consumedFoodResourceId)
        {
            if (actor == null || string.IsNullOrWhiteSpace(consumedFoodResourceId))
            {
                return _eatRestorePoints;
            }

            if (string.Equals(consumedFoodResourceId, BEER_RESOURCE_ID, StringComparison.Ordinal))
            {
                return 1;
            }

            int preferenceScore = actor.GetFoodPreferenceScore(consumedFoodResourceId);
            return Mathf.Max(0, preferenceScore) * 10;
        }

        private bool TryFindNearestStorageCell(int unitId, Vector2Int unitCell, out Vector2Int storageCell, out Vector2Int deliveryCell)
        {
            storageCell = unitCell;
            deliveryCell = unitCell;
            _buildingManager.FillActiveStorageDeliveryPoints(_storageDeliveryPointsBuffer);
            if (_storageDeliveryPointsBuffer.Count == 0)
            {
                return false;
            }

            _storageDeliveryCellsBuffer.Clear();
            for (int i = 0; i < _storageDeliveryPointsBuffer.Count; i++)
            {
                _storageDeliveryCellsBuffer.Add(_storageDeliveryPointsBuffer[i].DeliveryCell);
            }

            if (!_context.WorkCellResolver.TryFindNearestReachableExactCell(
                    unitId,
                    unitCell,
                    _storageDeliveryCellsBuffer,
                    out deliveryCell))
            {
                return false;
            }

            for (int i = 0; i < _storageDeliveryPointsBuffer.Count; i++)
            {
                BuildingManager.StorageDeliveryPoint candidatePoint = _storageDeliveryPointsBuffer[i];
                if (candidatePoint.DeliveryCell != deliveryCell) continue;

                storageCell = candidatePoint.StorageCell;
                return true;
            }

            return false;
        }

        private bool TrySleepAtNearestModule(UnitTaskState state, float tickMinutes)
        {
            if (!state.HasSleepTarget)
            {
                if (TryFindNearestSleepWorkCell(state.UnitId, state.CurrentCell, out Vector2Int sleepWorkCell))
                {
                    state.HasSleepTarget = true;
                    state.SleepTargetCell = sleepWorkCell;
                }
                else
                {
                    state.HasSleepTarget = false;
                    state.SleepTargetCell = state.CurrentCell;
                }
            }

            if (state.HasSleepTarget && state.CurrentCell != state.SleepTargetCell)
            {
                NavigationStepResult stepResult = _context.Navigation.TryStep(
                    state.UnitId,
                    ref state.CurrentCell,
                    state.SleepTargetCell,
                    out Vector2Int fromCell,
                    out Vector2Int toCell,
                    out MovementActionType actionType);

                if (stepResult == NavigationStepResult.Stepped)
                {
                    Vector2Int moveDirection = toCell - fromCell;
                    state.Actor.SetFacing(moveDirection);
                    _syncActorStepPosition(state, fromCell, toCell, actionType);
                    _context.OnUnitCellChanged?.Invoke(state.CurrentCell);
                    state.State = UnitExecutionState.Moving;
                }
                else if (stepResult == NavigationStepResult.Blocked)
                {
                    state.HasSleepTarget = false;
                    state.SleepTargetCell = state.CurrentCell;
                }

                return true;
            }

            state.State = UnitExecutionState.Sleeping;
            state.SleepRemainingMinutes = Mathf.Max(0f, state.SleepRemainingMinutes - tickMinutes);

            int sleepRelief = Mathf.RoundToInt(tickMinutes);
            state.Actor.SetSleepDesire(state.Actor.SleepDesire - sleepRelief);

            if (state.SleepRemainingMinutes <= 0f || state.Actor.SleepDesire <= 40)
            {
                state.LocalNeedState = UnitLocalNeedState.Rest;
                state.State = UnitExecutionState.Resting;
                state.HasSleepTarget = false;
            }

            return true;
        }

        private bool TryFindNearestSleepWorkCell(int unitId, Vector2Int unitCell, out Vector2Int workCell)
        {
            workCell = unitCell;
            float bestDistance = float.MaxValue;
            _buildingManager.FillActiveBuildings(_activeBuildingsBuffer);

            for (int i = 0; i < _activeBuildingsBuffer.Count; i++)
            {
                BuildingRuntimeEntity entity = _activeBuildingsBuffer[i];
                if (entity?.BuildingDef == null) continue;
                if (entity.BuildingDef.ObjectType != BuildObjectType.SleepModule) continue;

                UnitTaskRecord pseudoTask = new UnitTaskRecord
                {
                    TaskId = -1,
                    TaskType = UnitTaskType.Sleep,
                    TargetCell = entity.AnchorCell
                };

                if (!_context.WorkCellResolver.TryFindWorkCell(unitId, unitCell, pseudoTask, out Vector2Int candidateCell)) continue;
                if (IsSleepCellOccupiedByOtherUnit(unitId, candidateCell)) continue;
                float distance = Vector2Int.Distance(unitCell, candidateCell);
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                workCell = candidateCell;
            }

            return bestDistance < float.MaxValue;
        }

        private bool IsSleepCellOccupiedByOtherUnit(int unitId, Vector2Int sleepCell)
        {
            for (int i = 0; i < _unitOrder.Count; i++)
            {
                int otherUnitId = _unitOrder[i];
                if (otherUnitId == unitId) continue;
                if (!_statesByUnitId.TryGetValue(otherUnitId, out UnitTaskState otherState) || otherState == null) continue;

                if (!otherState.IsInLocalNeedFlow || otherState.LocalNeedState != UnitLocalNeedState.Sleep) continue;

                bool occupiesCell = otherState.CurrentCell == sleepCell
                                   || (otherState.HasSleepTarget && otherState.SleepTargetCell == sleepCell);
                if (occupiesCell)
                {
                    return true;
                }
            }

            return false;
        }
    }
}