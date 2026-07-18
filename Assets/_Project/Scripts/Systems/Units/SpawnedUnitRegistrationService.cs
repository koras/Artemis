﻿using System;
using System.Collections.Generic;
using _Project.Scripts.Presentation.Character;
using _Project.Scripts.Systems.Grid;
using UnityEngine;


namespace _Project.Scripts.Systems.Units
{
    /// <summary>
    /// Регистрирует заспавненных персонажей в оркестраторе задач юнитов.
    /// </summary>
    public static class SpawnedUnitRegistrationService
    {
        private static readonly string[] NamePrefixes =
        {
            "Astra", "Luna", "Nova", "Orion", "Vega", "Argo", "Helios", "Rhea", "Atlas", "Nix"
        };

        private static readonly string[] NameSuffixes =
        {
            "Stone", "Vale", "Ray", "Drift", "Ward", "Flint", "Brook", "Quill", "Frost", "Dawn"
        };

        /// <summary>
        /// Назначает id и стартовые клетки всем персонажам и регистрирует их в orchestrator.
        /// </summary>
        // Method RegisterAll: executes the RegisterAll workflow.
        public static void RegisterAll(
            IReadOnlyList<CharacterActor> spawnedCharacters,
            GridCoordinateConverter gridCoordinateConverter,
            UnitTaskOrchestratorService unitTaskOrchestratorService)
        {
            int unitId = 1;
            var random = new System.Random(73123);

            for (int i = 0; i < spawnedCharacters.Count; i++)
            {
                CharacterActor actor = spawnedCharacters[i];
                if (actor == null) continue;

                Vector2 actorWorld = new Vector2(actor.transform.position.x, actor.transform.position.y);
                Vector2Int startCell = gridCoordinateConverter.WorldToCell(actorWorld);

                // Фиксируем стартовую позицию как текущую и целевую, чтобы не было первого рывка.
                actor.SnapToWorldPosition(actor.transform.position);

                string characterNameKey = $"character_{unitId:0000}";
                string displayName = GenerateRandomDisplayName(random);
                unitTaskOrchestratorService.RegisterUnit(unitId, actor, startCell, displayName, characterNameKey);
                unitId++;
            }
        }

        /// <summary>
        /// Генерирует временное рандомное имя без привязки к данным персонажа.
        /// </summary>
        // Method GenerateRandomDisplayName: executes the GenerateRandomDisplayName workflow.
        private static string GenerateRandomDisplayName(System.Random random)
        {
            int prefixIndex = random.Next(0, NamePrefixes.Length);
            int suffixIndex = random.Next(0, NameSuffixes.Length);
            int serial = random.Next(10, 99);
            return $"{NamePrefixes[prefixIndex]} {NameSuffixes[suffixIndex]}-{serial}";
        }
    }
}
