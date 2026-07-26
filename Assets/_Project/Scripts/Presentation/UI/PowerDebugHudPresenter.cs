using _Project.Scripts.Data.Construction;
using _Project.Scripts.Data.Grid;
using _Project.Scripts.Data.Power;
using _Project.Scripts.Data.Water;
using _Project.Scripts.Data.Oxygen;
using _Project.Scripts.Systems.Construction;
using _Project.Scripts.Systems.Oxygen;
using _Project.Scripts.Systems.Power;
using _Project.Scripts.Systems.Water;
using UnityEngine;
using UnityEngine.UIElements;

namespace _Project.Scripts.Presentation.UI
{
    /// <summary>
    /// Отладочная HUD-панель энергосети: показывает состояние здания под курсором и его сети.
    /// </summary>
    public sealed class PowerDebugHudPresenter
    {
        private readonly VisualElement _panel;
        private readonly Label _hoverCellLabel;
        private readonly Label _networkLabel;
        private readonly Label _buildingLabel;
        private readonly Label _powerLabel;
        private readonly Label _batteryLabel;

        public PowerDebugHudPresenter(VisualElement root)
        {
            _panel = root?.Q<VisualElement>("power-debug-panel");
            _hoverCellLabel = root?.Q<Label>("power-debug-hover-cell");
            _networkLabel = root?.Q<Label>("power-debug-network");
            _buildingLabel = root?.Q<Label>("power-debug-building");
            _powerLabel = root?.Q<Label>("power-debug-power");
            _batteryLabel = root?.Q<Label>("power-debug-battery");
        }

        public bool IsAvailable => _panel != null;

        public void SetVisible(bool isVisible)
        {
            if (_panel == null) return;
            _panel.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void Render(
            bool hasHoveredCell,
            Vector2Int hoveredCell,
            GridState gridState,
            BuildingManager buildingManager,
            PowerNetworkService powerNetworkService,
            WaterSimulationService waterSimulationService,
            OxygenSimulationService oxygenSimulationService)
        {
            if (_panel == null) return;

            if (!hasHoveredCell || gridState == null || !gridState.IsInside(hoveredCell.x, hoveredCell.y))
            {
                _hoverCellLabel.text = "Hover: -";
                _networkLabel.text = "Network: -";
                _buildingLabel.text = "Building: -";
                _powerLabel.text = "Power: -";
                _batteryLabel.text = "Battery: -";
                return;
            }

            ref readonly Cell hoveredCellData = ref gridState.GetCell(hoveredCell.x, hoveredCell.y);
            _hoverCellLabel.text = $"Hover: ({hoveredCell.x},{hoveredCell.y})";
            _networkLabel.text = hoveredCellData.HasCable
                ? $"Network: cable id={hoveredCellData.CableNetworkId}"
                : "Network: no cable on hovered cell";

            if (buildingManager == null || !buildingManager.TryGetActiveBuildingByCell(hoveredCell, out BuildingRuntimeEntity building))
            {
                _buildingLabel.text = "Building: none";
                _powerLabel.text = "Power: -";
                _batteryLabel.text = "Battery/Water: -";
                return;
            }

            Vector2Int powerPortCell = building.AnchorCell + building.BuildingDef.PowerInputOffset;
            _buildingLabel.text =
                $"Building: {building.BuildingDef.ObjectType} anchor=({building.AnchorCell.x},{building.AnchorCell.y}) port=({powerPortCell.x},{powerPortCell.y}) op={building.IsOperational}";

            if (powerNetworkService == null)
            {
                _powerLabel.text = "Power: service is null";
                _batteryLabel.text = "Battery/Water: -";
                return;
            }

            BuildingPowerRuntimeState powerState = powerNetworkService.GetBuildingState(building.AnchorCell);
            _powerLabel.text =
                $"Power: req={powerState.RequestedPowerKw:0.##}kW sup={powerState.SuppliedPowerKw:0.##}kW powered={powerState.IsPowered}";

            if (building.BuildingDef.ObjectType == BuildObjectType.ElectricBattery)
            {
                float chargeKwh = powerNetworkService.GetBatteryChargeKwh(building.AnchorCell);
                float soc01 = powerNetworkService.GetBatteryStateOfCharge01(building.AnchorCell);
                float capacityKwh = Mathf.Max(0f, building.BuildingDef.BatteryCapacityKwh);
                _batteryLabel.text =
                    $"Battery: charge={chargeKwh:0.##}/{capacityKwh:0.##} kWh ({(soc01 * 100f):0.##}%)";
            }
            else
            {
                _batteryLabel.text = "Battery: not a battery";
            }

            BuildingWaterRuntimeState waterState = waterSimulationService != null
                ? waterSimulationService.GetBuildingState(building.AnchorCell)
                : default;
            int waterNetworkId = waterState.WaterNetworkId;
            _buildingLabel.text +=
                $"\nWater: role={building.BuildingDef.WaterRole} networkId={waterNetworkId} producerEnabled={building.IsWaterProducerEnabled}";
            _powerLabel.text +=
                $"\nTank: {waterState.TankCurrentLiters:0.##}/{waterState.TankCapacityLiters:0.##} L";
            _batteryLabel.text +=
                $"\nWater flow: produced={waterState.LastProducedLiters:0.##}L consumed={waterState.LastConsumedLiters:0.##}L req={waterState.LastRequestedLiters:0.##}L";

            BuildingOxygenRuntimeState oxygenState = oxygenSimulationService != null
                ? oxygenSimulationService.GetBuildingState(building.AnchorCell)
                : default;
            int oxygenNetworkId = oxygenState.OxygenNetworkId;
            _buildingLabel.text +=
                $"\nOxygen: role={building.BuildingDef.OxygenRole} networkId={oxygenNetworkId} producerEnabled={building.IsOxygenProducerEnabled}";
            _powerLabel.text +=
                $"\nO2 Tank: {oxygenState.TankCurrentLiters:0.##}/{oxygenState.TankCapacityLiters:0.##} L";
            _batteryLabel.text +=
                $"\nO2 flow: produced={oxygenState.LastProducedLiters:0.##}L consumed={oxygenState.LastConsumedLiters:0.##}L req={oxygenState.LastRequestedLiters:0.##}L";
        }
    }
}