using System;
using _Project.Scripts.Data.Character;
using UnityEngine;

namespace _Project.Scripts.Data.Character
{
    /// <summary>
    /// Конфиг спауна: сколько, какие персонажи и зона появления.
    /// </summary>
    [CreateAssetMenu(
        fileName = "CharacterSpawnConfig",
        menuName = "Artemis/Character/Spawn Config")]
    public sealed class CharacterSpawnConfig : ScriptableObject
    {
        [Header("How many characters to spawn")]
        [SerializeField] [Min(1)] private int _spawnCount = 3;

        [Header("Who can be spawned")]
        [SerializeField] private CharacterDefinition[] _definitions;

        [Header("Spawn area (world space)")]
        [SerializeField] private Vector2 _minWorldPosition = new(-5f, 0f);
        [SerializeField] private Vector2 _maxWorldPosition = new(5f, 5f);

        public int SpawnCount => _spawnCount;
        public CharacterDefinition[] Definitions => _definitions;
        public Vector2 MinWorldPosition => _minWorldPosition;
        public Vector2 MaxWorldPosition => _maxWorldPosition;

        public bool HasDefinitions()
        {
            return _definitions != null && _definitions.Length > 0;
        }
    }
}