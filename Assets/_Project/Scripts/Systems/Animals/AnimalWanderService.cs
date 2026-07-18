using _Project.Scripts.Data.Animals;
using _Project.Scripts.Data.Pathfinding;
using _Project.Scripts.Systems.Navigation;
using UnityEngine;
using _Project.Scripts.Data.Grid;

namespace _Project.Scripts.Systems.Animals
{
    /// <summary>
    /// Chooses local random wander goals that are valid under the shared navigation rules.
    /// </summary>
    public sealed class AnimalWanderService
    {
        private const int TargetSelectionAttempts = 10;

        private readonly GridState _gridState;
        private readonly CharacterNavigationService _navigationService;

        public AnimalWanderService(
            GridState gridState,
            CharacterNavigationService navigationService)
        {
            _gridState = gridState;
            _navigationService = navigationService;
        }

        public bool TryAssignRandomGoal(
            AnimalRuntimeState state,
            AnimalDefinition definition,
            System.Random random)
        {
            int radius = Mathf.Max(1, definition.WanderRadiusCells);

            for (int attempt = 0; attempt < TargetSelectionAttempts; attempt++)
            {
                Vector2Int candidateCell = BuildCandidateCell(state.CurrentCell, radius, random);
                if (!_gridState.IsInside(candidateCell.x, candidateCell.y))
                {
                    continue;
                }

                if (candidateCell == state.CurrentCell)
                {
                    continue;
                }

                bool hasPath = _navigationService.TryBuildPath(
                    state.AnimalId,
                    state.CurrentCell,
                    candidateCell,
                    out PathResult path);

                if (!hasPath || path.Edges.Count == 0)
                {
                    continue;
                }

                state.GoalCell = candidateCell;
                state.HasGoalCell = true;
                return true;
            }

            return false;
        }

        public float PickIdlePauseSeconds(AnimalDefinition definition, System.Random random)
        {
            Vector2 pauseRange = definition.IdlePauseRangeSeconds;
            float minPause = Mathf.Max(0f, Mathf.Min(pauseRange.x, pauseRange.y));
            float maxPause = Mathf.Max(minPause, Mathf.Max(pauseRange.x, pauseRange.y));
            double random01 = random.NextDouble();
            return minPause + (float)random01 * (maxPause - minPause);
        }

        private static Vector2Int BuildCandidateCell(Vector2Int originCell, int radius, System.Random random)
        {
            int offsetX = random.Next(-radius, radius + 1);
            int offsetY = random.Next(-radius, radius + 1);
            return new Vector2Int(originCell.x + offsetX, originCell.y + offsetY);
        }
    }
}
