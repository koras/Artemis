using _Project.Scripts.Presentation.UI;
using _Project.Scripts.Presentation.UI.Offers;
using _Project.Scripts.Presentation.UI.Shop;
using _Project.Scripts.Systems.ColonyEvents;
using _Project.Scripts.Systems.Offers;
using _Project.Scripts.Systems.Resources;
using _Project.Scripts.Systems.Shop;
using _Project.Scripts.Systems.Simulation;
using _Project.Scripts.Systems.Units;
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace _Project.Scripts.Bootstrap.Runtime
{
    /// <summary>
    /// Composes HUD presenters and panel controllers.
    /// </summary>
    internal static class UiRuntimeComposer
    {
        public static UiRuntimeContext Compose(
            UIDocument uiDocument,
            ConstructionToolDefinitions constructionToolDefinitions,
            HudMenuIconSet hudMenuIconSet,
            ResourceInventoryService resourceInventoryService,
            SceneResourceObjectService sceneResourceObjectService,
            ColonyEventService colonyEventService,
            OfferSystemService offerSystemService,
            ShopSystemService shopSystemService,
            bool enableAiLogs,
            bool isPowerDebugVisible,
            PowerBuildingOverlayService powerBuildingOverlayService,
            UnitNeedPolicy unitNeedPolicy,
            GameTimeService gameTimeService,
            int missionCount,
            Action pauseAction,
            Action playAction,
            Action<float> speedChangeAction)
        {
            if (uiDocument != null && !uiDocument.enabled)
            {
                uiDocument.enabled = true;
            }

            HudSetupService.HudSetupResult hudSetupResult = HudSetupService.Setup(
                uiDocument,
                constructionToolDefinitions,
                hudMenuIconSet,
                resourceInventoryService,
                sceneResourceObjectService,
                colonyEventService,
                offerSystemService,
                shopSystemService,
                enableAiLogs,
                pauseAction,
                playAction,
                speedChangeAction);

            var context = new UiRuntimeContext
            {
                ConstructionToolPanelController = hudSetupResult.ConstructionToolPanelController,
                TaskQueuePanelPresenter = hudSetupResult.TaskQueuePanelPresenter,
                ResourceInventoryPanelPresenter = hudSetupResult.ResourceInventoryPanelPresenter,
                GameTimeHudPresenter = hudSetupResult.GameTimeHudPresenter,
                ColonyEventHudPresenter = hudSetupResult.ColonyEventHudPresenter,
                OfferPanelPresenter = hudSetupResult.OfferPanelPresenter,
                ShopPanelPresenter = hudSetupResult.ShopPanelPresenter,
                HudWindowCoordinator = hudSetupResult.HudWindowCoordinator,
                HudRootElement = uiDocument != null ? uiDocument.rootVisualElement : null
            };

            EnsurePowerDebugPanelAttached(context.HudRootElement);
            context.PowerDebugHudPresenter = new PowerDebugHudPresenter(context.HudRootElement);
            context.CharacterDiagnosticsPanelPresenter = new CharacterDiagnosticsPanelPresenter(
                context.HudRootElement,
                unitNeedPolicy != null ? unitNeedPolicy.CriticalHunger : 220,
                unitNeedPolicy != null ? unitNeedPolicy.CriticalSleep : 220,
                context.HudWindowCoordinator);
            context.AnimalDiagnosticsPanelPresenter = new AnimalDiagnosticsPanelPresenter(
                context.HudRootElement,
                context.HudWindowCoordinator);
            powerBuildingOverlayService?.SetHudRoot(context.HudRootElement);

            if (context.GameTimeHudPresenter == null)
            {
                context.IsHudInitialized = false;
                return context;
            }

            context.IsHudInitialized = true;
            context.GameTimeHudPresenter.Refresh(gameTimeService);
            context.GameTimeHudPresenter.RefreshRocketMissionCount(missionCount);
            context.PowerDebugHudPresenter.SetVisible(isPowerDebugVisible);
            return context;
        }

        private static void EnsurePowerDebugPanelAttached(VisualElement hudRoot)
        {
            if (hudRoot == null)
            {
                return;
            }

            if (hudRoot.Q<VisualElement>("power-debug-panel") != null)
            {
                return;
            }

            VisualTreeAsset powerDebugPanelAsset = Resources.Load<VisualTreeAsset>("UI/PowerDebugPanel");
            if (powerDebugPanelAsset == null)
            {
                Debug.LogWarning("[PowerDebugHud] Missing Resources/UI/PowerDebugPanel.uxml.");
                return;
            }

            TemplateContainer panelTree = powerDebugPanelAsset.CloneTree();
            hudRoot.Add(panelTree);
        }
    }
}
