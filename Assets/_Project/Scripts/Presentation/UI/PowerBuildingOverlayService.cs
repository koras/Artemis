using System.Collections.Generic;
using _Project.Scripts.Data.Construction;
using _Project.Scripts.Data.Grid;
using _Project.Scripts.Data.Oxygen;
using _Project.Scripts.Data.Power;
using _Project.Scripts.Data.Water;
using _Project.Scripts.Input;
using _Project.Scripts.Systems.Construction;
using _Project.Scripts.Systems.Grid;
using _Project.Scripts.Systems.Oxygen;
using _Project.Scripts.Systems.Power;
using _Project.Scripts.Systems.Simulation;
using _Project.Scripts.Systems.Water;
using UnityEngine;
using UnityEngine.UIElements;

namespace _Project.Scripts.Presentation.UI
{
    /// <summary>
    /// Shows power/water/oxygen overlay labels above buildings in overlay-capable tool modes.
    /// </summary>
    public sealed class PowerBuildingOverlayService
    {
        private enum OverlayVisualState
        {
            Ok = 0,
            Fail = 1
        }

        private readonly struct OverlayStateSnapshot
        {
            public readonly string LabelText;
            public readonly bool ShowNoPowerIcon;
            public readonly bool IsVisibleForBuilding;
            public readonly OverlayVisualState VisualState;

            public OverlayStateSnapshot(
                string labelText,
                bool showNoPowerIcon,
                bool isVisibleForBuilding,
                OverlayVisualState visualState)
            {
                LabelText = labelText;
                ShowNoPowerIcon = showNoPowerIcon;
                IsVisibleForBuilding = isVisibleForBuilding;
                VisualState = visualState;
            }

            public bool Equals(OverlayStateSnapshot other)
            {
                return LabelText == other.LabelText
                       && ShowNoPowerIcon == other.ShowNoPowerIcon
                       && IsVisibleForBuilding == other.IsVisibleForBuilding
                       && VisualState == other.VisualState;
            }
        }

        private sealed class OverlayView
        {
            public Vector2Int Anchor;
            public Vector2Int Size;
            public VisualElement Root;
            public Label TextLabel;
            public VisualElement NoPowerIcon;
            public float LastFontSize = -1f;
            public float LastIconSize = -1f;
            public float LastIconMarginTop = -1f;
        }

        private readonly Dictionary<Vector2Int, OverlayView> _viewsByAnchor = new Dictionary<Vector2Int, OverlayView>();
        private readonly Dictionary<Vector2Int, OverlayStateSnapshot> _stateByAnchor = new Dictionary<Vector2Int, OverlayStateSnapshot>();
        private readonly List<BuildingRuntimeEntity> _activeBuildingsBuffer = new List<BuildingRuntimeEntity>();
        private readonly HashSet<Vector2Int> _aliveAnchorsBuffer = new HashSet<Vector2Int>();
        private readonly List<Vector2Int> _anchorsToRemoveBuffer = new List<Vector2Int>();
        private readonly Dictionary<int, float> _generationByComponentBuffer = new Dictionary<int, float>();
        private readonly GridCoordinateConverter _gridCoordinateConverter;
        private readonly GridState _gridState;
        private readonly GameTimeService _gameTimeService;
        private readonly SolarPowerProductionService _solarPowerProductionService;
        private readonly Camera _worldCamera;
        private readonly float _cellSize;
        private readonly Texture2D _noPowerIconTexture;
        private readonly StyleBackground _noPowerIconBackground;
        private readonly VisualTreeAsset _overlayItemTemplate;

        private VisualElement _hudRoot;
        private VisualElement _overlayLayer;
        private ToolMode _currentToolMode = ToolMode.None;
        private bool _isContentDirty = true;
        private bool _isOverlayLayerVisible = true;

        public PowerBuildingOverlayService(
            VisualElement hudRoot,
            GridCoordinateConverter gridCoordinateConverter,
            GridState gridState,
            GameTimeService gameTimeService,
            Camera worldCamera,
            float cellSize,
            Texture2D noPowerIconTexture)
        {
            _hudRoot = hudRoot;
            _gridCoordinateConverter = gridCoordinateConverter;
            _gridState = gridState;
            _gameTimeService = gameTimeService;
            _solarPowerProductionService = new SolarPowerProductionService();
            _worldCamera = worldCamera;
            _cellSize = Mathf.Max(0.001f, cellSize);
            _noPowerIconTexture = noPowerIconTexture;
            _noPowerIconBackground = noPowerIconTexture != null ? new StyleBackground(noPowerIconTexture) : default;
            _overlayItemTemplate = Resources.Load<VisualTreeAsset>("UI/PowerBuildingOverlayItem");
            EnsureOverlayLayer();
        }

        public void SetHudRoot(VisualElement hudRoot)
        {
            if (ReferenceEquals(_hudRoot, hudRoot))
            {
                return;
            }

            _hudRoot = hudRoot;
            _overlayLayer = null;
            _viewsByAnchor.Clear();
            _stateByAnchor.Clear();
            _isOverlayLayerVisible = true;
            MarkContentDirty();
            EnsureOverlayLayer();
        }

        public void HandleToolModeChanged(ToolMode toolMode)
        {
            _currentToolMode = toolMode;
            if (!IsOverlayToolMode(_currentToolMode))
            {
                SetAllVisible(false);
                return;
            }

            MarkContentDirty();
            SetAllVisible(true);
        }

        public void MarkContentDirty()
        {
            _isContentDirty = true;
        }

        public void Refresh(
            BuildingManager buildingManager,
            PowerNetworkService powerNetworkService,
            WaterSimulationService waterSimulationService,
            OxygenSimulationService oxygenSimulationService)
        {
            if (_isContentDirty)
            {
                RefreshContent(buildingManager, powerNetworkService, waterSimulationService, oxygenSimulationService);
            }

            RefreshLayout();
        }

        public void RefreshContent(
            BuildingManager buildingManager,
            PowerNetworkService powerNetworkService,
            WaterSimulationService waterSimulationService,
            OxygenSimulationService oxygenSimulationService)
        {
            EnsureOverlayLayer();
            if (_overlayLayer == null)
            {
                return;
            }

            if (buildingManager == null || !IsOverlayToolMode(_currentToolMode))
            {
                _isContentDirty = false;
                SetAllVisible(false);
                return;
            }

            SetAllVisible(true);
            buildingManager.FillActiveBuildings(_activeBuildingsBuffer);
            _aliveAnchorsBuffer.Clear();
            RebuildGenerationByComponent(_activeBuildingsBuffer);

            for (int i = 0; i < _activeBuildingsBuffer.Count; i++)
            {
                BuildingRuntimeEntity entity = _activeBuildingsBuffer[i];
                if (entity == null || entity.BuildingDef == null)
                {
                    continue;
                }

                Vector2Int anchor = entity.AnchorCell;
                _aliveAnchorsBuffer.Add(anchor);

                if (!ShouldShowForBuilding(entity.BuildingDef))
                {
                    Hide(anchor);
                    _stateByAnchor.Remove(anchor);
                    continue;
                }

                OverlayView view = GetOrCreate(anchor);
                view.Anchor = anchor;
                view.Size = entity.Size;

                OverlayStateSnapshot nextSnapshot = BuildOverlayStateSnapshot(
                    entity,
                    powerNetworkService,
                    waterSimulationService,
                    oxygenSimulationService);

                if (!_stateByAnchor.TryGetValue(anchor, out OverlayStateSnapshot currentSnapshot)
                    || !nextSnapshot.Equals(currentSnapshot))
                {
                    ApplyContentState(view, nextSnapshot);
                    _stateByAnchor[anchor] = nextSnapshot;
                }
            }

            _anchorsToRemoveBuffer.Clear();
            foreach (KeyValuePair<Vector2Int, OverlayView> pair in _viewsByAnchor)
            {
                if (_aliveAnchorsBuffer.Contains(pair.Key))
                {
                    continue;
                }

                pair.Value?.Root?.RemoveFromHierarchy();
                _anchorsToRemoveBuffer.Add(pair.Key);
            }

            for (int i = 0; i < _anchorsToRemoveBuffer.Count; i++)
            {
                Vector2Int anchor = _anchorsToRemoveBuffer[i];
                _viewsByAnchor.Remove(anchor);
                _stateByAnchor.Remove(anchor);
            }

            _isContentDirty = false;
        }

        public void RefreshLayout()
        {
            EnsureOverlayLayer();
            if (_overlayLayer == null)
            {
                return;
            }

            if (!IsOverlayToolMode(_currentToolMode))
            {
                SetAllVisible(false);
                return;
            }

            SetAllVisible(true);
            foreach (KeyValuePair<Vector2Int, OverlayView> pair in _viewsByAnchor)
            {
                if (!_stateByAnchor.TryGetValue(pair.Key, out OverlayStateSnapshot snapshot)
                    || !snapshot.IsVisibleForBuilding)
                {
                    continue;
                }

                UpdateLayout(pair.Value);
            }
        }

        private static bool IsOverlayToolMode(ToolMode mode)
        {
            return mode == ToolMode.BuildStorage
                   || mode == ToolMode.BuildSolarPanel
                   || mode == ToolMode.BuildRegolithProcessingUnit
                   || mode == ToolMode.BuildSleepModule
                   || mode == ToolMode.BuildBattery
                   || mode == ToolMode.BuildDinner
                   || mode == ToolMode.BuildOxygenStorage
                   || mode == ToolMode.BuildOxigenProcessingUnit
                   || mode == ToolMode.BuildWaterReclamation
                   || mode == ToolMode.BuildWaterProcessingUnit
                   || mode == ToolMode.BuildCable
                   || mode == ToolMode.CancelCablePlan
                   || mode == ToolMode.ExitCablePlan
                   || mode == ToolMode.BuildWater
                   || mode == ToolMode.CancelWaterPlan
                   || mode == ToolMode.ExitWaterPlan
                   || mode == ToolMode.BuildOxygen
                   || mode == ToolMode.CancelOxygenPlan
                   || mode == ToolMode.ExitOxygenPlan;
        }

        private bool ShouldShowForBuilding(BuildingDef buildingDef)
        {
            if (buildingDef == null) return false;
            bool usesPower = buildingDef.UsesPowerNetwork
                             && (buildingDef.RequiresPower
                                 || buildingDef.PowerGenerationKwDay > 0f
                                 || buildingDef.BatteryCapacityKwh > 0f
                                 || buildingDef.ObjectType == BuildObjectType.ElectricBattery);
            bool usesWater = buildingDef.UsesWaterNetwork && buildingDef.WaterRole != WaterRole.None;
            bool usesOxygen = buildingDef.UsesOxygenNetwork && buildingDef.OxygenRole != OxygenRole.None;
            return usesPower || usesWater || usesOxygen;
        }

        private OverlayView GetOrCreate(Vector2Int anchor)
        {
            if (_viewsByAnchor.TryGetValue(anchor, out OverlayView existing) && existing != null && existing.Root != null)
            {
                return existing;
            }

            VisualElement root;
            Label text;
            VisualElement icon;
            if (_overlayItemTemplate != null)
            {
                TemplateContainer itemTree = _overlayItemTemplate.CloneTree();
                root = itemTree.Q<VisualElement>("power-overlay-item");
                text = itemTree.Q<Label>("power-overlay-item-label");
                icon = itemTree.Q<VisualElement>("power-overlay-item-icon");
                if (root != null && text != null && icon != null)
                {
                    root.name = $"power-overlay-{anchor.x}-{anchor.y}";
                    _overlayLayer.Add(itemTree);
                }
                else
                {
                    root = CreateFallbackRoot(anchor);
                    text = CreateFallbackLabel();
                    icon = CreateFallbackIcon();
                    root.Add(text);
                    root.Add(icon);
                    _overlayLayer.Add(root);
                }
            }
            else
            {
                root = CreateFallbackRoot(anchor);
                text = CreateFallbackLabel();
                icon = CreateFallbackIcon();
                root.Add(text);
                root.Add(icon);
                _overlayLayer.Add(root);
            }

            icon.style.backgroundImage = _noPowerIconBackground;
            text.style.unityFontStyleAndWeight = FontStyle.Bold;

            var created = new OverlayView
            {
                Anchor = anchor,
                Root = root,
                TextLabel = text,
                NoPowerIcon = icon
            };

            _viewsByAnchor[anchor] = created;
            return created;
        }

        private OverlayStateSnapshot BuildOverlayStateSnapshot(
            BuildingRuntimeEntity entity,
            PowerNetworkService powerNetworkService,
            WaterSimulationService waterSimulationService,
            OxygenSimulationService oxygenSimulationService)
        {
            BuildingDef def = entity.BuildingDef;
            bool powerOk = IsPowerStateOk(entity, def, powerNetworkService, _generationByComponentBuffer);
            BuildingWaterRuntimeState waterState = waterSimulationService != null
                ? waterSimulationService.GetBuildingState(entity.AnchorCell)
                : default;
            bool waterOk = IsWaterStateOk(def, waterState);
            BuildingOxygenRuntimeState oxygenState = oxygenSimulationService != null
                ? oxygenSimulationService.GetBuildingState(entity.AnchorCell)
                : default;
            bool oxygenOk = IsOxygenStateOk(def, oxygenState);
            bool hasPower = def.UsesPowerNetwork
                            && (def.RequiresPower
                                || def.PowerGenerationKwDay > 0f
                                || def.BatteryCapacityKwh > 0f
                                || def.ObjectType == BuildObjectType.ElectricBattery);
            bool hasWater = def.UsesWaterNetwork && def.WaterRole != WaterRole.None;
            bool hasOxygen = def.UsesOxygenNetwork && def.OxygenRole != OxygenRole.None;
            bool allOk = (!hasPower || powerOk) && (!hasWater || waterOk) && (!hasOxygen || oxygenOk);

            return new OverlayStateSnapshot(
                BuildResourceLabelText(entity, powerNetworkService, waterSimulationService, oxygenSimulationService),
                ShouldShowNoPowerIcon(entity, powerNetworkService),
                true,
                allOk ? OverlayVisualState.Ok : OverlayVisualState.Fail);
        }

        private void ApplyContentState(OverlayView view, OverlayStateSnapshot snapshot)
        {
            if (view == null || view.Root == null)
            {
                return;
            }

            view.TextLabel.text = snapshot.LabelText;
            view.NoPowerIcon.style.display = snapshot.ShowNoPowerIcon && _noPowerIconTexture != null
                ? DisplayStyle.Flex
                : DisplayStyle.None;

            Color okColor = new Color(0.45f, 0.85f, 1f, 1f);
            Color failColor = new Color(1f, 0.42f, 0.42f, 1f);
            Color frameColor = snapshot.VisualState == OverlayVisualState.Ok ? okColor : failColor;
            Color frameBgColor = snapshot.VisualState == OverlayVisualState.Ok
                ? new Color(0.06f, 0.12f, 0.18f, 0.86f)
                : new Color(0.20f, 0.08f, 0.08f, 0.86f);

            view.TextLabel.style.color = frameColor;
            view.Root.style.borderLeftColor = frameColor;
            view.Root.style.borderRightColor = frameColor;
            view.Root.style.borderTopColor = frameColor;
            view.Root.style.borderBottomColor = frameColor;
            view.Root.style.backgroundColor = frameBgColor;
        }

        private void UpdateLayout(OverlayView view)
        {
            if (_overlayLayer == null || _worldCamera == null || view == null || view.Root == null)
            {
                return;
            }

            Vector2 anchorCenter = _gridCoordinateConverter.CellToWorldCenter(view.Anchor);
            float objectLeftWorldX = anchorCenter.x - (_cellSize * 0.5f);
            float objectTopWorldY = anchorCenter.y + ((view.Size.y - 0.5f) * _cellSize);
            float panelTopWorldY = objectTopWorldY + _cellSize;
            float panelRightWorldX = objectLeftWorldX + (view.Size.x * _cellSize);

            Vector3 worldTopLeft = new Vector3(objectLeftWorldX, panelTopWorldY, 0f);
            Vector3 worldTopRight = new Vector3(panelRightWorldX, panelTopWorldY, 0f);
            Vector3 screenTopLeft = _worldCamera.WorldToScreenPoint(worldTopLeft);
            Vector3 screenTopRight = _worldCamera.WorldToScreenPoint(worldTopRight);
            if (screenTopLeft.z < 0f || screenTopRight.z < 0f)
            {
                view.Root.style.display = DisplayStyle.None;
                return;
            }

            float rootWidth = Mathf.Max(1f, _overlayLayer.resolvedStyle.width);
            float rootHeight = Mathf.Max(1f, _overlayLayer.resolvedStyle.height);
            float panelLeft = screenTopLeft.x / Mathf.Max(1f, Screen.width) * rootWidth;
            float panelRight = screenTopRight.x / Mathf.Max(1f, Screen.width) * rootWidth;
            float panelTop = (Screen.height - screenTopLeft.y) / Mathf.Max(1f, Screen.height) * rootHeight;
            float panelWidth = Mathf.Max(80f, Mathf.Abs(panelRight - panelLeft));

            view.Root.style.left = panelLeft;
            view.Root.style.top = panelTop;
            view.Root.style.width = panelWidth;
            ApplyZoomResponsiveStyle(view, worldTopLeft);
            view.Root.style.display = DisplayStyle.Flex;
        }

        private static string BuildResourceLabelText(
            BuildingRuntimeEntity entity,
            PowerNetworkService powerNetworkService,
            WaterSimulationService waterSimulationService,
            OxygenSimulationService oxygenSimulationService)
        {
            string powerLine = BuildPowerLabelText(entity, powerNetworkService);
            string waterLine = BuildWaterLabelText(entity, waterSimulationService);
            string oxygenLine = BuildOxygenLabelText(entity, oxygenSimulationService);
            if (string.IsNullOrEmpty(powerLine) && string.IsNullOrEmpty(waterLine) && string.IsNullOrEmpty(oxygenLine))
            {
                return string.Empty;
            }

            if (string.IsNullOrEmpty(powerLine))
            {
                return string.IsNullOrEmpty(waterLine)
                    ? $"O2 {oxygenLine}"
                    : string.IsNullOrEmpty(oxygenLine)
                        ? $"W {waterLine}"
                        : $"W {waterLine}\nO2 {oxygenLine}";
            }

            if (string.IsNullOrEmpty(waterLine))
            {
                return string.IsNullOrEmpty(oxygenLine)
                    ? $"P {powerLine}"
                    : $"P {powerLine}\nO2 {oxygenLine}";
            }

            if (string.IsNullOrEmpty(oxygenLine))
            {
                return $"P {powerLine}\nW {waterLine}";
            }

            return $"P {powerLine}\nW {waterLine}\nO2 {oxygenLine}";
        }

        private static string BuildPowerLabelText(BuildingRuntimeEntity entity, PowerNetworkService powerNetworkService)
        {
            BuildingDef def = entity.BuildingDef;
            bool isBattery = def.ObjectType == BuildObjectType.ElectricBattery || def.BatteryCapacityKwh > 0f;
            if (isBattery)
            {
                float max = Mathf.Max(0f, def.BatteryCapacityKwh);
                float current = powerNetworkService != null ? Mathf.Max(0f, powerNetworkService.GetBatteryChargeKwh(entity.AnchorCell)) : 0f;
                return $"B {current:0.#}/{max:0.#} kWh";
            }

            bool isConsumer = def.RequiresPower && def.PowerConsumptionKw > 0f;
            bool isGenerator = def.PowerGenerationKwDay > 0f;
            if (isConsumer && isGenerator)
            {
                return $"-{def.PowerConsumptionKw:0.#} +{def.PowerGenerationKwDay:0.#} kW";
            }

            if (isConsumer)
            {
                return $"-{def.PowerConsumptionKw:0.#} kW";
            }

            if (isGenerator)
            {
                return $"+{def.PowerGenerationKwDay:0.#} kW";
            }

            return string.Empty;
        }

        private static string BuildWaterLabelText(BuildingRuntimeEntity entity, WaterSimulationService waterSimulationService)
        {
            BuildingDef def = entity.BuildingDef;
            if (def == null)
            {
                return string.Empty;
            }

            BuildingWaterRuntimeState state = waterSimulationService != null
                ? waterSimulationService.GetBuildingState(entity.AnchorCell)
                : default;

            if (def.WaterRole == WaterRole.Producer)
            {
                return $"+{Mathf.Max(0f, def.WaterProductionLitersPerHour):0.#} L/h";
            }

            if (def.WaterRole == WaterRole.Consumer)
            {
                return $"-{Mathf.Max(0f, state.LastRequestedLiters):0.#} L/t";
            }

            if (def.WaterRole == WaterRole.Storage)
            {
                return $"S {Mathf.Max(0f, state.TankCurrentLiters):0.#}/{Mathf.Max(0f, state.TankCapacityLiters):0.#} L";
            }

            return string.Empty;
        }

        private static string BuildOxygenLabelText(BuildingRuntimeEntity entity, OxygenSimulationService oxygenSimulationService)
        {
            BuildingDef def = entity.BuildingDef;
            if (def == null)
            {
                return string.Empty;
            }

            BuildingOxygenRuntimeState state = oxygenSimulationService != null
                ? oxygenSimulationService.GetBuildingState(entity.AnchorCell)
                : default;

            if (def.OxygenRole == OxygenRole.Producer)
            {
                return $"+{Mathf.Max(0f, def.OxygenProductionLitersPerHour):0.#} L/h";
            }

            if (def.OxygenRole == OxygenRole.Consumer)
            {
                return $"-{Mathf.Max(0f, state.LastRequestedLiters):0.#} L/t";
            }

            if (def.OxygenRole == OxygenRole.Storage)
            {
                return $"S {Mathf.Max(0f, state.TankCurrentLiters):0.#}/{Mathf.Max(0f, state.TankCapacityLiters):0.#} L";
            }

            return string.Empty;
        }

        private static bool ShouldShowNoPowerIcon(BuildingRuntimeEntity entity, PowerNetworkService powerNetworkService)
        {
            BuildingDef def = entity.BuildingDef;
            if (def == null) return false;
            if (!def.RequiresPower) return false;
            if (def.PowerConsumptionKw <= 0f) return false;
            if (powerNetworkService == null) return true;

            BuildingPowerRuntimeState state = powerNetworkService.GetBuildingState(entity.AnchorCell);
            return !state.IsPowered;
        }

        private void Hide(Vector2Int anchor)
        {
            if (!_viewsByAnchor.TryGetValue(anchor, out OverlayView view) || view?.Root == null)
            {
                return;
            }

            view.Root.style.display = DisplayStyle.None;
        }

        private void SetAllVisible(bool isVisible)
        {
            if (_overlayLayer == null || _isOverlayLayerVisible == isVisible)
            {
                return;
            }

            _isOverlayLayerVisible = isVisible;
            _overlayLayer.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void EnsureOverlayLayer()
        {
            if (_hudRoot == null)
            {
                _overlayLayer = null;
                return;
            }

            if (_overlayLayer != null)
            {
                return;
            }

            _overlayLayer = _hudRoot.Q<VisualElement>("power-building-overlay-layer");
            if (_overlayLayer == null)
            {
                _overlayLayer = new VisualElement
                {
                    name = "power-building-overlay-layer"
                };
                _overlayLayer.style.position = Position.Absolute;
                _overlayLayer.style.left = 0f;
                _overlayLayer.style.top = 0f;
                _overlayLayer.style.right = 0f;
                _overlayLayer.style.bottom = 0f;
                _overlayLayer.pickingMode = PickingMode.Ignore;
                _hudRoot.Add(_overlayLayer);
            }

            _overlayLayer.style.display = _isOverlayLayerVisible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void ApplyZoomResponsiveStyle(OverlayView view, Vector3 worldPoint)
        {
            if (_worldCamera == null || view == null)
            {
                return;
            }

            Vector3 nextWorldPoint = worldPoint + new Vector3(0f, _cellSize, 0f);
            Vector3 screenA = _worldCamera.WorldToScreenPoint(worldPoint);
            Vector3 screenB = _worldCamera.WorldToScreenPoint(nextWorldPoint);
            float cellHeightPixels = Mathf.Abs(screenB.y - screenA.y);
            if (cellHeightPixels <= 0.01f)
            {
                return;
            }

            float fontSize = Mathf.Clamp(cellHeightPixels * 0.33f, 10f, 30f);
            float iconSize = Mathf.Clamp(cellHeightPixels * 0.42f, 10f, 26f);
            float iconMarginTop = Mathf.Clamp(cellHeightPixels * 0.08f, 1f, 6f);

            if (Mathf.Abs(view.LastFontSize - fontSize) > 0.25f)
            {
                view.TextLabel.style.fontSize = fontSize;
                view.LastFontSize = fontSize;
            }

            if (Mathf.Abs(view.LastIconSize - iconSize) > 0.25f)
            {
                view.NoPowerIcon.style.width = iconSize;
                view.NoPowerIcon.style.height = iconSize;
                view.LastIconSize = iconSize;
            }

            if (Mathf.Abs(view.LastIconMarginTop - iconMarginTop) > 0.25f)
            {
                view.NoPowerIcon.style.marginTop = iconMarginTop;
                view.LastIconMarginTop = iconMarginTop;
            }
        }

        private static VisualElement CreateFallbackRoot(Vector2Int anchor)
        {
            var root = new VisualElement
            {
                name = $"power-overlay-{anchor.x}-{anchor.y}"
            };
            root.style.position = Position.Absolute;
            root.style.flexDirection = FlexDirection.Column;
            root.style.alignItems = Align.Center;
            root.style.justifyContent = Justify.Center;
            root.style.unityTextAlign = TextAnchor.MiddleCenter;
            root.pickingMode = PickingMode.Ignore;
            return root;
        }

        private static Label CreateFallbackLabel()
        {
            var text = new Label();
            text.style.unityTextAlign = TextAnchor.MiddleCenter;
            text.style.color = Color.white;
            text.style.fontSize = 12f;
            text.style.whiteSpace = WhiteSpace.Normal;
            return text;
        }

        private static VisualElement CreateFallbackIcon()
        {
            var icon = new VisualElement();
            icon.style.width = 14f;
            icon.style.height = 14f;
            icon.style.marginTop = 2f;
            icon.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            icon.style.backgroundRepeat = new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat);
            icon.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
            icon.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
            return icon;
        }

        private bool IsPowerStateOk(
            BuildingRuntimeEntity entity,
            BuildingDef def,
            PowerNetworkService powerNetworkService,
            Dictionary<int, float> generationByComponent)
        {
            if (def == null || !def.UsesPowerNetwork)
            {
                return true;
            }

            bool isConsumer = def.RequiresPower && def.PowerConsumptionKw > 0f;
            bool isBattery = def.ObjectType == BuildObjectType.ElectricBattery || def.BatteryCapacityKwh > 0f;
            if (isBattery)
            {
                float chargeKwh = powerNetworkService != null
                    ? Mathf.Max(0f, powerNetworkService.GetBatteryChargeKwh(entity.AnchorCell))
                    : 0f;
                int componentId = GetBuildingComponentId(entity);
                generationByComponent.TryGetValue(componentId, out float componentGenerationKw);
                return chargeKwh > 0.0001f || componentGenerationKw > 0.0001f;
            }

            if (isConsumer)
            {
                if (powerNetworkService == null)
                {
                    return false;
                }

                BuildingPowerRuntimeState state = powerNetworkService.GetBuildingState(entity.AnchorCell);
                return state.IsPowered && state.SuppliedPowerKw + 0.0001f >= state.RequestedPowerKw;
            }

            return true;
        }

        private static bool IsWaterStateOk(BuildingDef def, BuildingWaterRuntimeState state)
        {
            if (def == null || !def.UsesWaterNetwork || def.WaterRole == WaterRole.None)
            {
                return true;
            }

            if (def.WaterRole == WaterRole.Producer)
            {
                return state.WaterNetworkId != 0 && state.IsProducerEnabled;
            }

            if (def.WaterRole == WaterRole.Consumer)
            {
                bool hasWaterNow = state.TankCurrentLiters > 0.001f;
                bool hasUnmetRequest = state.LastRequestedLiters > state.LastConsumedLiters + 0.001f;
                bool hasNoWaterAccess = state.WaterNetworkId == 0 && !hasWaterNow;
                return !hasNoWaterAccess && (hasWaterNow || !hasUnmetRequest);
            }

            if (def.WaterRole == WaterRole.Storage)
            {
                return state.WaterNetworkId != 0 || state.TankCurrentLiters > 0.001f;
            }

            return true;
        }

        private static bool IsOxygenStateOk(BuildingDef def, BuildingOxygenRuntimeState state)
        {
            if (def == null || !def.UsesOxygenNetwork || def.OxygenRole == OxygenRole.None)
            {
                return true;
            }

            if (def.OxygenRole == OxygenRole.Producer)
            {
                return state.OxygenNetworkId != 0 && state.IsProducerEnabled;
            }

            if (def.OxygenRole == OxygenRole.Consumer)
            {
                bool hasOxygenNow = state.TankCurrentLiters > 0.001f;
                bool hasUnmetRequest = state.LastRequestedLiters > state.LastConsumedLiters + 0.001f;
                bool hasNoNetworkAccess = state.OxygenNetworkId == 0 && !hasOxygenNow;
                return !hasNoNetworkAccess && (hasOxygenNow || !hasUnmetRequest);
            }

            if (def.OxygenRole == OxygenRole.Storage)
            {
                return state.OxygenNetworkId != 0 || state.TankCurrentLiters > 0.001f;
            }

            return true;
        }

        private void RebuildGenerationByComponent(List<BuildingRuntimeEntity> activeBuildings)
        {
            _generationByComponentBuffer.Clear();
            if (activeBuildings == null || _gridState == null || _gameTimeService == null || _solarPowerProductionService == null)
            {
                return;
            }

            for (int i = 0; i < activeBuildings.Count; i++)
            {
                BuildingRuntimeEntity entity = activeBuildings[i];
                if (entity?.BuildingDef == null || !entity.BuildingDef.UsesPowerNetwork)
                {
                    continue;
                }

                float generationKw = _solarPowerProductionService.GetCurrentGenerationKw(entity.BuildingDef, _gameTimeService);
                if (generationKw <= 0.0001f)
                {
                    continue;
                }

                int componentId = GetBuildingComponentId(entity);
                if (_generationByComponentBuffer.TryGetValue(componentId, out float current))
                {
                    _generationByComponentBuffer[componentId] = current + generationKw;
                }
                else
                {
                    _generationByComponentBuffer[componentId] = generationKw;
                }
            }
        }

        private int GetBuildingComponentId(BuildingRuntimeEntity entity)
        {
            if (entity?.BuildingDef == null || _gridState == null)
            {
                return int.MinValue;
            }

            Vector2Int portCell = entity.AnchorCell + entity.BuildingDef.PowerInputOffset;
            if (!_gridState.IsInside(portCell.x, portCell.y))
            {
                return int.MinValue;
            }

            Cell port = _gridState.GetCell(portCell.x, portCell.y);
            return port.CableNetworkId;
        }
    }
}
