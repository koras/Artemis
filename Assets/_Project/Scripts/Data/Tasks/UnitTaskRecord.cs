using UnityEngine;
using _Project.Scripts.Data.Construction;

namespace _Project.Scripts.Data.Tasks
{
    /// <summary>
    /// Описание одной задачи для глобального задачника.
    /// </summary>
    public sealed class UnitTaskRecord
    {
        public int TaskId;
        public UnitTaskType TaskType;
        public Vector2Int TargetCell;
        public TaskPriority BasePriority;
        public int CreatedAtTick;
        public int ReservedByUnitId;
        public int ReserveTick;
        public UnitTaskStatus Status;
        public BuildTaskPayload BuildPayload;
        public LifeModuleTaskPayload LifeModulePayload;
        public int ParentBuildTaskId;
        public int RemainingWorkTicks;
        public string ResourceDropId;
        public int ResourceDropAmount;
        // Stores planning-time reservation so cancellation can refund the exact resource amount.
        public string PlanningCostResourceId;
        public int PlanningCostAmount;
    }
}
