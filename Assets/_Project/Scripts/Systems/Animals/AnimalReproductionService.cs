using System.Collections.Generic;
using _Project.Scripts.Data.Animals;
using _Project.Scripts.Data.Pathfinding;
using _Project.Scripts.Systems.Grid;
using _Project.Scripts.Systems.Navigation;
using UnityEngine;
using _Project.Scripts.Data.Grid;

namespace _Project.Scripts.Systems.Animals
{
    /// <summary>
    /// Handles timer-based reproduction and valid nearby child placement for animals.
    /// </summary>
    public sealed class AnimalReproductionService
    {
        private const int BirthPlacementAttempts = 8;
        private const int BirthPlacementRadiusCells = 2;

        private readonly GridState _gridState;
        private readonly CharacterNavigationService _navigationService;
        private readonly GridCoordinateConverter _gridCoordinateConverter;

        public AnimalReproductionService(
            GridState gridState,
            CharacterNavigationService navigationService,
            GridCoordinateConverter gridCoordinateConverter)
        {
            _gridState = gridState;
            _navigationService = navigationService;
            _gridCoordinateConverter = gridCoordinateConverter;
        }

        public bool TryBuildBirthRequest(
            AnimalRuntimeState parentState,
            Dictionary<string, int> populationBySpeciesId,
            HashSet<Vector2Int> occupiedCells,
            System.Random random,
            out AnimalBirthRequest birthRequest)
        {
            birthRequest = default;

            AnimalDefinition definition = parentState.Definition;
            if (definition == null)
            {
                return false;
            }

            if (definition.LifecycleMode != AnimalLifecycleMode.LiveBirth)
            {
                return false;
            }

            if (parentState.ReproductionElapsedGameHours < definition.ReproductionIntervalGameHours)
            {
                return false;
            }

            parentState.ReproductionElapsedGameHours = 0f;

            int population = populationBySpeciesId.TryGetValue(definition.SpeciesId, out int currentPopulation)
                ? currentPopulation
                : 0;
            if (population >= definition.MaxPopulation)
            {
                return false;
            }

            if (!TryFindBirthCell(parentState, occupiedCells, random, out Vector2Int birthCell))
            {
                return false;
            }

            birthRequest = new AnimalBirthRequest(definition, birthCell);
            return true;
        }

        private bool TryFindBirthCell(
            AnimalRuntimeState parentState,
            HashSet<Vector2Int> occupiedCells,
            System.Random random,
            out Vector2Int birthCell)
        {
            for (int attempt = 0; attempt < BirthPlacementAttempts; attempt++)
            {
                int offsetX = random.Next(-BirthPlacementRadiusCells, BirthPlacementRadiusCells + 1);
                int offsetY = random.Next(-BirthPlacementRadiusCells, BirthPlacementRadiusCells + 1);
                birthCell = new Vector2Int(parentState.CurrentCell.x + offsetX, parentState.CurrentCell.y + offsetY);

                if (!_gridState.IsInside(birthCell.x, birthCell.y))
                {
                    continue;
                }

                if (birthCell == parentState.CurrentCell)
                {
                    continue;
                }

                if (occupiedCells.Contains(birthCell))
                {
                    continue;
                }

                bool hasPath = _navigationService.TryBuildPath(
                    -parentState.AnimalId,
                    parentState.CurrentCell,
                    birthCell,
                    out PathResult path);

                if (!hasPath || path.Edges.Count == 0)
                {
                    continue;
                }

                return true;
            }

            birthCell = parentState.CurrentCell;
            return false;
        }
    }

    public readonly struct AnimalBirthRequest
    {
        public readonly AnimalDefinition Definition;
        public readonly Vector2Int BirthCell;

        public AnimalBirthRequest(
            AnimalDefinition definition,
            Vector2Int birthCell)
        {
            Definition = definition;
            BirthCell = birthCell;
        }
    }
}
