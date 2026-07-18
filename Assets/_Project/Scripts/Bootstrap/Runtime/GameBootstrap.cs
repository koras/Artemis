using System.Collections.Generic;
using _Project.Scripts.Data.Animals;
using _Project.Scripts.Data.Character;
using _Project.Scripts.Data.ColonyEvents;
using _Project.Scripts.Data.Grid;
using _Project.Scripts.Input;
using _Project.Scripts.Presentation.Character;
using _Project.Scripts.Presentation.Grid;
using _Project.Scripts.Systems.Character;
using _Project.Scripts.Systems.Grid;
using _Project.Scripts.Systems.Navigation;
using _Project.Scripts.Systems.Resources;
using _Project.Scripts.Systems.Simulation;
using _Project.Scripts.Systems.Offers;
using _Project.Scripts.Systems.External;
using _Project.Scripts.Systems.Tasks;
using _Project.Scripts.Systems.Units;
using UnityEngine;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using _Project.Scripts.Data.Tasks;
using _Project.Scripts.Data.Construction;
using _Project.Scripts.Presentation.Animals;
using _Project.Scripts.Presentation.Buildings;
using _Project.Scripts.Presentation.UI;
using _Project.Scripts.Presentation.UI.Offers;
using _Project.Scripts.Systems.Construction;
using _Project.Scripts.Systems.Animals;
using _Project.Scripts.Systems.ColonyEvents;
using _Project.Scripts.Data.Offers;
using _Project.Scripts.Data.Shop;
using _Project.Scripts.Systems.Shop;
using _Project.Scripts.Systems.Oxygen;
using _Project.Scripts.Systems.Power;
using _Project.Scripts.Systems.Water;
using _Project.Scripts.Presentation.UI.Shop;



namespace _Project.Scripts.Bootstrap.Runtime
{
    /// <summary>
    /// Main runtime entry point: world grid, renderer, input, simulation, and unit AI tasks.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        private const int LocalRefreshRadius = 3;

        // World grid width (in cells).
        [Header("Grid")]
        [SerializeField] private int _width = 500;
        // World grid height (in cells).
        [SerializeField] private int _height = 500;

        // Simulation tick interval in seconds.
        // Every interval triggers RunTick().
        [Header("Simulation")]
        [SerializeField] private float _tickIntervalSeconds = 3f;
        [SerializeField] private float _maxSimulationSpeedMultiplier = 20f;
        [Header("Character Needs")]
        [SerializeField] [Min(0f)] private float _hungerIncreasePerGameHour = 10f;
        [SerializeField] [Min(0f)] private float _sleepDesireIncreasePerGameHour = 10f;
        // Initial origin offset for grid/world coordinate conversion.
        [SerializeField] private Vector2 _gridOrigin = Vector2.zero;
        // Timeout in ticks for task reservation entries.
        // If a unit does not reach the reserved task in time, the reservation is cleared.
        [SerializeField] private int _taskReservationTimeoutTicks = 120;

        [Header("Cell Size")]
        // Size of one grid cell in world units.
        [SerializeField] private int _cellSize = 1;

        [Header("AI Debug")]
        // Enables verbose AI diagnostics.
        [SerializeField] private bool _enableAiLogs = true;
        // Draw cached character navigation paths in Scene view.
        [SerializeField] private bool _drawNavigationPathGizmos = true;
        [SerializeField] private bool _drawNavigationPathRuntime = true;
        [SerializeField] private bool _drawNavigationWalkabilityGizmos = true;
        [SerializeField] private int _navigationWalkabilityGizmoMarginCells = 2;
        // Radius of path points in gizmo debug rendering.
        [SerializeField] private float _navigationPathGizmoPointRadius = 0.08f;

        // Draw water mask index (0..15) over cells in Scene view (editor only).
        [SerializeField] private bool _drawWaterMaskIndexGizmos = true;
        // Limits water mask index label drawing area around camera center in cells.
        [SerializeField] private int _waterMaskIndexGizmoRadiusInCells = 24;
        // Draw cable tile indices over cells in Scene view (editor only).
        [SerializeField] private bool _drawCableTileIndexGizmos = true;
        // Limits cable index label drawing area around camera center in cells.
        [SerializeField] private int _cableTileIndexGizmoRadiusInCells = 24;
        // Draws ship landing reservation labels in Scene view (editor only).
        [SerializeField] private bool _drawShipLandingZoneDebugGizmos = true;
        // Draws upper dig-protection reservation labels in Scene view (editor only).
        [SerializeField] private bool _drawShipDigProtectionDebugGizmos = true;

        // World grid data container.
        private GridState _gridState;
        // Converter between world coordinates and grid cells.
        private GridCoordinateConverter _gridCoordinateConverter;
        [Header("Camera")]
        // Camera movement logic.
        [SerializeField] private float _cameraMoveSpeed = 20f;
        // Stable scene-owned target that Cinemachine follows during panning.
        [SerializeField] private Transform _cameraFollowTarget;

        [Header("Grid Render")]
        // Renderer component with tilemap/tile links for grid rendering.
        [SerializeField] private GridTilemapRenderSettings _gridTilemapRenderSettings;
        [SerializeField] private SpriteRenderer _fogOverlayRenderer;
        [SerializeField] private Material _fogOverlayMaterial;
        // Distance in cells where fog reaches 50% darkness.
        [SerializeField] private float _fogHalfDarkDistanceCells = 2.5f;
        // Distance in cells where fog reaches 100% darkness.
        [SerializeField] private float _fogFullDarkDistanceCells = 5f;


        [Header("UI")]
        // Root HUD UIDocument.
        [SerializeField] private UIDocument _uiDocument;
        // Builder provider for construction definitions (ladder/storage), injected from bootstrap.
        [SerializeField] private ConstructionToolDefinitions _constructionToolDefinitions;
        // HUD icon set where each button icon is assigned manually in Inspector.
        [SerializeField] private HudMenuIconSet _hudMenuIconSet;
        // Current HUD visibility state.
        private bool _isHudVisible = true;


        // Tilemap layer visualization service (base, markers, previews).
        private GridTileVisualService _gridTileVisualService;
        private FogMaskService _fogMaskService;
        private FogOverlayRenderer _fogOverlayVisualService;

        private bool _isTaskQueueVisible;
        private bool _isPowerDebugVisible;
        private bool _isSimulationPaused;
        private float _simulationSpeedMultiplier = 1f;
        // Accumulates real frame time until the next simulation tick should run.
        private float _simulationTickTimerSeconds;
        private float _defaultFixedDeltaTime;
        private MaterialTransitionOverlayService _materialTransitionOverlayService;


        [Header("Character Spawn")]
        [SerializeField] private CharacterSpawnConfig _characterSpawnConfig;
        [SerializeField] private Transform _characterSpawnRoot;
        [SerializeField] private int _characterSpawnSeed = 12345;

        [Header("Animal Spawn")]
        [SerializeField] private AnimalDebugSpawnConfig[] _animalDebugSpawnConfigs;


        // Task/Unit services
        // Character needs policy (hunger/sleep/etc.).
        private UnitNeedPolicy _unitNeedPolicy;
        // Orchestrator for the full unit task lifecycle.
        private UnitTaskOrchestratorService _unitTaskOrchestratorService;
        // Construction manager (create/finalize construction tasks).
        private _Project.Scripts.Systems.Construction.BuildingManager _buildingManager;


        [Header("Construction")]
        // Current simulation tick counter.
        private int _tickCounter;
        // Registry mapping BuildingDef to runtime view prefabs.
        [SerializeField] private BuildingViewRegistry _buildingViewRegistry;
        [SerializeField] private Sprite _powerNoSourceIcon;
        [SerializeField] private List<ColonyEventDefinition> _colonyEventCatalog;
        [SerializeField] private List<OfferDefinition> _offerCatalog;
        [SerializeField] private List<ShopOfferDefinition> _shopCatalog;
        // Root for all persistent runtime building hierarchies.
        [SerializeField] private Transform _buildingsRoot;
        [Header("Scene Resource Prefabs")]
        [SerializeField] private Transform _resourceObjectsRoot;
        [SerializeField] private GameObject _ironResourcePrefab;
        [SerializeField] private GameObject _titanResourcePrefab;
        [SerializeField] private GameObject _rogaliteResourcePrefab;
        [SerializeField] private GameObject _aluminiumResourcePrefab;
        [SerializeField] private List<CellType> _resourceFallSupportCellTypes = new List<CellType> { CellType.Iron, CellType.Titan, CellType.Rogalite, CellType.Aluminium };
        [SerializeField] private List<BuildObjectType> _resourceFallSupportBuildObjectTypes = new List<BuildObjectType> { BuildObjectType.Storage, BuildObjectType.RocketData };
        [SerializeField] private float _resourceFallStepDurationSeconds = 0.18f;
        [SerializeField] private LifeModuleConstructionConfig _lifeModuleConstructionConfig;

        [Header("Iron Rocket")]
        [SerializeField] private GameObject _rocketIronDataPrefab;
        [SerializeField] private Transform _externalObjectsRoot;
        [SerializeField] private string _externalStorageObjectName = "RocketData";
        [SerializeField] private Vector2Int _rocketSpawnCell = new Vector2Int(48, 99);
        [SerializeField] private Vector2Int _rocketLandingCell = new Vector2Int(48, 95);
        [SerializeField] private bool _rocketShouldLand = true;
        [SerializeField] private IronRocketArrivalService.ArrivalOutcomeMode _rocketArrivalOutcomeMode = IronRocketArrivalService.ArrivalOutcomeMode.Success;
        [SerializeField] private float _rocketDescendDurationSeconds = 2f;
        [SerializeField] private float _rocketAscendDurationSeconds = 2f;
        [SerializeField] private int _rocketStayDurationGameHours = 1;
        [SerializeField] private IronRocketArrivalService.ArrivalCadenceMode _rocketCadenceMode = IronRocketArrivalService.ArrivalCadenceMode.ByDays;
        [SerializeField] private int _rocketCadenceValue = 7;
        private RuntimeHandles _runtimeHandles;
        private BootstrapDebugFacade _bootstrapDebugFacade;
        private readonly List<BuildingRuntimeEntity> _activeBuildingsTickBuffer = new List<BuildingRuntimeEntity>();

        /// <summary>
        /// System initialization entry point.
        /// Creates world/services, subscribes input and hover handlers, initializes UI, and starts the tick loop.
        /// </summary>
        private void Awake()
        {
            _defaultFixedDeltaTime = Time.fixedDeltaTime;
            EnsureHudDocumentEnabled();
        }

        private void Start()
        {
            // Гарантируем, что корневой HUD уже активен до сборки UI-зависимостей:
            // часть сервисов сразу читает визуальное дерево документа.
            EnsureHudDocumentEnabled();

            // Фасад собирает отладочную отрисовку в одном месте, чтобы дальше не держать
            // логику gizmos и debug-лейблов прямо в bootstrap.
            _bootstrapDebugFacade = new BootstrapDebugFacade(_navigationPathGizmoPointRadius);

            // Сначала поднимаем мир: сетку, координаты, рендер и базовые runtime-контексты.
            // Все следующие этапы опираются на уже созданный GridState и world-сервисы.
            InitializeCoreWorld();

            // Отдельный хук под визуальный слой.
            // Сейчас он почти пустой, но порядок сохраняем как точку расширения пайплайна.
            InitializeRenderingAndFog();

            // После мира собираем gameplay-сервисы: строительство, сети, симуляции, задачи и AI.
            // Здесь появляются основные доменные зависимости игры.
            InitializeGameplaySystems();

            // UI и input инициализируем после gameplay:
            // панели и обработчики уже должны знать, с какими runtime-сервисами работать.
            InitializeUiAndInput();

            // Персонажей спавним только после того, как готовы мир, задачи и навигация,
            // иначе их будет некуда корректно зарегистрировать.
            InitializeCharacters();

            // Животные поднимаются отдельным этапом после общей gameplay-сборки
            // по той же причине: им нужен уже готовый runtime-контекст.
            InitializeAnimals();

            // И только в самом конце связываем события и разрешаем тик-цикл,
            // чтобы промежуточные этапы инициализации не реагировали на неполное состояние.
            WireEventsAndStartLoop();
        }

        // Stage 1: world-level systems and rendering roots.
        private void InitializeCoreWorld()
        {
            // Compose создаёт базовый runtime мира:
            // GridState, координатные сервисы, рендер, fog и сопутствующие world-объекты.
            WorldRuntimeContext world = WorldRuntimeComposer.Compose(
                _width,
                _height,
                _cellSize,
                _gridOrigin,
                _cameraMoveSpeed,
                _cameraFollowTarget,
                _gridTilemapRenderSettings,
                _fogOverlayRenderer,
                _fogOverlayMaterial,
                _fogHalfDarkDistanceCells,
                _fogFullDarkDistanceCells);

            // Подписываемся сразу после создания мира, чтобы все дальнейшие изменения ячеек
            // могли обновлять fog и переходные материалы централизованно через bootstrap.
            world.GridState.CellChanged += OnGridCellChanged;

            // Сохраняем world-контекст как основу всего runtime-графа.
            _runtimeHandles = new RuntimeHandles { World = world };

            // Поддерживаем старые приватные поля синхронизированными с новым контекстом,
            // потому что часть кода bootstrap пока ещё читает зависимости напрямую из полей.
            SyncLegacyWorldFields(world);

            // Камеру позиционируем после создания сетки:
            // сервису нужны реальные размеры мира и origin, чтобы стартовая точка была корректной.
            CameraStartPositionService.Apply(Camera.main, _cameraFollowTarget, _width, _height, _cellSize, _gridOrigin);
        }

        // Stage 2: no-op in this iteration because rendering is composed with world context.
        private void InitializeRenderingAndFog()
        {
            // Reserved tilemap now participates in build preview rendering.
            // Do not repaint legacy reserved debug overlay here to avoid layer conflicts.
        }


        private List<ShopOfferDefinition> ResolveShopCatalog()
        {
            if (_shopCatalog != null && _shopCatalog.Count > 0)
            {
                return _shopCatalog;
            }

            // Fallback for scenes where catalog links were not rewired yet.
            ShopOfferDefinition[] fromResources = Resources.LoadAll<ShopOfferDefinition>("Shop");
            if (fromResources == null || fromResources.Length == 0)
            {
                return new List<ShopOfferDefinition>();
            }

            var list = new List<ShopOfferDefinition>(fromResources.Length);
            for (int i = 0; i < fromResources.Length; i++)
            {
                if (fromResources[i] != null)
                {
                    list.Add(fromResources[i]);
                }
            }

            return list;
        }

        // Stage 3: gameplay graph and service composition.
        private void InitializeGameplaySystems()
        {
            // Compose собирает доменную часть игры поверх уже созданного мира:
            // менеджеры строительства, task board, сети, симуляции ресурсов, ракеты и т.д.
            GameplayRuntimeContext gameplay = GameplayRuntimeComposer.Compose(
                _runtimeHandles.World,
                _animalDebugSpawnConfigs,
                _characterSpawnConfig,
                _characterSpawnRoot,
                _characterSpawnSeed,
                _gridOrigin,
                _buildingViewRegistry,
                _buildingsRoot,
                ColonyEventCatalogResolver.Resolve(_colonyEventCatalog),
                _offerCatalog,
                ResolveShopCatalog(),
                _rocketIronDataPrefab,
                _externalObjectsRoot,
                _rocketSpawnCell,
                _rocketLandingCell,
                _rocketShouldLand,
                _rocketArrivalOutcomeMode,
                _rocketDescendDurationSeconds,
                _rocketAscendDurationSeconds,
                _rocketStayDurationGameHours,
                _rocketCadenceMode,
                _rocketCadenceValue,
                _enableAiLogs,
                _powerNoSourceIcon,
                _cellSize,
                _resourceObjectsRoot,
                _ironResourcePrefab,
                _titanResourcePrefab,
                _rogaliteResourcePrefab,
                _aluminiumResourcePrefab,
                _resourceFallSupportCellTypes,
                _resourceFallSupportBuildObjectTypes,
                _resourceFallStepDurationSeconds,
                _lifeModuleConstructionConfig,
                OnUnitCellChanged,
                OnRocketMissionResolved);

            // После сборки публикуем gameplay-контекст в общих runtime-handles,
            // чтобы остальные этапы инициализации читали уже одну и ту же ссылку.
            _runtimeHandles.Gameplay = gameplay;

            // Синхронизируем старые поля bootstrap с новым контекстом.
            // Это временный мост между старой и новой организацией зависимостей.
            SyncLegacyGameplayFields(gameplay);

            // Восстановление состояния офферов идёт после создания OfferSystemService:
            // до этого сохранять/читать просто некуда.
            RestoreOfferSystemState();

            // Отдельно регистрируем внешний склад ракеты.
            // Он зависит и от grid, и от BuildingManager, поэтому вызывается после Compose.
            var rocketExternalStorageRegistrationService = new RocketExternalStorageRegistrationService(
                _gridState,
                _runtimeHandles?.Gameplay?.BuildingManager ?? _buildingManager,
                _externalObjectsRoot,
                _externalStorageObjectName,
                _rocketSpawnCell);
            rocketExternalStorageRegistrationService.Register();
        }

        // Stage 4: HUD setup and input-tool services.
        private void InitializeUiAndInput()
        {
            // Повторно страхуем активность HUD перед сборкой UI:
            // в Unity документ мог быть выключен сценой или предыдущим жизненным циклом.
            EnsureHudDocumentEnabled();

            // UI-контекст связывает визуальные панели с уже существующими gameplay/world сервисами.
            // Поэтому он создаётся только после InitializeGameplaySystems().
            UiRuntimeContext ui = UiRuntimeComposer.Compose(
                _uiDocument,
                _constructionToolDefinitions,
                _hudMenuIconSet,
                _runtimeHandles.Gameplay.ResourceInventoryService,
                _runtimeHandles.Gameplay.SceneResourceObjectService,
                _runtimeHandles.Gameplay.ColonyEventService,
                _runtimeHandles.Gameplay.OfferSystemService,
                _runtimeHandles.Gameplay.ShopSystemService,
                _enableAiLogs,
                _isPowerDebugVisible,
                _runtimeHandles.Gameplay.PowerBuildingOverlayService,
                _runtimeHandles.Gameplay.UnitNeedPolicy,
                _runtimeHandles.World.GameTimeService,
                _runtimeHandles.Gameplay.IronRocketArrivalService != null ? _runtimeHandles.Gameplay.IronRocketArrivalService.MissionCount : 0,
                PauseSimulation,
                ResumeSimulation,
                SetSimulationSpeed);
            _runtimeHandles.Ui = ui;

            // Сразу проталкиваем текущее состояние паузы и скорости в HUD,
            // чтобы визуал не ждал первого пользовательского события или тика.
            RefreshSimulationControls();

            // Главный маршрутизатор пользовательских инструментов.
            // Сюда пробрасываются grid, сервисы строительства, предпросмотры и task-сервисы,
            // чтобы весь world-input сходился в одну точку.
            _runtimeHandles.Gameplay.ToolInputInteractionService = new ToolInputInteractionService(
                _runtimeHandles.World.GridState,
                _runtimeHandles.Gameplay.BuildingPlacementService,
                _runtimeHandles.World.GridTileVisualService,
                _runtimeHandles.Ui.ConstructionToolPanelController,
                _runtimeHandles.Gameplay.UnitTaskOrchestratorService,
                _runtimeHandles.Gameplay.GlobalTaskBoardService,
                _runtimeHandles.Gameplay.SceneResourceObjectService,
                _runtimeHandles.Gameplay.CablePreviewRefreshService,
                _runtimeHandles.Gameplay.ConstructionDigVisualCallbackService.OnCablePreviewCleared,
                _runtimeHandles.Gameplay.LifeModulePlacementService,
                _runtimeHandles.Gameplay.LifeModulePreviewRefreshService,
                _runtimeHandles.Gameplay.ConstructionDigVisualCallbackService.OnLifeModulePreviewCleared,
                _runtimeHandles.Gameplay.WaterPreviewRefreshService,
                _runtimeHandles.Gameplay.ConstructionDigVisualCallbackService.OnWaterPreviewCleared,
                _runtimeHandles.Gameplay.OxygenPreviewRefreshService,
                _runtimeHandles.Gameplay.ConstructionDigVisualCallbackService.OnOxygenPreviewCleared,
                cell => _runtimeHandles.Gameplay.AnimalSimulationService != null
                    && _runtimeHandles.Gameplay.AnimalSimulationService.HasAnimalAtCell(cell),
                () => _tickCounter);

            // Hover/highlight сервис создаём после input, потому что он реагирует на наведение
            // и использует уже готовые grid-визуалы.
            _runtimeHandles.Gameplay.GridHoverHighlightService = new GridHoverHighlightService(
                _runtimeHandles.World.GridState,
                _runtimeHandles.World.GridTileVisualService,
                _gridTilemapRenderSettings.HoverHighlightFadeInSeconds,
                _gridTilemapRenderSettings.HoverHighlightFadeOutSeconds);

            // Tint-сервис переключает визуальный режим строительства и зависит от visual callback'ов,
            // которые уже собраны в gameplay-контексте.
            _runtimeHandles.Gameplay.BuildModeVisualTintService = new BuildModeVisualTintService(
                _gridTilemapRenderSettings,
                _runtimeHandles.Gameplay.ConstructionDigVisualCallbackService);
            SyncLegacyGameplayFields(_runtimeHandles.Gameplay);

            // Финальные UI-шаги выполняем после сборки всех presenters/services:
            // обновляем очередь задач, применяем видимость и показываем корень HUD в консистентном состоянии.
            EnsureTaskQueueRefreshService();
            ApplyTaskQueueVisibility();
            EnsureHudRootVisible();
        }

        // Stage 5: unit spawn and registration.
        private void InitializeCharacters()
        {
            // Сначала создаём всех персонажей, чтобы дальше одним пакетом настроить их runtime-состояние.
            List<CharacterActor> spawnedCharacters = _runtimeHandles.Gameplay.CharacterSpawnSystem.SpawnAll();

            // Глобальный множитель движения должен быть установлен до первых тиков AI,
            // иначе часть юнитов начнёт жить с устаревшей скоростью.
            CharacterActor.SetGlobalMovementSpeedMultiplier(_simulationSpeedMultiplier);

            // CharacterGroupService агрегирует needs-геймплей для уже заспавненных юнитов,
            // поэтому он создаётся после SpawnAll().
            _runtimeHandles.Gameplay.CharacterGroupService = new CharacterGroupService(
                spawnedCharacters,
                _hungerIncreasePerGameHour,
                _sleepDesireIncreasePerGameHour);

            // После спавна юниты должны попасть в orchestrator:
            // без регистрации они существуют на сцене, но не участвуют в задачах и AI-цикле.
            SpawnedUnitRegistrationService.RegisterAll(
                spawnedCharacters,
                _runtimeHandles.World.GridCoordinateConverter,
                _runtimeHandles.Gameplay.UnitTaskOrchestratorService);
            SyncLegacyGameplayFields(_runtimeHandles.Gameplay);

            // Пересобираем dig-задачи по текущей сетке уже после регистрации юнитов,
            // чтобы orchestrator сразу видел актуальный набор работ.
            _runtimeHandles.Gameplay.GlobalTaskBoardService.SyncDigTasksFromGrid(_runtimeHandles.World.GridState, _tickCounter);

            // После появления юнитов обновляем раскрытие тумана войны:
            // стартовое состояние обзора должно учитывать всех активных персонажей.
            RevealFromAllUnits();
        }

        // Stage 5b: temporary debug animal spawn with a separate simulation pipeline.
        private void InitializeAnimals()
        {
            // Debug-спавн животных вынесен в отдельный этап:
            // их симуляция опирается на уже собранный gameplay-runtime.
            _runtimeHandles.Gameplay.AnimalSimulationService?.SpawnDebugAnimals();

            // Как и для персонажей, скорость нужно выставить до первого тика их логики.
            AnimalActor.SetGlobalMovementSpeedMultiplier(_simulationSpeedMultiplier);
        }

        // Stage 6: wiring and tick loop startup.
        private void WireEventsAndStartLoop()
        {
            // Здесь связываем input-координатор, панели и hover/debug callbacks.
            // Делать это раньше нельзя: до этого момента часть зависимостей ещё не была собрана.
            InputRuntimeBinder.Bind(
                _runtimeHandles.World.GameInputRuntimeCoordinator,
                _runtimeHandles.Gameplay.ToolInputInteractionService,
                _runtimeHandles.Ui.ConstructionToolPanelController,
                _runtimeHandles.Gameplay.GridHoverHighlightService,
                _runtimeHandles.Gameplay.PowerBuildingOverlayService,
                _runtimeHandles.Gameplay.BuildModeVisualTintService,
                HandlePowerDebugCellHovered,
                HandlePowerDebugCellHoverExited);

            // Последняя защитная проверка перед запуском симуляции.
            // Если какой-то обязательный runtime-узел не поднялся, лучше не тикануть ни разу.
            if (!ValidateRuntimeReady())
            {
                return;
            }

            // Таймер тиков обнуляем только после успешной сборки runtime,
            // чтобы первый Update() стартовал из предсказуемого состояния.
            _simulationTickTimerSeconds = 0f;
        }


        /// <summary>
        /// Main simulation tick.
        /// Processing order:
        /// 1) increase tick counter,
        /// 2) simulate world,
        /// 3) synchronize tasks from the world,
        /// 4) clear expired reservations,
        /// 5) process unit AI,
        /// 6) update presenters.
        /// </summary>
        private void RunTick()
        {
            if (!IsRuntimeTickReady())
            {
                return;
            }
            if (_isSimulationPaused)
            {
                return;
            }

            WorldRuntimeContext world = _runtimeHandles.World;
            GameplayRuntimeContext gameplay = _runtimeHandles.Gameplay;
            float adjustedTickSeconds = _tickIntervalSeconds;
            List<BuildingRuntimeEntity> activeBuildings = null;
            if (gameplay.BuildingManager != null)
            {
                // Собираем снимок активных построек один раз в начале тика
                // и затем переиспользуем его в сетях/симуляциях вместо повторных обходов.
                gameplay.BuildingManager.FillActiveBuildings(_activeBuildingsTickBuffer);
                activeBuildings = _activeBuildingsTickBuffer;
            }

            // Новый тик начинается именно здесь:
            // дальше все сервисы читают уже обновлённый номер тика как "текущее время симуляции".
            _tickCounter++;

            // BuildingManager обновляет внутреннее состояние построек до остальных систем,
            // чтобы питание, вода и кислород считались по актуальному набору объектов.
            gameplay.BuildingManager?.Tick(_tickCounter);

            // Игровое время двигаем в начале тика:
            // зависящие от времени сервисы в этом же проходе должны видеть уже новое значение.
            world.GameTimeService.Tick(adjustedTickSeconds);

            // События колонии проверяем до симуляций ресурсов и AI,
            // чтобы они могли влиять на текущее состояние этого же тика.
            gameplay.ColonyEventService?.Tick();

            // Сначала пересчитываем топологию водяной сети,
            // потому что сама водная симуляция ниже опирается на актуальные WaterNetworkId.
            gameplay.WaterNetworkService?.Recalculate();
            if (activeBuildings != null && gameplay.PowerNetworkService != null)
            {
                // Перед расчётом питания пробрасываем актуальный набор активных построек,
                // чтобы сеть работала не по устаревшим runtime-сущностям.
                gameplay.PowerNetworkService.SyncActiveBuildings(activeBuildings);

                // Электросеть считается до применения состояний зданий:
                // результат расчёта определяет, какие объекты сейчас реально запитаны.
                gameplay.PowerNetworkService.Recalculate(world.GameTimeService, adjustedTickSeconds);

                // После расчёта сразу применяем питание к зданиям,
                // чтобы последующие системы читали уже корректный power-state.
                gameplay.BuildingManager.ApplyPowerStates(gameplay.PowerNetworkService);
            }
            if (activeBuildings != null && gameplay.WaterSimulationService != null)
            {
                // Водной симуляции также нужен свежий список активных зданий:
                // например, потребители и источники должны совпадать с текущим runtime-составом.
                gameplay.WaterSimulationService.SyncActiveBuildings(activeBuildings);

                // Сама вода считается после WaterNetworkService,
                // потому что расход/подача идут уже внутри найденных connected-компонент.
                gameplay.WaterSimulationService.Recalculate(adjustedTickSeconds);
            }

            // Аналогично воде, сначала обновляем связность кислородной сети,
            // а уже затем считаем распределение ресурса.
            gameplay.OxygenNetworkService?.Recalculate();
            if (activeBuildings != null && gameplay.OxygenSimulationService != null)
            {
                gameplay.OxygenSimulationService.SyncActiveBuildings(activeBuildings);
                gameplay.OxygenSimulationService.Recalculate(adjustedTickSeconds);
            }

            // Оверлей помечаем dirty после расчётов сетей и ресурсов:
            // к моменту рефреша у него уже есть финальные данные текущего тика.
            gameplay.PowerBuildingOverlayService?.MarkContentDirty();

            // Базовая world-сimulation идёт после сетевых систем:
            // ей нужен уже обновлённый grid/runtime-state этого тика.
            world.SimulationSystem.Tick(world.GridState);

            // Чистим протухшие резервы раньше AI,
            // чтобы юниты в этом же тике не опирались на давно потерянные брони задач.
            gameplay.GlobalTaskBoardService.ReleaseStaleReservations(_tickCounter, _taskReservationTimeoutTicks);

            // Основной AI-проход по юнитам.
            // К этому моменту мир, ресурсы и резервы уже приведены в консистентное состояние.
            gameplay.UnitTaskOrchestratorService.TickAll(adjustedTickSeconds, _tickCounter);

            // После выбора/выполнения задач обновляем needs-подсистему персонажей.
            gameplay.CharacterGroupService.Tick(adjustedTickSeconds);

            // Животные тикают после персонажей как отдельный симуляционный контур.
            gameplay.AnimalSimulationService?.TickAll(adjustedTickSeconds);
            gameplay.OfferSystemService?.Tick();
            gameplay.ShopSystemService?.Tick();
            gameplay.IronRocketArrivalService?.Tick();

            // UI-оверлей обновляем в конце тика,
            // когда все расчёты выше уже завершились и можно безопасно отрисовать итоговое состояние.
            gameplay.PowerBuildingOverlayService?.RefreshContent(
                gameplay.BuildingManager,
                gameplay.PowerNetworkService,
                gameplay.WaterSimulationService,
                gameplay.OxygenSimulationService);

            if (_isTaskQueueVisible)
            {
                _runtimeHandles.Ui.TaskQueueHudRefreshService?.Refresh();
            }

            if (_runtimeHandles.Ui.CharacterDiagnosticsPanelPresenter != null)
            {
                // Keep the top roster synced every tick even when the details panel is closed.
                _runtimeHandles.Ui.CharacterDiagnosticsPanelPresenter.Render(
                    gameplay.UnitTaskOrchestratorService.GetUnitDiagnosticsSnapshot());
            }

            _runtimeHandles.Ui.GameTimeHudPresenter?.Refresh(world.GameTimeService);
        }

        // UI command: fully stop simulation ticks.
        private void PauseSimulation()
        {
            _isSimulationPaused = true;
            _simulationTickTimerSeconds = 0f;
            ApplySimulationPauseState();
            RefreshSimulationControls();
        }

        // UI command: resume simulation ticks.
        private void ResumeSimulation()
        {
            _isSimulationPaused = false;
            _simulationTickTimerSeconds = 0f;
            ApplySimulationPauseState();
            RefreshSimulationControls();
        }

        // UI command: switch the simulation speed preset with a safe clamp.
        private void SetSimulationSpeed(float speedMultiplier)
        {
            _simulationSpeedMultiplier = Mathf.Clamp(speedMultiplier, 1f, _maxSimulationSpeedMultiplier);
            _isSimulationPaused = false;
            _simulationTickTimerSeconds = 0f;
            ApplySimulationPauseState();
            RefreshSimulationControls();
        }

        private void ApplySimulationPauseState()
        {
            float unityTimeScale = _isSimulationPaused
                ? 0f
                : _simulationSpeedMultiplier;
            float movementSpeedMultiplier = _isSimulationPaused
                ? 0f
                : _simulationSpeedMultiplier;

            Time.timeScale = unityTimeScale;
            Time.fixedDeltaTime = _defaultFixedDeltaTime
                * (_isSimulationPaused ? 1f : Mathf.Max(0.01f, unityTimeScale));

            CharacterActor.SetGlobalPauseState(_isSimulationPaused);
            CharacterActor.SetGlobalMovementSpeedMultiplier(movementSpeedMultiplier);
            AnimalActor.SetGlobalMovementSpeedMultiplier(movementSpeedMultiplier);
            _runtimeHandles?.Gameplay?.AnimalSimulationService?.SetPaused(_isSimulationPaused);
            _runtimeHandles?.Gameplay?.SceneResourceObjectService?.SetFallingPaused(_isSimulationPaused);
            _runtimeHandles?.Gameplay?.IronRocketArrivalService?.SetPaused(_isSimulationPaused);
        }

        private bool IsRuntimeTickReady()
        {
            return _runtimeHandles != null
                && _runtimeHandles.World != null
                && _runtimeHandles.Gameplay != null
                && _runtimeHandles.World.SimulationSystem != null
                && _runtimeHandles.World.GridState != null
                && _runtimeHandles.Gameplay.GlobalTaskBoardService != null
                && _runtimeHandles.Gameplay.UnitTaskOrchestratorService != null;
        }

        /// <summary>
        /// Per-frame update:
        /// - read mouse position,
        /// - update camera,
        /// - update world-to-grid position,
        /// - update hovered cell.
        /// </summary>
        private void Update()
        {
            if (_runtimeHandles == null)
            {
                return;
            }

            if (!_runtimeHandles.Ui.IsHudInitialized)
            {
                RebuildHudContext();
                EnsureTaskQueueRefreshService();
                ApplyTaskQueueVisibility();
            }

            if (IsHudTogglePressed())
            {
                ToggleHudVisibility();
            }

            if (IsTaskQueueTogglePressed())
            {
                ToggleTaskQueueVisibility();
            }

            if (IsPowerDebugTogglePressed())
            {
                TogglePowerDebugVisibility();
            }

            if (!_isSimulationPaused)
            {
                float simulationFrameDeltaTime = Time.unscaledDeltaTime * _simulationSpeedMultiplier;
                _runtimeHandles.Gameplay.UnitTaskOrchestratorService?.TickMovementFrame(simulationFrameDeltaTime);
                _runtimeHandles.Gameplay.AnimalSimulationService?.TickMovementFrame(simulationFrameDeltaTime);
                _runtimeHandles.Gameplay.SceneResourceObjectService?.TickFalling(simulationFrameDeltaTime);
            }
            bool shouldBlockHudZoom = IsWorldInputBlockedByHud();
            float blockedHudZoomSize = shouldBlockHudZoom && Camera.main != null
                ? Camera.main.orthographicSize
                : 0f;
            _runtimeHandles.World.GameInputRuntimeCoordinator?.Update(shouldBlockHudZoom);
            TryHandleAnimalWorldClick();
            RefreshAnimalDiagnosticsPanel();
            if (shouldBlockHudZoom && Camera.main != null)
            {
                // Shop/Offers may consume wheel scroll for UI; keep camera zoom unchanged while those windows are open.
                Camera.main.orthographicSize = blockedHudZoomSize;
            }

            _runtimeHandles.Gameplay.PowerBuildingOverlayService?.Refresh(
                _runtimeHandles.Gameplay.BuildingManager,
                _runtimeHandles.Gameplay.PowerNetworkService,
                _runtimeHandles.Gameplay.WaterSimulationService,
                _runtimeHandles.Gameplay.OxygenSimulationService);
            _runtimeHandles.Gameplay.ConstructionDigVisualCallbackService?.RefreshPowerWarnings(
                _runtimeHandles.Gameplay.BuildingManager,
                _runtimeHandles.Gameplay.PowerNetworkService);
            _runtimeHandles.Gameplay.ConstructionDigVisualCallbackService?.RefreshBatteryAnimations(
                _runtimeHandles.Gameplay.BuildingManager,
                _runtimeHandles.Gameplay.PowerNetworkService);
            _runtimeHandles.Gameplay.ConstructionDigVisualCallbackService?.RefreshWaterWarnings(
                _runtimeHandles.Gameplay.BuildingManager,
                _runtimeHandles.Gameplay.WaterSimulationService);
            _runtimeHandles.Gameplay.ConstructionDigVisualCallbackService?.RefreshOxygenWarnings(
                _runtimeHandles.Gameplay.BuildingManager,
                _runtimeHandles.Gameplay.OxygenSimulationService);
            _runtimeHandles.Gameplay.ConstructionDigVisualCallbackService?.RefreshLightPhaseVisuals(
                _runtimeHandles.World.GameTimeService);

            if (_isPowerDebugVisible)
            {
                if (IsWaterProducerTogglePressed())
                {
                    TryToggleHoveredWaterProducer();
                }

                _runtimeHandles.Ui.PowerDebugHudPresenter?.Render(
                    _bootstrapDebugFacade.HasHoveredPowerDebugCell,
                    _bootstrapDebugFacade.HoveredPowerDebugCell,
                    _runtimeHandles.World.GridState,
                    _runtimeHandles.Gameplay.BuildingManager,
                    _runtimeHandles.Gameplay.PowerNetworkService,
                    _runtimeHandles.Gameplay.WaterSimulationService,
                    _runtimeHandles.Gameplay.OxygenSimulationService);
            }

            if (_drawNavigationPathRuntime)
            {
                _bootstrapDebugFacade.DrawNavigationPathRuntime(
                    _runtimeHandles.Gameplay.CharacterNavigationService,
                    _runtimeHandles.World.GridCoordinateConverter);
            }

            ProcessSimulationTicks(Time.unscaledDeltaTime);
        }

        private void ToggleHudVisibility()
        {
            if (_runtimeHandles?.Ui?.HudRootElement == null)
            {
                return;
            }

            _isHudVisible = !_isHudVisible;
            _runtimeHandles.Ui.HudRootElement.style.display = _isHudVisible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static bool IsHudTogglePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame;
#else
            return UnityEngine.Input.GetKeyDown(KeyCode.F1);
#endif
        }

        private static bool IsTaskQueueTogglePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.oKey.wasPressedThisFrame;
#else
            return UnityEngine.Input.GetKeyDown(KeyCode.O);
#endif
        }

        private static bool IsPowerDebugTogglePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.iKey.wasPressedThisFrame;
#else
            return UnityEngine.Input.GetKeyDown(KeyCode.I);
#endif
        }

        private static bool IsWaterProducerTogglePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame;
#else
            return UnityEngine.Input.GetKeyDown(KeyCode.P);
#endif
        }

        private void TryToggleHoveredWaterProducer()
        {
            if (_runtimeHandles?.Gameplay?.BuildingManager == null || _runtimeHandles?.World?.GridState == null)
            {
                return;
            }

            if (!_bootstrapDebugFacade.HasHoveredPowerDebugCell)
            {
                return;
            }

            Vector2Int hoveredCell = _bootstrapDebugFacade.HoveredPowerDebugCell;
            if (!_runtimeHandles.World.GridState.IsInside(hoveredCell.x, hoveredCell.y))
            {
                return;
            }

            if (!_runtimeHandles.Gameplay.BuildingManager.TryGetActiveBuildingByCell(hoveredCell, out BuildingRuntimeEntity building) || building == null)
            {
                return;
            }

            _runtimeHandles.Gameplay.BuildingManager.TryToggleWaterProducer(building.AnchorCell, out _);
        }

        private void ToggleTaskQueueVisibility()
        {
            _isTaskQueueVisible = !_isTaskQueueVisible;
            if (_isTaskQueueVisible)
            {
                _runtimeHandles?.Ui?.HudWindowCoordinator?.CloseAll(TaskQueuePanelPresenter.WINDOW_ID);
            }

            ApplyTaskQueueVisibility();
            if (_isTaskQueueVisible)
            {
                _runtimeHandles?.Ui?.TaskQueueHudRefreshService?.Refresh();
            }
        }

        private void ApplyTaskQueueVisibility()
        {
            _runtimeHandles?.Ui?.TaskQueuePanelPresenter?.SetVisible(_isTaskQueueVisible);
        }

        private void TogglePowerDebugVisibility()
        {
            _isPowerDebugVisible = !_isPowerDebugVisible;
            _runtimeHandles?.Ui?.PowerDebugHudPresenter?.SetVisible(_isPowerDebugVisible);
        }

        private void RebuildHudContext()
        {
            if (_runtimeHandles == null || _runtimeHandles.World == null || _runtimeHandles.Gameplay == null)
            {
                return;
            }

            ConstructionToolPanelController previousConstructionToolPanelController = _runtimeHandles.Ui != null
                ? _runtimeHandles.Ui.ConstructionToolPanelController
                : null;
            ColonyEventHudPresenter previousColonyEventHudPresenter = _runtimeHandles.Ui != null
                ? _runtimeHandles.Ui.ColonyEventHudPresenter
                : null;

            previousColonyEventHudPresenter?.Dispose();

            _runtimeHandles.Ui = UiRuntimeComposer.Compose(
                _uiDocument,
                _constructionToolDefinitions,
                _hudMenuIconSet,
                _runtimeHandles.Gameplay.ResourceInventoryService,
                _runtimeHandles.Gameplay.SceneResourceObjectService,
                _runtimeHandles.Gameplay.ColonyEventService,
                _runtimeHandles.Gameplay.OfferSystemService,
                _runtimeHandles.Gameplay.ShopSystemService,
                _enableAiLogs,
                _isPowerDebugVisible,
                _runtimeHandles.Gameplay.PowerBuildingOverlayService,
                _runtimeHandles.Gameplay.UnitNeedPolicy,
                _runtimeHandles.World.GameTimeService,
                _runtimeHandles.Gameplay.IronRocketArrivalService != null ? _runtimeHandles.Gameplay.IronRocketArrivalService.MissionCount : 0,
                PauseSimulation,
                ResumeSimulation,
                SetSimulationSpeed);
            RebindToolSelectionHandler(previousConstructionToolPanelController, _runtimeHandles.Ui.ConstructionToolPanelController);
            EnsureHudRootVisible();
        }

        private void EnsureHudDocumentEnabled()
        {
            if (_uiDocument == null)
            {
                return;
            }

            // Keep HUD document active even if bootstrap fails before UI composition stage.
            if (!_uiDocument.enabled)
            {
                _uiDocument.enabled = true;
            }
        }

        private void EnsureHudRootVisible()
        {
            if (_uiDocument == null)
            {
                return;
            }

            VisualElement root = _uiDocument.rootVisualElement;
            if (root == null)
            {
                return;
            }

            root.style.display = DisplayStyle.Flex;
            VisualElement hudRoot = root.Q<VisualElement>("hud-root");
            if (hudRoot != null)
            {
                hudRoot.style.display = DisplayStyle.Flex;
            }
        }

        private void RebindToolSelectionHandler(
            ConstructionToolPanelController previousController,
            ConstructionToolPanelController currentController)
        {
            ToolInputInteractionService toolInputInteractionService = _runtimeHandles?.Gameplay?.ToolInputInteractionService;
            if (toolInputInteractionService == null)
            {
                return;
            }

            if (previousController != null)
            {
                previousController.ToolSelectionChanged -= toolInputInteractionService.HandleToolSelectionChanged;
            }

            if (currentController != null)
            {
                currentController.ToolSelectionChanged -= toolInputInteractionService.HandleToolSelectionChanged;
                currentController.ToolSelectionChanged += toolInputInteractionService.HandleToolSelectionChanged;
            }
        }

        private void OnDrawGizmos()
        {
            if (_drawNavigationPathGizmos)
            {
                _bootstrapDebugFacade?.DrawNavigationPathGizmos(_runtimeHandles?.Gameplay?.CharacterNavigationService, _runtimeHandles?.World?.GridCoordinateConverter);
            }

            if (_drawNavigationWalkabilityGizmos)
            {
                _bootstrapDebugFacade?.DrawNavigationWalkabilityGizmos(
                    _runtimeHandles?.Gameplay?.CharacterNavigationService,
                    _runtimeHandles?.World?.GridCoordinateConverter,
                    _runtimeHandles?.World?.GridState,
                    _navigationWalkabilityGizmoMarginCells);
            }

            DrawWaterMaskIndexGizmos();
            DrawCableTileIndexGizmos();
            DrawShipLandingZoneDebugGizmos();
            DrawShipDigProtectionDebugGizmos();
        }

        /// <summary>
        /// Unsubscribes from events and stops the tick loop when this object is destroyed.
        /// </summary>
        private void OnDestroy()
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = _defaultFixedDeltaTime > 0f ? _defaultFixedDeltaTime : 0.02f;
            PersistOfferSystemState();
            DisposeRuntimeBindings();
            DisposeRuntimeServices();
        }

        private void DisposeRuntimeBindings()
        {
            if (_runtimeHandles?.World?.GridState != null)
            {
                _runtimeHandles.World.GridState.CellChanged -= OnGridCellChanged;
            }

            InputRuntimeBinder.Unbind(
                _runtimeHandles?.World?.GameInputRuntimeCoordinator,
                _runtimeHandles?.Gameplay?.ToolInputInteractionService,
                _runtimeHandles?.Ui?.ConstructionToolPanelController,
                _runtimeHandles?.Gameplay?.GridHoverHighlightService,
                _runtimeHandles?.Gameplay?.PowerBuildingOverlayService,
                _runtimeHandles?.Gameplay?.BuildModeVisualTintService,
                HandlePowerDebugCellHovered,
                HandlePowerDebugCellHoverExited);

            _runtimeHandles?.World?.GameInputRuntimeCoordinator?.Dispose();
            _runtimeHandles?.Ui?.ConstructionToolPanelController?.Unbind();
        }

        private bool IsWorldInputBlockedByHud()
        {
            return _runtimeHandles?.Ui?.HudWindowCoordinator != null
                && _runtimeHandles.Ui.HudWindowCoordinator.HasBlockingWindowOpen;
        }

        private void TryHandleAnimalWorldClick()
        {
#if ENABLE_INPUT_SYSTEM
            if (IsWorldInputBlockedByHud())
            {
                return;
            }

            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame || Camera.main == null)
            {
                return;
            }

            if (_runtimeHandles?.Gameplay?.ToolInputInteractionService != null
                && _runtimeHandles.Gameplay.ToolInputInteractionService.CurrentToolMode != ToolMode.None)
            {
                return;
            }

            AnimalWorldSelectionService animalWorldSelectionService = _runtimeHandles?.Gameplay?.AnimalWorldSelectionService;
            AnimalDiagnosticsPanelPresenter animalDiagnosticsPanelPresenter = _runtimeHandles?.Ui?.AnimalDiagnosticsPanelPresenter;
            HudWindowCoordinator hudWindowCoordinator = _runtimeHandles?.Ui?.HudWindowCoordinator;
            if (animalWorldSelectionService == null || animalDiagnosticsPanelPresenter == null || hudWindowCoordinator == null)
            {
                return;
            }

            Vector2 mouseScreen = Mouse.current.position.ReadValue();
            Vector3 mouseWorld3 = Camera.main.ScreenToWorldPoint(new Vector3(mouseScreen.x, mouseScreen.y, 0f));
            Vector2 mouseWorld = new Vector2(mouseWorld3.x, mouseWorld3.y);
            if (!animalWorldSelectionService.TryHandleWorldClick(mouseWorld, out AnimalDiagnosticsSnapshot snapshot))
            {
                return;
            }

            hudWindowCoordinator.CloseAll(AnimalDiagnosticsPanelPresenter.WINDOW_ID);
            animalDiagnosticsPanelPresenter.SetVisible(true);
            animalDiagnosticsPanelPresenter.Render(snapshot);
#endif
        }

        private void RefreshAnimalDiagnosticsPanel()
        {
            AnimalDiagnosticsPanelPresenter animalDiagnosticsPanelPresenter = _runtimeHandles?.Ui?.AnimalDiagnosticsPanelPresenter;
            AnimalWorldSelectionService animalWorldSelectionService = _runtimeHandles?.Gameplay?.AnimalWorldSelectionService;
            if (animalDiagnosticsPanelPresenter == null || animalWorldSelectionService == null || !animalDiagnosticsPanelPresenter.IsVisible)
            {
                return;
            }

            if (!animalWorldSelectionService.TryGetSelectedSnapshot(out AnimalDiagnosticsSnapshot snapshot))
            {
                animalDiagnosticsPanelPresenter.SetVisible(false);
                return;
            }

            animalDiagnosticsPanelPresenter.Render(snapshot);
        }

        private void DisposeRuntimeServices()
        {
            _runtimeHandles?.Ui?.ResourceInventoryPanelPresenter?.Dispose();
            _runtimeHandles?.Ui?.ColonyEventHudPresenter?.Dispose();
            _runtimeHandles?.Ui?.OfferPanelPresenter?.Dispose();
            _runtimeHandles?.Ui?.ShopPanelPresenter?.Dispose();
            if (_runtimeHandles?.Gameplay?.IronRocketArrivalService != null)
            {
                _runtimeHandles.Gameplay.IronRocketArrivalService.MissionResolved -= OnRocketMissionResolved;
                _runtimeHandles.Gameplay.IronRocketArrivalService.Dispose();
            }

            _runtimeHandles?.Gameplay?.OfferSystemService?.Dispose();

            _runtimeHandles?.Gameplay?.GridHoverHighlightService?.Dispose();
        }

        private void OnRocketMissionResolved(IronRocketArrivalService.RocketMissionResult missionResult)
        {
            if (missionResult.IsSuccess)
            {
                _runtimeHandles?.Ui?.GameTimeHudPresenter?.RefreshRocketMissionCount(missionResult.MissionCount);
                _runtimeHandles?.Gameplay?.OfferSystemService?.ResolveOffersOnMissionArrived(missionResult.MissionCount);
            }

            _runtimeHandles?.Gameplay?.ShopSystemService?.OnRocketMissionResolved(missionResult);
            _runtimeHandles?.Ui?.ResourceInventoryPanelPresenter?.Render();
            _runtimeHandles?.Ui?.ShopPanelPresenter?.Render();
        }

        private bool ValidateRuntimeReady()
        {
            if (!IsRuntimeTickReady())
            {
                Debug.LogError("[GameBootstrap] Runtime initialization failed. Tick loop is disabled.");
                return false;
            }

            return true;
        }

        private void EnsureTaskQueueRefreshService()
        {
            if (_runtimeHandles?.Ui == null || _runtimeHandles?.Gameplay == null)
            {
                return;
            }

            if (_runtimeHandles.Ui.TaskQueueHudRefreshService != null)
            {
                return;
            }

            if (_runtimeHandles.Ui.TaskQueuePanelPresenter == null || _runtimeHandles.Gameplay.TaskQueueHudBuilder == null)
            {
                return;
            }

            _runtimeHandles.Ui.TaskQueueHudRefreshService = new TaskQueueHudRefreshService(
                _runtimeHandles.Ui.TaskQueuePanelPresenter,
                _runtimeHandles.Gameplay.TaskQueueHudBuilder);
            _runtimeHandles.Ui.TaskQueueHudRefreshService.Refresh();
        }

        private void SyncLegacyWorldFields(WorldRuntimeContext world)
        {
            _gridState = world.GridState;
            _gridCoordinateConverter = world.GridCoordinateConverter;
            _gridTileVisualService = world.GridTileVisualService;
            _fogMaskService = world.FogMaskService;
            _fogOverlayVisualService = world.FogOverlayRenderer;
            _materialTransitionOverlayService = world.MaterialTransitionOverlayService;
        }

        private void SyncLegacyGameplayFields(GameplayRuntimeContext gameplay)
        {
            _buildingManager = gameplay.BuildingManager;
            _unitNeedPolicy = gameplay.UnitNeedPolicy;
            _unitTaskOrchestratorService = gameplay.UnitTaskOrchestratorService;
        }

        // Pushes runtime simulation state back into the HUD after UI commands and startup wiring.
        private void RefreshSimulationControls()
        {
            _runtimeHandles?.Ui?.GameTimeHudPresenter?.RefreshControls(_isSimulationPaused, _simulationSpeedMultiplier);
        }

        // Converts the selected speed preset into the real cadence of simulation ticks.
        private void ProcessSimulationTicks(float deltaTime)
        {
            if (_isSimulationPaused || !IsRuntimeTickReady())
            {
                return;
            }

            // Интервал тика зависит от множителя скорости:
            // чем выше скорость, тем чаще вызывается RunTick().
            float tickInterval = _tickIntervalSeconds / Mathf.Max(1f, _simulationSpeedMultiplier);

            // Накапливаем реальное время кадра, а не сразу тикаем каждый Update,
            // чтобы симуляция жила в собственном ритме, независимом от FPS.
            _simulationTickTimerSeconds += deltaTime;

            // Prevent a single long frame from producing an unbounded catch-up burst.
            int safetyTicksRemaining = 8;
            while (_simulationTickTimerSeconds >= tickInterval && safetyTicksRemaining > 0)
            {
                // Вычитаем один интервал и исполняем ровно один полноценный симуляционный тик.
                _simulationTickTimerSeconds -= tickInterval;
                RunTick();
                safetyTicksRemaining--;
            }

            if (safetyTicksRemaining == 0)
            {
                // Если кадр был слишком длинным, обрезаем хвост догоняющих тиков,
                // иначе игра может провалиться в бесконечное "догоняние" и ещё сильнее лагать.
                _simulationTickTimerSeconds = 0f;
            }
        }

        /// <summary>
        /// Repaints debug tiles for occupied cells (reserved/planned/built).
        /// </summary>
        private void RefreshReservedDebugOverlay()
        {
            _gridTileVisualService?.RenderReservedDebugOverlay(_gridState, IsCellReservedOrBuilt);
        }

        private void OnGridCellChanged(Vector2Int cell, Cell previousCell, Cell currentCell)
        {
            if (previousCell.Type != currentCell.Type)
            {
                _materialTransitionOverlayService?.RefreshArea(cell, LocalRefreshRadius);
                _fogMaskService?.SyncCellTypeFog(cell);
                _fogOverlayVisualService?.ApplyDelta(_fogMaskService != null ? _fogMaskService.ConsumeDirtyCells() : null, _fogMaskService);
            }
        }

        private void OnUnitCellChanged(Vector2Int cell)
        {
            if (_fogMaskService == null || _fogOverlayVisualService == null)
            {
                return;
            }

            _fogMaskService.RevealFrom(cell);
            _fogOverlayVisualService.ApplyDelta(_fogMaskService.ConsumeDirtyCells(), _fogMaskService);
        }

        private void HandlePowerDebugCellHovered(Vector2Int cell)
        {
            _bootstrapDebugFacade?.HandlePowerDebugCellHovered(cell);
        }

        private void HandlePowerDebugCellHoverExited()
        {
            _bootstrapDebugFacade?.HandlePowerDebugCellHoverExited();
        }

        private void RevealFromAllUnits()
        {
            if (_unitTaskOrchestratorService == null || _fogMaskService == null || _fogOverlayVisualService == null)
            {
                return;
            }

            List<Vector2Int> unitCells = _unitTaskOrchestratorService.GetUnitCellsSnapshot();
            for (int i = 0; i < unitCells.Count; i++)
            {
                _fogMaskService.RevealFrom(unitCells[i]);
                _fogOverlayVisualService.ApplyDelta(_fogMaskService.ConsumeDirtyCells(), _fogMaskService);
            }
        }

        /// <summary>
        /// Returns true when a cell is occupied by construction or already built.
        /// </summary>
        private bool IsCellReservedOrBuilt(Vector2Int cell)
        {
            Cell cellData = _gridState.GetCell(cell.x, cell.y);
            if (cellData.ReservedByUnitId != 0) return true;
            if (cellData.IsOccupiedByBuilding) return true;
            if (cellData.BuildObjectType.HasValue) return true;
            if (_buildingManager != null && _buildingManager.IsPlannedCell(cell)) return true;
            return false;
        }

        private void RestoreOfferSystemState()
        {
            // Offer system state restore is intentionally disabled in this prototype branch.
            // OfferSystemService currently does not restore state from PlayerPrefs here.
        }

        private void PersistOfferSystemState()
        {
            // Offer system state persistence is intentionally disabled in this prototype branch.
            // OfferSystemService currently does not save state into PlayerPrefs here.
        }

        private void DrawWaterMaskIndexGizmos()
        {
#if UNITY_EDITOR
            if (!_drawWaterMaskIndexGizmos)
            {
                return;
            }

            if (_gridState == null || _gridCoordinateConverter == null)
            {
                return;
            }

            Camera sceneCamera = Camera.current != null ? Camera.current : Camera.main;
            if (sceneCamera == null)
            {
                return;
            }

            Vector2Int centerCell = _gridCoordinateConverter.WorldToCell(sceneCamera.transform.position);
            int radius = Mathf.Max(1, _waterMaskIndexGizmoRadiusInCells);
            int minX = Mathf.Max(0, centerCell.x - radius);
            int maxX = Mathf.Min(_gridState.Width - 1, centerCell.x + radius);
            int minY = Mathf.Max(0, centerCell.y - radius);
            int maxY = Mathf.Min(_gridState.Height - 1, centerCell.y + radius);

            Color previousColor = Handles.color;
            Handles.color = new Color(0.2f, 0.95f, 1f, 0.95f);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    Cell cell = _gridState.GetCell(x, y);
                    if (!cell.HasWater && !cell.IsWaterPreviewVisible)
                    {
                        continue;
                    }

                    int waterMaskIndex = cell.IsWaterPreviewVisible
                        ? (cell.WaterPreviewMask4 & 0x0F)
                        : (cell.WaterMask4 & 0x0F);
                    Vector2 cellCenter = _gridCoordinateConverter.CellToWorldCenter(new Vector2Int(x, y));
                    Vector3 labelPosition = new Vector3(cellCenter.x, cellCenter.y + 0.15f, 0f);
                    Handles.Label(labelPosition, waterMaskIndex.ToString());
                }
            }

            Handles.color = previousColor;
#endif
        }

        private void DrawCableTileIndexGizmos()
        {
#if UNITY_EDITOR
            if (!_drawCableTileIndexGizmos)
            {
                return;
            }

            if (_gridState == null || _gridCoordinateConverter == null)
            {
                return;
            }

            Camera sceneCamera = Camera.current != null ? Camera.current : Camera.main;
            if (sceneCamera == null)
            {
                return;
            }

            Vector2Int centerCell = _gridCoordinateConverter.WorldToCell(sceneCamera.transform.position);
            int radius = Mathf.Max(1, _cableTileIndexGizmoRadiusInCells);
            int minX = Mathf.Max(0, centerCell.x - radius);
            int maxX = Mathf.Min(_gridState.Width - 1, centerCell.x + radius);
            int minY = Mathf.Max(0, centerCell.y - radius);
            int maxY = Mathf.Min(_gridState.Height - 1, centerCell.y + radius);

            Color previousColor = Handles.color;
            Handles.color = new Color(1f, 0.8f, 0.2f, 0.95f);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    Cell cell = _gridState.GetCell(x, y);
                    if (!cell.HasCable && !cell.IsCablePreviewVisible)
                    {
                        continue;
                    }

                    // Show tile index by mask4 (0..15), matching Cable*TilesByMask4 arrays.
                    int tileIndex = cell.IsCablePreviewVisible
                        ? (cell.CablePreviewMask4 & 0x0F)
                        : (cell.CableMask4 & 0x0F);
                    Vector2 cellCenter = _gridCoordinateConverter.CellToWorldCenter(new Vector2Int(x, y));
                    Vector3 labelPosition = new Vector3(cellCenter.x, cellCenter.y + 0.32f, 0f);
                    Handles.Label(labelPosition, tileIndex.ToString());
                }
            }

            Handles.color = previousColor;
#endif
        }

        private void DrawShipLandingZoneDebugGizmos()
        {
#if UNITY_EDITOR
            if (!_drawShipLandingZoneDebugGizmos)
            {
                return;
            }

            if (_gridState == null || _gridCoordinateConverter == null)
            {
                return;
            }

            Color previousColor = Handles.color;
            Handles.color = new Color(1f, 0.25f, 0.25f, 0.95f);

            ShipLandingZoneRules.GetBounds(_gridState.Width, _gridState.Height, out int zoneMinX, out int zoneMaxX, out int zoneMinY, out int zoneMaxY);
            int minX = Mathf.Max(0, zoneMinX);
            int maxX = Mathf.Min(_gridState.Width - 1, zoneMaxX);
            int minY = Mathf.Max(0, zoneMinY);
            int maxY = Mathf.Min(_gridState.Height - 1, zoneMaxY);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (!ShipLandingZoneRules.IsInsideLandingZone(_gridState.Width, _gridState.Height, x, y))
                    {
                        continue;
                    }

                    Vector2 cellCenter = _gridCoordinateConverter.CellToWorldCenter(new Vector2Int(x, y));
                    Vector3 labelPosition = new Vector3(cellCenter.x, cellCenter.y + 0.48f, 0f);
                    Handles.Label(labelPosition, "1");
                }
            }

            Handles.color = previousColor;
#endif
        }

        private void DrawShipDigProtectionDebugGizmos()
        {
#if UNITY_EDITOR
            if (!_drawShipDigProtectionDebugGizmos)
            {
                return;
            }

            if (_gridState == null || _gridCoordinateConverter == null)
            {
                return;
            }

            Color previousColor = Handles.color;
            Handles.color = new Color(1f, 0.95f, 0.2f, 0.95f);

            ShipLandingZoneRules.GetDigProtectionBounds(_gridState.Width, _gridState.Height, out int zoneMinX, out int zoneMaxX, out int zoneMinY, out int zoneMaxY);
            int minX = Mathf.Max(0, zoneMinX);
            int maxX = Mathf.Min(_gridState.Width - 1, zoneMaxX);
            int minY = Mathf.Max(0, zoneMinY);
            int maxY = Mathf.Min(_gridState.Height - 1, zoneMaxY);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (!ShipLandingZoneRules.IsInsideDigProtectionZone(_gridState.Width, _gridState.Height, x, y))
                    {
                        continue;
                    }

                    Vector2 cellCenter = _gridCoordinateConverter.CellToWorldCenter(new Vector2Int(x, y));
                    Vector3 labelPosition = new Vector3(cellCenter.x, cellCenter.y + 0.18f, 0f);
                    Handles.Label(labelPosition, "2");
                }
            }

            Handles.color = previousColor;
#endif
        }
    }
}
