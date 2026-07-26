using System.Collections.Generic;
using _Project.Scripts.Data.Animals;
using _Project.Scripts.Data.Grid;
using _Project.Scripts.Presentation.Animals;
using _Project.Scripts.Systems.Grid;
using _Project.Scripts.Systems.Pathfinding;
using UnityEngine;
using Object = UnityEngine.Object;

namespace _Project.Scripts.Systems.Animals
{
    /// <summary>
    /// Stores non-blocking egg world entities by cell for future hatch gameplay.
    /// </summary>
    public sealed class AnimalEggService
    {
        private const float MinRotationZDegrees = -15f;
        private const float MaxRotationZDegrees = 15f;

        private static readonly List<AnimalEggRuntimeState> EggsSnapshotBuffer = new List<AnimalEggRuntimeState>();

        private readonly GridState _gridState;
        private readonly GridCoordinateConverter _gridCoordinateConverter;
        private readonly Dictionary<int, AnimalEggRuntimeState> _eggsById = new Dictionary<int, AnimalEggRuntimeState>();
        private readonly Dictionary<Vector2Int, int> _eggIdByCell = new Dictionary<Vector2Int, int>();
        private int _nextEggId = 1;
        private Transform _eggRoot;

        public AnimalEggService(
            GridState gridState,
            GridCoordinateConverter gridCoordinateConverter)
        {
            _gridState = gridState;
            _gridCoordinateConverter = gridCoordinateConverter;
        }

        public bool TrySpawnEgg(AnimalEggDefinition definition, Vector2Int cell, out AnimalEggRuntimeState eggState)
        {
            eggState = null;

            if (definition == null || definition.EggPrefab == null)
            {
                return false;
            }

            if (!_gridState.IsInside(cell.x, cell.y))
            {
                return false;
            }

            if (!CanUseCellForEgg(cell))
            {
                return false;
            }

            float rotationZ = UnityEngine.Random.Range(MinRotationZDegrees, MaxRotationZDegrees);
            Vector2 world = _gridCoordinateConverter.CellToWorldCenter(cell);
            AnimalEggActor actor = Object.Instantiate(
                definition.EggPrefab,
                new Vector3(world.x, world.y, 0f),
                Quaternion.Euler(0f, 0f, rotationZ),
                EnsureEggRoot());
            actor.SetSourceSpeciesId(definition.SourceSpeciesId);
            actor.SnapToWorldPosition(new Vector3(world.x, world.y, 0f));

            eggState = new AnimalEggRuntimeState
            {
                EggId = _nextEggId++,
                Definition = definition,
                Actor = actor,
                Cell = cell,
                AgeGameHours = 0f,
                IsHatching = false
            };

            _eggsById[eggState.EggId] = eggState;
            _eggIdByCell[cell] = eggState.EggId;
            ApplyGravityFall(eggState);
            return true;
        }

        public bool HasEggAtCell(Vector2Int cell)
        {
            return _eggIdByCell.ContainsKey(cell);
        }

        public List<AnimalEggRuntimeState> GetEggsSnapshot()
        {
            EggsSnapshotBuffer.Clear();
            foreach (KeyValuePair<int, AnimalEggRuntimeState> pair in _eggsById)
            {
                EggsSnapshotBuffer.Add(pair.Value);
            }

            return EggsSnapshotBuffer;
        }

        public void TickAll(float gameHoursDelta, System.Action<AnimalDefinition, Vector2Int> onEggHatched)
        {
            if (_eggsById.Count == 0)
            {
                return;
            }

            var eggsSnapshot = GetEggsSnapshot();
            for (int i = 0; i < eggsSnapshot.Count; i++)
            {
                AnimalEggRuntimeState eggState = eggsSnapshot[i];
                if (eggState == null || eggState.Definition == null || eggState.Actor == null || eggState.IsHatching)
                {
                    continue;
                }

                ApplyGravityFall(eggState);
                eggState.AgeGameHours += gameHoursDelta;
                if (eggState.AgeGameHours < eggState.Definition.HatchDurationGameHours)
                {
                    continue;
                }

                eggState.IsHatching = true;
                eggState.Actor.PlayHatchAnimation(
                    eggState.Definition.HatchAnimationDurationSeconds,
                    () => CompleteHatch(eggState, onEggHatched));
            }
        }

        public void SetPaused(bool isPaused)
        {
            foreach (KeyValuePair<int, AnimalEggRuntimeState> pair in _eggsById)
            {
                AnimalEggActor actor = pair.Value != null
                    ? pair.Value.Actor
                    : null;
                actor?.SetPaused(isPaused);
            }
        }

        public void NotifyCellBecameEmpty(Vector2Int emptiedCell)
        {
            if (_eggsById.Count == 0 || !_gridState.IsInside(emptiedCell.x, emptiedCell.y))
            {
                return;
            }

            var eggsSnapshot = GetEggsSnapshot();
            for (int i = 0; i < eggsSnapshot.Count; i++)
            {
                AnimalEggRuntimeState eggState = eggsSnapshot[i];
                if (eggState == null || eggState.Actor == null || eggState.IsHatching)
                {
                    continue;
                }

                ApplyGravityFall(eggState);
            }
        }

        private void CompleteHatch(AnimalEggRuntimeState eggState, System.Action<AnimalDefinition, Vector2Int> onEggHatched)
        {
            if (eggState == null)
            {
                return;
            }

            AnimalDefinition hatchAnimalDefinition = eggState.Definition != null
                ? eggState.Definition.HatchAnimalDefinition
                : null;
            if (hatchAnimalDefinition != null)
            {
                onEggHatched?.Invoke(hatchAnimalDefinition, eggState.Cell);
            }

            RemoveEgg(eggState);
        }

        private void RemoveEgg(AnimalEggRuntimeState eggState)
        {
            _eggsById.Remove(eggState.EggId);
            _eggIdByCell.Remove(eggState.Cell);
            if (eggState.Actor != null)
            {
                Object.Destroy(eggState.Actor.gameObject);
            }
        }

        private bool CanUseCellForEgg(Vector2Int cell)
        {
            return _gridState.IsInside(cell.x, cell.y) && !_eggIdByCell.ContainsKey(cell);
        }

        private void ApplyGravityFall(AnimalEggRuntimeState eggState)
        {
            if (eggState == null || eggState.Actor == null || !_gridState.IsInside(eggState.Cell.x, eggState.Cell.y))
            {
                return;
            }

            Vector2Int landingCell = eggState.Cell;
            while (TryGetFallTargetCell(landingCell, eggState.EggId, out Vector2Int nextCell))
            {
                landingCell = nextCell;
            }

            if (landingCell == eggState.Cell)
            {
                return;
            }

            _eggIdByCell.Remove(eggState.Cell);
            eggState.Cell = landingCell;
            _eggIdByCell[eggState.Cell] = eggState.EggId;

            Vector2 world = _gridCoordinateConverter.CellToWorldCenter(eggState.Cell);
            eggState.Actor.SnapToWorldPosition(new Vector3(world.x, world.y, 0f));
        }

        private bool TryGetFallTargetCell(Vector2Int currentCell, int eggId, out Vector2Int nextCell)
        {
            nextCell = currentCell;

            ref readonly Cell cell = ref _gridState.GetCell(currentCell.x, currentCell.y);
            Vector2Int down = MovementSupportRules.GetDownDirection(cell);
            Vector2Int candidateCell = currentCell + down;
            if (!_gridState.IsInside(candidateCell.x, candidateCell.y))
            {
                return false;
            }

            if (IsSupportingEgg(candidateCell, eggId))
            {
                return false;
            }

            // Egg can pass through ladder cells; ladder is not a resting support for it.
            nextCell = candidateCell;
            return true;
        }

        private bool IsSupportingEgg(Vector2Int supportCell, int fallingEggId)
        {
            if (_eggIdByCell.TryGetValue(supportCell, out int supportEggId) && supportEggId != fallingEggId)
            {
                return true;
            }

            ref readonly Cell cell = ref _gridState.GetCell(supportCell.x, supportCell.y);
            if (MovementSupportRules.IsLadderCell(cell))
            {
                return false;
            }

            if (MovementSupportRules.IsBridgeCell(cell))
            {
                return true;
            }

            return !MovementSupportRules.IsAirCell(cell);
        }

        private Transform EnsureEggRoot()
        {
            if (_eggRoot != null)
            {
                return _eggRoot;
            }

            var rootObject = new GameObject("AnimalEggs");
            _eggRoot = rootObject.transform;
            return _eggRoot;
        }
    }
}