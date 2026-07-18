using _Project.Scripts.Systems.Resources;
using _Project.Scripts.Systems.ColonyEvents;
using _Project.Scripts.Systems.Offers;
using _Project.Scripts.Systems.Shop;
using _Project.Scripts.Presentation.UI.Offers;
using _Project.Scripts.Presentation.UI.Shop;
using System;
using UnityEngine.UIElements;
using UnityEngine;

namespace _Project.Scripts.Presentation.UI
{
    public static class HudSetupService
    {
        public readonly struct HudSetupResult
        {
            public readonly TaskQueuePanelPresenter TaskQueuePanelPresenter;
            public readonly ResourceInventoryPanelPresenter ResourceInventoryPanelPresenter;
            public readonly GameTimeHudPresenter GameTimeHudPresenter;
            public readonly ColonyEventHudPresenter ColonyEventHudPresenter;
            public readonly OfferPanelPresenter OfferPanelPresenter;
            public readonly ShopPanelPresenter ShopPanelPresenter;
            public readonly HudWindowCoordinator HudWindowCoordinator;
            public readonly ConstructionToolPanelController ConstructionToolPanelController;

            public HudSetupResult(
                TaskQueuePanelPresenter taskQueuePanelPresenter,
                ResourceInventoryPanelPresenter resourceInventoryPanelPresenter,
                GameTimeHudPresenter gameTimeHudPresenter,
                ColonyEventHudPresenter colonyEventHudPresenter,
                OfferPanelPresenter offerPanelPresenter,
                ShopPanelPresenter shopPanelPresenter,
                HudWindowCoordinator hudWindowCoordinator,
                ConstructionToolPanelController constructionToolPanelController)
            {
                TaskQueuePanelPresenter = taskQueuePanelPresenter;
                ResourceInventoryPanelPresenter = resourceInventoryPanelPresenter;
                GameTimeHudPresenter = gameTimeHudPresenter;
                ColonyEventHudPresenter = colonyEventHudPresenter;
                OfferPanelPresenter = offerPanelPresenter;
                ShopPanelPresenter = shopPanelPresenter;
                HudWindowCoordinator = hudWindowCoordinator;
                ConstructionToolPanelController = constructionToolPanelController;
            }
        }

        public static HudSetupResult Setup(
            UIDocument uiDocument,
            ConstructionToolDefinitions constructionToolDefinitions,
            HudMenuIconSet hudMenuIconSet,
            ResourceInventoryService resourceInventoryService,
            SceneResourceObjectService sceneResourceObjectService,
            ColonyEventService colonyEventService,
            OfferSystemService offerSystemService,
            ShopSystemService shopSystemService,
            bool enableAiLogs,
            Action pauseAction,
            Action playAction,
            Action<float> speedChangeAction)
        {
            var panelController = new ConstructionToolPanelController(
                constructionToolDefinitions != null ? constructionToolDefinitions.LadderBuildingDef : null,
                constructionToolDefinitions != null ? constructionToolDefinitions.BridgeBuildingDef : null,
                constructionToolDefinitions != null ? constructionToolDefinitions.StorageBuildingDef : null,
                constructionToolDefinitions != null ? constructionToolDefinitions.SolarPanelBuildingDef : null,
                constructionToolDefinitions != null ? constructionToolDefinitions.RegolithProcessingUnitBuildingDef : null,
                constructionToolDefinitions != null ? constructionToolDefinitions.SleepModuleBuildingDef : null,
                constructionToolDefinitions != null ? constructionToolDefinitions.BatteryBuildingDef : null,
                constructionToolDefinitions != null ? constructionToolDefinitions.DinnerBuildingDef : null,
                constructionToolDefinitions != null ? constructionToolDefinitions.OxygenStorageBuildingDef : null,
                constructionToolDefinitions != null ? constructionToolDefinitions.OxigenProcessingUnitBuildingDef : null,
                constructionToolDefinitions != null ? constructionToolDefinitions.WaterReclamationBuildingDef : null,
                constructionToolDefinitions != null ? constructionToolDefinitions.WaterProcessingUnitBuildingDef : null,
                enableAiLogs);
            var hudWindowCoordinator = new HudWindowCoordinator();

            if (uiDocument == null)
            {
                return new HudSetupResult(null, null, null, null, null, null, hudWindowCoordinator, panelController);
            }

            VisualElement root = uiDocument.rootVisualElement;
            if (root == null)
            {
                return new HudSetupResult(null, null, null, null, null, null, hudWindowCoordinator, panelController);
            }

            var taskQueuePanelPresenter = new TaskQueuePanelPresenter(root, hudWindowCoordinator);
            var resourceInventoryPanelPresenter = new ResourceInventoryPanelPresenter(root, resourceInventoryService, sceneResourceObjectService);
            var gameTimeHudPresenter = new GameTimeHudPresenter(root, pauseAction, playAction, speedChangeAction);
            var colonyEventHudPresenter = new ColonyEventHudPresenter(root, colonyEventService);
            var offerPanelPresenter = new OfferPanelPresenter(root, offerSystemService, hudWindowCoordinator);
            var shopPanelPresenter = new ShopPanelPresenter(root, shopSystemService, hudWindowCoordinator);
            _ = new BottomHudMenuPresenter(root, hudWindowCoordinator);
            MenuButtonIconBinder.Bind(root, hudMenuIconSet);

            Button shovelButton = root.Q<Button>("shovel-btn");
            Button destructionBtn = root.Q<Button>("destruction-btn");
            Button solarPanelBtn = root.Q<Button>("solar-panel-btn");
            Button tool2Button = root.Q<Button>("build-ladder-btn");
            Button tool10Button = root.Q<Button>("build-bridge-btn");
            Button tool3Button = root.Q<Button>("build-storage-btn");
            Button tool4Button = root.Q<Button>("regolith-processing-btn");
            Button tool5Button = root.Q<Button>("sleep-module-btn");
            Button tool6Button = root.Q<Button>("battery-btn");
            Button tool7Button = root.Q<Button>("dinner-btn");
            Button tool8Button = root.Q<Button>("oxygen-storage-btn");
            Button tool11Button = root.Q<Button>("oxygen-processing-btn");
            Button tool9Button = root.Q<Button>("water-reclamation-btn");
            Button tool12Button = root.Q<Button>("water-processing-btn");
            Button cableBuildButton = root.Q<Button>("build-cable-btn");
            Button cableCancelButton = root.Q<Button>("cancel-cable-btn");
            Button cableExitButton = root.Q<Button>("exit-cable-btn");
            Button waterBuildButton = root.Q<Button>("build-water-btn");
            Button waterCancelButton = root.Q<Button>("cancel-water-btn");
            Button waterExitButton = root.Q<Button>("exit-water-btn");
            Button oxygenBuildButton = root.Q<Button>("build-oxygen-btn");
            Button oxygenCancelButton = root.Q<Button>("cancel-oxygen-btn");
            Button oxygenExitButton = root.Q<Button>("exit-oxygen-btn");
            Button lifeModuleBuildButton = root.Q<Button>("build-life-module-btn");
            Button lifeModuleCancelButton = root.Q<Button>("cancel-life-module-btn");
            Button shovelCancelButton = root.Q<Button>("cancel-shovel-btn");

            panelController.Bind(destructionBtn, shovelButton, tool2Button, tool10Button, tool3Button, solarPanelBtn, tool4Button, tool5Button, tool6Button, tool7Button, tool8Button, tool11Button, tool9Button, tool12Button, cableBuildButton, cableCancelButton, cableExitButton, waterBuildButton, waterCancelButton, waterExitButton, oxygenBuildButton, oxygenCancelButton, oxygenExitButton, lifeModuleBuildButton, lifeModuleCancelButton, shovelCancelButton);

            return new HudSetupResult(
                taskQueuePanelPresenter,
                resourceInventoryPanelPresenter,
                gameTimeHudPresenter,
                colonyEventHudPresenter,
                offerPanelPresenter,
                shopPanelPresenter,
                hudWindowCoordinator,
                panelController);
        }
    }
}
