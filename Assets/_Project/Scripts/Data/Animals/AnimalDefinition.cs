using _Project.Scripts.Presentation.Animals;
using UnityEngine;

namespace _Project.Scripts.Data.Animals
{
    /// <summary>
    /// Species-level animal definition used by debug spawn and runtime simulation.
    /// </summary>
    [CreateAssetMenu(
        fileName = "AnimalDefinition",
        menuName = "Artemis/Animals/Definition")]
    public sealed class AnimalDefinition : ScriptableObject
    {
        [Header("Prefab")]
        [SerializeField] private AnimalActor _animalPrefab;

        [Header("Identity")]
        [SerializeField] private string _speciesId = "mouse";
        [SerializeField] private string _displayName = "Mouse";
        [SerializeField] private AnimalLifecycleMode _lifecycleMode = AnimalLifecycleMode.LiveBirth;

        [Header("Start State")]
        [SerializeField] [Min(0f)] private float _startHunger = 0f;
        [SerializeField] [Range(0f, 1f)] private float _startGrowth = 0.1f;
        [SerializeField] private bool _hasLoyaltyStat;
        [SerializeField] [Range(0, 100)] private int _minStartLoyalty;
        [SerializeField] [Range(0, 100)] private int _maxStartLoyalty = 100;

        [Header("Growth")]
        [SerializeField] [Min(0.01f)] private float _minVisualScale = 0.45f;
        [SerializeField] [Min(0.01f)] private float _maxVisualScale = 0.9f;
        [SerializeField] [Min(0.01f)] private float _growthDurationGameHours = 24f;
        [SerializeField] [Range(0f, 1f)] private float _growthPerFeeding;

        [Header("Runtime")]
        [SerializeField] [Min(0f)] private float _hungerIncreasePerGameHour = 6f;
        [SerializeField] [Min(0.01f)] private float _maxHunger = 300f;
        [SerializeField] [Min(0.01f)] private float _movementSpeedMultiplier = 1f;
        [SerializeField] [Min(0.01f)] private float _maxMoveSpeed = 2.2f;
        [SerializeField] [Min(0.01f)] private float _moveAcceleration = 6f;
        [SerializeField] [Min(0.01f)] private float _moveDeceleration = 9f;
        [SerializeField] [Min(0.01f)] private float _slowdownDistance = 0.3f;
        [SerializeField] [Min(0.001f)] private float _stopDistance = 0.03f;

        [Header("Live Birth")]
        [SerializeField] [Min(0.01f)] private float _reproductionIntervalGameHours = 18f;
        [SerializeField] [Min(1)] private int _maxPopulation = 8;

        [Header("Egg Laying")]
        [SerializeField] [Min(0.01f)] private float _eggLayIntervalGameHours = 24f;
        [SerializeField] private AnimalEggDefinition _eggDefinition;
        [SerializeField] [Range(0f, 1f)] private float _eggLayGrowthThreshold = 0.8f;

        [Header("Hunting")]
        [SerializeField] [Min(0f)] private float _huntStartHunger = 0f;
        [SerializeField] [Min(0f)] private float _huntHungerRelief = 0f;
        [SerializeField] private string _preySpeciesId = string.Empty;

        [Header("Wander")]
        [SerializeField] [Min(1)] private int _wanderRadiusCells = 10;
        [SerializeField] private Vector2 _idlePauseRangeSeconds = new Vector2(0.6f, 1.8f);

        public AnimalActor AnimalPrefab => _animalPrefab;
        public string SpeciesId => _speciesId;
        public string DisplayName => _displayName;
        public AnimalLifecycleMode LifecycleMode => _lifecycleMode;
        public float StartHunger => _startHunger;
        public float StartGrowth => _startGrowth;
        public bool HasLoyaltyStat => _hasLoyaltyStat;
        public int MinStartLoyalty => _minStartLoyalty;
        public int MaxStartLoyalty => _maxStartLoyalty;
        public float MinVisualScale => _minVisualScale;
        public float MaxVisualScale => _maxVisualScale;
        public float GrowthDurationGameHours => _growthDurationGameHours;
        public float GrowthPerFeeding => _growthPerFeeding;
        public float HungerIncreasePerGameHour => _hungerIncreasePerGameHour;
        public float MaxHunger => _maxHunger;
        public float MovementSpeedMultiplier => _movementSpeedMultiplier;
        public float MaxMoveSpeed => _maxMoveSpeed;
        public float MoveAcceleration => _moveAcceleration;
        public float MoveDeceleration => _moveDeceleration;
        public float SlowdownDistance => _slowdownDistance;
        public float StopDistance => _stopDistance;
        public float ReproductionIntervalGameHours => _reproductionIntervalGameHours;
        public int MaxPopulation => _maxPopulation;
        public float EggLayIntervalGameHours => _eggLayIntervalGameHours;
        public AnimalEggDefinition EggDefinition => _eggDefinition;
        public float EggLayGrowthThreshold => _eggLayGrowthThreshold;
        public float HuntStartHunger => _huntStartHunger;
        public float HuntHungerRelief => _huntHungerRelief;
        public string PreySpeciesId => _preySpeciesId;
        public int WanderRadiusCells => _wanderRadiusCells;
        public Vector2 IdlePauseRangeSeconds => _idlePauseRangeSeconds;
    }
}
