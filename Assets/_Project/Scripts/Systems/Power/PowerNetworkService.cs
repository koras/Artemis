using System.Collections.Generic;
using _Project.Scripts.Data.Construction;
using _Project.Scripts.Data.Power;
using _Project.Scripts.Data.Grid;
using _Project.Scripts.Systems.Simulation;
using UnityEngine;

namespace _Project.Scripts.Systems.Power
{
    /// <summary>
    /// Единый сервис расчёта энергосети на графе кабелей и портов построек.
    /// </summary>
    public sealed class PowerNetworkService
    {
        private readonly GridState _gridState;
        private readonly SolarPowerProductionService _solarService;
        private readonly BatteryStorageService _batteryStorageService;

        private readonly Dictionary<Vector2Int, BuildingRuntimeEntity> _buildingsByAnchor = new Dictionary<Vector2Int, BuildingRuntimeEntity>();
        private readonly Dictionary<Vector2Int, BuildingPowerRuntimeState> _buildingStates = new Dictionary<Vector2Int, BuildingPowerRuntimeState>();

        private int _nodeCounter;
        private int _lastComponentCount;

        public PowerNetworkSnapshot LastSnapshot { get; private set; }

        public PowerNetworkService(
            GridState gridState,
            SolarPowerProductionService solarService,
            BatteryStorageService batteryStorageService)
        {
            _gridState = gridState;
            _solarService = solarService;
            _batteryStorageService = batteryStorageService;
            LastSnapshot = new PowerNetworkSnapshot(new Dictionary<Vector2Int, BuildingPowerRuntimeState>(), 0f, 0f);
        }

        /// <summary>
        /// Полностью синхронизирует список активных построек с сетью.
        /// </summary>
        public void SyncActiveBuildings(List<BuildingRuntimeEntity> activeBuildings)
        {
            var aliveAnchors = new HashSet<Vector2Int>();
            for (int i = 0; i < activeBuildings.Count; i++)
            {
                BuildingRuntimeEntity entity = activeBuildings[i];
                if (entity == null || !entity.IsActive || entity.BuildingDef == null) continue;

                aliveAnchors.Add(entity.AnchorCell);
                _buildingsByAnchor[entity.AnchorCell] = entity;
                if (!_buildingStates.ContainsKey(entity.AnchorCell))
                {
                    _buildingStates[entity.AnchorCell] = new BuildingPowerRuntimeState();
                }

                if (entity.BuildingDef.ObjectType == BuildObjectType.ElectricBattery)
                {
                    _batteryStorageService.EnsureBattery(entity.AnchorCell, entity.BuildingDef.BatteryCapacityKwh);
                }
            }

            List<Vector2Int> toRemove = null;
            foreach (Vector2Int anchor in _buildingsByAnchor.Keys)
            {
                if (aliveAnchors.Contains(anchor)) continue;
                if (toRemove == null) toRemove = new List<Vector2Int>();
                toRemove.Add(anchor);
            }

            if (toRemove == null) return;
            for (int i = 0; i < toRemove.Count; i++)
            {
                Vector2Int anchor = toRemove[i];
                _buildingsByAnchor.Remove(anchor);
                _buildingStates.Remove(anchor);
                _batteryStorageService.RemoveBattery(anchor);
            }
        }

        /// <summary>
        /// Пересчитывает компоненты сети и питание построек на текущем тике.
        /// </summary>
        public void Recalculate(GameTimeService gameTimeService, float tickDurationSeconds)
        {
            float tickHours = Mathf.Max(0.0001f, tickDurationSeconds / 3600f);
            Dictionary<Vector2Int, int> componentByCell = BuildCableComponents();

            var buildingByComponent = new Dictionary<int, List<BuildingRuntimeEntity>>();
            var nextStates = new Dictionary<Vector2Int, BuildingPowerRuntimeState>(_buildingStates.Count);
            foreach (KeyValuePair<Vector2Int, BuildingRuntimeEntity> pair in _buildingsByAnchor)
            {
                BuildingRuntimeEntity entity = pair.Value;
                if (entity == null || entity.BuildingDef == null)
                {
                    continue;
                }

                if (!entity.BuildingDef.UsesPowerNetwork)
                {
                    // Объекты вне электросети не участвуют в графе кабелей и всегда считаются энерго-нейтральными.
                    nextStates[pair.Key] = new BuildingPowerRuntimeState
                    {
                        IsPowered = true,
                        RequestedPowerKw = 0f,
                        SuppliedPowerKw = 0f
                    };
                    continue;
                }

                Vector2Int portCell = GetInputPortCell(entity);
                if (!componentByCell.TryGetValue(portCell, out int componentId))
                {
                    componentId = -pair.Key.GetHashCode();
                }

                if (!buildingByComponent.TryGetValue(componentId, out List<BuildingRuntimeEntity> list))
                {
                    list = new List<BuildingRuntimeEntity>();
                    buildingByComponent[componentId] = list;
                }
                list.Add(entity);
            }

            float totalGeneration = 0f;
            float totalDemand = 0f;

            foreach (KeyValuePair<int, List<BuildingRuntimeEntity>> group in buildingByComponent)
            {
                ProcessComponent(group.Value, gameTimeService, tickHours, nextStates, ref totalGeneration, ref totalDemand);
            }

            _buildingStates.Clear();
            foreach (KeyValuePair<Vector2Int, BuildingPowerRuntimeState> pair in nextStates)
            {
                _buildingStates[pair.Key] = pair.Value;
            }

            LastSnapshot = new PowerNetworkSnapshot(new Dictionary<Vector2Int, BuildingPowerRuntimeState>(_buildingStates), totalGeneration, totalDemand);
            BuildGraphSnapshot(componentByCell);
        }

        /// <summary>
        /// Возвращает текущее состояние питания постройки по её якорю.
        /// </summary>
        public BuildingPowerRuntimeState GetBuildingState(Vector2Int anchorCell)
        {
            return _buildingStates.TryGetValue(anchorCell, out BuildingPowerRuntimeState state)
                ? state
                : default;
        }

        /// <summary>
        /// Возвращает текущий заряд батареи по якорю в кВт*ч.
        /// </summary>
        public float GetBatteryChargeKwh(Vector2Int anchorCell)
        {
            return _batteryStorageService.GetChargeKwh(anchorCell);
        }

        /// <summary>
        /// Возвращает текущий SoC батареи по якорю в диапазоне [0..1].
        /// </summary>
        public float GetBatteryStateOfCharge01(Vector2Int anchorCell)
        {
            if (!_buildingsByAnchor.TryGetValue(anchorCell, out BuildingRuntimeEntity entity)
                || entity == null
                || entity.BuildingDef == null)
            {
                return 0f;
            }

            return _batteryStorageService.GetStateOfCharge01(anchorCell, entity.BuildingDef.BatteryCapacityKwh);
        }

        private Dictionary<Vector2Int, int> BuildCableComponents()
        {
            var componentByCell = new Dictionary<Vector2Int, int>();
            int componentId = 1;

            for (int y = 0; y < _gridState.Height; y++)
            {
                for (int x = 0; x < _gridState.Width; x++)
                {
                    var cellPos = new Vector2Int(x, y);
                    Cell cell = _gridState.GetCell(x, y);
                    if (!cell.HasCable || componentByCell.ContainsKey(cellPos)) continue;

                    var queue = new Queue<Vector2Int>();
                    queue.Enqueue(cellPos);
                    componentByCell[cellPos] = componentId;

                    while (queue.Count > 0)
                    {
                        Vector2Int current = queue.Dequeue();
                        foreach (Vector2Int neighbor in EnumerateNeighbors4(current))
                        {
                            if (!_gridState.IsInside(neighbor.x, neighbor.y)) continue;
                            if (componentByCell.ContainsKey(neighbor)) continue;

                            Cell neighborCell = _gridState.GetCell(neighbor.x, neighbor.y);
                            if (!neighborCell.HasCable) continue;

                            componentByCell[neighbor] = componentId;
                            queue.Enqueue(neighbor);
                        }
                    }

                    componentId++;
                }
            }

            foreach (KeyValuePair<Vector2Int, int> pair in componentByCell)
            {
                Cell cell = _gridState.GetCell(pair.Key.x, pair.Key.y);
                cell.CableNetworkId = pair.Value;
                _gridState.SetCell(pair.Key.x, pair.Key.y, cell);
            }

            int componentCount = componentId - 1;
            // Логируем только реальное изменение числа компонент: удобно проверять split/merge сети в рантайме.
            if (componentCount != _lastComponentCount)
            {
                Debug.Log($"[PowerNetwork] Cable components changed: {_lastComponentCount} -> {componentCount}.");
                _lastComponentCount = componentCount;
            }

            return componentByCell;
        }

        private void ProcessComponent(
            List<BuildingRuntimeEntity> buildings,
            GameTimeService gameTimeService,
            float tickHours,
            Dictionary<Vector2Int, BuildingPowerRuntimeState> stateOutput,
            ref float totalGenerationKw,
            ref float totalDemandKw)
        {
            float availableKw = 0f;
            var consumers = new List<BuildingRuntimeEntity>();
            var batteries = new List<BuildingRuntimeEntity>();

            for (int i = 0; i < buildings.Count; i++)
            {
                BuildingDef def = buildings[i].BuildingDef;
                if (def == null) continue;

                float generation = _solarService.GetCurrentGenerationKw(def, gameTimeService);
                availableKw += generation;
                totalGenerationKw += generation;

                if (def.ObjectType == BuildObjectType.ElectricBattery)
                {
                    batteries.Add(buildings[i]);
                }

                if (def.RequiresPower && def.PowerConsumptionKw > 0f)
                {
                    consumers.Add(buildings[i]);
                    totalDemandKw += def.PowerConsumptionKw;
                }
            }

            consumers.Sort((a, b) => b.BuildingDef.PowerPriority.CompareTo(a.BuildingDef.PowerPriority));

            for (int i = 0; i < consumers.Count; i++)
            {
                BuildingRuntimeEntity consumer = consumers[i];
                float demandKw = Mathf.Max(0f, consumer.BuildingDef.PowerConsumptionKw);

                if (availableKw < demandKw)
                {
                    float missingKw = demandKw - availableKw;
                    // Для разряда сначала берем батареи с более высоким SoC, чтобы избегать постоянной нагрузки на одну и ту же.
                    batteries.Sort((a, b) =>
                    {
                        float socA = _batteryStorageService.GetStateOfCharge01(a.AnchorCell, a.BuildingDef.BatteryCapacityKwh);
                        float socB = _batteryStorageService.GetStateOfCharge01(b.AnchorCell, b.BuildingDef.BatteryCapacityKwh);
                        return socB.CompareTo(socA);
                    });

                    for (int b = 0; b < batteries.Count && missingKw > 0f; b++)
                    {
                        BuildingRuntimeEntity battery = batteries[b];
                        float batteryOut = _batteryStorageService.Discharge(
                            battery.AnchorCell,
                            missingKw,
                            battery.BuildingDef.BatteryMaxDischargeKw,
                            tickHours);
                        missingKw -= batteryOut;
                        availableKw += batteryOut;
                    }
                }

                float suppliedKw = Mathf.Min(demandKw, availableKw);
                availableKw -= suppliedKw;

                stateOutput[consumer.AnchorCell] = new BuildingPowerRuntimeState
                {
                    IsPowered = suppliedKw + 0.0001f >= demandKw,
                    RequestedPowerKw = demandKw,
                    SuppliedPowerKw = suppliedKw
                };
            }

            if (availableKw > 0f)
            {
                // Для заряда сначала пополняем батареи с меньшим SoC для выравнивания уровня сети.
                batteries.Sort((a, b) =>
                {
                    float socA = _batteryStorageService.GetStateOfCharge01(a.AnchorCell, a.BuildingDef.BatteryCapacityKwh);
                    float socB = _batteryStorageService.GetStateOfCharge01(b.AnchorCell, b.BuildingDef.BatteryCapacityKwh);
                    return socA.CompareTo(socB);
                });

                for (int i = 0; i < batteries.Count && availableKw > 0f; i++)
                {
                    BuildingRuntimeEntity battery = batteries[i];
                    float acceptedKw = _batteryStorageService.Charge(
                        battery.AnchorCell,
                        availableKw,
                        battery.BuildingDef.BatteryMaxChargeKw,
                        battery.BuildingDef.BatteryCapacityKwh,
                        tickHours);
                    availableKw -= acceptedKw;
                }
            }

            for (int i = 0; i < buildings.Count; i++)
            {
                Vector2Int anchor = buildings[i].AnchorCell;
                if (stateOutput.ContainsKey(anchor)) continue;

                stateOutput[anchor] = new BuildingPowerRuntimeState
                {
                    IsPowered = true,
                    RequestedPowerKw = 0f,
                    SuppliedPowerKw = 0f
                };
            }
        }

        private Vector2Int GetInputPortCell(BuildingRuntimeEntity entity)
        {
            Vector2Int offset = entity.BuildingDef.PowerInputOffset;
            return entity.AnchorCell + offset;
        }

        private IEnumerable<Vector2Int> EnumerateNeighbors4(Vector2Int cell)
        {
            yield return cell + Vector2Int.up;
            yield return cell + Vector2Int.right;
            yield return cell + Vector2Int.down;
            yield return cell + Vector2Int.left;
        }

        private void BuildGraphSnapshot(Dictionary<Vector2Int, int> componentByCell)
        {
            // Создаём узлы/рёбра для отладочного снапшота топологии сети.
            _nodeCounter = 0;
            var nodesByCell = new Dictionary<Vector2Int, PowerNode>();
            var edges = new List<PowerEdge>();

            foreach (Vector2Int cell in componentByCell.Keys)
            {
                var node = new PowerNode(++_nodeCounter, cell, cell, false);
                nodesByCell[cell] = node;
            }

            foreach (KeyValuePair<Vector2Int, PowerNode> pair in nodesByCell)
            {
                Vector2Int cell = pair.Key;
                int fromId = pair.Value.Id;
                foreach (Vector2Int neighbor in EnumerateNeighbors4(cell))
                {
                    if (!nodesByCell.TryGetValue(neighbor, out PowerNode neighborNode)) continue;
                    if (neighborNode.Id <= fromId) continue;
                    edges.Add(new PowerEdge(fromId, neighborNode.Id));
                }
            }
        }
    }
}