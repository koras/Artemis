using System;
using System.Collections.Generic;
using _Project.Scripts.Data.Animals;
using _Project.Scripts.Data.Character;
using _Project.Scripts.Data.ColonyEvents;
using _Project.Scripts.Data.Construction;
using _Project.Scripts.Data.Grid;
using _Project.Scripts.Data.Offers;
using _Project.Scripts.Data.Shop;
using _Project.Scripts.Presentation.Buildings;
using _Project.Scripts.Presentation.Character;
using _Project.Scripts.Presentation.Grid;
using _Project.Scripts.Presentation.UI;
using _Project.Scripts.Systems.Animals;
using _Project.Scripts.Systems.Character;
using _Project.Scripts.Systems.ColonyEvents;
using _Project.Scripts.Systems.Construction;
using _Project.Scripts.Systems.External;
using _Project.Scripts.Systems.Navigation;
using _Project.Scripts.Systems.Offers;
using _Project.Scripts.Systems.Oxygen;
using _Project.Scripts.Systems.Power;
using _Project.Scripts.Systems.Resources;
using _Project.Scripts.Systems.Shop;
using _Project.Scripts.Systems.Tasks;
using _Project.Scripts.Systems.Units;
using _Project.Scripts.Systems.Water;
using UnityEngine;

namespace _Project.Scripts.Bootstrap.Runtime
{
    /// <summary>
    /// Composes gameplay/runtime systems that depend on world context.
    /// </summary>
    internal static class GameplayRuntimeComposer
    {
        public static GameplayRuntimeContext Compose(
            WorldRuntimeContext world,
            AnimalDebugSpawnConfig[] animalDebugSpawnConfigs,
            CharacterSpawnConfig characterSpawnConfig,
            Transform characterSpawnRoot,
            int characterSpawnSeed,
            Vector2 gridOrigin,
            BuildingViewRegistry buildingViewRegistry,
            Transform buildingsRoot,
            List<ColonyEventDefinition> colonyEventCatalog,
            List<OfferDefinition> offerCatalog,
            List<ShopOfferDefinition> shopCatalog,
            GameObject rocketIronDataPrefab,
            Transform externalObjectsRoot,
            Vector2Int rocketSpawnCell,
            Vector2Int rocketLandingCell,
            bool rocketShouldLand,
            IronRocketArrivalService.ArrivalOutcomeMode rocketArrivalOutcomeMode,
            float rocketDescendDurationSeconds,
            float rocketAscendDurationSeconds,
            int rocketStayDurationGameHours,
            IronRocketArrivalService.ArrivalCadenceMode rocketCadenceMode,
            int rocketCadenceValue,
            bool enableAiLogs,
            Sprite powerNoSourceIcon,
            int cellSize,
            Transform resourceObjectsRoot,
            GameObject ironResourcePrefab,
            GameObject titanResourcePrefab,
            GameObject rogaliteResourcePrefab,
            GameObject aluminiumResourcePrefab,
            List<CellType> resourceFallSupportCellTypes,
            List<BuildObjectType> resourceFallSupportBuildObjectTypes,
            float resourceFallStepDurationSeconds,
            LifeModuleConstructionConfig lifeModuleConstructionConfig,
            Action<Vector2Int> onUnitCellChanged,
            Action<IronRocketArrivalService.RocketMissionResult> onRocketMissionResolved)
        {
            var context = new GameplayRuntimeContext();
            List<string> foodResourceIds = BuildFoodResourceIds(shopCatalog);

            context.ResourceInventoryService = new ResourceInventoryService();
            context.TaskScoringService = new TaskScoringService();
            context.GlobalTaskBoardService = new GlobalTaskBoardService(
                world.GridState,
                context.TaskScoringService,
                context.ResourceInventoryService,
                enableAiLogs);
            var sceneResourcePrefabByType = new Dictionary<SceneResourceType, GameObject>
            {
                { SceneResourceType.Iron, ironResourcePrefab },
                { SceneResourceType.Titan, titanResourcePrefab },
                { SceneResourceType.Rogalite, rogaliteResourcePrefab },
                { SceneResourceType.Aluminium, aluminiumResourcePrefab }
            };
            context.SceneResourceObjectService = new SceneResourceObjectService(
                world.GridState,
                context.GlobalTaskBoardService,
                world.GridCoordinateConverter,
                resourceObjectsRoot,
                sceneResourcePrefabByType,
                resourceFallSupportCellTypes,
                resourceFallSupportBuildObjectTypes,
                resourceFallStepDurationSeconds);
            context.ResourceInventoryService.AddDefaultStartingResources();
            context.IronRocketArrivalService = new IronRocketArrivalService(
                world.GameTimeService,
                world.GridCoordinateConverter,
                rocketIronDataPrefab,
                externalObjectsRoot,
                rocketCadenceMode,
                rocketCadenceValue,
                rocketSpawnCell,
                rocketLandingCell,
                rocketShouldLand,
                rocketArrivalOutcomeMode,
                rocketDescendDurationSeconds,
                rocketAscendDurationSeconds,
                rocketStayDurationGameHours);
            context.CharacterNavigationService = new CharacterNavigationService(world.GridState);
            context.AnimalDebugSpawnConfigs = animalDebugSpawnConfigs;
            context.AnimalEggService = new AnimalEggService(
                world.GridState,
                world.GridCoordinateConverter);
            context.AnimalSimulationService = new AnimalSimulationService(
                world.GridState,
                world.GridCoordinateConverter,
                context.CharacterNavigationService,
                context.AnimalEggService,
                animalDebugSpawnConfigs);
            context.AnimalWorldSelectionService = new AnimalWorldSelectionService(context.AnimalSimulationService);
            context.BuildingManager = new BuildingManager(world.GridState, context.GlobalTaskBoardService, context.ResourceInventoryService);
            context.CablePlacementService = new CablePlacementService();
            context.CablePreviewRefreshService = new CablePreviewRefreshService(
                world.GridState,
                world.GridTileVisualService,
                context.GlobalTaskBoardService);
            context.WaterPlacementService = new WaterPlacementService();
            context.WaterPreviewRefreshService = new WaterPreviewRefreshService(
                world.GridState,
                world.GridTileVisualService,
                context.GlobalTaskBoardService);
            context.WaterNetworkService = new WaterNetworkService(world.GridState);
            context.WaterSimulationService = new WaterSimulationService(world.GridState, context.ResourceInventoryService);
            context.OxygenPlacementService = new OxygenPlacementService();
            context.OxygenPreviewRefreshService = new OxygenPreviewRefreshService(
                world.GridState,
                world.GridTileVisualService,
                context.GlobalTaskBoardService);
            context.OxygenNetworkService = new OxygenNetworkService(world.GridState);
            context.OxygenSimulationService = new OxygenSimulationService(
                world.GridState,
                context.ResourceInventoryService,
                context.WaterSimulationService);
            var solarPowerProductionService = new SolarPowerProductionService();
            context.PowerNetworkService = new PowerNetworkService(
                world.GridState,
                solarPowerProductionService,
                new BatteryStorageService());
            context.ColonyEventService = new ColonyEventService(
                colonyEventCatalog,
                world.GameTimeService,
                context.ResourceInventoryService,
                solarPowerProductionService);
            context.BuildingPlacementService = new BuildingPlacementService(context.BuildingManager, context.GlobalTaskBoardService, enableAiLogs);
            context.LifeModulePlacementService = new LifeModulePlacementService(
                world.GridState,
                context.GlobalTaskBoardService,
                context.BuildingPlacementService,
                context.ResourceInventoryService,
                lifeModuleConstructionConfig);
            context.OfferSystemService = new OfferSystemService(
                offerCatalog,
                world.GridState,
                context.BuildingManager,
                context.ResourceInventoryService,
                world.GameTimeService);
            context.ShopSystemService = new ShopSystemService(
                shopCatalog,
                context.ResourceInventoryService,
                context.OfferSystemService,
                world.GameTimeService,
                context.IronRocketArrivalService);
            context.IronRocketArrivalService.MissionResolved += onRocketMissionResolved;
            context.LifeModulePreviewRefreshService = new LifeModulePreviewRefreshService(
                world.GridState,
                world.GridTileVisualService,
                context.GlobalTaskBoardService);
            context.ConstructionDigVisualCallbackService = new ConstructionDigVisualCallbackService(
                world.GridState,
                world.GridTileVisualService,
                world.GridCoordinateConverter,
                buildingViewRegistry,
                buildingsRoot,
                world.MaterialTransitionOverlayService,
                context.CablePlacementService,
                context.CablePreviewRefreshService,
                context.LifeModulePlacementService,
                context.LifeModulePreviewRefreshService,
                context.WaterPlacementService,
                context.WaterPreviewRefreshService,
                context.OxygenPlacementService,
                context.OxygenPreviewRefreshService);
            context.DigDurationPolicy = new DigDurationPolicy();
            context.TaskExecutionService = new TaskExecutionService(
                world.GridState,
                context.DigDurationPolicy,
                enableAiLogs,
                context.ConstructionDigVisualCallbackService.OnDigCompleted,
                cell =>
                {
                    context.SceneResourceObjectService?.NotifyCellBecameEmpty(cell);
                    context.AnimalEggService?.NotifyCellBecameEmpty(cell);
                });
            context.UnitNeedPolicy = new UnitNeedPolicy();
            context.CharacterAnimationService = new CharacterAnimationService(
                context.GlobalTaskBoardService,
                world.GridCoordinateConverter);
            context.UnitTaskOrchestratorService = new UnitTaskOrchestratorService(
                world.GridState,
                context.GlobalTaskBoardService,
                context.CharacterNavigationService,
                context.TaskExecutionService,
                context.UnitNeedPolicy,
                context.CharacterAnimationService,
                world.GridCoordinateConverter,
                context.ResourceInventoryService,
                context.SceneResourceObjectService,
                context.BuildingManager,
                context.LifeModulePlacementService,
                context.ConstructionDigVisualCallbackService.OnBuildCompleted,
                context.ConstructionDigVisualCallbackService.OnDestroyCompleted,
                context.ConstructionDigVisualCallbackService.OnCableBuildCompleted,
                context.ConstructionDigVisualCallbackService.OnCableDestroyCompleted,
                context.ConstructionDigVisualCallbackService.OnWaterBuildCompleted,
                context.ConstructionDigVisualCallbackService.OnWaterDestroyCompleted,
                context.ConstructionDigVisualCallbackService.OnOxygenBuildCompleted,
                context.ConstructionDigVisualCallbackService.OnOxygenDestroyCompleted,
                context.ConstructionDigVisualCallbackService.OnLifeModuleBuildCompleted,
                context.ConstructionDigVisualCallbackService.TriggerStorageInteractionByCell,
                onUnitCellChanged,
                foodResourceIds,
                enableAiLogs);
            context.GlobalTaskBoardService.SetDigTaskReachabilityEvaluator(
                context.UnitTaskOrchestratorService.CanAnyUnitReachDigTaskCell);
            context.TaskQueueHudBuilder = new TaskQueueHudBuilder(context.GlobalTaskBoardService, context.UnitTaskOrchestratorService);
            context.CharacterSpawnSystem = new CharacterSpawnSystem(
                characterSpawnConfig,
                world.GridState,
                gridOrigin,
                characterSpawnRoot,
                characterSpawnSeed,
                foodResourceIds);
            context.BuildingManager.SetWaterSimulationService(context.WaterSimulationService);
            context.BuildingManager.SetOxygenSimulationService(context.OxygenSimulationService);
            context.PowerBuildingOverlayService = new PowerBuildingOverlayService(
                null,
                world.GridCoordinateConverter,
                world.GridState,
                world.GameTimeService,
                Camera.main,
                cellSize,
                powerNoSourceIcon != null ? powerNoSourceIcon.texture : null);

            return context;
        }

        private static List<string> BuildFoodResourceIds(IReadOnlyList<ShopOfferDefinition> shopCatalog)
        {
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (shopCatalog == null)
            {
                return result;
            }

            for (int i = 0; i < shopCatalog.Count; i++)
            {
                ShopOfferDefinition definition = shopCatalog[i];
                if (definition?.Product == null || definition.Product.Category != ShopProductCategory.Food)
                {
                    continue;
                }

                string resourceId = definition.Product.ResourceId;
                if (string.IsNullOrWhiteSpace(resourceId) || !seen.Add(resourceId))
                {
                    continue;
                }

                result.Add(resourceId);
            }

            result.Sort(StringComparer.Ordinal);
            return result;
        }
    }
}
