using System;
using System.Collections.Generic;
using _Project.Scripts.Data.Construction;
using _Project.Scripts.Data.Grid;
using _Project.Scripts.Data.Tasks;
using _Project.Scripts.Systems.Pathfinding;
using _Project.Scripts.Systems.Resources;
using UnityEngine;

using _Project.Scripts.Systems.Construction;


namespace _Project.Scripts.Systems.Tasks
{
    /// <summary>
    /// Глобальный задачник мира: создает, резервирует и завершает задачи.
    /// </summary>
    public sealed class GlobalTaskBoardService
    {
        private readonly GridState _grid;
        private readonly TaskScoringService _scoring;
        private readonly bool _enableLogs;
        private readonly ResourceInventoryService _resourceInventoryService;
        private const string CABLE_BUILD_RESOURCE_ID = "Cable";
        private const int CABLE_BUILD_RESOURCE_AMOUNT = 1;
        private const int DEFAULT_CABLE_BUILD_TICKS = 1;
        private const string OXYGEN_BUILD_RESOURCE_ID = "Oxygen Pipe";
        private const int OXYGEN_BUILD_RESOURCE_AMOUNT = 1;
        private const int DEFAULT_OXYGEN_BUILD_TICKS = 1;
        private const string WATER_BUILD_RESOURCE_ID = "Water Pipe";
        private const int WATER_BUILD_RESOURCE_AMOUNT = 1;
        private const int DEFAULT_WATER_BUILD_TICKS = 1;

        private static readonly List<UnitTaskRecord> OpenTasksSnapshotBuffer = new List<UnitTaskRecord>(16);
        private static readonly List<UnitTaskRecord> OpenTasksOrderedForUnitBuffer = new List<UnitTaskRecord>(16);
        private static readonly List<UnitTaskRecord> ActiveTasksSnapshotBuffer = new List<UnitTaskRecord>(16);
        private static readonly List<Vector2Int> PendingBuildFootprintDigCellsBuffer = new List<Vector2Int>(16);

        private readonly Dictionary<int, UnitTaskRecord> _tasksById = new Dictionary<int, UnitTaskRecord>();
        private readonly Dictionary<Vector2Int, int> _taskIdByCell = new Dictionary<Vector2Int, int>();
        private readonly Dictionary<Vector2Int, int> _digTaskIdByCell = new Dictionary<Vector2Int, int>();
        private readonly Dictionary<Vector2Int, int> _cableTaskIdByCell = new Dictionary<Vector2Int, int>();
        private readonly Dictionary<Vector2Int, int> _waterTaskIdByCell = new Dictionary<Vector2Int, int>();
        private readonly Dictionary<Vector2Int, int> _oxygenTaskIdByCell = new Dictionary<Vector2Int, int>();
        private readonly Dictionary<Vector2Int, int> _lifeModuleTaskIdByCell = new Dictionary<Vector2Int, int>();
        private readonly List<int> _taskIds = new List<int>();
        private Func<Vector2Int, bool> _canActivateDigTaskForCell;

        private int _nextTaskId = 1;

        /// <summary>
        /// Создает глобальный задачник и подключает зависимости: сетку, скоринг и флаг логов.
        /// </summary>
        public GlobalTaskBoardService(
            GridState grid,
            TaskScoringService scoring,
            ResourceInventoryService resourceInventoryService,
            bool enableLogs)
        {
            _grid = grid;
            _scoring = scoring;
            _resourceInventoryService = resourceInventoryService;
            _enableLogs = enableLogs;
        }

        /// <summary>
        /// Сканирует сетку и создает задачи DigCell для всех помеченных клеток, где задачи еще нет.
        /// </summary>
        public void SyncDigTasksFromGrid(GridState grid, int currentTick)
        {
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    Cell cell = grid.GetCell(x, y);
                    if (!cell.IsDigMarked) continue;
                    if (!CellTraversalRules.IsDiggable(cell.Type)) continue;

                    TryActivateDigTaskIfReachable(new Vector2Int(x, y), currentTick);
                }
            }
        }

        /// <summary>
        /// Sets runtime reachability logic used before opening user dig tasks.
        /// </summary>
        public void SetDigTaskReachabilityEvaluator(Func<Vector2Int, bool> canActivateDigTaskForCell)
        {
            _canActivateDigTaskForCell = canActivateDigTaskForCell;
        }

        /// <summary>
        /// Creates a user dig task only when the marked cell has become reachable for at least one unit.
        /// </summary>
        public bool TryActivateDigTaskIfReachable(Vector2Int cellPos, int currentTick)
        {
            if (!_grid.IsInside(cellPos.x, cellPos.y))
            {
                return false;
            }

            if (ShipLandingZoneRules.IsInsideDigProtectionZone(_grid.Width, _grid.Height, cellPos))
            {
                Cell protectedCell = _grid.GetCell(cellPos.x, cellPos.y);
                if (protectedCell.IsDigMarked)
                {
                    protectedCell.IsDigMarked = false;
                    _grid.SetCell(cellPos.x, cellPos.y, protectedCell);
                }

                return false;
            }

            Cell cell = _grid.GetCell(cellPos.x, cellPos.y);
            if (!cell.IsDigMarked) return false;
            if (!CellTraversalRules.IsDiggable(cell.Type)) return false;
            if (_digTaskIdByCell.ContainsKey(cellPos)) return false;
            if (_canActivateDigTaskForCell == null) return false;
            if (!_canActivateDigTaskForCell(cellPos)) return false;

            return TryCreateDigTaskForCell(cellPos, currentTick);
        }

        /// <summary>
        /// Re-checks a changed cell and its orthogonal neighbors for newly reachable dig marks.
        /// </summary>
        public void TryActivateDigTasksAroundCell(Vector2Int originCell, int currentTick)
        {
            TryActivateDigTaskIfReachable(originCell, currentTick);
            TryActivateDigTaskIfReachable(originCell + Vector2Int.up, currentTick);
            TryActivateDigTaskIfReachable(originCell + Vector2Int.right, currentTick);
            TryActivateDigTaskIfReachable(originCell + Vector2Int.down, currentTick);
            TryActivateDigTaskIfReachable(originCell + Vector2Int.left, currentTick);
        }

        /// <summary>
        /// Creates a persistent cable plan marker and opens a cable task immediately only for Empty/Atmosphere cells.
        /// </summary>
        public bool TryPlanCableCell(Vector2Int targetCell, int currentTick, int buildTicks)
        {
            if (!_grid.IsInside(targetCell.x, targetCell.y))
            {
                return false;
            }

            Cell cell = _grid.GetCell(targetCell.x, targetCell.y);
            if (cell.HasCable || cell.IsCableMarked)
            {
                return false;
            }

            if (!_resourceInventoryService.TryRemove(CABLE_BUILD_RESOURCE_ID, CABLE_BUILD_RESOURCE_AMOUNT))
            {
                return false;
            }

            cell.IsCableMarked = true;
            _grid.SetCell(targetCell.x, targetCell.y, cell);
            TryActivateCableTaskIfMarkedAndBuildable(targetCell, currentTick, buildTicks);
            return true;
        }

        /// <summary>
        /// Opens a build cable task only when the planned cell has become Empty or Atmosphere.
        /// </summary>
        public bool TryActivateCableTaskIfMarkedAndBuildable(Vector2Int targetCell, int currentTick, int buildTicks = DEFAULT_CABLE_BUILD_TICKS)
        {
            if (!_grid.IsInside(targetCell.x, targetCell.y))
            {
                return false;
            }

            if (_cableTaskIdByCell.TryGetValue(targetCell, out int existingTaskId))
            {
                if (_tasksById.TryGetValue(existingTaskId, out UnitTaskRecord existingTask) && existingTask != null)
                {
                    return false;
                }

                _cableTaskIdByCell.Remove(targetCell);
            }

            Cell cell = _grid.GetCell(targetCell.x, targetCell.y);
            if (!cell.IsCableMarked || cell.HasCable || !CanBuildCableOnCellType(cell.Type))
            {
                return false;
            }

            var task = new UnitTaskRecord
            {
                TaskId = _nextTaskId++,
                TaskType = UnitTaskType.BuildCable,
                TargetCell = targetCell,
                BasePriority = TaskPriority.Normal,
                CreatedAtTick = currentTick,
                ReservedByUnitId = 0,
                ReserveTick = -1,
                Status = UnitTaskStatus.Open,
                RemainingWorkTicks = Mathf.Max(1, buildTicks)
            };

            _tasksById[task.TaskId] = task;
            _cableTaskIdByCell[targetCell] = task.TaskId;
            _taskIds.Add(task.TaskId);
            return true;
        }

        /// <summary>
        /// Re-checks a changed cell and its orthogonal neighbors for delayed cable plans.
        /// </summary>
        public void TryActivateCableTasksAroundCell(Vector2Int originCell, int currentTick, int buildTicks = DEFAULT_CABLE_BUILD_TICKS)
        {
            TryActivateCableTaskIfMarkedAndBuildable(originCell, currentTick, buildTicks);
            TryActivateCableTaskIfMarkedAndBuildable(originCell + Vector2Int.up, currentTick, buildTicks);
            TryActivateCableTaskIfMarkedAndBuildable(originCell + Vector2Int.right, currentTick, buildTicks);
            TryActivateCableTaskIfMarkedAndBuildable(originCell + Vector2Int.down, currentTick, buildTicks);
            TryActivateCableTaskIfMarkedAndBuildable(originCell + Vector2Int.left, currentTick, buildTicks);
        }

        /// <summary>
        /// Creates a persistent water plan marker and opens a task immediately only for Empty/Atmosphere cells.
        /// </summary>
        public bool TryPlanWaterCell(Vector2Int targetCell, int currentTick, int buildTicks)
        {
            if (!_grid.IsInside(targetCell.x, targetCell.y))
            {
                return false;
            }

            Cell cell = _grid.GetCell(targetCell.x, targetCell.y);
            if (cell.HasWater || cell.IsWaterMarked)
            {
                return false;
            }

            if (!_resourceInventoryService.TryRemove(WATER_BUILD_RESOURCE_ID, WATER_BUILD_RESOURCE_AMOUNT))
            {
                return false;
            }

            cell.IsWaterMarked = true;
            _grid.SetCell(targetCell.x, targetCell.y, cell);
            TryActivateWaterTaskIfMarkedAndBuildable(targetCell, currentTick, buildTicks);
            return true;
        }

        /// <summary>
        /// Opens a water task only when the planned cell has become Empty or Atmosphere.
        /// </summary>
        public bool TryActivateWaterTaskIfMarkedAndBuildable(Vector2Int targetCell, int currentTick, int buildTicks = DEFAULT_WATER_BUILD_TICKS)
        {
            if (!_grid.IsInside(targetCell.x, targetCell.y))
            {
                return false;
            }

            if (_waterTaskIdByCell.TryGetValue(targetCell, out int existingTaskId))
            {
                if (_tasksById.TryGetValue(existingTaskId, out UnitTaskRecord existingTask) && existingTask != null)
                {
                    return false;
                }

                _waterTaskIdByCell.Remove(targetCell);
            }

            Cell cell = _grid.GetCell(targetCell.x, targetCell.y);
            if (!cell.IsWaterMarked || cell.HasWater || !CanBuildPipeOnCellType(cell.Type))
            {
                return false;
            }

            var task = new UnitTaskRecord
            {
                TaskId = _nextTaskId++,
                TaskType = UnitTaskType.BuildWater,
                TargetCell = targetCell,
                BasePriority = TaskPriority.Normal,
                CreatedAtTick = currentTick,
                ReservedByUnitId = 0,
                ReserveTick = -1,
                Status = UnitTaskStatus.Open,
                RemainingWorkTicks = Mathf.Max(1, buildTicks)
            };

            _tasksById[task.TaskId] = task;
            _waterTaskIdByCell[targetCell] = task.TaskId;
            _taskIds.Add(task.TaskId);
            return true;
        }

        /// <summary>
        /// Re-checks a changed cell and its orthogonal neighbors for delayed water plans.
        /// </summary>
        public void TryActivateWaterTasksAroundCell(Vector2Int originCell, int currentTick, int buildTicks = DEFAULT_WATER_BUILD_TICKS)
        {
            TryActivateWaterTaskIfMarkedAndBuildable(originCell, currentTick, buildTicks);
            TryActivateWaterTaskIfMarkedAndBuildable(originCell + Vector2Int.up, currentTick, buildTicks);
            TryActivateWaterTaskIfMarkedAndBuildable(originCell + Vector2Int.right, currentTick, buildTicks);
            TryActivateWaterTaskIfMarkedAndBuildable(originCell + Vector2Int.down, currentTick, buildTicks);
            TryActivateWaterTaskIfMarkedAndBuildable(originCell + Vector2Int.left, currentTick, buildTicks);
        }

        /// <summary>
        /// Creates a persistent oxygen plan marker and opens a task immediately only for Empty/Atmosphere cells.
        /// </summary>
        public bool TryPlanOxygenCell(Vector2Int targetCell, int currentTick, int buildTicks)
        {
            if (!_grid.IsInside(targetCell.x, targetCell.y))
            {
                return false;
            }

            Cell cell = _grid.GetCell(targetCell.x, targetCell.y);
            if (cell.HasOxygen || cell.IsOxygenMarked)
            {
                return false;
            }

            if (!_resourceInventoryService.TryRemove(OXYGEN_BUILD_RESOURCE_ID, OXYGEN_BUILD_RESOURCE_AMOUNT))
            {
                return false;
            }

            cell.IsOxygenMarked = true;
            _grid.SetCell(targetCell.x, targetCell.y, cell);
            TryActivateOxygenTaskIfMarkedAndBuildable(targetCell, currentTick, buildTicks);
            return true;
        }

        /// <summary>
        /// Opens an oxygen task only when the planned cell has become Empty or Atmosphere.
        /// </summary>
        public bool TryActivateOxygenTaskIfMarkedAndBuildable(Vector2Int targetCell, int currentTick, int buildTicks = DEFAULT_OXYGEN_BUILD_TICKS)
        {
            if (!_grid.IsInside(targetCell.x, targetCell.y))
            {
                return false;
            }

            if (_oxygenTaskIdByCell.TryGetValue(targetCell, out int existingTaskId))
            {
                if (_tasksById.TryGetValue(existingTaskId, out UnitTaskRecord existingTask) && existingTask != null)
                {
                    return false;
                }

                _oxygenTaskIdByCell.Remove(targetCell);
            }

            Cell cell = _grid.GetCell(targetCell.x, targetCell.y);
            if (!cell.IsOxygenMarked || cell.HasOxygen || !CanBuildPipeOnCellType(cell.Type))
            {
                return false;
            }

            var task = new UnitTaskRecord
            {
                TaskId = _nextTaskId++,
                TaskType = UnitTaskType.BuildOxygen,
                TargetCell = targetCell,
                BasePriority = TaskPriority.Normal,
                CreatedAtTick = currentTick,
                ReservedByUnitId = 0,
                ReserveTick = -1,
                Status = UnitTaskStatus.Open,
                RemainingWorkTicks = Mathf.Max(1, buildTicks)
            };

            _tasksById[task.TaskId] = task;
            _oxygenTaskIdByCell[targetCell] = task.TaskId;
            _taskIds.Add(task.TaskId);
            return true;
        }

        /// <summary>
        /// Re-checks a changed cell and its orthogonal neighbors for delayed oxygen plans.
        /// </summary>
        public void TryActivateOxygenTasksAroundCell(Vector2Int originCell, int currentTick, int buildTicks = DEFAULT_OXYGEN_BUILD_TICKS)
        {
            TryActivateOxygenTaskIfMarkedAndBuildable(originCell, currentTick, buildTicks);
            TryActivateOxygenTaskIfMarkedAndBuildable(originCell + Vector2Int.up, currentTick, buildTicks);
            TryActivateOxygenTaskIfMarkedAndBuildable(originCell + Vector2Int.right, currentTick, buildTicks);
            TryActivateOxygenTaskIfMarkedAndBuildable(originCell + Vector2Int.down, currentTick, buildTicks);
            TryActivateOxygenTaskIfMarkedAndBuildable(originCell + Vector2Int.left, currentTick, buildTicks);
        }

        /// <summary>
        /// Re-checks every cell in a build footprint and each of its orthogonal neighbors.
        /// </summary>
        public void TryActivateDigTasksAroundBuildPayload(BuildTaskPayload payload, int currentTick)
        {
            if (payload == null || payload.BuildingDef == null)
            {
                return;
            }

            int width = payload.IsRotated ? payload.BuildingDef.Height : payload.BuildingDef.Width;
            int height = payload.IsRotated ? payload.BuildingDef.Width : payload.BuildingDef.Height;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    TryActivateDigTasksAroundCell(new Vector2Int(payload.AnchorCell.x + x, payload.AnchorCell.y + y), currentTick);
                }
            }
        }

        /// <summary>
        /// Точечно создаёт DigCell задачу для одной клетки, если она помечена под копку и задачи ещё нет.
        /// </summary>
        public bool TryCreateDigTaskForCell(Vector2Int cellPos, int currentTick)
        {
            if (!_grid.IsInside(cellPos.x, cellPos.y))
            {
                return false;
            }

            if (ShipLandingZoneRules.IsInsideDigProtectionZone(_grid.Width, _grid.Height, cellPos))
            {
                Cell protectedCell = _grid.GetCell(cellPos.x, cellPos.y);
                if (protectedCell.IsDigMarked)
                {
                    protectedCell.IsDigMarked = false;
                    _grid.SetCell(cellPos.x, cellPos.y, protectedCell);
                }

                return false;
            }

            Cell cell = _grid.GetCell(cellPos.x, cellPos.y);
            if (!cell.IsDigMarked) return false;
            if (!CellTraversalRules.IsDiggable(cell.Type)) return false;
            if (_digTaskIdByCell.ContainsKey(cellPos)) return false;

            var task = new UnitTaskRecord
            {
                TaskId = _nextTaskId++,
                TaskType = UnitTaskType.DigCell,
                TargetCell = cellPos,
                BasePriority = TaskPriority.Normal,
                CreatedAtTick = currentTick,
                ReservedByUnitId = 0,
                ReserveTick = -1,
                Status = UnitTaskStatus.Open
            };

            _tasksById[task.TaskId] = task;
            _digTaskIdByCell[cellPos] = task.TaskId;
            if (!_taskIdByCell.ContainsKey(cellPos))
            {
                _taskIdByCell[cellPos] = task.TaskId;
            }
            _taskIds.Add(task.TaskId);

            if (_enableLogs)
            {
            // Debug.Log($"[TaskBoard] Created DigTask id={task.TaskId} cell=({cellPos.x},{cellPos.y}) priority={task.BasePriority}");
            }

            return true;
        }
        /// <summary>
        /// Возвращает снимок открытых задач (только те, что ещё в очереди).
        /// </summary>
        public IReadOnlyList<UnitTaskRecord> GetOpenTasksSnapshot()
        {
            OpenTasksSnapshotBuffer.Clear();

            for (int i = 0; i < _taskIds.Count; i++)
            {
                int id = _taskIds[i];
                UnitTaskRecord task = _tasksById[id];
                if (task.Status != UnitTaskStatus.Open) continue;

                OpenTasksSnapshotBuffer.Add(task);
            }

            return OpenTasksSnapshotBuffer;
        }

        /// <summary>
        /// Returns open tasks sorted by board score for the specified unit position.
        /// Unit-specific refusal logic stays in the acquisition service.
        /// </summary>
        public IReadOnlyList<UnitTaskRecord> GetOpenTasksOrderedForUnit(Vector2Int unitCell, int currentTick)
        {
            OpenTasksOrderedForUnitBuffer.Clear();

            for (int i = 0; i < _taskIds.Count; i++)
            {
                int id = _taskIds[i];
                UnitTaskRecord candidate = _tasksById[id];
                if (candidate.Status != UnitTaskStatus.Open) continue;
                if (!_scoring.IsVisible(unitCell, candidate.TargetCell)) continue;
                if (!IsTaskStillValid(candidate)) continue;

                OpenTasksOrderedForUnitBuffer.Add(candidate);
            }

            OpenTasksOrderedForUnitBuffer.Sort((left, right) =>
            {
                float rightScore = _scoring.CalculateScore(right, unitCell, currentTick);
                float leftScore = _scoring.CalculateScore(left, unitCell, currentTick);
                return rightScore.CompareTo(leftScore);
            });

            return OpenTasksOrderedForUnitBuffer;
        }

        /// <summary>
        /// Возвращает снимок всех задач, кроме завершённых и проваленных.
        /// </summary>
        public IReadOnlyList<UnitTaskRecord> GetActiveTasksSnapshot()
        {
            ActiveTasksSnapshotBuffer.Clear();

            for (int i = 0; i < _taskIds.Count; i++)
            {
                int id = _taskIds[i];
                UnitTaskRecord task = _tasksById[id];
                if (task.Status == UnitTaskStatus.Completed || task.Status == UnitTaskStatus.Failed) continue;
                ActiveTasksSnapshotBuffer.Add(task);
            }

            return ActiveTasksSnapshotBuffer;
        }
        /// <summary>
        /// Ищет лучшую доступную задачу для юнита, резервирует ее за ним и возвращает наружу.
        /// </summary>
        /// <summary>
        /// Returns how many resource units are already claimed by unpaid BuildObject plans.
        /// </summary>
        public int GetReservedUnpaidBuildResourceAmount(string resourceId)
        {
            if (string.IsNullOrWhiteSpace(resourceId))
            {
                return 0;
            }

            int reservedAmount = 0;
            for (int i = 0; i < _taskIds.Count; i++)
            {
                int taskId = _taskIds[i];
                UnitTaskRecord task = _tasksById[taskId];
                if (task == null || task.TaskType != UnitTaskType.BuildObject)
                {
                    continue;
                }

                if (task.Status == UnitTaskStatus.Completed || task.Status == UnitTaskStatus.Failed)
                {
                    continue;
                }

                BuildTaskPayload payload = task.BuildPayload;
                if (payload == null || payload.IsBuildCostPaid || payload.BuildingDef == null || payload.BuildingDef.CostItems == null)
                {
                    continue;
                }

                for (int costIndex = 0; costIndex < payload.BuildingDef.CostItems.Length; costIndex++)
                {
                    BuildCostItem item = payload.BuildingDef.CostItems[costIndex];
                    if (!string.Equals(item.ResourceId, resourceId, System.StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (item.Amount > 0)
                    {
                        reservedAmount += item.Amount;
                    }
                }
            }

            return reservedAmount;
        }

        public bool TryReserveBestTaskForUnit(
            int unitId,
            Vector2Int unitCell,
            int currentTick,
            Func<UnitTaskRecord, bool> isTaskAllowed,
            Func<UnitTaskRecord, bool> isReachable,
            out UnitTaskRecord task)
        {
            task = null;
            float bestScore = float.NegativeInfinity;

            for (int i = 0; i < _taskIds.Count; i++)
            {
                int id = _taskIds[i];
                UnitTaskRecord candidate = _tasksById[id];
                if (candidate.Status != UnitTaskStatus.Open)
                {
                    LogBuildSkip(unitId, candidate, "status is not Open");
                    continue;
                }
                if (!isTaskAllowed(candidate))
                {
                    LogBuildSkip(unitId, candidate, "failed availability filters");
                    continue;
                }
                if (!_scoring.IsVisible(unitCell, candidate.TargetCell))
                {
                    LogBuildSkip(unitId, candidate, "target is outside visible radius");
                    continue;
                }
                if (!IsTaskStillValid(candidate))
                {
                    LogBuildSkip(unitId, candidate, "task is invalid for current world state");
                    continue;
                }
                if (!isReachable(candidate))
                {
                    LogBuildSkip(unitId, candidate, "no reachable work cell/path");
                    continue;
                }

                float score = _scoring.CalculateScore(candidate, unitCell, currentTick);
                if (score <= bestScore) continue;

                bestScore = score;
                task = candidate;
            }

            if (task == null) return false;

            if (_enableLogs && task.TaskType != UnitTaskType.BuildObject)
            {
                LogBestBuildNotSelected(unitId, unitCell, currentTick, bestScore, task);
            }

            task.Status = UnitTaskStatus.Reserved;
            task.ReservedByUnitId = unitId;
            task.ReserveTick = currentTick;

            SetCellReservation(task.TargetCell, unitId);

            if (_enableLogs)
            {
            // Debug.Log($"[TaskBoard] Reserved task={task.TaskId} by unit={unitId}");
            }

            return true;
        }

        public int CreateBuildTask(Vector2Int targetCell, int currentTick, BuildTaskPayload payload)
        {
            var task = new UnitTaskRecord
            {
                TaskId = _nextTaskId++,
                TaskType = UnitTaskType.BuildObject,
                TargetCell = targetCell,
                BasePriority = TaskPriority.Normal,
                CreatedAtTick = currentTick,
                ReservedByUnitId = 0,
                ReserveTick = -1,
                Status = UnitTaskStatus.Open,
                BuildPayload = payload,
                ParentBuildTaskId = 0
            };

            _tasksById[task.TaskId] = task;
            _taskIdByCell[targetCell] = task.TaskId;
            _taskIds.Add(task.TaskId);
            return task.TaskId;
        }

        public void CreateDestroyTask(Vector2Int targetCell, int currentTick, BuildTaskPayload payload)
        {
            var task = new UnitTaskRecord
            {
                TaskId = _nextTaskId++,
                TaskType = UnitTaskType.DestroyObject,
                TargetCell = targetCell,
                BasePriority = TaskPriority.Normal,
                CreatedAtTick = currentTick,
                ReservedByUnitId = 0,
                ReserveTick = -1,
                Status = UnitTaskStatus.Open,
                BuildPayload = payload,
                ParentBuildTaskId = 0
            };

            _tasksById[task.TaskId] = task;
            _taskIdByCell[targetCell] = task.TaskId;
            _taskIds.Add(task.TaskId);
        }

        public int CreateBuildLifeModuleTask(Vector2Int targetCell, int currentTick, LifeModuleTaskPayload payload)
        {
            if (payload == null || payload.OccupiedCells == null || payload.OccupiedCells.Length == 0)
            {
                return 0;
            }

            for (int i = 0; i < payload.OccupiedCells.Length; i++)
            {
                Vector2Int occupiedCell = payload.OccupiedCells[i];
                if (_lifeModuleTaskIdByCell.ContainsKey(occupiedCell))
                {
                    return 0;
                }
            }

            var task = new UnitTaskRecord
            {
                TaskId = _nextTaskId++,
                TaskType = UnitTaskType.BuildLifeModule,
                TargetCell = targetCell,
                BasePriority = TaskPriority.Normal,
                CreatedAtTick = currentTick,
                ReservedByUnitId = 0,
                ReserveTick = -1,
                Status = UnitTaskStatus.Open,
                LifeModulePayload = payload,
                RemainingWorkTicks = Mathf.Max(1, payload.RemainingBuildTicks)
            };

            payload.GroupId = task.TaskId;
            _tasksById[task.TaskId] = task;
            _taskIds.Add(task.TaskId);

            for (int i = 0; i < payload.OccupiedCells.Length; i++)
            {
                _lifeModuleTaskIdByCell[payload.OccupiedCells[i]] = task.TaskId;
            }

            int createdDigSubtasks = EnsureLifeModuleDigTasks(payload, currentTick);
            int existingPendingDigInFootprint = CountPendingDigTasksInLifeModuleFootprint(payload) - createdDigSubtasks;
            if (existingPendingDigInFootprint < 0)
            {
                existingPendingDigInFootprint = 0;
            }

            payload.RemainingClearSubtasks = createdDigSubtasks + existingPendingDigInFootprint;
            payload.IsExcavatingBeforeBuild = payload.RemainingClearSubtasks > 0;

            return task.TaskId;
        }

        /// <summary>
        /// Создаёт задачу прокладки кабеля в указанной клетке.
        /// </summary>
        public bool TryCreateBuildCableTask(Vector2Int targetCell, int currentTick, int buildTicks)
        {
            return TryPlanCableCell(targetCell, currentTick, buildTicks);
        }

        /// <summary>
        /// Returns how many additional cable build cells can still be planned from the shared inventory.
        /// </summary>
        public int GetAvailableCableBuildPlanCount()
        {
            if (_resourceInventoryService == null)
            {
                return 0;
            }

            return Mathf.Max(0, _resourceInventoryService.GetAmount(CABLE_BUILD_RESOURCE_ID) / CABLE_BUILD_RESOURCE_AMOUNT);
        }
        /// <summary>
        /// Возвращает количество дополнительных ячеек кабеля, которые можно запланировать из общего хранилища.
        /// </summary>
        /// <summary>
        /// Пытается создать задачу доставки выпавшего ресурса из storage к точке назначения.
        /// </summary>
        public bool TryCreateDroppedResourceDeliveryTask(
            Vector2Int targetCell,
            string resourceId,
            int amount,
            int currentTick)
        {
            if (!_grid.IsInside(targetCell.x, targetCell.y))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(resourceId) || amount <= 0)
            {
                return false;
            }

            if (_taskIdByCell.ContainsKey(targetCell))
            {
                return false;
            }

            var task = new UnitTaskRecord
            {
                TaskId = _nextTaskId++,
                TaskType = UnitTaskType.DeliverDroppedResource,
                TargetCell = targetCell,
                BasePriority = TaskPriority.Low,
                CreatedAtTick = currentTick,
                ReservedByUnitId = 0,
                ReserveTick = -1,
                Status = UnitTaskStatus.Open,
                ResourceDropId = resourceId,
                ResourceDropAmount = amount
            };

            _tasksById[task.TaskId] = task;
            _taskIdByCell[targetCell] = task.TaskId;
            _taskIds.Add(task.TaskId);
            return true;
        }

public bool TryEnsureDroppedResourceDeliveryTask(
    Vector2Int targetCell,
    string resourceId,
    int amount,
    int currentTick)
{
    if (!_grid.IsInside(targetCell.x, targetCell.y))
    {
        return false;
    }

    if (string.IsNullOrWhiteSpace(resourceId) || amount <= 0)
    {
        return false;
    }

    if (_taskIdByCell.TryGetValue(targetCell, out int existingTaskId))
    {
        if (!_tasksById.TryGetValue(existingTaskId, out UnitTaskRecord existingTask) || existingTask == null)
        {
            _taskIdByCell.Remove(targetCell);
            return TryCreateDroppedResourceDeliveryTask(targetCell, resourceId, amount, currentTick);
        }

        if (existingTask.TaskType != UnitTaskType.DeliverDroppedResource)
        {
            return false;
        }

        if (existingTask.Status == UnitTaskStatus.Completed || existingTask.Status == UnitTaskStatus.Failed)
        {
            existingTask.Status = UnitTaskStatus.Open;
            existingTask.ReservedByUnitId = 0;
            existingTask.ReserveTick = -1;
            SetCellReservation(targetCell, 0);
        }

        existingTask.ResourceDropId = resourceId;
        existingTask.ResourceDropAmount = amount;
        existingTask.CreatedAtTick = currentTick;
        existingTask.BasePriority = TaskPriority.Low;
        return true;
    }

    return TryCreateDroppedResourceDeliveryTask(targetCell, resourceId, amount, currentTick);
}

        public bool TryCreateDestroyCableTask(Vector2Int targetCell, int currentTick, int destroyTicks)
        {
            if (!_grid.IsInside(targetCell.x, targetCell.y))
            {
                return false;
            }

            if (_cableTaskIdByCell.ContainsKey(targetCell))
            {
                return false;
            }

            Cell cell = _grid.GetCell(targetCell.x, targetCell.y);
            if (!cell.HasCable)
            {
                return false;
            }

            var task = new UnitTaskRecord
            {
                TaskId = _nextTaskId++,
                TaskType = UnitTaskType.DestroyCable,
                TargetCell = targetCell,
                BasePriority = TaskPriority.Normal,
                CreatedAtTick = currentTick,
                ReservedByUnitId = 0,
                ReserveTick = -1,
                Status = UnitTaskStatus.Open,
                RemainingWorkTicks = Mathf.Max(1, destroyTicks)
            };

            _tasksById[task.TaskId] = task;
            _cableTaskIdByCell[targetCell] = task.TaskId;
            _taskIds.Add(task.TaskId);
            return true;
        }

        public bool TryCreateBuildWaterTask(Vector2Int targetCell, int currentTick, int buildTicks)
        {
            return TryPlanWaterCell(targetCell, currentTick, buildTicks);
        }

        public int GetAvailableWaterBuildPlanCount()
        {
            if (_resourceInventoryService == null)
            {
                return 0;
            }

            return Mathf.Max(0, _resourceInventoryService.GetAmount(WATER_BUILD_RESOURCE_ID) / WATER_BUILD_RESOURCE_AMOUNT);
        }

        public bool TryCreateDestroyWaterTask(Vector2Int targetCell, int currentTick, int destroyTicks)
        {
            if (!_grid.IsInside(targetCell.x, targetCell.y))
            {
                return false;
            }

            if (_waterTaskIdByCell.ContainsKey(targetCell))
            {
                return false;
            }

            Cell cell = _grid.GetCell(targetCell.x, targetCell.y);
            if (!cell.HasWater)
            {
                return false;
            }

            var task = new UnitTaskRecord
            {
                TaskId = _nextTaskId++,
                TaskType = UnitTaskType.DestroyWater,
                TargetCell = targetCell,
                BasePriority = TaskPriority.Normal,
                CreatedAtTick = currentTick,
                ReservedByUnitId = 0,
                ReserveTick = -1,
                Status = UnitTaskStatus.Open,
                RemainingWorkTicks = Mathf.Max(1, destroyTicks)
            };

            _tasksById[task.TaskId] = task;
            _waterTaskIdByCell[targetCell] = task.TaskId;
            _taskIds.Add(task.TaskId);
            return true;
        }

        public bool TryCreateBuildOxygenTask(Vector2Int targetCell, int currentTick, int buildTicks)
        {
            return TryPlanOxygenCell(targetCell, currentTick, buildTicks);
        }

        public int GetAvailableOxygenBuildPlanCount()
        {
            if (_resourceInventoryService == null)
            {
                return 0;
            }

            return Mathf.Max(0, _resourceInventoryService.GetAmount(OXYGEN_BUILD_RESOURCE_ID) / OXYGEN_BUILD_RESOURCE_AMOUNT);
        }

        public bool TryCreateDestroyOxygenTask(Vector2Int targetCell, int currentTick, int destroyTicks)
        {
            if (!_grid.IsInside(targetCell.x, targetCell.y))
            {
                return false;
            }

            if (_oxygenTaskIdByCell.ContainsKey(targetCell))
            {
                return false;
            }

            Cell cell = _grid.GetCell(targetCell.x, targetCell.y);
            if (!cell.HasOxygen)
            {
                return false;
            }

            var task = new UnitTaskRecord
            {
                TaskId = _nextTaskId++,
                TaskType = UnitTaskType.DestroyOxygen,
                TargetCell = targetCell,
                BasePriority = TaskPriority.Normal,
                CreatedAtTick = currentTick,
                ReservedByUnitId = 0,
                ReserveTick = -1,
                Status = UnitTaskStatus.Open,
                RemainingWorkTicks = Mathf.Max(1, destroyTicks)
            };

            _tasksById[task.TaskId] = task;
            _oxygenTaskIdByCell[targetCell] = task.TaskId;
            _taskIds.Add(task.TaskId);
            return true;
        }

        /// <summary>
        /// Создает дочерние задачи очистки для всех diggable-клеток footprint родительской стройки.
        /// </summary>
        public int CreateBuildClearSubtasks(int buildTaskId, BuildTaskPayload payload, int currentTick)
        {
            if (payload == null || payload.BuildingDef == null) return 0;

            int width = payload.IsRotated ? payload.BuildingDef.Height : payload.BuildingDef.Width;
            int height = payload.IsRotated ? payload.BuildingDef.Width : payload.BuildingDef.Height;
            int created = 0;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int gx = payload.AnchorCell.x + x;
                    int gy = payload.AnchorCell.y + y;
                    if (!_grid.IsInside(gx, gy)) continue;
                    if (ShipLandingZoneRules.IsInsideDigProtectionZone(_grid.Width, _grid.Height, gx, gy)) continue;

                    Vector2Int cellPos = new Vector2Int(gx, gy);
                    Cell cell = _grid.GetCell(gx, gy);
                    if (!CellTraversalRules.IsDiggable(cell.Type)) continue;
                    if (_digTaskIdByCell.ContainsKey(cellPos)) continue;

                    cell.IsDigMarked = true;
                    _grid.SetCell(gx, gy, cell);

                    var task = new UnitTaskRecord
                    {
                        TaskId = _nextTaskId++,
                        TaskType = UnitTaskType.ClearBuildCell,
                        TargetCell = cellPos,
                        BasePriority = TaskPriority.High,
                        CreatedAtTick = currentTick,
                        ReservedByUnitId = 0,
                        ReserveTick = -1,
                        Status = UnitTaskStatus.Open,
                        BuildPayload = null,
                        ParentBuildTaskId = buildTaskId
                    };

                    _tasksById[task.TaskId] = task;
                    _digTaskIdByCell[cellPos] = task.TaskId;
                    if (!_taskIdByCell.ContainsKey(cellPos))
                    {
                        _taskIdByCell[cellPos] = task.TaskId;
                    }
                    _taskIds.Add(task.TaskId);
                    created++;
                }
            }

            return created;
        }

        /// <summary>
        /// Считает незавершенные DigCell/ClearBuildCell задачи внутри footprint указанной стройки.
        /// </summary>
        public int CountPendingDigTasksInBuildFootprint(BuildTaskPayload payload)
        {
            if (payload == null || payload.BuildingDef == null) return 0;

            int width = payload.IsRotated ? payload.BuildingDef.Height : payload.BuildingDef.Width;
            int height = payload.IsRotated ? payload.BuildingDef.Width : payload.BuildingDef.Height;
            int pending = 0;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Vector2Int cellPos = new Vector2Int(payload.AnchorCell.x + x, payload.AnchorCell.y + y);
                    if (!_digTaskIdByCell.TryGetValue(cellPos, out int taskId)) continue;
                    if (!_tasksById.TryGetValue(taskId, out UnitTaskRecord task)) continue;
                    if (!IsPendingDigLikeTask(task))
                    {
                        CleanupInvalidDigLikeTask(task);
                        continue;
                    }
                    pending++;
                }
            }

            return pending;
        }

        /// <summary>
        /// Возвращает координаты клеток footprint, где ещё есть незавершённые dig/clear задачи.
        /// </summary>
        public List<Vector2Int> GetPendingDigCellsInBuildFootprint(BuildTaskPayload payload)
        {
            PendingBuildFootprintDigCellsBuffer.Clear();
            if (payload == null || payload.BuildingDef == null) return PendingBuildFootprintDigCellsBuffer;

            int width = payload.IsRotated ? payload.BuildingDef.Height : payload.BuildingDef.Width;
            int height = payload.IsRotated ? payload.BuildingDef.Width : payload.BuildingDef.Height;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Vector2Int cellPos = new Vector2Int(payload.AnchorCell.x + x, payload.AnchorCell.y + y);
                    if (!_digTaskIdByCell.TryGetValue(cellPos, out int taskId)) continue;
                    if (!_tasksById.TryGetValue(taskId, out UnitTaskRecord task)) continue;
                    if (!IsPendingDigLikeTask(task))
                    {
                        CleanupInvalidDigLikeTask(task);
                        continue;
                    }
                    PendingBuildFootprintDigCellsBuffer.Add(cellPos);
                }
            }

            return PendingBuildFootprintDigCellsBuffer;
        }

        public int CountPendingDigTasksInLifeModuleFootprint(LifeModuleTaskPayload payload)
        {
            if (payload?.OccupiedCells == null) return 0;

            int pending = 0;
            for (int i = 0; i < payload.OccupiedCells.Length; i++)
            {
                Vector2Int cellPos = payload.OccupiedCells[i];
                if (!_digTaskIdByCell.TryGetValue(cellPos, out int taskId)) continue;
                if (!_tasksById.TryGetValue(taskId, out UnitTaskRecord task)) continue;
                if (!IsPendingDigLikeTask(task))
                {
                    CleanupInvalidDigLikeTask(task);
                    continue;
                }

                pending++;
            }

            return pending;
        }

        public List<Vector2Int> GetPendingDigCellsInLifeModuleFootprint(LifeModuleTaskPayload payload)
        {
            var result = new List<Vector2Int>();
            if (payload?.OccupiedCells == null) return result;

            for (int i = 0; i < payload.OccupiedCells.Length; i++)
            {
                Vector2Int cellPos = payload.OccupiedCells[i];
                if (!_digTaskIdByCell.TryGetValue(cellPos, out int taskId)) continue;
                if (!_tasksById.TryGetValue(taskId, out UnitTaskRecord task)) continue;
                if (!IsPendingDigLikeTask(task))
                {
                    CleanupInvalidDigLikeTask(task);
                    continue;
                }

                result.Add(cellPos);
            }

            return result;
        }

        private bool IsPendingDigLikeTask(UnitTaskRecord task)
        {
            if (task == null) return false;
            if (task.Status == UnitTaskStatus.Completed || task.Status == UnitTaskStatus.Failed) return false;
            if (task.TaskType != UnitTaskType.DigCell && task.TaskType != UnitTaskType.ClearBuildCell) return false;
            return IsTaskStillValid(task);
        }

        private void CleanupInvalidDigLikeTask(UnitTaskRecord task)
        {
            if (task == null) return;
            if (task.TaskType != UnitTaskType.DigCell && task.TaskType != UnitTaskType.ClearBuildCell) return;
            if (task.Status == UnitTaskStatus.Completed || task.Status == UnitTaskStatus.Failed) return;

            FailTask(task);
            RemoveTaskCellLookup(task);
        }

        /// <summary>
        /// Уменьшает счетчик дочерних подзадач очистки у родительской стройки.
        /// </summary>
        public void NotifyBuildClearSubtaskCompleted(UnitTaskRecord clearTask)
        {
            if (clearTask == null) return;
            if (clearTask.TaskType != UnitTaskType.ClearBuildCell) return;
            if (clearTask.ParentBuildTaskId == 0) return;
            if (!_tasksById.TryGetValue(clearTask.ParentBuildTaskId, out UnitTaskRecord parentTask)) return;
            if (parentTask.TaskType != UnitTaskType.BuildObject) return;
            if (parentTask.BuildPayload == null) return;

            if (parentTask.BuildPayload.RemainingClearSubtasks > 0)
            {
                parentTask.BuildPayload.RemainingClearSubtasks--;
            }

            if (parentTask.BuildPayload.RemainingClearSubtasks <= 0)
            {
                parentTask.BuildPayload.RemainingClearSubtasks = 0;
                parentTask.BuildPayload.IsExcavatingBeforeBuild = false;
            }
        }


        /// <summary>
        /// Проверяет валидность задачи в зависимости от её типа.
        /// Для копки нужна пометка и diggable-клетка.
        /// Для строительства нужна валидная дефиниция и незавершённый прогресс.
        /// </summary>
        private bool IsTaskStillValid(UnitTaskRecord task)
        {
            if (!_grid.IsInside(task.TargetCell.x, task.TargetCell.y))
            {
                return false;
            }

            if (task.TaskType == UnitTaskType.DigCell)
            {
                if (ShipLandingZoneRules.IsInsideDigProtectionZone(_grid.Width, _grid.Height, task.TargetCell))
                {
                    return false;
                }

                Cell cell = _grid.GetCell(task.TargetCell.x, task.TargetCell.y);
                if (!cell.IsDigMarked) return false;
                return CellTraversalRules.IsDiggable(cell.Type);
            }

            if (task.TaskType == UnitTaskType.BuildObject)
            {
                if (task.BuildPayload == null) return false;
                if (task.BuildPayload.BuildingDef == null) return false;
                return task.BuildPayload.RemainingBuildTicks > 0;
            }

            if (task.TaskType == UnitTaskType.DestroyObject)
            {
                if (task.BuildPayload == null) return false;
                if (task.BuildPayload.BuildingDef == null) return false;
                return task.BuildPayload.RemainingBuildTicks > 0;
            }

            if (task.TaskType == UnitTaskType.ClearBuildCell)
            {
                if (ShipLandingZoneRules.IsInsideDigProtectionZone(_grid.Width, _grid.Height, task.TargetCell))
                {
                    return false;
                }

                Cell cell = _grid.GetCell(task.TargetCell.x, task.TargetCell.y);
                if (!cell.IsDigMarked) return false;
                if (!CellTraversalRules.IsDiggable(cell.Type)) return false;
                if (task.ParentBuildTaskId == 0) return false;
                if (!_tasksById.TryGetValue(task.ParentBuildTaskId, out UnitTaskRecord parentTask)) return false;
                if (parentTask.Status == UnitTaskStatus.Completed || parentTask.Status == UnitTaskStatus.Failed) return false;
                return parentTask.TaskType == UnitTaskType.BuildObject;
            }


            if (task.TaskType == UnitTaskType.DestroyCable)
            {
                Cell cell = _grid.GetCell(task.TargetCell.x, task.TargetCell.y);
                if (!cell.HasCable) return false;
                return task.RemainingWorkTicks > 0;
            }

            if (task.TaskType == UnitTaskType.BuildCable)
            {
                Cell cell = _grid.GetCell(task.TargetCell.x, task.TargetCell.y);
                if (cell.HasCable) return false;
                if (!cell.IsCableMarked) return false;
                if (!CanBuildCableOnCellType(cell.Type)) return false;
                return task.RemainingWorkTicks > 0;
            }

            if (task.TaskType == UnitTaskType.BuildLifeModule)
            {
                if (task.LifeModulePayload == null || task.LifeModulePayload.OccupiedCells == null || task.LifeModulePayload.Parts == null)
                {
                    return false;
                }

                return task.RemainingWorkTicks > 0;
            }

            if (task.TaskType == UnitTaskType.BuildWater)
            {
                Cell cell = _grid.GetCell(task.TargetCell.x, task.TargetCell.y);
                if (cell.HasWater) return false;
                if (!cell.IsWaterMarked) return false;
                if (!CanBuildPipeOnCellType(cell.Type)) return false;
                return task.RemainingWorkTicks > 0;
            }

            if (task.TaskType == UnitTaskType.DestroyWater)
            {
                Cell cell = _grid.GetCell(task.TargetCell.x, task.TargetCell.y);
                if (!cell.HasWater) return false;
                return task.RemainingWorkTicks > 0;
            }

            if (task.TaskType == UnitTaskType.BuildOxygen)
            {
                Cell cell = _grid.GetCell(task.TargetCell.x, task.TargetCell.y);
                if (cell.HasOxygen) return false;
                if (!cell.IsOxygenMarked) return false;
                if (!CanBuildPipeOnCellType(cell.Type)) return false;
                return task.RemainingWorkTicks > 0;
            }

            if (task.TaskType == UnitTaskType.DestroyOxygen)
            {
                Cell cell = _grid.GetCell(task.TargetCell.x, task.TargetCell.y);
                if (!cell.HasOxygen) return false;
                return task.RemainingWorkTicks > 0;
            }

            if (task.TaskType == UnitTaskType.DeliverDroppedResource)
            {
                return !string.IsNullOrWhiteSpace(task.ResourceDropId) && task.ResourceDropAmount > 0;
            }

            return true;
        }

        private void LogBuildSkip(int unitId, UnitTaskRecord candidate, string reason)
        {
            if (!_enableLogs) return;
            if (candidate == null || candidate.TaskType != UnitTaskType.BuildObject) return;

                //   Debug.Log($"[TaskBoard][BuildDebug] unit={unitId} buildTask={candidate.TaskId} skip: {reason}");
        }

        private void LogBestBuildNotSelected(int unitId, Vector2Int unitCell, int currentTick, float selectedScore, UnitTaskRecord selectedTask)
        {
            for (int i = 0; i < _taskIds.Count; i++)
            {
                int id = _taskIds[i];
                UnitTaskRecord candidate = _tasksById[id];
                if (candidate.TaskType != UnitTaskType.BuildObject) continue;
                if (candidate.Status != UnitTaskStatus.Open) continue;
                if (!_scoring.IsVisible(unitCell, candidate.TargetCell)) continue;
                if (!IsTaskStillValid(candidate)) continue;

                float buildScore = _scoring.CalculateScore(candidate, unitCell, currentTick);
                if (buildScore <= selectedScore) continue;

            // Debug.Log($"[TaskBoard][BuildDebug] unit={unitId} buildTask={candidate.TaskId} score={buildScore:0.###} lost to selected task={selectedTask.TaskId} score={selectedScore:0.###}");
                return;
            }
        }

        /// <summary>
        /// Пытается зарезервировать конкретную открытую задачу по id.
        /// </summary>
        public bool TryReserveTaskByIdForUnit(
            int taskId,
            int unitId,
            int currentTick,
            Func<UnitTaskRecord, bool> isTaskAllowed,
            Func<UnitTaskRecord, bool> isReachable,
            out UnitTaskRecord task)
        {
            task = null;
            if (!_tasksById.TryGetValue(taskId, out UnitTaskRecord candidate)) return false;
            if (candidate.Status != UnitTaskStatus.Open) return false;
            if (!isTaskAllowed(candidate)) return false;
            if (!IsTaskStillValid(candidate)) return false;
            if (!isReachable(candidate)) return false;

            candidate.Status = UnitTaskStatus.Reserved;
            candidate.ReservedByUnitId = unitId;
            candidate.ReserveTick = currentTick;
            SetCellReservation(candidate.TargetCell, unitId);
            task = candidate;
            return true;
        }


        /// <summary>
        /// Отменяет задачу по клетке и возвращает payload, если это стройка.
        /// </summary>
        public bool CancelTaskByCell(
            Vector2Int targetCell,
            out BuildTaskPayload cancelledBuildPayload,
            out UnitTaskType cancelledTaskType)
        {
            cancelledBuildPayload = null;
            cancelledTaskType = default;

            if (!_taskIdByCell.TryGetValue(targetCell, out int taskId))
            {
                return false;
            }

            if (!_tasksById.TryGetValue(taskId, out UnitTaskRecord task))
            {
                return false;
            }

            if (task.Status == UnitTaskStatus.Completed || task.Status == UnitTaskStatus.Failed)
            {
                return false;
            }

            cancelledTaskType = task.TaskType;
            if (task.TaskType == UnitTaskType.BuildObject || task.TaskType == UnitTaskType.DestroyObject)
            {
                cancelledBuildPayload = task.BuildPayload;
            }

            if (task.TaskType == UnitTaskType.BuildObject)
            {
                FailBuildClearChildTasks(task.TaskId);
            }

// Помечаем как завершённую отменой и очищаем резерв.
            FailTask(task);

// Важно: удаляем связь клетка->task, иначе клетка "залипнет".
            RemoveTaskCellLookup(task);

            // Опционально: если хочешь полностью убирать из быстрого доступа.
            // _tasksById.Remove(taskId);

            return true;
        }


        /// <summary>
        /// Возвращает задачу по id, если она присутствует в задачнике.
        /// </summary>
        /// <summary>
        /// Проверяет задачу dropped-resource для указанной клетки.
        /// Проверяет задачу для Open-клетки, чтобы не создать её повторно.
        /// </summary>
        public bool TryMoveDroppedResourceTaskCell(Vector2Int fromCell, Vector2Int toCell)
        {
            if (fromCell == toCell) return true;
            if (!_taskIdByCell.TryGetValue(fromCell, out int taskId)) return false;
            if (!_tasksById.TryGetValue(taskId, out UnitTaskRecord task) || task == null) return false;
            if (task.TaskType != UnitTaskType.DeliverDroppedResource) return false;
            if (task.Status != UnitTaskStatus.Open) return false;
            if (_taskIdByCell.ContainsKey(toCell)) return false;

            _taskIdByCell.Remove(fromCell);
            _taskIdByCell[toCell] = taskId;
            task.TargetCell = toCell;
            return true;
        }

        public bool CancelCableTaskByCell(
            Vector2Int targetCell,
            out BuildTaskPayload cancelledBuildPayload,
            out UnitTaskType cancelledTaskType)
        {
            cancelledBuildPayload = null;
            cancelledTaskType = default;

            if (_cableTaskIdByCell.TryGetValue(targetCell, out int taskId))
            {
                if (_tasksById.TryGetValue(taskId, out UnitTaskRecord task) && task != null)
                {
                    cancelledTaskType = task.TaskType;
                    ClearCableMarkAndRefund(targetCell);
                    FailTask(task);
                    RemoveTaskCellLookup(task);
                    return true;
                }

                _cableTaskIdByCell.Remove(targetCell);
            }

            if (!_grid.IsInside(targetCell.x, targetCell.y))
            {
                return false;
            }

            Cell cell = _grid.GetCell(targetCell.x, targetCell.y);
            if (!cell.IsCableMarked)
            {
                return false;
            }

            ClearCableMarkAndRefund(targetCell);
            cancelledTaskType = UnitTaskType.BuildCable;
            return true;
        }

        public bool CancelWaterTaskByCell(
            Vector2Int targetCell,
            out BuildTaskPayload cancelledBuildPayload,
            out UnitTaskType cancelledTaskType)
        {
            cancelledBuildPayload = null;
            cancelledTaskType = default;

            if (_waterTaskIdByCell.TryGetValue(targetCell, out int taskId))
            {
                if (_tasksById.TryGetValue(taskId, out UnitTaskRecord task) && task != null)
                {
                    cancelledTaskType = task.TaskType;
                    ClearWaterMarkAndRefund(targetCell);
                    FailTask(task);
                    RemoveTaskCellLookup(task);
                    return true;
                }

                _waterTaskIdByCell.Remove(targetCell);
            }

            if (!_grid.IsInside(targetCell.x, targetCell.y))
            {
                return false;
            }

            Cell cell = _grid.GetCell(targetCell.x, targetCell.y);
            if (!cell.IsWaterMarked)
            {
                return false;
            }

            ClearWaterMarkAndRefund(targetCell);
            cancelledTaskType = UnitTaskType.BuildWater;
            return true;
        }

        public bool CancelOxygenTaskByCell(
            Vector2Int targetCell,
            out BuildTaskPayload cancelledBuildPayload,
            out UnitTaskType cancelledTaskType)
        {
            cancelledBuildPayload = null;
            cancelledTaskType = default;

            if (_oxygenTaskIdByCell.TryGetValue(targetCell, out int taskId))
            {
                if (_tasksById.TryGetValue(taskId, out UnitTaskRecord task) && task != null)
                {
                    cancelledTaskType = task.TaskType;
                    ClearOxygenMarkAndRefund(targetCell);
                    FailTask(task);
                    RemoveTaskCellLookup(task);
                    return true;
                }

                _oxygenTaskIdByCell.Remove(targetCell);
            }

            if (!_grid.IsInside(targetCell.x, targetCell.y))
            {
                return false;
            }

            Cell cell = _grid.GetCell(targetCell.x, targetCell.y);
            if (!cell.IsOxygenMarked)
            {
                return false;
            }

            ClearOxygenMarkAndRefund(targetCell);
            cancelledTaskType = UnitTaskType.BuildOxygen;
            return true;
        }

        public bool CancelLifeModuleTaskByCell(
            Vector2Int targetCell,
            out BuildTaskPayload cancelledBuildPayload,
            out UnitTaskType cancelledTaskType)
        {
            return CancelTaskByCellFromLookup(_lifeModuleTaskIdByCell, targetCell, out cancelledBuildPayload, out cancelledTaskType);
        }

        public bool CancelLifeModuleTaskByCell(
            Vector2Int targetCell,
            out LifeModuleTaskPayload cancelledLifeModulePayload,
            out UnitTaskType cancelledTaskType)
        {
            cancelledLifeModulePayload = null;
            cancelledTaskType = default;

            if (!_lifeModuleTaskIdByCell.TryGetValue(targetCell, out int taskId))
            {
                return false;
            }

            if (!_tasksById.TryGetValue(taskId, out UnitTaskRecord task) || task == null)
            {
                _lifeModuleTaskIdByCell.Remove(targetCell);
                return false;
            }

            cancelledLifeModulePayload = task.LifeModulePayload;
            bool cancelled = CancelTaskByCellFromLookup(_lifeModuleTaskIdByCell, targetCell, out _, out cancelledTaskType);
            if (!cancelled)
            {
                cancelledLifeModulePayload = null;
            }

            return cancelled;
        }
        public bool TryGetTask(int taskId, out UnitTaskRecord task)
        {
            return _tasksById.TryGetValue(taskId, out task);
        }

        /// <summary>
        /// Возвращает задачу, привязанную к клетке, если такая связь сейчас существует.
        /// </summary>
        public bool TryGetTaskByCell(Vector2Int cell, out UnitTaskRecord task)
        {
            task = null;
            if (!_taskIdByCell.TryGetValue(cell, out int taskId))
            {
                if (_cableTaskIdByCell.TryGetValue(cell, out taskId))
                {
                    return TryGetLiveTaskFromLookup(_cableTaskIdByCell, cell, out task);
                }

                if (_waterTaskIdByCell.TryGetValue(cell, out taskId))
                {
                    return TryGetLiveTaskFromLookup(_waterTaskIdByCell, cell, out task);
                }

                if (_oxygenTaskIdByCell.TryGetValue(cell, out taskId))
                {
                    return TryGetLiveTaskFromLookup(_oxygenTaskIdByCell, cell, out task);
                }

                if (_lifeModuleTaskIdByCell.TryGetValue(cell, out taskId))
                {
                    return TryGetLiveTaskFromLookup(_lifeModuleTaskIdByCell, cell, out task);
                }

                return false;
            }

            return TryGetLiveTaskFromLookup(_taskIdByCell, cell, out task);
        }

        public bool TryGetCableTaskByCell(Vector2Int cell, out UnitTaskRecord task)
        {
            return TryGetLiveTaskFromLookup(_cableTaskIdByCell, cell, out task);
        }

        public bool TryGetWaterTaskByCell(Vector2Int cell, out UnitTaskRecord task)
        {
            return TryGetLiveTaskFromLookup(_waterTaskIdByCell, cell, out task);
        }

        public bool TryGetOxygenTaskByCell(Vector2Int cell, out UnitTaskRecord task)
        {
            return TryGetLiveTaskFromLookup(_oxygenTaskIdByCell, cell, out task);
        }

        public bool TryGetLifeModuleTaskByCell(Vector2Int cell, out UnitTaskRecord task)
        {
            return TryGetLiveTaskFromLookup(_lifeModuleTaskIdByCell, cell, out task);
        }

        /// <summary>
        /// Переводит задачу в InProgress, если ее действительно зарезервировал этот юнит.
        /// </summary>
        public bool MarkInProgress(int taskId, int unitId)
        {
            if (!_tasksById.TryGetValue(taskId, out UnitTaskRecord task)) return false;
            if (task.Status != UnitTaskStatus.Reserved) return false;
            if (task.ReservedByUnitId != unitId) return false;

            task.Status = UnitTaskStatus.InProgress;
            return true;
        }

        /// <summary>
        /// Завершает задачу и освобождает резерв клетки.
        /// </summary>
        public bool CompleteTask(int taskId, int unitId)
        {
            if (!_tasksById.TryGetValue(taskId, out UnitTaskRecord task)) return false;
            if (task.ReservedByUnitId != unitId) return false;

            task.Status = UnitTaskStatus.Completed;
            task.ReservedByUnitId = 0;
            task.ReserveTick = -1;
            ClearCellReservation(task.TargetCell);
            RemoveTaskCellLookup(task);

            if (_enableLogs)
            {
                //      Debug.Log($"[TaskBoard] Completed task={task.TaskId} by unit={unitId}");
            }

            if (task.TaskType == UnitTaskType.DigCell)
            {
                NotifyBuildsDigCellCompleted(task.TargetCell);
            }
            else if (task.TaskType == UnitTaskType.BuildCable && _grid.IsInside(task.TargetCell.x, task.TargetCell.y))
            {
                Cell cell = _grid.GetCell(task.TargetCell.x, task.TargetCell.y);
                cell.IsCableMarked = false;
                _grid.SetCell(task.TargetCell.x, task.TargetCell.y, cell);

                if (_taskIdByCell.TryGetValue(task.TargetCell, out int blockingTaskId)
                    && _tasksById.TryGetValue(blockingTaskId, out UnitTaskRecord blockingTask)
                    && blockingTask != null
                    && !IsTaskStillValid(blockingTask))
                {
                    FailTask(blockingTask);
                    RemoveTaskCellLookup(blockingTask);
                }
            }
            else if (task.TaskType == UnitTaskType.BuildWater && _grid.IsInside(task.TargetCell.x, task.TargetCell.y))
            {
                Cell cell = _grid.GetCell(task.TargetCell.x, task.TargetCell.y);
                cell.IsWaterMarked = false;
                _grid.SetCell(task.TargetCell.x, task.TargetCell.y, cell);

                if (_taskIdByCell.TryGetValue(task.TargetCell, out int blockingTaskId)
                    && _tasksById.TryGetValue(blockingTaskId, out UnitTaskRecord blockingTask)
                    && blockingTask != null
                    && !IsTaskStillValid(blockingTask))
                {
                    FailTask(blockingTask);
                    RemoveTaskCellLookup(blockingTask);
                }
            }
            else if (task.TaskType == UnitTaskType.BuildOxygen && _grid.IsInside(task.TargetCell.x, task.TargetCell.y))
            {
                Cell cell = _grid.GetCell(task.TargetCell.x, task.TargetCell.y);
                cell.IsOxygenMarked = false;
                _grid.SetCell(task.TargetCell.x, task.TargetCell.y, cell);

                if (_taskIdByCell.TryGetValue(task.TargetCell, out int blockingTaskId)
                    && _tasksById.TryGetValue(blockingTaskId, out UnitTaskRecord blockingTask)
                    && blockingTask != null
                    && !IsTaskStillValid(blockingTask))
                {
                    FailTask(blockingTask);
                    RemoveTaskCellLookup(blockingTask);
                }
            }

            return true;
        }

        /// <summary>
        /// Снимает резерв задачи вручную (например, если юнит застрял или ушел в needs).
        /// </summary>
        public bool ReleaseTaskReservation(int taskId, int unitId, string reason)
        {
            if (!_tasksById.TryGetValue(taskId, out UnitTaskRecord task)) return false;
            if (task.ReservedByUnitId != unitId) return false;
            if (task.Status != UnitTaskStatus.Reserved && task.Status != UnitTaskStatus.InProgress) return false;

            task.Status = UnitTaskStatus.Open;
            task.ReservedByUnitId = 0;
            task.ReserveTick = -1;
            ClearCellReservation(task.TargetCell);

            if (_enableLogs)
            {
            // Debug.Log($"[TaskBoard] Released reserve task={task.TaskId} unit={unitId} reason={reason}");
            }

            return true;
        }

        /// <summary>
        /// Снимает устаревшие резервы, которые слишком долго висят в статусе Reserved.
        /// </summary>
        public void ReleaseStaleReservations(int currentTick, int timeoutTicks)
        {
            for (int i = 0; i < _taskIds.Count; i++)
            {
                UnitTaskRecord task = _tasksById[_taskIds[i]];
                if (task.Status != UnitTaskStatus.Reserved) continue;
                if (task.ReserveTick < 0) continue;
                if (currentTick - task.ReserveTick <= timeoutTicks) continue;

                int unitId = task.ReservedByUnitId;
                task.Status = UnitTaskStatus.Open;
                task.ReservedByUnitId = 0;
                task.ReserveTick = -1;
                ClearCellReservation(task.TargetCell);

                if (_enableLogs)
                {
            // Debug.Log($"[TaskBoard] Released stale reserve task={task.TaskId} unit={unitId} reason=timeout");
                }
            }
        }

        private void RemoveTaskCellLookup(UnitTaskRecord task)
        {
            if (task == null) return;

            if ((task.TaskType == UnitTaskType.DigCell || task.TaskType == UnitTaskType.ClearBuildCell)
                && _digTaskIdByCell.TryGetValue(task.TargetCell, out int digTaskId)
                && digTaskId == task.TaskId)
            {
                _digTaskIdByCell.Remove(task.TargetCell);
            }

            if (_taskIdByCell.TryGetValue(task.TargetCell, out int mappedTaskId)
                && mappedTaskId == task.TaskId)
            {
                _taskIdByCell.Remove(task.TargetCell);
            }

            if (_cableTaskIdByCell.TryGetValue(task.TargetCell, out int cableTaskId)
                && cableTaskId == task.TaskId)
            {
                _cableTaskIdByCell.Remove(task.TargetCell);
            }

            if (_waterTaskIdByCell.TryGetValue(task.TargetCell, out int waterTaskId)
                && waterTaskId == task.TaskId)
            {
                _waterTaskIdByCell.Remove(task.TargetCell);
            }

            if (_oxygenTaskIdByCell.TryGetValue(task.TargetCell, out int oxygenTaskId)
                && oxygenTaskId == task.TaskId)
            {
                _oxygenTaskIdByCell.Remove(task.TargetCell);
            }

            if (task.TaskType == UnitTaskType.BuildLifeModule && task.LifeModulePayload?.OccupiedCells != null)
            {
                for (int i = 0; i < task.LifeModulePayload.OccupiedCells.Length; i++)
                {
                    Vector2Int occupiedCell = task.LifeModulePayload.OccupiedCells[i];
                    if (_lifeModuleTaskIdByCell.TryGetValue(occupiedCell, out int lifeModuleTaskId)
                        && lifeModuleTaskId == task.TaskId)
                    {
                        _lifeModuleTaskIdByCell.Remove(occupiedCell);
                    }
                }
            }
        }

        private bool TryGetLiveTaskFromLookup(Dictionary<Vector2Int, int> lookup, Vector2Int cell, out UnitTaskRecord task)
        {
            task = null;
            if (!lookup.TryGetValue(cell, out int taskId))
            {
                return false;
            }

            if (!_tasksById.TryGetValue(taskId, out task) || task == null)
            {
                lookup.Remove(cell);
                return false;
            }

            if (task.Status == UnitTaskStatus.Completed || task.Status == UnitTaskStatus.Failed)
            {
                RemoveTaskCellLookup(task);
                task = null;
                return false;
            }

            if (!IsTaskStillValid(task))
            {
                FailTask(task);
                RemoveTaskCellLookup(task);
                task = null;
                return false;
            }

            return true;
        }

        private bool CancelTaskByCellFromLookup(
            Dictionary<Vector2Int, int> lookup,
            Vector2Int targetCell,
            out BuildTaskPayload cancelledBuildPayload,
            out UnitTaskType cancelledTaskType)
        {
            cancelledBuildPayload = null;
            cancelledTaskType = default;
            if (!lookup.TryGetValue(targetCell, out int taskId))
            {
                return false;
            }

            if (!_tasksById.TryGetValue(taskId, out UnitTaskRecord task) || task == null)
            {
                lookup.Remove(targetCell);
                return false;
            }

            if (task.Status == UnitTaskStatus.Completed || task.Status == UnitTaskStatus.Failed)
            {
                return false;
            }

            cancelledTaskType = task.TaskType;
            if (task.TaskType == UnitTaskType.BuildObject || task.TaskType == UnitTaskType.DestroyObject)
            {
                cancelledBuildPayload = task.BuildPayload;
            }

            if (task.TaskType == UnitTaskType.BuildObject)
            {
                FailBuildClearChildTasks(task.TaskId);
            }

            RefundPlanningCost(task);
            FailTask(task);
            RemoveTaskCellLookup(task);
            return true;
        }

        private void FailTask(UnitTaskRecord task)
        {
            if (task == null) return;
            task.Status = UnitTaskStatus.Failed;
            task.ReservedByUnitId = 0;
            task.ReserveTick = -1;
            ClearCellReservation(task.TargetCell);
        }

        private void RefundPlanningCost(UnitTaskRecord task)
        {
            if (task == null) return;
            if (string.IsNullOrWhiteSpace(task.PlanningCostResourceId)) return;
            if (task.PlanningCostAmount <= 0) return;

            _resourceInventoryService.Add(task.PlanningCostResourceId, task.PlanningCostAmount);
            task.PlanningCostResourceId = null;
            task.PlanningCostAmount = 0;
        }

        private void ClearCableMarkAndRefund(Vector2Int targetCell)
        {
            if (!_grid.IsInside(targetCell.x, targetCell.y))
            {
                return;
            }

            Cell cell = _grid.GetCell(targetCell.x, targetCell.y);
            if (!cell.IsCableMarked)
            {
                return;
            }

            cell.IsCableMarked = false;
            _grid.SetCell(targetCell.x, targetCell.y, cell);
            _resourceInventoryService.Add(CABLE_BUILD_RESOURCE_ID, CABLE_BUILD_RESOURCE_AMOUNT);
        }

        private void ClearWaterMarkAndRefund(Vector2Int targetCell)
        {
            if (!_grid.IsInside(targetCell.x, targetCell.y))
            {
                return;
            }

            Cell cell = _grid.GetCell(targetCell.x, targetCell.y);
            if (!cell.IsWaterMarked)
            {
                return;
            }

            cell.IsWaterMarked = false;
            _grid.SetCell(targetCell.x, targetCell.y, cell);
            _resourceInventoryService.Add(WATER_BUILD_RESOURCE_ID, WATER_BUILD_RESOURCE_AMOUNT);
        }

        private void ClearOxygenMarkAndRefund(Vector2Int targetCell)
        {
            if (!_grid.IsInside(targetCell.x, targetCell.y))
            {
                return;
            }

            Cell cell = _grid.GetCell(targetCell.x, targetCell.y);
            if (!cell.IsOxygenMarked)
            {
                return;
            }

            cell.IsOxygenMarked = false;
            _grid.SetCell(targetCell.x, targetCell.y, cell);
            _resourceInventoryService.Add(OXYGEN_BUILD_RESOURCE_ID, OXYGEN_BUILD_RESOURCE_AMOUNT);
        }

        private static bool CanBuildCableOnCellType(CellType cellType)
        {
            return CanBuildPipeOnCellType(cellType);
        }

        private static bool CanBuildPipeOnCellType(CellType cellType)
        {
            return cellType == CellType.Empty || cellType == CellType.Atmosphere;
        }

        private void FailBuildClearChildTasks(int parentBuildTaskId)
        {
            for (int i = 0; i < _taskIds.Count; i++)
            {
                int taskId = _taskIds[i];
                UnitTaskRecord task = _tasksById[taskId];
                if (task.TaskType != UnitTaskType.ClearBuildCell) continue;
                if (task.ParentBuildTaskId != parentBuildTaskId) continue;
                if (task.Status == UnitTaskStatus.Completed || task.Status == UnitTaskStatus.Failed) continue;

                if (_grid.IsInside(task.TargetCell.x, task.TargetCell.y))
                {
                    Cell cell = _grid.GetCell(task.TargetCell.x, task.TargetCell.y);
                    cell.IsDigMarked = false;
                    _grid.SetCell(task.TargetCell.x, task.TargetCell.y, cell);
                }

                FailTask(task);
                RemoveTaskCellLookup(task);
            }
        }

        private void NotifyBuildsDigCellCompleted(Vector2Int completedCell)
        {
            for (int i = 0; i < _taskIds.Count; i++)
            {
                int taskId = _taskIds[i];
                UnitTaskRecord task = _tasksById[taskId];
                if (task.TaskType != UnitTaskType.BuildObject) continue;
                if (task.BuildPayload == null || task.BuildPayload.BuildingDef == null) continue;
                if (task.Status == UnitTaskStatus.Completed || task.Status == UnitTaskStatus.Failed) continue;
                if (!IsCellInsideBuildFootprint(task.BuildPayload, completedCell)) continue;
                if (task.BuildPayload.RemainingClearSubtasks <= 0) continue;

                task.BuildPayload.RemainingClearSubtasks--;
                if (task.BuildPayload.RemainingClearSubtasks <= 0)
                {
                    task.BuildPayload.RemainingClearSubtasks = 0;
                    task.BuildPayload.IsExcavatingBeforeBuild = false;
                }
            }

            for (int i = 0; i < _taskIds.Count; i++)
            {
                int taskId = _taskIds[i];
                UnitTaskRecord task = _tasksById[taskId];
                if (task.TaskType != UnitTaskType.BuildLifeModule) continue;
                if (task.LifeModulePayload?.OccupiedCells == null) continue;
                if (task.Status == UnitTaskStatus.Completed || task.Status == UnitTaskStatus.Failed) continue;
                if (!IsCellInsideLifeModuleFootprint(task.LifeModulePayload, completedCell)) continue;
                if (task.LifeModulePayload.RemainingClearSubtasks <= 0) continue;

                task.LifeModulePayload.RemainingClearSubtasks--;
                if (task.LifeModulePayload.RemainingClearSubtasks <= 0)
                {
                    task.LifeModulePayload.RemainingClearSubtasks = 0;
                    task.LifeModulePayload.IsExcavatingBeforeBuild = false;
                }
            }
        }

        private static bool IsCellInsideBuildFootprint(BuildTaskPayload payload, Vector2Int cell)
        {
            int width = payload.IsRotated ? payload.BuildingDef.Height : payload.BuildingDef.Width;
            int height = payload.IsRotated ? payload.BuildingDef.Width : payload.BuildingDef.Height;

            return cell.x >= payload.AnchorCell.x
                   && cell.x < payload.AnchorCell.x + width
                   && cell.y >= payload.AnchorCell.y
                   && cell.y < payload.AnchorCell.y + height;
        }

        private static bool IsCellInsideLifeModuleFootprint(LifeModuleTaskPayload payload, Vector2Int cell)
        {
            if (payload?.OccupiedCells == null)
            {
                return false;
            }

            for (int i = 0; i < payload.OccupiedCells.Length; i++)
            {
                if (payload.OccupiedCells[i] == cell)
                {
                    return true;
                }
            }

            return false;
        }

        private int EnsureLifeModuleDigTasks(LifeModuleTaskPayload payload, int currentTick)
        {
            if (payload?.OccupiedCells == null)
            {
                return 0;
            }

            int created = 0;
            for (int i = 0; i < payload.OccupiedCells.Length; i++)
            {
                Vector2Int cellPos = payload.OccupiedCells[i];
                if (!_grid.IsInside(cellPos.x, cellPos.y))
                {
                    continue;
                }

                if (ShipLandingZoneRules.IsInsideDigProtectionZone(_grid.Width, _grid.Height, cellPos))
                {
                    continue;
                }

                Cell cell = _grid.GetCell(cellPos.x, cellPos.y);
                if (!CellTraversalRules.IsDiggable(cell.Type))
                {
                    continue;
                }

                if (cell.IsDigMarked && _digTaskIdByCell.ContainsKey(cellPos))
                {
                    continue;
                }

                cell.IsDigMarked = true;
                _grid.SetCell(cellPos.x, cellPos.y, cell);

                if (TryCreateDigTaskForCell(cellPos, currentTick))
                {
                    created++;
                }
            }

            return created;
        }

        /// <summary>
        /// Записывает в клетку id юнита, который зарезервировал задачу.
        /// </summary>
        private void SetCellReservation(Vector2Int targetCell, int unitId)
        {
            if (!_grid.IsInside(targetCell.x, targetCell.y)) return;
            Cell cell = _grid.GetCell(targetCell.x, targetCell.y);
            cell.ReservedByUnitId = unitId;
            _grid.SetCell(targetCell.x, targetCell.y, cell);
        }

        /// <summary>
        /// Очищает резерв клетки (ставит ReservedByUnitId = 0).
        /// </summary>
        private void ClearCellReservation(Vector2Int targetCell)
        {
            if (!_grid.IsInside(targetCell.x, targetCell.y)) return;
            Cell cell = _grid.GetCell(targetCell.x, targetCell.y);
            cell.ReservedByUnitId = 0;
            _grid.SetCell(targetCell.x, targetCell.y, cell);
        }
    }
}