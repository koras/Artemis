using _Project.Scripts.Presentation.Animals;
using UnityEngine;

namespace _Project.Scripts.Systems.Animals
{
    /// <summary>
    /// Resolves world clicks into selected animal diagnostics without touching unit task flow.
    /// </summary>
    public sealed class AnimalWorldSelectionService
    {
        private readonly AnimalSimulationService _animalSimulationService;
        private int _selectedAnimalId;

        public AnimalWorldSelectionService(AnimalSimulationService animalSimulationService)
        {
            _animalSimulationService = animalSimulationService;
        }

        public bool TryHandleWorldClick(Vector2 worldPoint, out AnimalDiagnosticsSnapshot snapshot)
        {
            snapshot = default;

            Collider2D[] hits = Physics2D.OverlapPointAll(worldPoint);
            if (hits == null || hits.Length == 0)
            {
                return false;
            }

            AnimalActor bestActor = null;
            int bestSortingLayerValue = int.MinValue;
            int bestSortingOrder = int.MinValue;
            float bestCenterDistanceSqr = float.MaxValue;

            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];
                if (hit == null)
                {
                    continue;
                }

                AnimalActor actor = hit.GetComponentInParent<AnimalActor>();
                if (actor == null)
                {
                    continue;
                }

                SpriteRenderer renderer = actor.SelectionRenderer;
                int sortingLayerValue = renderer != null
                    ? SortingLayer.GetLayerValueFromID(renderer.sortingLayerID)
                    : int.MinValue;
                int sortingOrder = renderer != null
                    ? renderer.sortingOrder
                    : int.MinValue;
                Vector3 center = renderer != null
                    ? renderer.bounds.center
                    : actor.transform.position;
                float centerDistanceSqr = ((Vector2)center - worldPoint).sqrMagnitude;

                if (bestActor != null
                    && sortingLayerValue < bestSortingLayerValue)
                {
                    continue;
                }

                if (bestActor != null
                    && sortingLayerValue == bestSortingLayerValue
                    && sortingOrder < bestSortingOrder)
                {
                    continue;
                }

                if (bestActor != null
                    && sortingLayerValue == bestSortingLayerValue
                    && sortingOrder == bestSortingOrder
                    && centerDistanceSqr >= bestCenterDistanceSqr)
                {
                    continue;
                }

                bestActor = actor;
                bestSortingLayerValue = sortingLayerValue;
                bestSortingOrder = sortingOrder;
                bestCenterDistanceSqr = centerDistanceSqr;
            }

            if (bestActor == null
                || !_animalSimulationService.TryGetAnimalDiagnosticsSnapshot(bestActor, out snapshot))
            {
                return false;
            }

            _selectedAnimalId = snapshot.AnimalId;
            return true;
        }

        public bool TryGetSelectedSnapshot(out AnimalDiagnosticsSnapshot snapshot)
        {
            snapshot = default;
            return _selectedAnimalId != 0
                && _animalSimulationService.TryGetAnimalDiagnosticsSnapshot(_selectedAnimalId, out snapshot);
        }
    }
}
