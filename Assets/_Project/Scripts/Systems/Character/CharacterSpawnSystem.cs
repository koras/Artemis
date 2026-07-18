using System;
using System.Collections.Generic;
using _Project.Scripts.Data.Character;
using _Project.Scripts.Data.Grid;
using _Project.Scripts.Presentation.Character;
using UnityEngine;
using Object = UnityEngine.Object;

namespace _Project.Scripts.Systems.Character
{
    /// <summary>
    /// Spawns characters onto the grid using the project's fixed top-row layout rule.
    /// </summary>
    public sealed class CharacterSpawnSystem
    {
        private readonly CharacterSpawnConfig _spawnConfig;
        private readonly GridState _gridState;
        private readonly Vector2 _gridOrigin;
        private readonly Transform _spawnRoot;
        private readonly System.Random _random;
        private readonly List<string> _foodResourceIds;

        public CharacterSpawnSystem(
            CharacterSpawnConfig spawnConfig,
            GridState gridState,
            Vector2 gridOrigin,
            Transform spawnRoot,
            int randomSeed,
            IReadOnlyList<string> foodResourceIds)
        {
            _spawnConfig = spawnConfig;
            _gridState = gridState;
            _gridOrigin = gridOrigin;
            _spawnRoot = spawnRoot;
            _random = new System.Random(randomSeed);
            _foodResourceIds = foodResourceIds != null
                ? new List<string>(foodResourceIds)
                : new List<string>();
        }

        public List<CharacterActor> SpawnAll()
        {
            ValidateInput();

            var result = new List<CharacterActor>(_spawnConfig.SpawnCount);

            for (int i = 0; i < _spawnConfig.SpawnCount; i++)
            {
                var definition = PickDefinition();
                var spawnPosition = BuildSpawnWorldPositionForIndex(i);

                var actor = Object.Instantiate(
                    definition.CharacterPrefab,
                    spawnPosition,
                    Quaternion.identity,
                    _spawnRoot);

                // Apply start needs and runtime-generated food preferences.
                actor.InitializeRandomSkin(_random);
                actor.SetHunger(definition.StartHunger);
                actor.SetSleepDesire(definition.StartSleepDesire);
                actor.SetMood(definition.StartMood);
                actor.SetFoodPreferences(BuildFoodPreferences());

                result.Add(actor);
            }

            return result;
        }

        private Vector3 BuildSpawnWorldPositionForIndex(int spawnIndex)
        {
            int centerX = _gridState.Width / 2;
            int y = _gridState.Height - 5; // Fixed fifth row from the top.

            int offsetX = GetHorizontalOffset(spawnIndex);
            int x = centerX + offsetX;

            // Clamp X so the spawn layout never escapes the grid width.
            x = Mathf.Clamp(x, 0, _gridState.Width - 1);

            float worldX = _gridOrigin.x + (x + 0.5f) * _gridState.CellSize;
            float worldY = _gridOrigin.y + (y + 0.5f) * _gridState.CellSize;

            return new Vector3(worldX, worldY, 0f);
        }

        /// <summary>
        /// 0 -> 0 (center), 1 -> +1, 2 -> -1, 3 -> +2, 4 -> -2...
        /// Only horizontal neighboring cells are used.
        /// </summary>
        private static int GetHorizontalOffset(int spawnIndex)
        {
            if (spawnIndex == 0)
            {
                return 0;
            }

            int step = (spawnIndex + 1) / 2; // 1,1,2,2,3,3...
            bool toRight = (spawnIndex % 2) == 1;

            return toRight ? step : -step;
        }

        private void ValidateInput()
        {
            if (_spawnConfig == null)
            {
                throw new InvalidOperationException("CharacterSpawnConfig is not assigned.");
            }

            if (_spawnConfig.Definitions == null || _spawnConfig.Definitions.Length == 0)
            {
                throw new InvalidOperationException("Character definitions are not configured.");
            }

            if (_gridState == null)
            {
                throw new InvalidOperationException("GridState is not assigned.");
            }

            if (_gridState.Height < 5)
            {
                throw new InvalidOperationException("Grid height is too small for top-5-row spawn rule.");
            }
        }

        private Dictionary<string, int> BuildFoodPreferences()
        {
            var result = new Dictionary<string, int>(_foodResourceIds.Count);

            for (int i = 0; i < _foodResourceIds.Count; i++)
            {
                string resourceId = _foodResourceIds[i];
                if (string.IsNullOrWhiteSpace(resourceId))
                {
                    continue;
                }

                result[resourceId] = _random.Next(0, 11);
            }

            return result;
        }

        private Vector3 BuildSpawnWorldPosition()
        {
            // Middle column.
            int x = _gridState.Width / 2;

            // Fifth row from the top (top row = Height - 1).
            int y = _gridState.Height - 5;

            float worldX = _gridOrigin.x + (x + 0.5f) * _gridState.CellSize;
            float worldY = _gridOrigin.y + (y + 0.5f) * _gridState.CellSize;

            return new Vector3(worldX, worldY, 0f);
        }

        private CharacterDefinition PickDefinition()
        {
            int index = _random.Next(0, _spawnConfig.Definitions.Length);
            return _spawnConfig.Definitions[index];
        }
    }
}
