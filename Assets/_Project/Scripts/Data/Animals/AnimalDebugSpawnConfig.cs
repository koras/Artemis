using System;
using UnityEngine;

namespace _Project.Scripts.Data.Animals
{
    /// <summary>
    /// Temporary scene-owned debug spawn settings for one animal species.
    /// </summary>
    [Serializable]
    public sealed class AnimalDebugSpawnConfig
    {
        [SerializeField] private AnimalDefinition _definition;
        [SerializeField] [Min(0)] private int _initialSpawnCount = 0;
        [SerializeField] private Vector2Int _spawnCell = new Vector2Int(50, 93);
        [SerializeField] private int _spawnSeed = 12345;

        public AnimalDefinition Definition => _definition;
        public int InitialSpawnCount => _initialSpawnCount;
        public Vector2Int SpawnCell => _spawnCell;
        public int SpawnSeed => _spawnSeed;
    }
}
