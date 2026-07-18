using _Project.Scripts.Data.Construction;
using UnityEngine;

namespace _Project.Scripts.Presentation.UI
{
    public sealed class ConstructionToolDefinitions : MonoBehaviour
    {
        [Header("Construction Defs")]
        [SerializeField] private BuildingDef _ladderBuildingDef;
        [SerializeField] private BuildingDef _bridgeBuildingDef;
        [SerializeField] private BuildingDef _storageBuildingDef;
        [SerializeField] private BuildingDef _solarPanelBuildingDef;
        [SerializeField] private BuildingDef _regolithProcessingUnitBuildingDef;
        [SerializeField] private BuildingDef _sleepModuleBuildingDef;
        [SerializeField] private BuildingDef _batteryBuildingDef;
        [SerializeField] private BuildingDef _dinnerBuildingDef;
        [SerializeField] private BuildingDef _oxygenStorageBuildingDef;
        [SerializeField] private BuildingDef _oxigenProcessingUnitBuildingDef;
        [SerializeField] private BuildingDef _waterReclamationBuildingDef;
        [SerializeField] private BuildingDef _waterProcessingUnitBuildingDef;

        public BuildingDef LadderBuildingDef => _ladderBuildingDef;
        public BuildingDef BridgeBuildingDef => _bridgeBuildingDef;
        public BuildingDef StorageBuildingDef => _storageBuildingDef;
        public BuildingDef SolarPanelBuildingDef => _solarPanelBuildingDef;
        public BuildingDef RegolithProcessingUnitBuildingDef => _regolithProcessingUnitBuildingDef;
        public BuildingDef SleepModuleBuildingDef => _sleepModuleBuildingDef;
        public BuildingDef BatteryBuildingDef => _batteryBuildingDef;
        public BuildingDef DinnerBuildingDef => _dinnerBuildingDef;
        public BuildingDef OxygenStorageBuildingDef => _oxygenStorageBuildingDef;
        public BuildingDef OxigenProcessingUnitBuildingDef => _oxigenProcessingUnitBuildingDef;
        public BuildingDef WaterReclamationBuildingDef => _waterReclamationBuildingDef;
        public BuildingDef WaterProcessingUnitBuildingDef => _waterProcessingUnitBuildingDef;
    }
}
