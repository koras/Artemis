using _Project.Scripts.Data.Animals;
using _Project.Scripts.Presentation.Animals;
using UnityEngine;

namespace _Project.Scripts.Systems.Animals
{
    /// <summary>
    /// Runtime-only state for one simulated animal instance.
    /// </summary>
    public sealed class AnimalRuntimeState
    {
        public int AnimalId;
        public AnimalDefinition Definition;
        public AnimalActor Actor;
        public Vector2Int CurrentCell;
        public Vector2Int GoalCell;
        public bool HasGoalCell;
        public float IdleRemainingSeconds;
        public float Hunger;
        public int Loyalty;
        public float AgeGameHours;
        public float GrowthProgress;
        public float MovementSpeedMultiplier;
        public float ReproductionElapsedGameHours;
        public float EggLayElapsedGameHours;
        public int EggsLaidCount;
        public int HuntTargetAnimalId;
        // Tracks successful prey consumption for diagnostics and future behavior hooks.
        public int EatenPreyCount;
    }
}
