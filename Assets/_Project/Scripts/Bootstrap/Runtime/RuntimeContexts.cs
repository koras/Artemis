using _Project.Scripts.Data.Grid;
using _Project.Scripts.Data.Animals;
using _Project.Scripts.Input;
using _Project.Scripts.Presentation.Grid;
using _Project.Scripts.Presentation.UI;
using _Project.Scripts.Presentation.UI.Offers;
using _Project.Scripts.Presentation.UI.Shop;
using _Project.Scripts.Systems.Animals;
using _Project.Scripts.Systems.Character;
using _Project.Scripts.Systems.ColonyEvents;
using _Project.Scripts.Systems.Construction;
using _Project.Scripts.Systems.External;
using _Project.Scripts.Systems.Grid;
using _Project.Scripts.Systems.Navigation;
using _Project.Scripts.Systems.Offers;
using _Project.Scripts.Systems.Oxygen;
using _Project.Scripts.Systems.Power;
using _Project.Scripts.Systems.Resources;
using _Project.Scripts.Systems.Shop;
using _Project.Scripts.Systems.Simulation;
using _Project.Scripts.Systems.Tasks;
using _Project.Scripts.Systems.Units;
using _Project.Scripts.Systems.Water;
using UnityEngine.UIElements;

namespace _Project.Scripts.Bootstrap.Runtime
{
    /// <summary>
    /// Core world runtime references shared across bootstrap stages.
    /// </summary>
    internal sealed class WorldRuntimeContext
    {
        public GridSystem GridSystem;
        public SimulationSystem SimulationSystem;
        public GameTimeService GameTimeService;
        public GridState GridState;
        public GridCoordinateConverter GridCoordinateConverter;
        public GameInputRuntimeCoordinator GameInputRuntimeCoordinator;
        public GridTileVisualService GridTileVisualService;
        public FogMaskService FogMaskService;
        public FogOverlayRenderer FogOverlayRenderer;
        public MaterialTransitionOverlayService MaterialTransitionOverlayService;
    }

    /// <summary>
    /// Gameplay service graph references that power simulation ticks.
    /// </summary>
    internal sealed class GameplayRuntimeContext
    {
        public ResourceInventoryService ResourceInventoryService;
        public SceneResourceObjectService SceneResourceObjectService;
        public ColonyEventService ColonyEventService;
        public OfferSystemService OfferSystemService;
        public ShopSystemService ShopSystemService;
        public IronRocketArrivalService IronRocketArrivalService;
        public CharacterNavigationService CharacterNavigationService;
        public TaskScoringService TaskScoringService;
        public GlobalTaskBoardService GlobalTaskBoardService;
        public BuildingManager BuildingManager;
        public CablePlacementService CablePlacementService;
        public CablePreviewRefreshService CablePreviewRefreshService;
        public LifeModulePlacementService LifeModulePlacementService;
        public LifeModulePreviewRefreshService LifeModulePreviewRefreshService;
        public WaterPlacementService WaterPlacementService;
        public WaterPreviewRefreshService WaterPreviewRefreshService;
        public WaterNetworkService WaterNetworkService;
        public WaterSimulationService WaterSimulationService;
        public OxygenPlacementService OxygenPlacementService;
        public OxygenPreviewRefreshService OxygenPreviewRefreshService;
        public OxygenNetworkService OxygenNetworkService;
        public OxygenSimulationService OxygenSimulationService;
        public PowerNetworkService PowerNetworkService;
        public ConstructionDigVisualCallbackService ConstructionDigVisualCallbackService;
        public DigDurationPolicy DigDurationPolicy;
        public TaskExecutionService TaskExecutionService;
        public UnitNeedPolicy UnitNeedPolicy;
        public CharacterAnimationService CharacterAnimationService;
        public UnitTaskOrchestratorService UnitTaskOrchestratorService;
        public TaskQueueHudBuilder TaskQueueHudBuilder;
        public CharacterSpawnSystem CharacterSpawnSystem;
        public CharacterGroupService CharacterGroupService;
        public BuildingPlacementService BuildingPlacementService;
        public ToolInputInteractionService ToolInputInteractionService;
        public GridHoverHighlightService GridHoverHighlightService;
        public PowerBuildingOverlayService PowerBuildingOverlayService;
        public BuildModeVisualTintService BuildModeVisualTintService;
        public AnimalDebugSpawnConfig[] AnimalDebugSpawnConfigs;
        public AnimalEggService AnimalEggService;
        public AnimalSimulationService AnimalSimulationService;
        public AnimalWorldSelectionService AnimalWorldSelectionService;
    }

    /// <summary>
    /// HUD and presenter references used by update and cleanup stages.
    /// </summary>
    internal sealed class UiRuntimeContext
    {
        public ConstructionToolPanelController ConstructionToolPanelController;
        public TaskQueuePanelPresenter TaskQueuePanelPresenter;
        public ResourceInventoryPanelPresenter ResourceInventoryPanelPresenter;
        public GameTimeHudPresenter GameTimeHudPresenter;
        public ColonyEventHudPresenter ColonyEventHudPresenter;
        public OfferPanelPresenter OfferPanelPresenter;
        public ShopPanelPresenter ShopPanelPresenter;
        public HudWindowCoordinator HudWindowCoordinator;
        public HudMenuUnlockService HudMenuUnlockService;
        public TaskQueueHudRefreshService TaskQueueHudRefreshService;
        public PowerDebugHudPresenter PowerDebugHudPresenter;
        public CharacterDiagnosticsPanelPresenter CharacterDiagnosticsPanelPresenter;
        public AnimalDiagnosticsPanelPresenter AnimalDiagnosticsPanelPresenter;
        public VisualElement HudRootElement;
        public bool IsHudInitialized;
    }

    /// <summary>
    /// Aggregated runtime handles for tick/update/destroy lifecycle paths.
    /// </summary>
    internal sealed class RuntimeHandles
    {
        public WorldRuntimeContext World;
        public GameplayRuntimeContext Gameplay;
        public UiRuntimeContext Ui;
    }
}
