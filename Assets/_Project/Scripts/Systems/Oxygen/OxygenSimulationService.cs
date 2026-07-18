using System.Collections.Generic;
using _Project.Scripts.Data.Construction;
using _Project.Scripts.Data.Grid;
using _Project.Scripts.Data.Oxygen;
using _Project.Scripts.Systems.Resources;
using _Project.Scripts.Systems.Water;
using UnityEngine;

namespace _Project.Scripts.Systems.Oxygen
{
    /// <summary>
    /// Calculates oxygen production/distribution/consumption over already built oxygen networks.
    /// </summary>
    public sealed class OxygenSimulationService
    {
        private const string RegolithResourceId = "Rogalite";

        private readonly GridState _gridState;
        private readonly ResourceInventoryService _resourceInventoryService;
        private readonly WaterSimulationService _waterSimulationService;

        private readonly Dictionary<Vector2Int, BuildingRuntimeEntity> _buildingsByAnchor = new Dictionary<Vector2Int, BuildingRuntimeEntity>();
        private readonly Dictionary<Vector2Int, BuildingOxygenRuntimeState> _statesByAnchor = new Dictionary<Vector2Int, BuildingOxygenRuntimeState>();
        private readonly HashSet<int> _networksWithLiveSupply = new HashSet<int>();

        public OxygenNetworkSnapshot LastSnapshot { get; private set; }

        public OxygenSimulationService(
            GridState gridState,
            ResourceInventoryService resourceInventoryService,
            WaterSimulationService waterSimulationService)
        {
            _gridState = gridState;
            _resourceInventoryService = resourceInventoryService;
            _waterSimulationService = waterSimulationService;
            LastSnapshot = new OxygenNetworkSnapshot(new Dictionary<Vector2Int, BuildingOxygenRuntimeState>(), 0f, 0f);
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

                var state = new BuildingOxygenRuntimeState
                {
                    Role = entity.BuildingDef.OxygenRole,
                    OxygenNetworkId = 0,
                    IsProducerEnabled = entity.IsOxygenProducerEnabled,
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
            _networksWithLiveSupply.Clear();

            var buildingsByNetworkId = new Dictionary<int, List<BuildingRuntimeEntity>>();
            foreach (KeyValuePair<Vector2Int, BuildingRuntimeEntity> pair in _buildingsByAnchor)
            {
                BuildingRuntimeEntity entity = pair.Value;
                if (entity == null || entity.BuildingDef == null || !entity.BuildingDef.UsesOxygenNetwork)
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
            foreach (BuildingOxygenRuntimeState state in _statesByAnchor.Values)
            {
                totalConsumedLiters += Mathf.Max(0f, state.LastConsumedLiters);
            }

            LastSnapshot = new OxygenNetworkSnapshot(
                new Dictionary<Vector2Int, BuildingOxygenRuntimeState>(_statesByAnchor),
                totalProducedLiters,
                totalConsumedLiters);
        }

        /// <summary>
        /// Tries to consume oxygen from a consumer local tank.
        /// </summary>
        public bool TryConsumeOxygen(Vector2Int consumerAnchor, float liters, out OxygenConsumeResult result)
        {
            result = new OxygenConsumeResult
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

            if (entity.BuildingDef.OxygenRole != OxygenRole.Consumer)
            {
                result.Reason = "Building is not an oxygen consumer.";
                return false;
            }

            if (!_statesByAnchor.TryGetValue(consumerAnchor, out BuildingOxygenRuntimeState state))
            {
                result.Reason = "Oxygen state not initialized.";
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

        public BuildingOxygenRuntimeState GetBuildingState(Vector2Int anchorCell)
        {
            return _statesByAnchor.TryGetValue(anchorCell, out BuildingOxygenRuntimeState state)
                ? state
                : default;
        }

        public bool SetProducerEnabled(Vector2Int anchorCell, bool isEnabled)
        {
            if (!_buildingsByAnchor.TryGetValue(anchorCell, out BuildingRuntimeEntity entity) || entity?.BuildingDef == null)
            {
                return false;
            }

            entity.SetOxygenProducerEnabled(isEnabled);
            if (_statesByAnchor.TryGetValue(anchorCell, out BuildingOxygenRuntimeState state))
            {
                state.IsProducerEnabled = entity.IsOxygenProducerEnabled;
                _statesByAnchor[anchorCell] = state;
            }

            return true;
        }

        public bool ToggleProducer(Vector2Int anchorCell, out bool isEnabled)
        {
            isEnabled = false;
            if (!_buildingsByAnchor.TryGetValue(anchorCell, out BuildingRuntimeEntity entity) || entity?.BuildingDef == null)
            {
                return false;
            }

            isEnabled = entity.ToggleOxygenProducer();
            if (_statesByAnchor.TryGetValue(anchorCell, out BuildingOxygenRuntimeState state))
            {
                state.IsProducerEnabled = isEnabled;
                _statesByAnchor[anchorCell] = state;
            }

            return true;
        }

        /// <summary>
        /// Returns true when a consumer has network access to oxygen supply.
        /// </summary>
        public bool HasSupplyForConsumer(Vector2Int consumerAnchor)
        {
            if (!_statesByAnchor.TryGetValue(consumerAnchor, out BuildingOxygenRuntimeState state))
            {
                return false;
            }

            if (state.Role != OxygenRole.Consumer)
            {
                return true;
            }

            if (state.TankCurrentLiters > 0.001f)
            {
                return true;
            }

            int networkId = state.OxygenNetworkId;
            if (networkId <= 0)
            {
                return false;
            }

            if (_networksWithLiveSupply.Contains(networkId))
            {
                return true;
            }

            return AnyStorageCanDischarge(networkId);
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
                if (!_statesByAnchor.TryGetValue(entity.AnchorCell, out BuildingOxygenRuntimeState state))
                {
                    continue;
                }

                state.Role = def.OxygenRole;
                state.OxygenNetworkId = networkId;
                state.IsProducerEnabled = entity.IsOxygenProducerEnabled;
                state.TankCapacityLiters = ResolveTankCapacity(def);
                state.LastProducedLiters = 0f;
                state.LastReceivedLiters = 0f;
                state.LastRequestedLiters = 0f;
                state.LastConsumedLiters = 0f;
                _statesByAnchor[entity.AnchorCell] = state;

                if (def.OxygenRole == OxygenRole.Consumer)
                {
                    consumers.Add(entity);
                    continue;
                }

                if (def.OxygenRole == OxygenRole.Storage)
                {
                    storages.Add(entity);
                    continue;
                }

                if (def.OxygenRole != OxygenRole.Producer)
                {
                    continue;
                }

                if (!entity.IsOperational || !entity.IsOxygenProducerEnabled)
                {
                    continue;
                }

                float produced = ProduceOxygen(entity, tickHours);
                if (produced <= 0f)
                {
                    continue;
                }

                producedLiters += produced;
                networkAvailableLiters += produced;
                _networksWithLiveSupply.Add(networkId);

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
                if (discharged > 0f)
                {
                    _networksWithLiveSupply.Add(networkId);
                }
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
                if (!_statesByAnchor.TryGetValue(consumer.AnchorCell, out BuildingOxygenRuntimeState state))
                {
                    continue;
                }

                float tankCapacity = Mathf.Max(0f, state.TankCapacityLiters);
                float current = Mathf.Clamp(state.TankCurrentLiters, 0f, tankCapacity);
                float rawDeficit = Mathf.Max(0f, tankCapacity - current);
                float fillRateLimit = Mathf.Max(0f, consumer.BuildingDef.OxygenConsumerFillRateLitersPerHour) * tickHours;
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
                if (!_statesByAnchor.TryGetValue(pair.Key, out BuildingOxygenRuntimeState state))
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
                if (!_statesByAnchor.TryGetValue(storage.AnchorCell, out BuildingOxygenRuntimeState state))
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

                float fillLimit = Mathf.Max(0f, storage.BuildingDef.OxygenStorageFillRateLitersPerHour) * tickHours;
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
                if (!_statesByAnchor.TryGetValue(storage.AnchorCell, out BuildingOxygenRuntimeState state))
                {
                    continue;
                }

                float current = Mathf.Max(0f, state.TankCurrentLiters);
                if (current <= 0f)
                {
                    continue;
                }

                float dischargeLimit = Mathf.Max(0f, storage.BuildingDef.OxygenStorageDischargeRateLitersPerHour) * tickHours;
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

        private float ProduceOxygen(BuildingRuntimeEntity entity, float tickHours)
        {
            BuildingDef def = entity.BuildingDef;
            float productionCapByTime = Mathf.Max(0f, def.OxygenProductionLitersPerHour) * tickHours;
            if (productionCapByTime <= 0f)
            {
                return 0f;
            }

            float litersPerCycle = Mathf.Max(0f, def.OxygenProductionLitersPerCycle);
            int regolithPerCycle = Mathf.Max(0, def.OxygenProductionRegolithPerCycle);
            if (litersPerCycle <= 0f || regolithPerCycle <= 0)
            {
                return 0f;
            }

            int inventoryAmount = _resourceInventoryService != null
                ? _resourceInventoryService.GetAmount(RegolithResourceId)
                : 0;
            int maxCyclesByInventory = inventoryAmount / regolithPerCycle;
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
            if (cyclesToRun <= 0)
            {
                return 0f;
            }

            float waterPerHour = Mathf.Max(0f, def.OxygenWaterConsumptionLitersPerHour);
            float requestedWaterLiters = waterPerHour * tickHours;
            if (requestedWaterLiters > 0f)
            {
                if (_waterSimulationService == null)
                {
                    return 0f;
                }

                bool hasWater = _waterSimulationService.TryConsumeFromNetwork(entity.AnchorCell, requestedWaterLiters, out float grantedWater);
                if (!hasWater || grantedWater + 0.0001f < requestedWaterLiters)
                {
                    return 0f;
                }
            }

            int regolithToSpend = cyclesToRun * regolithPerCycle;
            bool removed = _resourceInventoryService != null
                && _resourceInventoryService.TryRemove(RegolithResourceId, regolithToSpend);
            if (!removed)
            {
                return 0f;
            }

            float produced = cyclesToRun * litersPerCycle;
            return Mathf.Min(productionCapByTime, produced);
        }

        private bool AnyStorageCanDischarge(int networkId)
        {
            foreach (KeyValuePair<Vector2Int, BuildingOxygenRuntimeState> pair in _statesByAnchor)
            {
                BuildingOxygenRuntimeState state = pair.Value;
                if (state.OxygenNetworkId != networkId || state.Role != OxygenRole.Storage)
                {
                    continue;
                }

                if (!_buildingsByAnchor.TryGetValue(pair.Key, out BuildingRuntimeEntity entity) || entity?.BuildingDef == null)
                {
                    continue;
                }

                float current = Mathf.Max(0f, state.TankCurrentLiters);
                float dischargeRate = Mathf.Max(0f, entity.BuildingDef.OxygenStorageDischargeRateLitersPerHour);
                if (current > 0.001f && dischargeRate > 0f)
                {
                    return true;
                }
            }

            return false;
        }

        private int ResolveBuildingNetworkId(BuildingRuntimeEntity entity)
        {
            Vector2Int portCell = entity.AnchorCell + entity.BuildingDef.OxygenPortOffset;
            if (!_gridState.IsInside(portCell.x, portCell.y))
            {
                return 0;
            }

            Cell cell = _gridState.GetCell(portCell.x, portCell.y);
            return cell.OxygenNetworkId;
        }

        private static float ResolveTankCapacity(BuildingDef def)
        {
            if (def == null)
            {
                return 0f;
            }

            return def.OxygenRole switch
            {
                OxygenRole.Consumer => Mathf.Max(0f, def.OxygenConsumerCapacityLiters),
                OxygenRole.Storage => Mathf.Max(0f, def.OxygenStorageCapacityLiters),
                _ => 0f
            };
        }
    }
}
