using _Project.Scripts.Presentation.Animals;
using UnityEngine;

namespace _Project.Scripts.Data.Animals
{
    /// <summary>
    /// Definition for a world egg entity created by egg-laying species.
    /// </summary>
    [CreateAssetMenu(
        fileName = "AnimalEggDefinition",
        menuName = "Artemis/Animals/Egg Definition")]
    public sealed class AnimalEggDefinition : ScriptableObject
    {
        [SerializeField] private AnimalEggActor _eggPrefab;
        [SerializeField] private string _sourceSpeciesId = "lunar_resident";
        [SerializeField] private string _displayName = "Lunar Egg";
        [SerializeField] private AnimalDefinition _hatchAnimalDefinition;
        [SerializeField] [Min(0.01f)] private float _hatchDurationGameHours = 24f;
        [SerializeField] [Min(0.01f)] private float _hatchAnimationDurationSeconds = 0.65f;

        public AnimalEggActor EggPrefab => _eggPrefab;
        public string SourceSpeciesId => _sourceSpeciesId;
        public string DisplayName => _displayName;
        public AnimalDefinition HatchAnimalDefinition => _hatchAnimalDefinition;
        public float HatchDurationGameHours => _hatchDurationGameHours;
        public float HatchAnimationDurationSeconds => _hatchAnimationDurationSeconds;
    }
}
