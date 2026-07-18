using System.Collections.Generic;
using _Project.Scripts.Data.Tasks;
using _Project.Scripts.Systems.Tasks;
using _Project.Scripts.Systems.Units;
using UnityEngine;

namespace _Project.Scripts.Presentation.UI
{
    /// <summary>
    /// РЎС‚СЂРѕРёС‚ ViewModel-СЃРїРёСЃРѕРє РґР»СЏ РїР°РЅРµР»Рё РѕС‡РµСЂРµРґРё Р·Р°РґР°С‡ HUD.
    /// </summary>
    public sealed class TaskQueueHudBuilder
    {
        // РСЃС‚РѕС‡РЅРёРє Р·Р°РґР°С‡ РґР»СЏ РѕС‚РѕР±СЂР°Р¶РµРЅРёСЏ.
        private readonly GlobalTaskBoardService _globalTaskBoardService;

        // РСЃС‚РѕС‡РЅРёРє РїРѕР·РёС†РёР№ СЋРЅРёС‚РѕРІ РґР»СЏ СЂР°СЃС‡С‘С‚Р° РґРёСЃС‚Р°РЅС†РёРё.
        private readonly UnitTaskOrchestratorService _unitTaskOrchestratorService;

        /// <summary>
        /// РРЅРёС†РёР°Р»РёР·РёСЂСѓРµС‚ builder Р·Р°РІРёСЃРёРјРѕСЃС‚СЏРјРё РґРѕРјРµРЅРЅРѕРіРѕ СЃР»РѕСЏ Р·Р°РґР°С‡.
        /// </summary>
        public TaskQueueHudBuilder(
            GlobalTaskBoardService globalTaskBoardService,
            UnitTaskOrchestratorService unitTaskOrchestratorService)
        {
            _globalTaskBoardService = globalTaskBoardService;
            _unitTaskOrchestratorService = unitTaskOrchestratorService;
        }

        /// <summary>
        /// Р’РѕР·РІСЂР°С‰Р°РµС‚ РіРѕС‚РѕРІС‹Рµ СЌР»РµРјРµРЅС‚С‹ РґР»СЏ РѕС‚СЂРёСЃРѕРІРєРё РїР°РЅРµР»Рё РѕС‡РµСЂРµРґРё Р·Р°РґР°С‡.
        /// </summary>
        public List<TaskQueueItemViewModel> BuildItems()
        {
            List<UnitTaskRecord> openTasks = _globalTaskBoardService.GetActiveTasksSnapshot();
            List<Vector2Int> unitCells = _unitTaskOrchestratorService.GetUnitCellsSnapshot();
            return BuildItemsFromSnapshots(openTasks, unitCells);
        }

        /// <summary>
        /// Р’РѕР·РІСЂР°С‰Р°РµС‚ hash С‚РµРєСѓС‰РµРіРѕ СЃРѕСЃС‚РѕСЏРЅРёСЏ СЃРїРёСЃРєР° Р·Р°РґР°С‡ РґР»СЏ РґРµС€С‘РІРѕР№ РїСЂРѕРІРµСЂРєРё "РёР·РјРµРЅРёР»РѕСЃСЊ / РЅРµ РёР·РјРµРЅРёР»РѕСЃСЊ".
        /// </summary>
        public int BuildStateHash()
        {
            List<UnitTaskRecord> openTasks = _globalTaskBoardService.GetActiveTasksSnapshot();
            List<Vector2Int> unitCells = _unitTaskOrchestratorService.GetUnitCellsSnapshot();
            return ComputeStateHash(openTasks, unitCells);
        }

        private List<TaskQueueItemViewModel> BuildItemsFromSnapshots(
            List<UnitTaskRecord> openTasks,
            List<Vector2Int> unitCells)
        {
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

        private static int ComputeStateHash(List<UnitTaskRecord> openTasks, List<Vector2Int> unitCells)
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
        /// РЎС‡РёС‚Р°РµС‚ Manhattan-РґРёСЃС‚Р°РЅС†РёСЋ РѕС‚ Р·Р°РґР°С‡Рё РґРѕ Р±Р»РёР¶Р°Р№С€РµРіРѕ СЋРЅРёС‚Р°.
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
        /// Р¤РѕСЂРјРёСЂСѓРµС‚ С‡РµР»РѕРІРµРєРѕ-РїРѕРЅСЏС‚РЅРѕРµ РёРјСЏ Р·Р°РґР°С‡Рё РґР»СЏ UI.
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
