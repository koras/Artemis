using System;
using _Project.Scripts.Data.Construction;
using _Project.Scripts.Input;
using UnityEngine.UIElements;

namespace _Project.Scripts.Presentation.UI
{
    public sealed class ConstructionToolPanelController
    {
        private readonly BuildingDef _ladderBuildingDef;
        private readonly BuildingDef _bridgeBuildingDef;
        private readonly BuildingDef _storageBuildingDef;
        private readonly BuildingDef _solarPanelBuildingDef;
        private readonly BuildingDef _regolithProcessingUnitBuildingDef;
        private readonly BuildingDef _sleepModuleBuildingDef;
        private readonly BuildingDef _batteryBuildingDef;
        private readonly BuildingDef _dinnerBuildingDef;
        private readonly BuildingDef _oxygenStorageBuildingDef;
        private readonly BuildingDef _oxigenProcessingUnitBuildingDef;
        private readonly BuildingDef _waterReclamationBuildingDef;
        private readonly BuildingDef _waterProcessingUnitBuildingDef;

        private BuildingDef _activeBuildingDef;
        private Button _shovelButton, _buildLadderButton, _buildBridgeButton, _buildStorageButton, _buildSolarPanelButton, _buildRegolithProcessingUnitButton,
            _buildSleepModuleButton, _buildBatteryButton, _buildDinnerButton, _buildOxygenStorageButton, _buildOxigenProcessingUnitButton, _buildWaterReclamationButton,
            _buildWaterProcessingUnitButton,
            _buildCableButton, _cancelCablePlanButton, _exitCablePlanButton, _buildWaterButton, _cancelWaterPlanButton, _exitWaterPlanButton,
            _buildOxygenButton, _cancelOxygenPlanButton, _exitOxygenPlanButton, _buildLifeModuleButton, _cancelLifeModulePlanButton, _cancelButton, _destructionButton;

        public event Action<ToolMode, BuildingDef> ToolSelectionChanged;

        public ConstructionToolPanelController(BuildingDef ladderBuildingDef, BuildingDef bridgeBuildingDef, BuildingDef storageBuildingDef, BuildingDef solarPanelBuildingDef,
            BuildingDef regolithProcessingUnitBuildingDef, BuildingDef sleepModuleBuildingDef, BuildingDef batteryBuildingDef,
            BuildingDef dinnerBuildingDef, BuildingDef oxygenStorageBuildingDef, BuildingDef oxigenProcessingUnitBuildingDef,
            BuildingDef waterReclamationBuildingDef, BuildingDef waterProcessingUnitBuildingDef, bool enableLogs)
        {
            _ladderBuildingDef = ladderBuildingDef;
            _bridgeBuildingDef = bridgeBuildingDef;
            _storageBuildingDef = storageBuildingDef;
            _solarPanelBuildingDef = solarPanelBuildingDef;
            _regolithProcessingUnitBuildingDef = regolithProcessingUnitBuildingDef;
            _sleepModuleBuildingDef = sleepModuleBuildingDef;
            _batteryBuildingDef = batteryBuildingDef;
            _dinnerBuildingDef = dinnerBuildingDef;
            _oxygenStorageBuildingDef = oxygenStorageBuildingDef;
            _oxigenProcessingUnitBuildingDef = oxigenProcessingUnitBuildingDef;
            _waterReclamationBuildingDef = waterReclamationBuildingDef;
            _waterProcessingUnitBuildingDef = waterProcessingUnitBuildingDef;
        }

        public void Bind(Button destructionButton, Button shovelButton, Button buildLadderButton, Button buildBridgeButton, Button buildStorageButton, Button buildSolarPanelButton,
            Button buildRegolithProcessingUnitButton, Button buildSleepModuleButton, Button buildBatteryButton, Button buildDinnerButton,
            Button buildOxygenStorageButton, Button buildOxigenProcessingUnitButton, Button buildWaterReclamationButton, Button buildWaterProcessingUnitButton,
            Button buildCableButton, Button cancelCablePlanButton,
            Button exitCablePlanButton, Button buildWaterButton, Button cancelWaterPlanButton, Button exitWaterPlanButton,
            Button buildOxygenButton, Button cancelOxygenPlanButton, Button exitOxygenPlanButton, Button buildLifeModuleButton, Button cancelLifeModulePlanButton, Button cancelButton)
        {
            _destructionButton = destructionButton; _shovelButton = shovelButton; _buildLadderButton = buildLadderButton; _buildBridgeButton = buildBridgeButton; _buildStorageButton = buildStorageButton;
            _buildSolarPanelButton = buildSolarPanelButton; _buildRegolithProcessingUnitButton = buildRegolithProcessingUnitButton; _buildSleepModuleButton = buildSleepModuleButton;
            _buildBatteryButton = buildBatteryButton; _buildDinnerButton = buildDinnerButton; _buildOxygenStorageButton = buildOxygenStorageButton;
            _buildOxigenProcessingUnitButton = buildOxigenProcessingUnitButton; _buildWaterReclamationButton = buildWaterReclamationButton; _buildWaterProcessingUnitButton = buildWaterProcessingUnitButton;
            _buildCableButton = buildCableButton; _cancelCablePlanButton = cancelCablePlanButton;
            _exitCablePlanButton = exitCablePlanButton; _buildWaterButton = buildWaterButton; _cancelWaterPlanButton = cancelWaterPlanButton;
            _exitWaterPlanButton = exitWaterPlanButton; _buildOxygenButton = buildOxygenButton; _cancelOxygenPlanButton = cancelOxygenPlanButton;
            _exitOxygenPlanButton = exitOxygenPlanButton; _buildLifeModuleButton = buildLifeModuleButton; _cancelLifeModulePlanButton = cancelLifeModulePlanButton; _cancelButton = cancelButton;

            if (_destructionButton != null) _destructionButton.clicked += OnDestructionClicked;
            if (_buildLadderButton != null) _buildLadderButton.clicked += OnBuildLadderClicked;
            if (_buildBridgeButton != null) _buildBridgeButton.clicked += OnBuildBridgeClicked;
            if (_buildStorageButton != null) _buildStorageButton.clicked += OnBuildStorageClicked;
            if (_buildSolarPanelButton != null) _buildSolarPanelButton.clicked += OnBuildSolarPanelClicked;
            if (_buildRegolithProcessingUnitButton != null) _buildRegolithProcessingUnitButton.clicked += OnBuildRegolithProcessingUnitClicked;
            if (_buildSleepModuleButton != null) _buildSleepModuleButton.clicked += OnBuildSleepModuleClicked;
            if (_buildBatteryButton != null) _buildBatteryButton.clicked += OnBuildBatteryClicked;
            if (_buildDinnerButton != null) _buildDinnerButton.clicked += OnBuildDinnerClicked;
            if (_buildOxygenStorageButton != null) _buildOxygenStorageButton.clicked += OnBuildOxygenStorageClicked;
            if (_buildOxigenProcessingUnitButton != null) _buildOxigenProcessingUnitButton.clicked += OnBuildOxigenProcessingUnitClicked;
            if (_buildWaterReclamationButton != null) _buildWaterReclamationButton.clicked += OnBuildWaterReclamationClicked;
            if (_buildWaterProcessingUnitButton != null) _buildWaterProcessingUnitButton.clicked += OnBuildWaterProcessingUnitClicked;
            if (_buildCableButton != null) _buildCableButton.clicked += OnBuildCableClicked;
            if (_cancelCablePlanButton != null) _cancelCablePlanButton.clicked += OnCancelCablePlanClicked;
            if (_exitCablePlanButton != null) _exitCablePlanButton.clicked += OnExitCablePlanClicked;
            if (_buildWaterButton != null) _buildWaterButton.clicked += OnBuildWaterClicked;
            if (_cancelWaterPlanButton != null) _cancelWaterPlanButton.clicked += OnCancelWaterPlanClicked;
            if (_exitWaterPlanButton != null) _exitWaterPlanButton.clicked += OnExitWaterPlanClicked;
            if (_buildOxygenButton != null) _buildOxygenButton.clicked += OnBuildOxygenClicked;
            if (_cancelOxygenPlanButton != null) _cancelOxygenPlanButton.clicked += OnCancelOxygenPlanClicked;
            if (_exitOxygenPlanButton != null) _exitOxygenPlanButton.clicked += OnExitOxygenPlanClicked;
            if (_buildLifeModuleButton != null) _buildLifeModuleButton.clicked += OnBuildLifeModuleClicked;
            if (_cancelLifeModulePlanButton != null) _cancelLifeModulePlanButton.clicked += OnCancelLifeModulePlanClicked;
            if (_shovelButton != null) _shovelButton.clicked += OnShovelClicked;
            if (_cancelButton != null) _cancelButton.clicked += OnCancelClicked;
        }

        public void Unbind() { }
        public BuildingDef GetActiveBuildingDef() => _activeBuildingDef;
        public void ClearActiveBuildingDef() => _activeBuildingDef = null;

        private void OnShovelClicked() { _activeBuildingDef = null; ToolSelectionChanged?.Invoke(ToolMode.Shovel, null); }
        private void OnCancelClicked() { _activeBuildingDef = null; ToolSelectionChanged?.Invoke(ToolMode.ShovelCancel, null); }
        private void OnDestructionClicked() { _activeBuildingDef = null; ToolSelectionChanged?.Invoke(ToolMode.DestroyObject, null); }
        private void OnBuildLadderClicked() { _activeBuildingDef = _ladderBuildingDef; ToolSelectionChanged?.Invoke(ToolMode.BuildLadder, _activeBuildingDef); }
        private void OnBuildBridgeClicked() { if (_bridgeBuildingDef == null) return; _activeBuildingDef = _bridgeBuildingDef; ToolSelectionChanged?.Invoke(ToolMode.BuildBridge, _activeBuildingDef); }
        private void OnBuildStorageClicked() { if (_storageBuildingDef == null) return; _activeBuildingDef = _storageBuildingDef; ToolSelectionChanged?.Invoke(ToolMode.BuildStorage, _activeBuildingDef); }
        private void OnBuildSolarPanelClicked() { if (_solarPanelBuildingDef == null) return; _activeBuildingDef = _solarPanelBuildingDef; ToolSelectionChanged?.Invoke(ToolMode.BuildSolarPanel, _activeBuildingDef); }
        private void OnBuildRegolithProcessingUnitClicked() { if (_regolithProcessingUnitBuildingDef == null) return; _activeBuildingDef = _regolithProcessingUnitBuildingDef; ToolSelectionChanged?.Invoke(ToolMode.BuildRegolithProcessingUnit, _activeBuildingDef); }
        private void OnBuildSleepModuleClicked() { if (_sleepModuleBuildingDef == null) return; _activeBuildingDef = _sleepModuleBuildingDef; ToolSelectionChanged?.Invoke(ToolMode.BuildSleepModule, _activeBuildingDef); }
        private void OnBuildBatteryClicked() { if (_batteryBuildingDef == null) return; _activeBuildingDef = _batteryBuildingDef; ToolSelectionChanged?.Invoke(ToolMode.BuildBattery, _activeBuildingDef); }
        private void OnBuildDinnerClicked() { if (_dinnerBuildingDef == null) return; _activeBuildingDef = _dinnerBuildingDef; ToolSelectionChanged?.Invoke(ToolMode.BuildDinner, _activeBuildingDef); }
        private void OnBuildOxygenStorageClicked() { if (_oxygenStorageBuildingDef == null) return; _activeBuildingDef = _oxygenStorageBuildingDef; ToolSelectionChanged?.Invoke(ToolMode.BuildOxygenStorage, _activeBuildingDef); }
        // Отдельный режим для Oxigen Processing Unit, чтобы объект выбирался как обычная постройка через UI.
        private void OnBuildOxigenProcessingUnitClicked() { if (_oxigenProcessingUnitBuildingDef == null) return; _activeBuildingDef = _oxigenProcessingUnitBuildingDef; ToolSelectionChanged?.Invoke(ToolMode.BuildOxigenProcessingUnit, _activeBuildingDef); }
        private void OnBuildWaterReclamationClicked() { if (_waterReclamationBuildingDef == null) return; _activeBuildingDef = _waterReclamationBuildingDef; ToolSelectionChanged?.Invoke(ToolMode.BuildWaterReclamation, _activeBuildingDef); }
        // Отдельный режим для WaterProcessingUnit: обычная постановка build-задач через общий pipeline.
        private void OnBuildWaterProcessingUnitClicked() { if (_waterProcessingUnitBuildingDef == null) return; _activeBuildingDef = _waterProcessingUnitBuildingDef; ToolSelectionChanged?.Invoke(ToolMode.BuildWaterProcessingUnit, _activeBuildingDef); }
        private void OnBuildCableClicked() { _activeBuildingDef = null; ToolSelectionChanged?.Invoke(ToolMode.BuildCable, null); }
        private void OnCancelCablePlanClicked() { _activeBuildingDef = null; ToolSelectionChanged?.Invoke(ToolMode.CancelCablePlan, null); }
        private void OnExitCablePlanClicked() { _activeBuildingDef = null; ToolSelectionChanged?.Invoke(ToolMode.ExitCablePlan, null); }
        private void OnBuildWaterClicked() { _activeBuildingDef = null; ToolSelectionChanged?.Invoke(ToolMode.BuildWater, null); }
        private void OnCancelWaterPlanClicked() { _activeBuildingDef = null; ToolSelectionChanged?.Invoke(ToolMode.CancelWaterPlan, null); }
        private void OnExitWaterPlanClicked() { _activeBuildingDef = null; ToolSelectionChanged?.Invoke(ToolMode.ExitWaterPlan, null); }
        private void OnBuildOxygenClicked() { _activeBuildingDef = null; ToolSelectionChanged?.Invoke(ToolMode.BuildOxygen, null); }
        private void OnCancelOxygenPlanClicked() { _activeBuildingDef = null; ToolSelectionChanged?.Invoke(ToolMode.CancelOxygenPlan, null); }
        private void OnExitOxygenPlanClicked() { _activeBuildingDef = null; ToolSelectionChanged?.Invoke(ToolMode.ExitOxygenPlan, null); }
        private void OnBuildLifeModuleClicked() { _activeBuildingDef = null; ToolSelectionChanged?.Invoke(ToolMode.BuildLifeModule, null); }
        private void OnCancelLifeModulePlanClicked() { _activeBuildingDef = null; ToolSelectionChanged?.Invoke(ToolMode.CancelLifeModulePlan, null); }
    }
}
