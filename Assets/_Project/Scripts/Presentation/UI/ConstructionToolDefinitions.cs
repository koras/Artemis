using _Project.Scripts.Data.Construction;
using UnityEngine;

namespace _Project.Scripts.Presentation.UI
{
    public sealed class ConstructionToolDefinitions : MonoBehaviour
    {
        [Header("Construction Defs")]
        [SerializeField] private LadderBuildingDef _ladderBuildingDef;
        [SerializeField] private BuildingDef _bridgeBuildingDef;
        [SerializeField] private BuildingDef _storageBuildingDef;
        [SerializeField] private BuildingDef _solarPanelBuildingDef;
        [SerializeField] private BuildingDef _regolithProcessingUnitBuildingDef;
        [SerializeField] private BuildingDef _sleepModuleBuildingDef;
        [SerializeField] private BuildingDef _batteryBuildingDef;
        [SerializeField] private BuildingDef _dinnerBuildingDef;
        [SerializeField] private BuildingDef _showerBuildingDef;
        [SerializeField] private BuildingDef _oxygenStorageBuildingDef;
        [SerializeField] private BuildingDef _oxigenProcessingUnitBuildingDef;
        [SerializeField] private BuildingDef _waterReclamationBuildingDef;
        [SerializeField] private BuildingDef _waterProcessingUnitBuildingDef;

        // Декоративные объекты доступны из отдельного меню «Декор».
        [Header("Decoration Defs")]
        [SerializeField] private BuildingDef _flowerSmall1BuildingDef;
        [SerializeField] private BuildingDef _flowerSmallBuildingDef;
        [SerializeField] private BuildingDef _flowerBig2BuildingDef;
        [SerializeField] private BuildingDef _flowerBig1BuildingDef;
        [SerializeField] private BuildingDef _paintAstroBuildingDef;
        [SerializeField] private BuildingDef _paintPlanetBuildingDef;
        [SerializeField] private BuildingDef _paintRockerBuildingDef;

        public LadderBuildingDef LadderBuildingDef => _ladderBuildingDef;
        public BuildingDef BridgeBuildingDef => _bridgeBuildingDef;
        public BuildingDef StorageBuildingDef => _storageBuildingDef;
        public BuildingDef SolarPanelBuildingDef => _solarPanelBuildingDef;
        public BuildingDef RegolithProcessingUnitBuildingDef => _regolithProcessingUnitBuildingDef;
        public BuildingDef SleepModuleBuildingDef => _sleepModuleBuildingDef;
        public BuildingDef BatteryBuildingDef => _batteryBuildingDef;
        public BuildingDef DinnerBuildingDef => _dinnerBuildingDef;
        public BuildingDef ShowerBuildingDef => _showerBuildingDef;
        public BuildingDef OxygenStorageBuildingDef => _oxygenStorageBuildingDef;
        public BuildingDef OxigenProcessingUnitBuildingDef => _oxigenProcessingUnitBuildingDef;
        public BuildingDef WaterReclamationBuildingDef => _waterReclamationBuildingDef;
        public BuildingDef WaterProcessingUnitBuildingDef => _waterProcessingUnitBuildingDef;
        public BuildingDef FlowerSmall1BuildingDef => _flowerSmall1BuildingDef;
        public BuildingDef FlowerSmallBuildingDef => _flowerSmallBuildingDef;
        public BuildingDef FlowerBig2BuildingDef => _flowerBig2BuildingDef;
        public BuildingDef FlowerBig1BuildingDef => _flowerBig1BuildingDef;
        public BuildingDef PaintAstroBuildingDef => _paintAstroBuildingDef;
        public BuildingDef PaintPlanetBuildingDef => _paintPlanetBuildingDef;
        public BuildingDef PaintRockerBuildingDef => _paintRockerBuildingDef;
    }
}
