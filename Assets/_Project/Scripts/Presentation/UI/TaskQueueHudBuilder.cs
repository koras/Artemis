using System.Collections.Generic;
using _Project.Scripts.Data.Tasks;
using _Project.Scripts.Systems.Tasks;
using _Project.Scripts.Systems.Units;
using UnityEngine;

namespace _Project.Scripts.Presentation.UI
{
    /// <summary>
    /// Строит ViewModel-список для панели очереди задач HUD.
    /// </summary>
    public sealed class TaskQueueHudBuilder
    {
        // Источник задач для отображения.
        private readonly GlobalTaskBoardService _globalTaskBoardService;

        // Источник позиций юнитов для расчёта дистанции.
        private readonly UnitTaskOrchestratorService _unitTaskOrchestratorService;

        /// <summary>
        /// Инициализирует builder зависимостями доменного слоя задач.
        /// </summary>
        public TaskQueueHudBuilder(
            GlobalTaskBoardService globalTaskBoardService,
            UnitTaskOrchestratorService unitTaskOrchestratorService)
        {
            _globalTaskBoardService = globalTaskBoardService;
            _unitTaskOrchestratorService = unitTaskOrchestratorService;
        }

        /// <summary>
        /// Возвращает готовые элементы для отрисовки панели очереди задач.
        /// </summary>
        public List<TaskQueueItemViewModel> BuildItems()
        {
            var openTasks = _globalTaskBoardService.GetActiveTasksSnapshot();
            List<Vector2Int> unitCells = _unitTaskOrchestratorService.GetUnitCellsSnapshot();
            return BuildItemsFromSnapshots(openTasks, unitCells);
        }

        /// <summary>
        /// Возвращает hash текущего состояния списка задач для дешёвой проверки "изменилось / не изменилось".
        /// </summary>
        public int BuildStateHash()
        {
            var openTasks = _globalTaskBoardService.GetActiveTasksSnapshot();
            List<Vector2Int> unitCells = _unitTaskOrchestratorService.GetUnitCellsSnapshot();
            return ComputeStateHash(openTasks, unitCells);
        }

        private List<TaskQueueItemViewModel> BuildItemsFromSnapshots(
            IReadOnlyList<UnitTaskRecord> openTasks,
            List<Vector2Int> unitCells)
        {
            //todo
            var items = new List<TaskQueueItemViewModel>(openTasks.Count);

            for (int i = 0; i < openTasks.Count; i++)
            {
                UnitTaskRecord task = openTasks[i];
                int distance = CalculateDistanceToNearestUnit(task.TargetCell, unitCells);
                string title = BuildTaskTitle(task);
                string unitBlockSummary = _unitTaskOrchestratorService.BuildPerUnitTaskEligibilitySummary(task.TaskId);
                string notTakenReason = BuildNotTakenReason(task, unitBlockSummary);
                string pendingClearingDetails = BuildPendingClearingDetails(task);

                items.Add(new TaskQueueItemViewModel(
                    title,
                    task.TaskId,
                    task.TaskType.ToString(),
                    $"{task.TargetCell.x},{task.TargetCell.y}",
                    task.Status.ToString(),
                    BuildWaitReason(task),
                    notTakenReason,
                    unitBlockSummary,
                    pendingClearingDetails,
                    task.BasePriority.ToString(),
                    distance
                ));
            }

            return items;
        }

        private static int ComputeStateHash(IReadOnlyList<UnitTaskRecord> openTasks, List<Vector2Int> unitCells)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + openTasks.Count;

                for (int i = 0; i < openTasks.Count; i++)
                {
                    UnitTaskRecord task = openTasks[i];
                    int distance = CalculateDistanceToNearestUnit(task.TargetCell, unitCells);

                    hash = hash * 31 + task.TaskId;
                    hash = hash * 31 + (int)task.TaskType;
                    hash = hash * 31 + (int)task.Status;
                    hash = hash * 31 + (int)task.BasePriority;
                    hash = hash * 31 + task.TargetCell.x;
                    hash = hash * 31 + task.TargetCell.y;
                    string waitReason = BuildWaitReason(task);
                    hash = hash * 31 + (waitReason != null ? waitReason.GetHashCode() : 0);
                    hash = hash * 31 + distance;
                }

                return hash;
            }
        }

        /// <summary>
        /// Считает Manhattan-дистанцию от задачи до ближайшего юнита.
        /// </summary>
        private static int CalculateDistanceToNearestUnit(Vector2Int taskCell, List<Vector2Int> unitCells)
        {
            if (unitCells == null || unitCells.Count == 0) return -1;

            int best = int.MaxValue;

            for (int i = 0; i < unitCells.Count; i++)
            {
                Vector2Int unitCell = unitCells[i];
                int dist = Mathf.Abs(taskCell.x - unitCell.x) + Mathf.Abs(taskCell.y - unitCell.y);

                if (dist < best)
                {
                    best = dist;
                }
            }

            return best;
        }

        /// <summary>
        /// Формирует человеко-понятное имя задачи для UI.
        /// </summary>
        private static string BuildTaskTitle(UnitTaskRecord task)
        {
            return $"{task.TaskType} #{task.TaskId} ({task.TargetCell.x},{task.TargetCell.y})";
        }

        private static string BuildWaitReason(UnitTaskRecord task)
        {
            if (task == null) return string.Empty;
            if (task.TaskType == UnitTaskType.BuildObject && task.BuildPayload != null && task.BuildPayload.RemainingClearSubtasks > 0)
            {
                return $"Waiting for clearing: {task.BuildPayload.RemainingClearSubtasks}";
            }

            if (task.TaskType == UnitTaskType.BuildLifeModule && task.LifeModulePayload != null && task.LifeModulePayload.RemainingClearSubtasks > 0)
            {
                return $"Waiting for clearing: {task.LifeModulePayload.RemainingClearSubtasks}";
            }

            if (task.Status == UnitTaskStatus.Reserved)
            {
                return $"Reserved by unit {task.ReservedByUnitId}";
            }

            if (task.Status == UnitTaskStatus.InProgress)
            {
                return "In progress";
            }

            return string.Empty;
        }

        private static string BuildNotTakenReason(UnitTaskRecord task, string unitBlockSummary)
        {
            if (task == null)
            {
                return "unknown";
            }

            if (task.Status == UnitTaskStatus.Reserved)
            {
                return $"already reserved by unit {task.ReservedByUnitId}";
            }

            if (task.Status == UnitTaskStatus.InProgress)
            {
                return "already in progress";
            }

            if (task.TaskType == UnitTaskType.BuildObject && task.BuildPayload != null && task.BuildPayload.RemainingClearSubtasks > 0)
            {
                return $"waiting for clearing subtasks: {task.BuildPayload.RemainingClearSubtasks}";
            }

            if (task.TaskType == UnitTaskType.BuildLifeModule && task.LifeModulePayload != null && task.LifeModulePayload.RemainingClearSubtasks > 0)
            {
                return $"waiting for clearing subtasks: {task.LifeModulePayload.RemainingClearSubtasks}";
            }

            // If task is open and still not taken, show current blockers reported by units.
            return string.IsNullOrWhiteSpace(unitBlockSummary)
                ? "no unit reported a concrete blocker yet"
                : $"open but not taken; unit blockers: {unitBlockSummary}";
        }

        private string BuildPendingClearingDetails(UnitTaskRecord task)
        {
            if (task == null || task.TaskType != UnitTaskType.BuildObject || task.BuildPayload == null)
            {
                return "-";
            }

            List<Vector2Int> pendingCells = _globalTaskBoardService.GetPendingDigCellsInBuildFootprint(task.BuildPayload);
            if (pendingCells == null || pendingCells.Count == 0)
            {
                return "no pending clearing cells";
            }

            var parts = new List<string>(pendingCells.Count);
            for (int i = 0; i < pendingCells.Count; i++)
            {
                Vector2Int cell = pendingCells[i];
                if (!_globalTaskBoardService.TryGetTaskByCell(cell, out UnitTaskRecord pendingTask) || pendingTask == null)
                {
                    parts.Add($"({cell.x},{cell.y}) -> no task");
                    continue;
                }

                parts.Add(
                    $"({cell.x},{cell.y}) -> id={pendingTask.TaskId}, type={pendingTask.TaskType}, status={pendingTask.Status}, reservedBy={pendingTask.ReservedByUnitId}");
            }

            return string.Join(" | ", parts);
        }
    }
}