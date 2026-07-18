using _Project.Scripts.Data.Animals;
using _Project.Scripts.Presentation.Animals;
using UnityEngine;

namespace _Project.Scripts.Systems.Animals
{
    /// <summary>
    /// Runtime-only state for one egg entity placed in the world.
    /// </summary>
    public sealed class AnimalEggRuntimeState
    {
        public int EggId;
        public AnimalEggDefinition Definition;
        public AnimalEggActor Actor;
        public Vector2Int Cell;
        public float AgeGameHours;
        public bool IsHatching;
    }
}
