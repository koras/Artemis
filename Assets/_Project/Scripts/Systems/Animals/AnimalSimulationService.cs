using System.Collections.Generic;
using _Project.Scripts.Data.Animals;
using _Project.Scripts.Data.Pathfinding;
using _Project.Scripts.Presentation.Animals;
using _Project.Scripts.Systems.Grid;
using _Project.Scripts.Systems.Navigation;
using UnityEngine;
using Object = UnityEngine.Object;
using _Project.Scripts.Data.Grid;

namespace _Project.Scripts.Systems.Animals
{
    /// <summary>
    /// Owns the full animal simulation loop: debug spawn, needs, growth, wandering, and reproduction.
    /// </summary>
    public sealed class AnimalSimulationService
    {
        private const int FirstAnimalId = 1000000;
        private const float GAME_MINUTES_PER_REAL_SECOND = 2f;
        private const int MaxEggsPerGrowthBasedAnimal = 2;

        private static readonly List<AnimalBirthRequest> BirthRequestsBuffer = new List<AnimalBirthRequest>();
        private static readonly Dictionary<string, int> PopulationBySpeciesIdBuffer = new Dictionary<string, int>(System.StringComparer.Ordinal);
        private static readonly HashSet<Vector2Int> OccupiedCellsBuffer = new HashSet<Vector2Int>();

        private readonly GridState _gridState;
        private readonly GridCoordinateConverter _gridCoordinateConverter;
        private readonly CharacterNavigationService _navigationService;
        private readonly AnimalWanderService _wanderService;
        private readonly AnimalReproductionService _reproductionService;
        private readonly AnimalEggService _animalEggService;
        private readonly AnimalDebugSpawnConfig[] _debugSpawnConfigs;
        private readonly List<AnimalRuntimeState> _states = new List<AnimalRuntimeState>();
        private readonly List<AnimalRuntimeState> _pendingRemovedStates = new List<AnimalRuntimeState>();
        private readonly HashSet<int> _pendingRemovedAnimalIds = new HashSet<int>();
        private readonly Dictionary<int, AnimalRuntimeState> _stateByAnimalId = new Dictionary<int, AnimalRuntimeState>();
        private readonly Dictionary<AnimalActor, AnimalRuntimeState> _stateByActor = new Dictionary<AnimalActor, AnimalRuntimeState>();
        private readonly Dictionary<string, System.Random> _randomBySpeciesId = new Dictionary<string, System.Random>(System.StringComparer.Ordinal);

        private int _nextAnimalId = FirstAnimalId;

        public AnimalSimulationService(
            GridState gridState,
            GridCoordinateConverter gridCoordinateConverter,
            CharacterNavigationService navigationService,
            AnimalEggService animalEggService,
            AnimalDebugSpawnConfig[] debugSpawnConfigs)
        {
            _gridState = gridState;
            _gridCoordinateConverter = gridCoordinateConverter;
            _navigationService = navigationService;
            _animalEggService = animalEggService;
            _debugSpawnConfigs = debugSpawnConfigs;
            _wanderService = new AnimalWanderService(_gridState, _navigationService);
            _reproductionService = new AnimalReproductionService(_gridState, _navigationService, _gridCoordinateConverter);
        }

        public void SpawnDebugAnimals()
        {
            if (_debugSpawnConfigs == null || _debugSpawnConfigs.Length == 0)
            {
                return;
            }

            for (int configIndex = 0; configIndex < _debugSpawnConfigs.Length; configIndex++)
            {
                AnimalDebugSpawnConfig debugSpawnConfig = _debugSpawnConfigs[configIndex];
                if (debugSpawnConfig == null || debugSpawnConfig.Definition == null)
                {
                    continue;
                }

                System.Random random = GetSpeciesRandom(debugSpawnConfig.Definition.SpeciesId, debugSpawnConfig.SpawnSeed);
                for (int spawnIndex = 0; spawnIndex < debugSpawnConfig.InitialSpawnCount; spawnIndex++)
                {
                    Vector2Int spawnCell = BuildDebugSpawnCell(debugSpawnConfig.SpawnCell, spawnIndex);
                    SpawnAnimal(debugSpawnConfig.Definition, spawnCell, random);
                }
            }
        }

        public void TickAll(float tickSeconds)
        {
            float gameHoursDelta = (tickSeconds * GAME_MINUTES_PER_REAL_SECOND) / 60f;
            if (_states.Count == 0)
            {
                _animalEggService?.TickAll(gameHoursDelta, HandleEggHatched);
                return;
            }

            int stateCount = _states.Count;

            for (int i = 0; i < stateCount; i++)
            {
                AnimalRuntimeState state = _states[i];
                if (state.Actor == null || state.Definition == null)
                {
                    continue;
                }

                if (_pendingRemovedAnimalIds.Contains(state.AnimalId))
                {
                    continue;
                }

                TickStats(state, gameHoursDelta);
                TickHunting(state);
                TickMovementAndWander(state, tickSeconds);
            }

            ProcessPendingAnimalRemovals();
            ProcessSpeciesLifecycle();
            _animalEggService?.TickAll(gameHoursDelta, HandleEggHatched);
        }

        public void TickMovementFrame(float deltaTime)
        {
            for (int i = 0; i < _states.Count; i++)
            {
                AnimalActor actor = _states[i].Actor;
                if (actor == null)
                {
                    continue;
                }

                actor.TickMovementFrame(deltaTime);
            }
        }

        public void SetPaused(bool isPaused)
        {
            _animalEggService?.SetPaused(isPaused);
        }

        public bool TryGetAnimalDiagnosticsSnapshot(int animalId, out AnimalDiagnosticsSnapshot snapshot)
        {
            snapshot = default;
            return _stateByAnimalId.TryGetValue(animalId, out AnimalRuntimeState state)
                && TryBuildDiagnosticsSnapshot(state, out snapshot);
        }

        public bool TryGetAnimalDiagnosticsSnapshot(AnimalActor actor, out AnimalDiagnosticsSnapshot snapshot)
        {
            snapshot = default;
            return actor != null
                && _stateByActor.TryGetValue(actor, out AnimalRuntimeState state)
                && TryBuildDiagnosticsSnapshot(state, out snapshot);
        }

        public bool HasAnimalAtCell(Vector2Int cell)
        {
            for (int i = 0; i < _states.Count; i++)
            {
                AnimalRuntimeState state = _states[i];
                if (state != null && state.Actor != null && state.CurrentCell == cell)
                {
                    return true;
                }
            }

            return false;
        }

        private void TickStats(AnimalRuntimeState state, float gameHoursDelta)
        {
            AnimalDefinition definition = state.Definition;
            state.Hunger += gameHoursDelta * definition.HungerIncreasePerGameHour;
            state.AgeGameHours += gameHoursDelta;
            state.ReproductionElapsedGameHours += gameHoursDelta;
            state.EggLayElapsedGameHours += gameHoursDelta;

            if (definition.GrowthPerFeeding > 0f)
            {
                state.GrowthProgress = Mathf.Clamp01(state.GrowthProgress);
            }
            else if (definition.GrowthDurationGameHours <= 0.01f)
            {
                state.GrowthProgress = 1f;
            }
            else
            {
                state.GrowthProgress = Mathf.Clamp01(state.GrowthProgress + gameHoursDelta / definition.GrowthDurationGameHours);
            }

            state.Actor.SetHunger(state.Hunger);
            state.Actor.SetAgeGameHours(state.AgeGameHours);
            state.Actor.SetLoyalty(state.Loyalty);
            state.Actor.SetGrowthProgress(
                state.GrowthProgress,
                definition.MinVisualScale,
                definition.MaxVisualScale);
        }

        private void TickMovementAndWander(AnimalRuntimeState state, float tickSeconds)
        {
            AnimalDefinition definition = state.Definition;
            System.Random random = GetSpeciesRandom(definition.SpeciesId, 0);
            RefreshActiveHuntGoal(state);

            if (state.HasGoalCell)
            {
                if (!state.Actor.IsAtMoveTarget())
                {
                    return;
                }

                NavigationStepResult stepResult = _navigationService.TryStep(
                    state.AnimalId,
                    ref state.CurrentCell,
                    state.GoalCell,
                    out Vector2Int fromCell,
                    out Vector2Int toCell,
                    out MovementActionType actionType);

                if (stepResult == NavigationStepResult.Stepped)
                {
                    state.Actor.SetFacing(toCell - fromCell);
                    ApplyActorMoveTarget(state, fromCell, toCell, actionType);
                    return;
                }

                state.HasGoalCell = false;
                state.IdleRemainingSeconds = _wanderService.PickIdlePauseSeconds(definition, random);
                _navigationService.ClearPath(state.AnimalId);
                return;
            }

            if (TryAssignHuntGoal(state))
            {
                return;
            }

            if (state.IdleRemainingSeconds > 0f)
            {
                state.IdleRemainingSeconds = Mathf.Max(0f, state.IdleRemainingSeconds - tickSeconds);
                return;
            }

            if (_wanderService.TryAssignRandomGoal(state, definition, random))
            {
                return;
            }

            state.IdleRemainingSeconds = _wanderService.PickIdlePauseSeconds(definition, random);
        }

        private void TickHunting(AnimalRuntimeState hunterState)
        {
            AnimalDefinition definition = hunterState.Definition;
            if (definition == null
                || string.IsNullOrWhiteSpace(definition.PreySpeciesId)
                || definition.HuntStartHunger <= 0f
                || definition.HuntHungerRelief <= 0f)
            {
                hunterState.HuntTargetAnimalId = 0;
                return;
            }

            if (hunterState.Hunger < definition.HuntStartHunger)
            {
                hunterState.HuntTargetAnimalId = 0;
                return;
            }

            AnimalRuntimeState preyState = ResolveBestPreyTarget(hunterState);
            if (preyState == null)
            {
                hunterState.HuntTargetAnimalId = 0;
                return;
            }

            hunterState.HuntTargetAnimalId = preyState.AnimalId;
            if (hunterState.CurrentCell != preyState.CurrentCell)
            {
                return;
            }

            // Hunting resolves only when predator and prey meet in the same simulation cell.
            hunterState.Hunger = Mathf.Max(0f, hunterState.Hunger - definition.HuntHungerRelief);
            hunterState.Actor.SetHunger(hunterState.Hunger);
            hunterState.HuntTargetAnimalId = 0;
            hunterState.EatenPreyCount++;
            ApplyGrowthFromFeeding(hunterState);
            QueueAnimalRemoval(preyState);
            _navigationService.ClearPath(hunterState.AnimalId);
            hunterState.HasGoalCell = false;
            hunterState.GoalCell = hunterState.CurrentCell;
            hunterState.IdleRemainingSeconds = 0f;
            Debug.Log($"[AnimalHunt] {definition.DisplayName} ate {preyState.Definition.DisplayName} at cell ({hunterState.CurrentCell.x},{hunterState.CurrentCell.y}).");
        }

        private bool TryAssignHuntGoal(AnimalRuntimeState hunterState)
        {
            AnimalDefinition definition = hunterState.Definition;
            if (definition == null
                || string.IsNullOrWhiteSpace(definition.PreySpeciesId)
                || definition.HuntStartHunger <= 0f
                || hunterState.Hunger < definition.HuntStartHunger)
            {
                return false;
            }

            AnimalRuntimeState preyState = ResolveBestPreyTarget(hunterState);
            if (preyState == null)
            {
                hunterState.HuntTargetAnimalId = 0;
                return false;
            }

            hunterState.HuntTargetAnimalId = preyState.AnimalId;
            hunterState.GoalCell = preyState.CurrentCell;
            hunterState.HasGoalCell = true;
            hunterState.IdleRemainingSeconds = 0f;
            return true;
        }

        private void RefreshActiveHuntGoal(AnimalRuntimeState hunterState)
        {
            if (hunterState.HuntTargetAnimalId == 0)
            {
                return;
            }

            if (!_stateByAnimalId.TryGetValue(hunterState.HuntTargetAnimalId, out AnimalRuntimeState preyState)
                || preyState == null
                || preyState.Actor == null
                || _pendingRemovedAnimalIds.Contains(preyState.AnimalId))
            {
                hunterState.HuntTargetAnimalId = 0;
                return;
            }

            hunterState.GoalCell = preyState.CurrentCell;
            hunterState.HasGoalCell = true;
            hunterState.IdleRemainingSeconds = 0f;
        }

        private void ProcessSpeciesLifecycle()
        {
            if (_states.Count == 0)
            {
                return;
            }

            int stateCount = _states.Count;
            Dictionary<string, int> populationBySpeciesId = BuildPopulationBySpeciesIdBuffer();
            HashSet<Vector2Int> occupiedCells = BuildOccupiedCellsBuffer();
            BirthRequestsBuffer.Clear();

            for (int i = 0; i < stateCount; i++)
            {
                AnimalRuntimeState state = _states[i];
                if (state.Actor == null || state.Definition == null)
                {
                    continue;
                }

                if (state.Definition.LifecycleMode == AnimalLifecycleMode.EggLayer)
                {
                    TryLayEgg(state);
                    continue;
                }

                if (state.Definition.LifecycleMode != AnimalLifecycleMode.LiveBirth)
                {
                    continue;
                }

                System.Random random = GetSpeciesRandom(state.Definition.SpeciesId, 0);
                bool hasBirthRequest = _reproductionService.TryBuildBirthRequest(
                    state,
                    populationBySpeciesId,
                    occupiedCells,
                    random,
                    out AnimalBirthRequest birthRequest);

                if (!hasBirthRequest)
                {
                    continue;
                }

                BirthRequestsBuffer.Add(birthRequest);
                populationBySpeciesId[state.Definition.SpeciesId]++;
                occupiedCells.Add(birthRequest.BirthCell);
            }

            for (int i = 0; i < BirthRequestsBuffer.Count; i++)
            {
                AnimalBirthRequest request = BirthRequestsBuffer[i];
                System.Random random = GetSpeciesRandom(request.Definition.SpeciesId, 0);
                SpawnAnimal(request.Definition, request.BirthCell, random);
            }
        }

        private void TryLayEgg(AnimalRuntimeState state)
        {
            AnimalDefinition definition = state.Definition;
            if (definition == null || definition.EggDefinition == null || _animalEggService == null)
            {
                return;
            }

            if (definition.GrowthPerFeeding > 0f)
            {
                if (state.GrowthProgress < definition.EggLayGrowthThreshold)
                {
                    return;
                }

                if (state.EggsLaidCount >= MaxEggsPerGrowthBasedAnimal)
                {
                    return;
                }
            }

            if (state.EggLayElapsedGameHours < definition.EggLayIntervalGameHours)
            {
                return;
            }

            if (_animalEggService.TrySpawnEgg(definition.EggDefinition, state.CurrentCell, out _))
            {
                state.EggLayElapsedGameHours = 0f;
                state.EggsLaidCount++;
            }
        }

        private void HandleEggHatched(AnimalDefinition definition, Vector2Int spawnCell)
        {
            if (definition == null)
            {
                return;
            }

            System.Random random = GetSpeciesRandom(definition.SpeciesId, 0);
            SpawnAnimal(definition, spawnCell, random);
        }

        private AnimalRuntimeState SpawnAnimal(
            AnimalDefinition definition,
            Vector2Int spawnCell,
            System.Random random)
        {
            Vector3 spawnWorldPosition = BuildWorldPosition(spawnCell);
            AnimalActor actor = Object.Instantiate(
                definition.AnimalPrefab,
                spawnWorldPosition,
                Quaternion.identity);

            actor.SnapToWorldPosition(spawnWorldPosition);
            int loyalty = RollStartLoyalty(definition, random);

            var state = new AnimalRuntimeState
            {
                AnimalId = _nextAnimalId++,
                Definition = definition,
                Actor = actor,
                CurrentCell = spawnCell,
                GoalCell = spawnCell,
                HasGoalCell = false,
                IdleRemainingSeconds = _wanderService.PickIdlePauseSeconds(definition, random),
                Hunger = definition.StartHunger,
                Loyalty = loyalty,
                AgeGameHours = 0f,
                GrowthProgress = definition.StartGrowth,
                MovementSpeedMultiplier = definition.MovementSpeedMultiplier,
                ReproductionElapsedGameHours = 0f,
                EggsLaidCount = 0,
                HuntTargetAnimalId = 0,
                EatenPreyCount = 0
            };

            actor.SetHunger(state.Hunger);
            actor.SetAgeGameHours(state.AgeGameHours);
            actor.SetLoyalty(state.Loyalty);
            actor.SetMovementSpeedMultiplier(state.MovementSpeedMultiplier);
            actor.ConfigureMovement(
                definition.MaxMoveSpeed,
                definition.MoveAcceleration,
                definition.MoveDeceleration,
                definition.SlowdownDistance,
                definition.StopDistance);
            actor.SetGrowthProgress(state.GrowthProgress, definition.MinVisualScale, definition.MaxVisualScale);
            _states.Add(state);
            _stateByAnimalId[state.AnimalId] = state;
            _stateByActor[actor] = state;
            return state;
        }

        private AnimalRuntimeState ResolveBestPreyTarget(AnimalRuntimeState hunterState)
        {
            AnimalDefinition hunterDefinition = hunterState.Definition;
            AnimalRuntimeState bestPrey = null;
            int bestDistance = int.MaxValue;

            for (int i = 0; i < _states.Count; i++)
            {
                AnimalRuntimeState preyState = _states[i];
                if (preyState == null
                    || preyState == hunterState
                    || preyState.Actor == null
                    || preyState.Definition == null
                    || !string.Equals(preyState.Definition.SpeciesId, hunterDefinition.PreySpeciesId, System.StringComparison.Ordinal))
                {
                    continue;
                }

                int distance = Mathf.Abs(preyState.CurrentCell.x - hunterState.CurrentCell.x)
                    + Mathf.Abs(preyState.CurrentCell.y - hunterState.CurrentCell.y);
                if (bestPrey != null && distance >= bestDistance)
                {
                    continue;
                }

                if (preyState.CurrentCell != hunterState.CurrentCell)
                {
                    bool hasPath = _navigationService.TryBuildPath(
                        hunterState.AnimalId,
                        hunterState.CurrentCell,
                        preyState.CurrentCell,
                        out PathResult path);
                    if (!hasPath || path.Edges == null || path.Edges.Count == 0)
                    {
                        continue;
                    }
                }

                bestPrey = preyState;
                bestDistance = distance;
            }

            return bestPrey;
        }

        private void RemoveAnimal(AnimalRuntimeState state)
        {
            if (state == null)
            {
                return;
            }

            _navigationService.ClearPath(state.AnimalId);
            _states.Remove(state);
            _stateByAnimalId.Remove(state.AnimalId);
            if (state.Actor != null)
            {
                _stateByActor.Remove(state.Actor);
                Object.Destroy(state.Actor.gameObject);
            }
        }

        private void QueueAnimalRemoval(AnimalRuntimeState state)
        {
            if (state == null || !_pendingRemovedAnimalIds.Add(state.AnimalId))
            {
                return;
            }

            _pendingRemovedStates.Add(state);
        }

        private void ProcessPendingAnimalRemovals()
        {
            if (_pendingRemovedStates.Count == 0)
            {
                return;
            }

            for (int i = 0; i < _pendingRemovedStates.Count; i++)
            {
                RemoveAnimal(_pendingRemovedStates[i]);
            }

            _pendingRemovedStates.Clear();
            _pendingRemovedAnimalIds.Clear();
        }

        private Vector2Int BuildDebugSpawnCell(Vector2Int anchorCell, int spawnIndex)
        {
            int offsetX = GetHorizontalOffset(spawnIndex);
            int x = Mathf.Clamp(anchorCell.x + offsetX, 0, _gridState.Width - 1);
            int y = Mathf.Clamp(anchorCell.y, 0, _gridState.Height - 1);
            return new Vector2Int(x, y);
        }

        private static int RollStartLoyalty(AnimalDefinition definition, System.Random random)
        {
            int minLoyalty = Mathf.Clamp(definition.MinStartLoyalty, 0, 100);
            int maxLoyalty = Mathf.Clamp(definition.MaxStartLoyalty, minLoyalty, 100);
            return random.Next(minLoyalty, maxLoyalty + 1);
        }

        private Vector3 BuildWorldPosition(Vector2Int cell)
        {
            Vector2 world = _gridCoordinateConverter.CellToWorldCenter(cell);
            return new Vector3(world.x, world.y, 0f);
        }

        private void ApplyActorMoveTarget(
            AnimalRuntimeState state,
            Vector2Int fromCell,
            Vector2Int toCell,
            MovementActionType actionType)
        {
            Vector3 finalWorldPosition = BuildWorldPosition(toCell);
            if (TryBuildWaypointWorldPosition(fromCell, toCell, actionType, out Vector3 waypointWorldPosition))
            {
                state.Actor.SetMoveTargetViaWaypoint(waypointWorldPosition, finalWorldPosition);
                return;
            }

            state.Actor.SetMoveTarget(finalWorldPosition);
        }

        private bool TryBuildWaypointWorldPosition(
            Vector2Int fromCell,
            Vector2Int toCell,
            MovementActionType actionType,
            out Vector3 waypointWorldPosition)
        {
            waypointWorldPosition = default;

            Vector2Int delta = toCell - fromCell;
            if (Mathf.Abs(delta.x) != 1 || Mathf.Abs(delta.y) != 1)
            {
                return false;
            }

            if (actionType == MovementActionType.JumpUp1)
            {
                waypointWorldPosition = BuildWorldPosition(new Vector2Int(fromCell.x, toCell.y));
                return true;
            }

            if (actionType == MovementActionType.Fall)
            {
                waypointWorldPosition = BuildWorldPosition(new Vector2Int(toCell.x, fromCell.y));
                return true;
            }

            return false;
        }

        private Dictionary<string, int> BuildPopulationBySpeciesIdBuffer()
        {
            PopulationBySpeciesIdBuffer.Clear();

            for (int i = 0; i < _states.Count; i++)
            {
                AnimalRuntimeState state = _states[i];
                if (state.Definition == null)
                {
                    continue;
                }

                string speciesId = state.Definition.SpeciesId;
                PopulationBySpeciesIdBuffer.TryGetValue(speciesId, out int currentPopulation);
                PopulationBySpeciesIdBuffer[speciesId] = currentPopulation + 1;
            }

            return PopulationBySpeciesIdBuffer;
        }

        private HashSet<Vector2Int> BuildOccupiedCellsBuffer()
        {
            OccupiedCellsBuffer.Clear();

            for (int i = 0; i < _states.Count; i++)
            {
                OccupiedCellsBuffer.Add(_states[i].CurrentCell);
            }

            return OccupiedCellsBuffer;
        }

        private System.Random GetSpeciesRandom(string speciesId, int fallbackSeed)
        {
            string resolvedSpeciesId = string.IsNullOrWhiteSpace(speciesId)
                ? "animal"
                : speciesId;

            if (_randomBySpeciesId.TryGetValue(resolvedSpeciesId, out System.Random random))
            {
                return random;
            }

            random = new System.Random(fallbackSeed == 0 ? resolvedSpeciesId.GetHashCode() : fallbackSeed);
            _randomBySpeciesId[resolvedSpeciesId] = random;
            return random;
        }

        private static bool TryBuildDiagnosticsSnapshot(AnimalRuntimeState state, out AnimalDiagnosticsSnapshot snapshot)
        {
            snapshot = default;
            if (state == null || state.Definition == null || state.Actor == null)
            {
                return false;
            }

            AnimalDefinition definition = state.Definition;
            bool isEggLayer = definition.LifecycleMode == AnimalLifecycleMode.EggLayer && definition.EggDefinition != null;
            bool usesGrowthBasedEggLaying = isEggLayer && definition.GrowthPerFeeding > 0f;
            bool canLayEgg = isEggLayer
                && (!usesGrowthBasedEggLaying || state.EggsLaidCount < MaxEggsPerGrowthBasedAnimal);
            bool tracksPreyEaten = !string.IsNullOrWhiteSpace(definition.PreySpeciesId) && definition.HuntHungerRelief > 0f;
            float eggLayRemainingGameHours = canLayEgg
                ? Mathf.Max(0f, definition.EggLayIntervalGameHours - state.EggLayElapsedGameHours)
                : 0f;
            if (usesGrowthBasedEggLaying)
            {
                eggLayRemainingGameHours = state.GrowthProgress >= definition.EggLayGrowthThreshold
                    ? Mathf.Max(0f, definition.EggLayIntervalGameHours - state.EggLayElapsedGameHours)
                    : 0f;
            }

            snapshot = new AnimalDiagnosticsSnapshot(
                state.AnimalId,
                definition.DisplayName,
                definition.SpeciesId,
                state.CurrentCell,
                state.Hunger,
                definition.MaxHunger,
                state.Actor.CurrentSpeed,
                definition.MaxMoveSpeed * definition.MovementSpeedMultiplier,
                state.Loyalty,
                definition.HasLoyaltyStat,
                canLayEgg,
                state.GrowthProgress,
                tracksPreyEaten,
                state.EatenPreyCount,
                usesGrowthBasedEggLaying,
                definition.EggLayGrowthThreshold,
                state.EggsLaidCount,
                usesGrowthBasedEggLaying ? MaxEggsPerGrowthBasedAnimal : 0,
                eggLayRemainingGameHours,
                definition.EggLayIntervalGameHours,
                definition.LifecycleMode);
            return true;
        }

        private void ApplyGrowthFromFeeding(AnimalRuntimeState state)
        {
            AnimalDefinition definition = state.Definition;
            if (definition == null || definition.GrowthPerFeeding <= 0f)
            {
                return;
            }

            // Feeding-based species grow only from successful hunts and stop at full maturity.
            state.GrowthProgress = Mathf.Clamp01(state.GrowthProgress + definition.GrowthPerFeeding);
            state.Actor.SetGrowthProgress(
                state.GrowthProgress,
                definition.MinVisualScale,
                definition.MaxVisualScale);

            // Growth-based species only unlock egg laying here; the actual spawn cadence stays in TryLayEgg.
        }

        /// <summary>
        /// Reuses the same top-row spread as the temporary character debug spawn.
        /// </summary>
        private static int GetHorizontalOffset(int spawnIndex)
        {
            if (spawnIndex == 0)
            {
                return 0;
            }

            int step = (spawnIndex + 1) / 2;
            bool toRight = (spawnIndex % 2) == 1;
            return toRight ? step : -step;
        }
    }
}