using _Project.Scripts.Data.Construction;
using UnityEngine;

namespace _Project.Scripts.Data.Construction
{
    /// <summary>
    /// Допданные задачи строительства.
    /// </summary>
    public sealed class BuildTaskPayload
    {
        public BuildingDef BuildingDef;   // Что именно строим.
        public Vector2Int AnchorCell;     // Якорная клетка объекта (левый-нижний угол).
        public bool IsRotated;            // Повернут ли объект.
        public bool IsExcavatingBeforeBuild; // Флаг ожидания подзадач очистки footprint перед строительством.
        public int RemainingClearSubtasks; // Сколько дочерних задач очистки еще не завершено.
        public bool IsBuildCostPaid;     // Стоимость уже списана со склада перед началом работы.
        public int RemainingBuildTicks;   // Оставшийся прогресс задачи.
    }
}