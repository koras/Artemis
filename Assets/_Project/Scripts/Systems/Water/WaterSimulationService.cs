using System.Collections.Generic;
using _Project.Scripts.Data.Construction;
using _Project.Scripts.Data.Grid;
using _Project.Scripts.Data.Water;
using _Project.Scripts.Systems.Resources;
using UnityEngine;

namespace _Project.Scripts.Systems.Water
{
    /// <summary>
    /// Calculates water production/distribution/consumption over already built water networks.
    /// </summary>
    public sealed class WaterSimulationService
    {
        private const string RogaliteResourceId = "Rogalite";

        private readonly GridState _gridState;
        private readonly ResourceInventoryService _resourceInventoryService;

        private readonly Dictionary<Vector2Int, BuildingRuntimeEntity> _buildingsByAnchor = new Dictionary<Vector2Int, BuildingRuntimeEntity>();
        private readonly Dictionary<Vector2Int, BuildingWaterRuntimeState> _statesByAnchor = new Dictionary<Vector2Int, BuildingWaterRuntimeState>();

        public WaterNetworkSnapshot LastSnapshot { get; private set; }

        public WaterSimulationService(GridState gridState, ResourceInventoryService resourceInventoryService)
        {
            _gridState = gridState;
            _resourceInventoryService = resourceInventoryService;
            LastSnapshot = new WaterNetworkSnapshot(new Dictionary<Vector2Int, BuildingWaterRuntimeState>(), 0f, 0f);
        }

        /// <summary>
        /// Synchronizes active building list and initializes missing runtime states.
        /// </summary>
        public void SyncActiveBuildings(List<BuildingRuntimeEntity> activeBuildings)
        {
            var aliveAnchors = new HashSet<Vector2Int>();
            for (int i = 0; i < activeBuildings.Count; i++)
            {
                BuildingRuntimeEntity entity = activeBuildings[i];
                if (entity == null || !entity.IsActive || entity.BuildingDef == null)
                {
                    continue;
                }

                aliveAnchors.Add(entity.AnchorCell);
                _buildingsByAnchor[entity.AnchorCell] = entity;

                if (_statesByAnchor.ContainsKey(entity.AnchorCell))
                {
                    continue;
                }

                var state = new BuildingWaterRuntimeState
                {
                    Role = entity.BuildingDef.WaterRole,
                    WaterNetworkId = 0,
                    IsProducerEnabled = entity.IsWaterProducerEnabled,
                    TankCapacityLiters = ResolveTankCapacity(entity.BuildingDef),
                    TankCurrentLiters = 0f,
                    LastProducedLiters = 0f,
                    LastReceivedLiters = 0f,
                    LastRequestedLiters = 0f,
                    LastConsumedLiters = 0f
                };
                _statesByAnchor[entity.AnchorCell] = state;
            }

            List<Vector2Int> toRemove = null;
            foreach (Vector2Int anchor in _buildingsByAnchor.Keys)
            {
                if (aliveAnchors.Contains(anchor))
                {
                    continue;
                }

                if (toRemove == null)
                {
                    toRemove = new List<Vector2Int>();
                }

                toRemove.Add(anchor);
            }

            if (toRemove == null)
            {
                return;
            }

            for (int i = 0; i < toRemove.Count; i++)
            {
                Vector2Int anchor = toRemove[i];
                _buildingsByAnchor.Remove(anchor);
                _statesByAnchor.Remove(anchor);
            }
        }

        /// <summary>
        /// Recalculates producer output and consumer tank filling for one simulation tick.
        /// </summary>
        public void Recalculate(float tickDurationSeconds)
        {
            float tickHours = Mathf.Max(0.0001f, tickDurationSeconds / 3600f);
            float totalProducedLiters = 0f;

            var buildingsByNetworkId = new Dictionary<int, List<BuildingRuntimeEntity>>();
            foreach (KeyValuePair<Vector2Int, BuildingRuntimeEntity> pair in _buildingsByAnchor)
            {
                BuildingRuntimeEntity entity = pair.Value;
                if (entity == null || entity.BuildingDef == null || !entity.BuildingDef.UsesWaterNetwork)
                {
                    continue;
                }

                int networkId = ResolveBuildingNetworkId(entity);
                if (!buildingsByNetworkId.TryGetValue(networkId, out List<BuildingRuntimeEntity> list))
                {
                    list = new List<BuildingRuntimeEntity>();
                    buildingsByNetworkId[networkId] = list;
                }

                list.Add(entity);
            }

            foreach (KeyValuePair<int, List<BuildingRuntimeEntity>> group in buildingsByNetworkId)
            {
                float producedLitersInNetwork = ProcessNetwork(group.Key, group.Value, tickHours);
                totalProducedLiters += producedLitersInNetwork;
            }

            float totalConsumedLiters = 0f;
            foreach (BuildingWaterRuntimeState state in _statesByAnchor.Values)
            {
                totalConsumedLiters += Mathf.Max(0f, state.LastConsumedLiters);
            }

            LastSnapshot = new WaterNetworkSnapshot(
                new Dictionary<Vector2Int, BuildingWaterRuntimeState>(_statesByAnchor),
                totalProducedLiters,
                totalConsumedLiters);
        }

        /// <summary>
        /// Tries to consume water from a consumer local tank.
        /// </summary>
        public bool TryConsumeWater(Vector2Int consumerAnchor, float liters, out WaterConsumeResult result)
        {
            result = new WaterConsumeResult
            {
                RequestedLiters = Mathf.Max(0f, liters),
                GrantedLiters = 0f,
                Success = false,
                Reason = "Unknown"
            };

            if (liters <= 0f)
            {
                result.Success = true;
                result.Reason = "Zero request.";
                return true;
            }

            if (!_buildingsByAnchor.TryGetValue(consumerAnchor, out BuildingRuntimeEntity entity) || entity?.BuildingDef == null)
            {
                result.Reason = "Building not found.";
                return false;
            }

            if (entity.BuildingDef.WaterRole != WaterRole.Consumer)
            {
                result.Reason = "Building is not a water consumer.";
                return false;
            }

            if (!_statesByAnchor.TryGetValue(consumerAnchor, out BuildingWaterRuntimeState state))
            {
                result.Reason = "Water state not initialized.";
                return false;
            }

            float request = Mathf.Max(0f, liters);
            float granted = Mathf.Min(request, Mathf.Max(0f, state.TankCurrentLiters));
            state.TankCurrentLiters -= granted;
            state.LastRequestedLiters = request;
            state.LastConsumedLiters = granted;
            _statesByAnchor[consumerAnchor] = state;

            result.GrantedLiters = granted;
            result.Success = granted + 0.0001f >= request;
            result.Reason = granted > 0f
                ? (result.Success ? "Consumed from local tank." : "Partially consumed from local tank.")
                : "Consumer tank is empty.";
            return granted > 0f;
        }

        /// <summary>
        /// Returns last known runtime state for a building.
        /// </summary>
        public BuildingWaterRuntimeState GetBuildingState(Vector2Int anchorCell)
        {
            return _statesByAnchor.TryGetValue(anchorCell, out BuildingWaterRuntimeState state)
                ? state
                : default;
        }

        /// <summary>
        /// Tries to consume water from storages in requester's water network.
        /// </summary>
        public bool TryConsumeFromNetwork(Vector2Int requesterAnchor, float liters, out float grantedLiters)
        {
            grantedLiters = 0f;
            float request = Mathf.Max(0f, liters);
            if (request <= 0f)
            {
                return true;
            }

            if (!_buildingsByAnchor.TryGetValue(requesterAnchor, out BuildingRuntimeEntity entity) || entity?.BuildingDef == null)
            {
                return false;
            }

            int networkId = ResolveBuildingNetworkId(entity);
            if (networkId <= 0)
            {
                return false;
            }

            foreach (KeyValuePair<Vector2Int, BuildingRuntimeEntity> pair in _buildingsByAnchor)
            {
                BuildingRuntimeEntity candidate = pair.Value;
                if (candidate?.BuildingDef == null || candidate.BuildingDef.WaterRole != WaterRole.Storage)
                {
                    continue;
                }

                if (ResolveBuildingNetworkId(candidate) != networkId)
                {
                    continue;
                }

                if (!_statesByAnchor.TryGetValue(candidate.AnchorCell, out BuildingWaterRuntimeState storageState))
                {
                    continue;
                }

                float available = Mathf.Max(0f, storageState.TankCurrentLiters);
                if (available <= 0f)
                {
                    continue;
                }

                float take = Mathf.Min(available, request - grantedLiters);
                storageState.TankCurrentLiters = available - take;
                _statesByAnchor[candidate.AnchorCell] = storageState;
                grantedLiters += take;

                if (grantedLiters + 0.0001f >= request)
                {
                    break;
                }
            }

            return grantedLiters + 0.0001f >= request;
        }

        /// <summary>
        /// Sets producer enabled flag for an active building.
        /// </summary>
        public bool SetProducerEnabled(Vector2Int anchorCell, bool isEnabled)
        {
            if (!_buildingsByAnchor.TryGetValue(anchorCell, out BuildingRuntimeEntity entity) || entity?.BuildingDef == null)
            {
                return false;
            }

            entity.SetWaterProducerEnabled(isEnabled);
            if (_statesByAnchor.TryGetValue(anchorCell, out BuildingWaterRuntimeState state))
            {
                state.IsProducerEnabled = entity.IsWaterProducerEnabled;
                _statesByAnchor[anchorCell] = state;
            }

            return true;
        }

        /// <summary>
        /// Toggles producer enabled flag and returns resulting state.
        /// </summary>
        public bool ToggleProducer(Vector2Int anchorCell, out bool isEnabled)
        {
            isEnabled = false;
            if (!_buildingsByAnchor.TryGetValue(anchorCell, out BuildingRuntimeEntity entity) || entity?.BuildingDef == null)
            {
                return false;
            }

            isEnabled = entity.ToggleWaterProducer();
            if (_statesByAnchor.TryGetValue(anchorCell, out BuildingWaterRuntimeState state))
            {
                state.IsProducerEnabled = isEnabled;
                _statesByAnchor[anchorCell] = state;
            }

            return true;
        }

        private float ProcessNetwork(int networkId, List<BuildingRuntimeEntity> buildings, float tickHours)
        {
            float producedLiters = 0f;
            float networkAvailableLiters = 0f;
            var consumers = new List<BuildingRuntimeEntity>();
            var storages = new List<BuildingRuntimeEntity>();

            for (int i = 0; i < buildings.Count; i++)
            {
                BuildingRuntimeEntity entity = buildings[i];
                if (entity?.BuildingDef == null)
                {
                    continue;
                }

                BuildingDef def = entity.BuildingDef;
                if (!_statesByAnchor.TryGetValue(entity.AnchorCell, out BuildingWaterRuntimeState state))
                {
                    continue;
                }

                state.Role = def.WaterRole;
                state.WaterNetworkId = networkId;
                state.IsProducerEnabled = entity.IsWaterProducerEnabled;
                state.TankCapacityLiters = ResolveTankCapacity(def);
                state.LastProducedLiters = 0f;
                state.LastReceivedLiters = 0f;
                state.LastRequestedLiters = 0f;
                state.LastConsumedLiters = 0f;
                _statesByAnchor[entity.AnchorCell] = state;

                if (def.WaterRole == WaterRole.Consumer)
                {
                    consumers.Add(entity);
                    continue;
                }

                if (def.WaterRole == WaterRole.Storage)
                {
                    storages.Add(entity);
                    continue;
                }

                if (def.WaterRole != WaterRole.Producer)
                {
                    continue;
                }

                if (!entity.IsOperational || !entity.IsWaterProducerEnabled)
                {
                    continue;
                }

                float produced = ProduceWater(def, tickHours);
                if (produced <= 0f)
                {
                    continue;
                }

                producedLiters += produced;
                networkAvailableLiters += produced;

                state.LastProducedLiters = produced;
                _statesByAnchor[entity.AnchorCell] = state;
            }

            if (networkAvailableLiters > 0f && storages.Count > 0)
            {
                float acceptedByStorage = FillStorages(storages, networkAvailableLiters, tickHours);
                networkAvailableLiters = Mathf.Max(0f, networkAvailableLiters - acceptedByStorage);
            }

            if (networkAvailableLiters <= 0f && storages.Count > 0)
            {
                float discharged = DischargeStorages(storages, tickHours);
                networkAvailableLiters += discharged;
            }

            if (networkAvailableLiters <= 0f || consumers.Count == 0)
            {
                return producedLiters;
            }

            var deficits = new Dictionary<Vector2Int, float>(consumers.Count);
            float totalDeficit = 0f;
            for (int i = 0; i < consumers.Count; i++)
            {
                BuildingRuntimeEntity consumer = consumers[i];
                if (!_statesByAnchor.TryGetValue(consumer.AnchorCell, out BuildingWaterRuntimeState state))
                {
                    continue;
                }

                float tankCapacity = Mathf.Max(0f, state.TankCapacityLiters);
                float current = Mathf.Clamp(state.TankCurrentLiters, 0f, tankCapacity);
                float rawDeficit = Mathf.Max(0f, tankCapacity - current);
                float fillRateLimit = Mathf.Max(0f, consumer.BuildingDef.WaterConsumerFillRateLitersPerHour) * tickHours;
                float effectiveDeficit = Mathf.Min(rawDeficit, fillRateLimit);

                if (effectiveDeficit <= 0f)
                {
                    continue;
                }

                deficits[consumer.AnchorCell] = effectiveDeficit;
                totalDeficit += effectiveDeficit;
            }

            if (totalDeficit <= 0f)
            {
                return producedLiters;
            }

            foreach (KeyValuePair<Vector2Int, float> pair in deficits)
            {
                float share = networkAvailableLiters * (pair.Value / totalDeficit);
                float granted = Mathf.Min(pair.Value, share);
                if (!_statesByAnchor.TryGetValue(pair.Key, out BuildingWaterRuntimeState state))
                {
                    continue;
                }

                state.TankCurrentLiters = Mathf.Min(state.TankCapacityLiters, state.TankCurrentLiters + granted);
                state.LastReceivedLiters = granted;
                _statesByAnchor[pair.Key] = state;
            }

            return producedLiters;
        }

        private float FillStorages(List<BuildingRuntimeEntity> storages, float availableLiters, float tickHours)
        {
            if (availableLiters <= 0f)
            {
                return 0f;
            }

            float consumedByStorage = 0f;
            for (int i = 0; i < storages.Count; i++)
            {
                BuildingRuntimeEntity storage = storages[i];
                if (!_statesByAnchor.TryGetValue(storage.AnchorCell, out BuildingWaterRuntimeState state))
                {
                    continue;
                }

                float capacity = Mathf.Max(0f, state.TankCapacityLiters);
                float current = Mathf.Clamp(state.TankCurrentLiters, 0f, capacity);
                float free = Mathf.Max(0f, capacity - current);
                if (free <= 0f)
                {
                    continue;
                }

                float fillLimit = Mathf.Max(0f, storage.BuildingDef.WaterStorageFillRateLitersPerHour) * tickHours;
                if (fillLimit <= 0f)
                {
                    continue;
                }

                float accepted = Mathf.Min(free, fillLimit, availableLiters - consumedByStorage);
                if (accepted <= 0f)
                {
                    continue;
                }

                state.TankCurrentLiters = current + accepted;
                _statesByAnchor[storage.AnchorCell] = state;
                consumedByStorage += accepted;

                if (consumedByStorage + 0.0001f >= availableLiters)
                {
                    break;
                }
            }

            return consumedByStorage;
        }

        private float DischargeStorages(List<BuildingRuntimeEntity> storages, float tickHours)
        {
            float dischargedLiters = 0f;
            for (int i = 0; i < storages.Count; i++)
            {
                BuildingRuntimeEntity storage = storages[i];
                if (!_statesByAnchor.TryGetValue(storage.AnchorCell, out BuildingWaterRuntimeState state))
                {
                    continue;
                }

                float current = Mathf.Max(0f, state.TankCurrentLiters);
                if (current <= 0f)
                {
                    continue;
                }

                float dischargeLimit = Mathf.Max(0f, storage.BuildingDef.WaterStorageDischargeRateLitersPerHour) * tickHours;
                if (dischargeLimit <= 0f)
                {
                    continue;
                }

                float released = Mathf.Min(current, dischargeLimit);
                if (released <= 0f)
                {
                    continue;
                }

                state.TankCurrentLiters = current - released;
                _statesByAnchor[storage.AnchorCell] = state;
                dischargedLiters += released;
            }

            return dischargedLiters;
        }

        private float ProduceWater(BuildingDef def, float tickHours)
        {
            float productionCapByTime = Mathf.Max(0f, def.WaterProductionLitersPerHour) * tickHours;
            if (productionCapByTime <= 0f)
            {
                return 0f;
            }

            float litersPerCycle = Mathf.Max(0f, def.WaterProductionLitersPerCycle);
            int rogalitePerCycle = Mathf.Max(0, def.WaterProductionRogalitePerCycle);
            if (litersPerCycle <= 0f || rogalitePerCycle <= 0)
            {
                return 0f;
            }

            int inventoryAmount = _resourceInventoryService != null
                ? _resourceInventoryService.GetAmount(RogaliteResourceId)
                : 0;
            int maxCyclesByInventory = inventoryAmount / rogalitePerCycle;
            if (maxCyclesByInventory <= 0)
            {
                return 0f;
            }

            int maxCyclesByTime = Mathf.FloorToInt(productionCapByTime / litersPerCycle);
            if (maxCyclesByTime <= 0)
            {
                maxCyclesByTime = 1;
            }

            int cyclesToRun = Mathf.Min(maxCyclesByInventory, maxCyclesByTime);
            int rogaliteToSpend = cyclesToRun * rogalitePerCycle;
            bool removed = _resourceInventoryService != null
                && _resourceInventoryService.TryRemove(RogaliteResourceId, rogaliteToSpend);
            if (!removed)
            {
                return 0f;
            }

            float produced = cyclesToRun * litersPerCycle;
            return Mathf.Min(productionCapByTime, produced);
        }

        private int ResolveBuildingNetworkId(BuildingRuntimeEntity entity)
        {
            Vector2Int portCell = entity.AnchorCell + entity.BuildingDef.WaterPortOffset;
            if (!_gridState.IsInside(portCell.x, portCell.y))
            {
                return 0;
            }

            Cell cell = _gridState.GetCell(portCell.x, portCell.y);
            return cell.WaterNetworkId;
        }

        private static float ResolveTankCapacity(BuildingDef def)
        {
            if (def == null)
            {
                return 0f;
            }

            return def.WaterRole switch
            {
                WaterRole.Consumer => Mathf.Max(0f, def.WaterConsumerCapacityLiters),
                WaterRole.Storage => Mathf.Max(0f, def.WaterStorageCapacityLiters),
                _ => 0f
            };
        }
    }
}
