using _Project.Scripts.Data.Tasks;
using UnityEngine;

namespace _Project.Scripts.Systems.Tasks
{
    /// <summary>
    /// Сервис оценки задач для конкретного юнита.
    /// Нужен для выбора "лучшей" задачи из глобального задачника.
    /// Формула v1:
    /// score = priorityWeight - distanceWeight + ageBonus.
    /// </summary>
    public sealed class TaskScoringService
    {
        // Радиус видимости юнита в клетках (Манхэттен-метрика).
        // Задачи за пределом радиуса не рассматриваются вообще.
        private const int VisionRadius = 25;

        /// <summary>
        /// Проверяет, видна ли цель из текущей клетки юнита.
        /// </summary>
        /// <param name="unitCell">Текущая клетка юнита.</param>
        /// <param name="targetCell">Клетка задачи.</param>
        /// <returns>True, если цель в радиусе видимости.</returns>
        public bool IsVisible(Vector2Int unitCell, Vector2Int targetCell)
        {
            return Manhattan(unitCell, targetCell) <= VisionRadius;
        }

        /// <summary>
        /// Вычисляет итоговый score задачи для юнита в текущий тик.
        /// Чем score выше, тем привлекательнее задача.
        /// </summary>
        /// <param name="task">Кандидат задачи.</param>
        /// <param name="unitCell">Клетка юнита на момент выбора.</param>
        /// <param name="currentTick">Текущий тик симуляции.</param>
        /// <returns>Итоговый score для сортировки задач.</returns>
        public float CalculateScore(UnitTaskRecord task, Vector2Int unitCell, int currentTick)
        {
            // Дистанция в клетках по Манхэттену.
            int distance = Manhattan(unitCell, task.TargetCell);

            // Возраст задачи в тиках: чем старше, тем больше бонус,
            // чтобы старые задачи не висели бесконечно.
            int age = Mathf.Max(0, currentTick - task.CreatedAtTick);

            // Вклад базового приоритета.
            float priorityWeight = GetPriorityWeight(task.BasePriority);

            // Штраф за удаленность: дальние задачи менее выгодны.
            float distanceWeight = distance * 1.25f;

            // Бонус за "старение" задачи.
            float ageBonus = age * 0.2f;

            return priorityWeight - distanceWeight + ageBonus;
        }

        /// <summary>
        /// Манхэттен-расстояние между двумя клетками.
        /// Используется как дешёвая клеточная метрика близости.
        /// </summary>
        public static int Manhattan(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }

        /// <summary>
        /// Преобразует enum приоритета в числовой вес.
        /// Чем выше вес, тем сильнее задача "тянется" вверх в выборе.
        /// </summary>
        private static float GetPriorityWeight(TaskPriority priority)
        {
            switch (priority)
            {
                case TaskPriority.Critical: return 100f;
                case TaskPriority.High: return 60f;
                case TaskPriority.Normal: return 35f;
                case TaskPriority.Low:
                default:
                    return 20f;
            }
        }
    }
}
