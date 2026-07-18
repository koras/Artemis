using _Project.Scripts.Data.Animals;
using UnityEngine;

namespace _Project.Scripts.Systems.Animals
{
    /// <summary>
    /// Lightweight diagnostics read model for one selected animal.
    /// </summary>
    public readonly struct AnimalDiagnosticsSnapshot
    {
        public readonly int AnimalId;
        public readonly string DisplayName;
        public readonly string SpeciesId;
        public readonly Vector2Int CurrentCell;
        public readonly float Hunger;
        public readonly float MaxHunger;
        public readonly float CurrentSpeed;
        public readonly float MaxMoveSpeed;
        public readonly int Loyalty;
        public readonly bool HasLoyalty;
        public readonly bool CanLayEgg;
        public readonly float GrowthProgress;
        public readonly bool TracksPreyEaten;
        public readonly int EatenPreyCount;
        public readonly bool UsesGrowthBasedEggLaying;
        public readonly float EggLayGrowthThreshold;
        public readonly int EggsLaidCount;
        public readonly int MaxEggsPerAnimal;
        public readonly float EggLayRemainingGameHours;
        public readonly float EggLayIntervalGameHours;
        public readonly AnimalLifecycleMode LifecycleMode;

        public AnimalDiagnosticsSnapshot(
            int animalId,
            string displayName,
            string speciesId,
            Vector2Int currentCell,
            float hunger,
            float maxHunger,
            float currentSpeed,
            float maxMoveSpeed,
            int loyalty,
            bool hasLoyalty,
            bool canLayEgg,
            float growthProgress,
            bool tracksPreyEaten,
            int eatenPreyCount,
            bool usesGrowthBasedEggLaying,
            float eggLayGrowthThreshold,
            int eggsLaidCount,
            int maxEggsPerAnimal,
            float eggLayRemainingGameHours,
            float eggLayIntervalGameHours,
            AnimalLifecycleMode lifecycleMode)
        {
            AnimalId = animalId;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Animal" : displayName;
            SpeciesId = string.IsNullOrWhiteSpace(speciesId) ? "animal" : speciesId;
            CurrentCell = currentCell;
            Hunger = hunger;
            MaxHunger = Mathf.Max(0.01f, maxHunger);
            CurrentSpeed = currentSpeed;
            MaxMoveSpeed = Mathf.Max(0.01f, maxMoveSpeed);
            Loyalty = loyalty;
            HasLoyalty = hasLoyalty;
            CanLayEgg = canLayEgg;
            GrowthProgress = Mathf.Clamp01(growthProgress);
            TracksPreyEaten = tracksPreyEaten;
            EatenPreyCount = Mathf.Max(0, eatenPreyCount);
            UsesGrowthBasedEggLaying = usesGrowthBasedEggLaying;
            EggLayGrowthThreshold = Mathf.Clamp01(eggLayGrowthThreshold);
            EggsLaidCount = Mathf.Max(0, eggsLaidCount);
            MaxEggsPerAnimal = Mathf.Max(0, maxEggsPerAnimal);
            EggLayRemainingGameHours = Mathf.Max(0f, eggLayRemainingGameHours);
            EggLayIntervalGameHours = Mathf.Max(0f, eggLayIntervalGameHours);
            LifecycleMode = lifecycleMode;
        }
    }
}
