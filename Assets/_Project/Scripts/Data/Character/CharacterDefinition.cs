using _Project.Scripts.Presentation.Character;
using UnityEngine;

namespace _Project.Scripts.Data.Character
{
    /// <summary>
    /// Character type definition: prefab and start need values for spawned units.
    /// </summary>
    [CreateAssetMenu(
        fileName = "CharacterDefinition",
        menuName = "Artemis/Character/Definition")]
    public sealed class CharacterDefinition : ScriptableObject
    {
        [SerializeField] private CharacterActor _characterPrefab;

        [Header("Start Needs")]
        [SerializeField] [Range(0, 300)] private int _startHunger = 50;
        [SerializeField] [Range(0, 300)] private int _startSleepDesire = 50;
        [SerializeField] [Range(0, 100)] private int _startMood = 60;

        public CharacterActor CharacterPrefab => _characterPrefab;
        public int StartHunger => _startHunger;
        public int StartSleepDesire => _startSleepDesire;
        public int StartMood => _startMood;
    }
}
