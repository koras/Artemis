﻿using System.Collections.Generic;
using _Project.Scripts.Data.Grid;
using _Project.Scripts.Data.Tasks;
using _Project.Scripts.Systems.Construction;
using _Project.Scripts.Systems.Resources;
using _Project.Scripts.Systems.Tasks;
using UnityEngine;

namespace _Project.Scripts.Systems.Units
{
    /// <summary>
    /// Handles resource delivery flow for units.
    /// </summary>
    public sealed class UnitResourceDeliveryService
    {
        private readonly BuildingManager _buildingManager;
        private readonly ResourceInventoryService _resourceInventoryService;
        private readonly SceneResourceObjectService _sceneResourceObjectService;
        private readonly GlobalTaskBoardService _taskBoard;
        private readonly UnitWorkCellResolver _workCellResolver;
        private readonly bool _enableLogs;
        private readonly System.Action<UnitTaskState, bool> _resetUnitTask;
        private readonly System.Func<Vector2Int, float> _onStorageDeliveryCompleted;
        private readonly List<BuildingManager.StorageDeliveryPoint> _storageDeliveryPointsBuffer = new List<BuildingManager.StorageDeliveryPoint>();
        private const string CABLE_SALVAGE_RESOURCE_ID = "Cable";
        private const int CABLE_SALVAGE_AMOUNT = 1;
        private const string WATER_SALVAGE_RESOURCE_ID = "Water Pipe";
        private const int WATER_SALVAGE_AMOUNT = 1;
        private const string OXYGEN_SALVAGE_RESOURCE_ID = "Oxygen Pipe";
        private const int OXYGEN_SALVAGE_AMOUNT = 1;

        // Method UnitResourceDeliveryService: executes the UnitResourceDeliveryService workflow.
        public UnitResourceDeliveryService(
            BuildingManager buildingManager,
            ResourceInventoryService resourceInventoryService,
            SceneResourceObjectService sceneResourceObjectService,
            GlobalTaskBoardService taskBoard,
            UnitWorkCellResolver workCellResolver,
            bool enableLogs,
            System.Action<UnitTaskState, bool> resetUnitTask,
            System.Func<Vector2Int, float> onStorageDeliveryCompleted)
        {
            _buildingManager = buildingManager;
            _resourceInventoryService = resourceInventoryService;
            _sceneResourceObjectService = sceneResourceObjectService;
            _taskBoard = taskBoard;
            _workCellResolver = workCellResolver;
            _enableLogs = enableLogs;
            _resetUnitTask = resetUnitTask;
            _onStorageDeliveryCompleted = onStorageDeliveryCompleted;
        }

        // Method AddCableSalvageResource: executes the AddCableSalvageResource workflow.
        public void AddCableSalvageResource()
        {
            _resourceInventoryService.Add(CABLE_SALVAGE_RESOURCE_ID, CABLE_SALVAGE_AMOUNT);
        }

        // Method AddWaterSalvageResource: executes the AddWaterSalvageResource workflow.
        public void AddWaterSalvageResource()
        {
            _resourceInventoryService.Add(WATER_SALVAGE_RESOURCE_ID, WATER_SALVAGE_AMOUNT);
        }

        // Method AddOxygenSalvageResource: executes the AddOxygenSalvageResource workflow.
        public void AddOxygenSalvageResource()
        {
            _resourceInventoryService.Add(OXYGEN_SALVAGE_RESOURCE_ID, OXYGEN_SALVAGE_AMOUNT);
        }

        // Method StartResourceDelivery: executes the StartResourceDelivery workflow.
        public void StartResourceDelivery(UnitTaskState state, CellType startedDigCellType, int minedAmount)
        {
            state.CarriedResourceId = GetResourceIdByCellType(startedDigCellType);
            state.CarriedResourceAmount = minedAmount;
            state.State = UnitExecutionState.DeliveringResource;
            state.MoveNoProgressSeconds = 0f;
            state.IsWaitingForStorageInteraction = false;
            state.StorageInteractionWaitRemainingSeconds = 0f;

            if (TryFindNearestStorageDeliveryCell(state.UnitId, state.CurrentCell, out Vector2Int storageCell, out Vector2Int deliveryCell))
            {
                state.HasResourceStorageTarget = true;
                state.CurrentStorageTargetCell = storageCell;
                state.CurrentGoalCell = deliveryCell;
                return;
            }

            state.HasResourceStorageTarget = false;
            state.CurrentStorageTargetCell = state.CurrentCell;
            state.CurrentGoalCell = state.CurrentCell;
        }

        // Method ProcessResourceDelivery: executes the ProcessResourceDelivery workflow.
        public void ProcessResourceDelivery(UnitTaskState state, float tickSeconds)
        {
            if (state.IsWaitingForStorageInteraction)
            {
                state.CurrentGoalCell = state.CurrentCell;
                state.MoveNoProgressSeconds = 0f;
                state.StorageInteractionWaitRemainingSeconds = Mathf.Max(0f, state.StorageInteractionWaitRemainingSeconds - tickSeconds);
                if (state.StorageInteractionWaitRemainingSeconds > 0f)
                {
                    return;
                }

                ContinueAfterResourceDelivery(state);
                return;
            }

            if (state.CarriedResourceAmount <= 0 || string.IsNullOrWhiteSpace(state.CarriedResourceId))
            {
                ContinueAfterResourceDelivery(state);
                return;
            }

            bool isAtDeliveryCell = state.CurrentCell == state.CurrentGoalCell;
            if (state.HasResourceStorageTarget
                && isAtDeliveryCell
                && _workCellResolver.CanWorkWithTargetFromCell(state.CurrentCell, state.CurrentStorageTargetCell))
            {
                _resourceInventoryService.Add(state.CarriedResourceId, state.CarriedResourceAmount);
                float interactionWaitSeconds = Mathf.Max(0f, _onStorageDeliveryCompleted?.Invoke(state.CurrentStorageTargetCell) ?? 0f);
                state.IsWaitingForStorageInteraction = interactionWaitSeconds > 0f;
                state.StorageInteractionWaitRemainingSeconds = interactionWaitSeconds;
                state.CurrentGoalCell = state.CurrentCell;
                state.MoveNoProgressSeconds = 0f;
                if (state.IsWaitingForStorageInteraction)
                {
                    return;
                }

                ContinueAfterResourceDelivery(state);
                return;
            }

            if (TryFindNearestStorageDeliveryCell(state.UnitId, state.CurrentCell, out Vector2Int storageCell, out Vector2Int deliveryCell))
            {
                state.HasResourceStorageTarget = true;
                state.CurrentStorageTargetCell = storageCell;
                state.CurrentGoalCell = deliveryCell;
                return;
            }

            if (DropCarriedResourceAsSceneObject(state, out Vector2Int spawnedCell))
            {
                ContinueAfterResourceDelivery(state);
                EnsureDroppedResourceTaskAfterCompletion(spawnedCell);
                return;
            }

            ContinueAfterResourceDelivery(state);
        }

        // Method ClearCarriedResource: executes the ClearCarriedResource workflow.
        public void ClearCarriedResource(UnitTaskState state)
        {
            state.CarriedResourceId = null;
            state.CarriedResourceAmount = 0;
            state.HasResourceStorageTarget = false;
            state.CurrentStorageTargetCell = state.CurrentCell;
            state.IsWaitingForStorageInteraction = false;
            state.StorageInteractionWaitRemainingSeconds = 0f;
        }

        // Method TryFindNearestStorageDeliveryCell: executes the TryFindNearestStorageDeliveryCell workflow.
        public bool TryFindNearestStorageDeliveryCell(int unitId, Vector2Int unitCell, out Vector2Int storageCell, out Vector2Int deliveryCell)
        {
            storageCell = unitCell;
            deliveryCell = unitCell;
            float bestDistance = float.PositiveInfinity;
            _buildingManager.FillActiveStorageDeliveryPoints(_storageDeliveryPointsBuffer);

            if (_storageDeliveryPointsBuffer.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < _storageDeliveryPointsBuffer.Count; i++)
            {
                BuildingManager.StorageDeliveryPoint candidatePoint = _storageDeliveryPointsBuffer[i];
                if (!_workCellResolver.TryFindNearestReachableCell(unitId, unitCell, candidatePoint.DeliveryCell, out Vector2Int reachableCell)
                    || reachableCell != candidatePoint.DeliveryCell)
                {
                    continue;
                }

                float distance = Mathf.Abs(candidatePoint.DeliveryCell.x - unitCell.x)
                                 + Mathf.Abs(candidatePoint.DeliveryCell.y - unitCell.y);
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                storageCell = candidatePoint.StorageCell;
                deliveryCell = candidatePoint.DeliveryCell;
            }

            return !float.IsInfinity(bestDistance) && !float.IsNaN(bestDistance);
        }

        // Method TryCompleteDroppedResourceDelivery: executes the TryCompleteDroppedResourceDelivery workflow.
        public bool TryCompleteDroppedResourceDelivery(UnitTaskState state, UnitTaskRecord task)
        {
            if (task == null || task.TaskType != UnitTaskType.DeliverDroppedResource) return false;
            if (_sceneResourceObjectService == null) return false;

            if (!TryFindNearestStorageDeliveryCell(state.UnitId, state.CurrentCell, out Vector2Int storageCell, out _))
            {
                return false;
            }

            if (!_workCellResolver.CanWorkWithTargetFromCell(state.CurrentCell, storageCell))
            {
                return false;
            }

            if (!_sceneResourceObjectService.TryTakeFromCell(task.TargetCell, out _, out string resourceId, out int amount))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(resourceId) || amount <= 0)
            {
                return false;
            }

            _resourceInventoryService.Add(resourceId, amount);
            _onStorageDeliveryCompleted?.Invoke(storageCell);

            if (_sceneResourceObjectService.TryPeekResourceAtCell(task.TargetCell, out string nextResourceId, out int nextAmount))
            {
                _taskBoard.TryEnsureDroppedResourceDeliveryTask(
                    task.TargetCell,
                    nextResourceId,
                    nextAmount,
                    Time.frameCount);
            }

            return true;
        }

        /// <summary>
        /// Picks up dropped resource into unit carry and starts normal storage delivery flow.
        /// </summary>
        // Method TryPickupDroppedResourceAndStartDelivery: executes the TryPickupDroppedResourceAndStartDelivery workflow.
        public bool TryPickupDroppedResourceAndStartDelivery(UnitTaskState state, UnitTaskRecord task)
        {
            if (task == null || task.TaskType != UnitTaskType.DeliverDroppedResource) return false;
            if (_sceneResourceObjectService == null) return false;

            if (!_sceneResourceObjectService.TryTakeFromCell(task.TargetCell, out _, out string resourceId, out int amount))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(resourceId) || amount <= 0)
            {
                return false;
            }

            state.CarriedResourceId = resourceId;
            state.CarriedResourceAmount = amount;
            state.State = UnitExecutionState.DeliveringResource;
            state.MoveNoProgressSeconds = 0f;
            state.IsWaitingForStorageInteraction = false;
            state.StorageInteractionWaitRemainingSeconds = 0f;

            if (TryFindNearestStorageDeliveryCell(state.UnitId, state.CurrentCell, out Vector2Int storageCell, out Vector2Int deliveryCell))
            {
                state.HasResourceStorageTarget = true;
                state.CurrentStorageTargetCell = storageCell;
                state.CurrentGoalCell = deliveryCell;
            }
            else
            {
                state.HasResourceStorageTarget = false;
                state.CurrentStorageTargetCell = state.CurrentCell;
                state.CurrentGoalCell = state.CurrentCell;
            }

            return true;
        }

        // Method ContinueAfterResourceDelivery: executes the ContinueAfterResourceDelivery workflow.
        private void ContinueAfterResourceDelivery(UnitTaskState state)
        {
            ClearCarriedResource(state);
            if (!_taskBoard.TryGetTask(state.CurrentTaskId, out UnitTaskRecord task))
            {
                _resetUnitTask(state, false);
                return;
            }

            if (task.TaskType == UnitTaskType.ClearBuildCell)
            {
                _taskBoard.NotifyBuildClearSubtaskCompleted(task);
            }

            _taskBoard.CompleteTask(task.TaskId, state.UnitId);
            _resetUnitTask(state, false);
        }

        // Method GetMinedAmount: executes the GetMinedAmount workflow.
        public static int GetMinedAmount(CellType cellType)
        {
            if (cellType == CellType.Iron) return 1;
            if (cellType == CellType.Titan) return 2;
            if (cellType == CellType.Aluminium) return 2;
            if (cellType == CellType.Rogalite) return 3;
            return 0;
        }

        // Method GetResourceIdByCellType: executes the GetResourceIdByCellType workflow.
        private static string GetResourceIdByCellType(CellType cellType)
        {
            if (cellType == CellType.Iron) return "Iron";
            if (cellType == CellType.Titan) return "Titan";
            if (cellType == CellType.Aluminium) return "aluminium";
            if (cellType == CellType.Rogalite) return "Rogalite";
            return null;
        }

        // Method DropCarriedResourceAsSceneObject: executes the DropCarriedResourceAsSceneObject workflow.
        private bool DropCarriedResourceAsSceneObject(UnitTaskState state, out Vector2Int spawnedCell)
        {
            spawnedCell = state.CurrentCell;
            if (_sceneResourceObjectService == null) return false;
            if (string.IsNullOrWhiteSpace(state.CarriedResourceId) || state.CarriedResourceAmount <= 0) return false;

            if (!TryMapResourceIdToSceneType(state.CarriedResourceId, out SceneResourceType sceneResourceType))
            {
                return false;
            }

            Vector2Int dropCell = state.CurrentCell;
            if (!_sceneResourceObjectService.TrySpawnAtCell(
                    sceneResourceType,
                    state.CarriedResourceId,
                    state.CarriedResourceAmount,
                    dropCell,
                    out _,
                    out spawnedCell))
            {
                return false;
            }

            _taskBoard.TryEnsureDroppedResourceDeliveryTask(
                spawnedCell,
                state.CarriedResourceId,
                state.CarriedResourceAmount,
                Time.frameCount);

            return true;
        }

        // Method EnsureDroppedResourceTaskAfterCompletion: executes the EnsureDroppedResourceTaskAfterCompletion workflow.
        private void EnsureDroppedResourceTaskAfterCompletion(Vector2Int cell)
        {
            if (_sceneResourceObjectService == null) return;
            if (!_sceneResourceObjectService.TryPeekResourceAtCell(cell, out string resourceId, out int amount)) return;
            if (string.IsNullOrWhiteSpace(resourceId) || amount <= 0) return;

            _taskBoard.TryEnsureDroppedResourceDeliveryTask(
                cell,
                resourceId,
                amount,
                Time.frameCount);
        }

        // Method TryMapResourceIdToSceneType: executes the TryMapResourceIdToSceneType workflow.
        private static bool TryMapResourceIdToSceneType(string resourceId, out SceneResourceType sceneResourceType)
        {
            return SceneResourceTypeExtensions.TryParseResourceId(resourceId, out sceneResourceType);
        }
    }
}

