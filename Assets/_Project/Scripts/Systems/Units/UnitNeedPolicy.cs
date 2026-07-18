using _Project.Scripts.Data.Tasks;
using _Project.Scripts.Presentation.Character;

namespace _Project.Scripts.Systems.Units
{
    /// <summary>
    /// Политика приоритета личных потребностей юнита.
    /// </summary>
    public sealed class UnitNeedPolicy
    {
        private const int CriticalHungerThreshold = 220;
        private const int CriticalSleepThreshold = 220;
        private const int StartEatThreshold = 140;
        private const int StartSleepThreshold = 160;

        public int CriticalHunger => CriticalHungerThreshold;
        public int CriticalSleep => CriticalSleepThreshold;

        // Method IsNeedCritical: executes the IsNeedCritical workflow.
        // RU: Метод IsNeedCritical выполняет соответствующий этап логики IsNeedCritical.
        public bool IsNeedCritical(CharacterActor actor)
        {
            return actor != null
                   && (actor.Hunger >= CriticalHungerThreshold || actor.SleepDesire >= CriticalSleepThreshold);
        }

        // Method GetCriticalNeedTask: executes the GetCriticalNeedTask workflow.
        // RU: Метод GetCriticalNeedTask выполняет соответствующий этап логики GetCriticalNeedTask.
        public UnitTaskType GetCriticalNeedTask(CharacterActor actor)
        {
            if (actor == null) return UnitTaskType.Eat;
            if (actor.SleepDesire >= CriticalSleepThreshold && actor.SleepDesire >= actor.Hunger) return UnitTaskType.Sleep;
            return UnitTaskType.Eat;
        }

        // Method DecideLocalNeed: executes the DecideLocalNeed workflow.
        // RU: Метод DecideLocalNeed выполняет соответствующий этап логики DecideLocalNeed.
        public UnitLocalNeedState DecideLocalNeed(CharacterActor actor, bool workQuotaReached)
        {
            if (actor == null) return UnitLocalNeedState.Rest;

            if (actor.SleepDesire >= StartSleepThreshold && actor.SleepDesire >= actor.Hunger)
            {
                return UnitLocalNeedState.Sleep;
            }

            if (actor.Hunger >= StartEatThreshold)
            {
                return UnitLocalNeedState.Eat;
            }

            return workQuotaReached ? UnitLocalNeedState.Rest : UnitLocalNeedState.None;
        }
    }
}
