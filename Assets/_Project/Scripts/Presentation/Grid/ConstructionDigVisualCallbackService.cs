using _Project.Scripts.Data.Construction;
using _Project.Scripts.Data.Grid;
using _Project.Scripts.Presentation.Buildings;
using _Project.Scripts.Systems.Grid;
using _Project.Scripts.Systems.Oxygen;
using _Project.Scripts.Systems.Power;
using _Project.Scripts.Systems.Water;
using _Project.Scripts.Systems.Construction;
using _Project.Scripts.Data.Water;
using _Project.Scripts.Data.Oxygen;
using _Project.Scripts.Data.Power;
using _Project.Scripts.Systems.Simulation;
using System.Collections.Generic;
using UnityEngine;

namespace _Project.Scripts.Presentation.Grid
{
    /// <summary>
    /// Обрабатывает визуальные коллбеки после завершения копки и строительства.
    /// </summary>
    public sealed class ConstructionDigVisualCallbackService
    {
        private readonly GridState _gridState;
        private readonly GridTileVisualService _gridTileVisualService;
        private readonly GridCoordinateConverter _gridCoordinateConverter;
        private readonly BuildingViewRegistry _buildingViewRegistry;
        private readonly Transform _buildingsRoot;
        private readonly Dictionary<Vector2Int, BuildingViewBase> _viewsByAnchor =
            new Dictionary<Vector2Int, BuildingViewBase>();
        private readonly MaterialTransitionOverlayService _materialTransitionOverlayService;
        private readonly CablePlacementService _cablePlacementService;
        private readonly CablePreviewRefreshService _cablePreviewRefreshService;
        private readonly LifeModulePlacementService _lifeModulePlacementService;
        private readonly LifeModulePreviewRefreshService _lifeModulePreviewRefreshService;
        private readonly WaterPlacementService _waterPlacementService;
        private readonly WaterPreviewRefreshService _waterPreviewRefreshService;
        private readonly OxygenPlacementService _oxygenPlacementService;
        private readonly OxygenPreviewRefreshService _oxygenPreviewRefreshService;
        private bool _isCableBuildTintActive;
        private Color _cableBuildTintColor = Color.white;

        public ConstructionDigVisualCallbackService(
            GridState gridState,
            GridTileVisualService gridTileVisualService,
            GridCoordinateConverter gridCoordinateConverter,
            BuildingViewRegistry buildingViewRegistry,
            Transform buildingsRoot,
            MaterialTransitionOverlayService materialTransitionOverlayService,
            CablePlacementService cablePlacementService,
            CablePreviewRefreshService cablePreviewRefreshService,
            LifeModulePlacementService lifeModulePlacementService,
            LifeModulePreviewRefreshService lifeModulePreviewRefreshService,
            WaterPlacementService waterPlacementService,
            WaterPreviewRefreshService waterPreviewRefreshService,
            OxygenPlacementService oxygenPlacementService,
            OxygenPreviewRefreshService oxygenPreviewRefreshService)
        {
            _materialTransitionOverlayService = materialTransitionOverlayService;
            _gridState = gridState;
            _gridTileVisualService = gridTileVisualService;
            _gridCoordinateConverter = gridCoordinateConverter;
            _buildingViewRegistry = buildingViewRegistry;
            _buildingsRoot = buildingsRoot;
            _cablePlacementService = cablePlacementService;
            _cablePreviewRefreshService = cablePreviewRefreshService;
            _lifeModulePlacementService = lifeModulePlacementService;
            _lifeModulePreviewRefreshService = lifeModulePreviewRefreshService;
            _waterPlacementService = waterPlacementService;
            _waterPreviewRefreshService = waterPreviewRefreshService;
            _oxygenPlacementService = oxygenPlacementService;
            _oxygenPreviewRefreshService = oxygenPreviewRefreshService;
        }

        /// <summary>
        /// Вызывается после завершения копки клетки.
        /// </summary>
        public void OnDigCompleted(Vector2Int cellPos)
        {
            _gridTileVisualService.SetTaskMarker(cellPos, false);
            Cell cell = _gridState.GetCell(cellPos.x, cellPos.y);
            _gridTileVisualService.SetGroundByCellType(cellPos, cell.Type);
            _materialTransitionOverlayService?.RefreshAround(cellPos);
        }

        /// <summary>
        /// Вызывается после завершения строительства объекта.
        /// </summary>
        public void OnBuildCompleted(BuildTaskPayload payload)
        {
            if (payload == null || payload.BuildingDef == null)
            {
                return;
            }

            Vector2Int size = payload.IsRotated
                ? new Vector2Int(payload.BuildingDef.Height, payload.BuildingDef.Width)
                : new Vector2Int(payload.BuildingDef.Width, payload.BuildingDef.Height);

            for (int y = 0; y < size.y; y++)
            {
                for (int x = 0; x < size.x; x++)
                {
                    Vector2Int cellPos = new Vector2Int(payload.AnchorCell.x + x, payload.AnchorCell.y + y);
                    // Clear any stale dig/build preview tiles after build completion.
                    _gridTileVisualService.SetDigPreview(cellPos, false);
                    _gridTileVisualService.SetBuildPreviewTile(cellPos, null);
                    _gridTileVisualService.SetTaskMarker(cellPos, false);
                    _gridTileVisualService.SetBuildTaskMarker(cellPos, false);
                    _materialTransitionOverlayService?.RefreshAround(cellPos);
                }
            }
            // Anchor-level preview (scaled by footprint) is drawn in one tile cell, clear it explicitly too.
            _gridTileVisualService.SetBuildPreviewTile(payload.AnchorCell, null);

            if (_buildingViewRegistry != null
                && _buildingViewRegistry.TryGetViewPrefab(payload.BuildingDef, out BuildingViewBase buildingViewPrefab))
            {
                SpawnBuildingView(buildingViewPrefab, payload.AnchorCell, size);
                return;
            }

            // Debug.LogWarning($"[Build] View prefab not mapped for '{payload.BuildingDef.name}' ({payload.BuildingDef.ObjectType}). Sprite-only mode: no tilemap fallback.");
        }

        /// <summary>
        /// Вызывается после завершения демонтажа построенного объекта.
        /// </summary>
        public void OnDestroyCompleted(BuildTaskPayload payload)
        {
            if (payload == null || payload.BuildingDef == null)
            {
                return;
            }

            Vector2Int size = payload.IsRotated
                ? new Vector2Int(payload.BuildingDef.Height, payload.BuildingDef.Width)
                : new Vector2Int(payload.BuildingDef.Width, payload.BuildingDef.Height);

            for (int y = 0; y < size.y; y++)
            {
                for (int x = 0; x < size.x; x++)
                {
                    Vector2Int cellPos = new Vector2Int(payload.AnchorCell.x + x, payload.AnchorCell.y + y);
                    _gridTileVisualService.SetDestructionMarker(cellPos, false);
                    Cell cell = _gridState.GetCell(cellPos.x, cellPos.y);
                    _gridTileVisualService.SetGroundByCellType(cellPos, cell.Type);
                    _materialTransitionOverlayService?.RefreshAround(cellPos);
                }
            }

            if (_viewsByAnchor.TryGetValue(payload.AnchorCell, out BuildingViewBase view))
            {
                _viewsByAnchor.Remove(payload.AnchorCell);
                Object.Destroy(view.gameObject);
            }
        }

        /// <summary>
        /// Снимает preview прокладки кабеля с клетки после завершения/отмены задачи.
        /// </summary>
        public void OnCablePreviewCleared(Vector2Int cellPos)
        {
            if (_gridState.IsInside(cellPos.x, cellPos.y))
            {
                Cell cell = _gridState.GetCell(cellPos.x, cellPos.y);
                cell.IsCableMarked = false;
                cell.IsCablePreviewVisible = false;
                cell.CablePreviewShapeId = 0;
                cell.CablePreviewRotationZ = 0f;
                _gridState.SetCell(cellPos.x, cellPos.y, cell);
            }

            _gridTileVisualService.SetCablePreview(cellPos, false, 0);
        }

        /// <summary>
        /// Финализирует прокладку кабеля в клетке и обновляет соседние визуалы.
        /// </summary>
        public void OnCableBuildCompleted(Vector2Int cellPos)
        {
            LogCableCrossState("BeforePlace", cellPos);
            // Если сервис прокладки кабеля не передан, завершить метод без действий.
            if (_cablePlacementService == null) return;
            // Пытаемся зафиксировать кабель в grid-состоянии; если не удалось, выходим.
            if (!_cablePlacementService.TryPlaceCable(_gridState, cellPos)) return;
            // Глобально валидируем маски всех построенных кабелей после изменения топологии сети.
            _cablePlacementService.RecalculateAllCableMasks(_gridState);
            LogCableCrossState("AfterPlace", cellPos);

            // После постройки снимаем preview только в текущей клетке.
            // Соседние preview-клетки сохраняем, чтобы планы прокладки не исчезали.
            // Проверяем, что целевая клетка существует внутри границ сетки.
            if (_gridState.IsInside(cellPos.x, cellPos.y))
            {
                // Читаем текущее состояние клетки для точечной правки preview-полей.
                Cell cell = _gridState.GetCell(cellPos.x, cellPos.y);
                // Сбрасываем флаг отображения preview-кабеля в этой клетке.
                cell.IsCablePreviewVisible = false;
                // Обнуляем id формы preview, так как план для клетки завершён.
                cell.CablePreviewShapeId = 0;
                // Обнуляем угол поворота preview-тайла в debug-состоянии клетки.
                cell.CablePreviewRotationZ = 0f;
                // Записываем обновлённое состояние клетки обратно в grid.
                _gridState.SetCell(cellPos.x, cellPos.y, cell);
            }
            // Удаляем preview-тайл из tilemap в целевой клетке.
            _gridTileVisualService.SetCablePreview(cellPos, false, 0);
            _cablePreviewRefreshService.RefreshBuiltAround(cellPos);
            _cablePreviewRefreshService.ReconcileAllBuilt();
            _cablePreviewRefreshService.ReconcileAllPlannedFromTaskBoard();
            LogCableCrossState("AfterVisualRefresh", cellPos);
        }


        /// <summary>
        /// Финализирует демонтаж кабеля: удаляет cable из клетки и обновляет соседние визуалы.
        /// </summary>
        public void OnCableDestroyCompleted(Vector2Int cellPos)
        {
            if (_cablePlacementService == null) return;
            if (!_cablePlacementService.TryRemoveCable(_gridState, cellPos)) return;

            _gridTileVisualService.SetDestructionMarker(cellPos, false);
            _cablePlacementService.RecalculateAllCableMasks(_gridState);
            _cablePreviewRefreshService.RefreshBuiltAround(cellPos);
            _cablePreviewRefreshService.ReconcileAllBuilt();
            _cablePreviewRefreshService.ReconcileAllPlannedFromTaskBoard();
        }

        public void OnLifeModuleBuildCompleted(LifeModuleTaskPayload payload)
        {
            if (payload == null || _lifeModulePlacementService == null || _lifeModulePreviewRefreshService == null)
            {
                return;
            }

            _lifeModulePlacementService.FinalizeBuild(payload);
            _lifeModulePreviewRefreshService.RebuildBuilt();
            _lifeModulePreviewRefreshService.RebuildPreview();
        }

        public void OnLifeModulePreviewCleared(LifeModuleTaskPayload payload)
        {
            if (payload == null || _lifeModulePlacementService == null || _lifeModulePreviewRefreshService == null)
            {
                return;
            }

            _lifeModulePlacementService.RefundBuildCost(payload);
            _lifeModulePlacementService.ReleasePreviewState(payload);
            _lifeModulePreviewRefreshService.RebuildPreview();
        }


        public void OnWaterPreviewCleared(Vector2Int cellPos)
        {
            if (_gridState.IsInside(cellPos.x, cellPos.y))
            {
                Cell cell = _gridState.GetCell(cellPos.x, cellPos.y);
                cell.IsWaterMarked = false;
                cell.IsWaterPreviewVisible = false;
                cell.WaterPreviewMask4 = 0;
                cell.WaterPreviewShapeId = 0;
                cell.WaterPreviewRotationZ = 0f;
                _gridState.SetCell(cellPos.x, cellPos.y, cell);
            }

            _gridTileVisualService.SetWaterPreview(cellPos, false, 0);
        }

        public void OnWaterBuildCompleted(Vector2Int cellPos)
        {
            if (_waterPlacementService == null) return;
            if (!_waterPlacementService.TryPlaceWater(_gridState, cellPos)) return;

            _waterPlacementService.RecalculateAllWaterMasks(_gridState);

            if (_gridState.IsInside(cellPos.x, cellPos.y))
            {
                Cell cell = _gridState.GetCell(cellPos.x, cellPos.y);
                cell.IsWaterMarked = false;
                cell.IsWaterPreviewVisible = false;
                cell.WaterPreviewMask4 = 0;
                cell.WaterPreviewShapeId = 0;
                cell.WaterPreviewRotationZ = 0f;
                _gridState.SetCell(cellPos.x, cellPos.y, cell);
            }

            _gridTileVisualService.SetWaterPreview(cellPos, false, 0);
            _waterPreviewRefreshService.RefreshBuiltAround(cellPos);
            _waterPreviewRefreshService.ReconcileAllBuilt();
            _waterPreviewRefreshService.ReconcileAllPlannedFromTaskBoard();
        }

        public void OnWaterDestroyCompleted(Vector2Int cellPos)
        {
            if (_waterPlacementService == null) return;
            if (!_waterPlacementService.TryRemoveWater(_gridState, cellPos)) return;

            _gridTileVisualService.SetDestructionMarker(cellPos, false);
            _waterPlacementService.RecalculateAllWaterMasks(_gridState);
            _waterPreviewRefreshService.RefreshBuiltAround(cellPos);
            _waterPreviewRefreshService.ReconcileAllBuilt();
            _waterPreviewRefreshService.ReconcileAllPlannedFromTaskBoard();
        }

        public void OnOxygenPreviewCleared(Vector2Int cellPos)
        {
            if (_gridState.IsInside(cellPos.x, cellPos.y))
            {
                Cell cell = _gridState.GetCell(cellPos.x, cellPos.y);
                cell.IsOxygenMarked = false;
                cell.IsOxygenPreviewVisible = false;
                cell.OxygenPreviewMask4 = 0;
                cell.OxygenPreviewShapeId = 0;
                cell.OxygenPreviewRotationZ = 0f;
                _gridState.SetCell(cellPos.x, cellPos.y, cell);
            }

            _gridTileVisualService.SetOxygenPreview(cellPos, false, 0);
        }

        public void OnOxygenBuildCompleted(Vector2Int cellPos)
        {
            if (_oxygenPlacementService == null) return;
            if (!_oxygenPlacementService.TryPlaceOxygen(_gridState, cellPos)) return;

            _oxygenPlacementService.RecalculateAllOxygenMasks(_gridState);

            if (_gridState.IsInside(cellPos.x, cellPos.y))
            {
                Cell cell = _gridState.GetCell(cellPos.x, cellPos.y);
                cell.IsOxygenMarked = false;
                cell.IsOxygenPreviewVisible = false;
                cell.OxygenPreviewMask4 = 0;
                cell.OxygenPreviewShapeId = 0;
                cell.OxygenPreviewRotationZ = 0f;
                _gridState.SetCell(cellPos.x, cellPos.y, cell);
            }

            _gridTileVisualService.SetOxygenPreview(cellPos, false, 0);
            _oxygenPreviewRefreshService.RefreshBuiltAround(cellPos);
            _oxygenPreviewRefreshService.ReconcileAllBuilt();
            _oxygenPreviewRefreshService.ReconcileAllPlannedFromTaskBoard();
        }

        public void OnOxygenDestroyCompleted(Vector2Int cellPos)
        {
            if (_oxygenPlacementService == null) return;
            if (!_oxygenPlacementService.TryRemoveOxygen(_gridState, cellPos)) return;

            _gridTileVisualService.SetDestructionMarker(cellPos, false);
            _oxygenPlacementService.RecalculateAllOxygenMasks(_gridState);
            _oxygenPreviewRefreshService.RefreshBuiltAround(cellPos);
            _oxygenPreviewRefreshService.ReconcileAllBuilt();
            _oxygenPreviewRefreshService.ReconcileAllPlannedFromTaskBoard();
        }

        /// <summary>
        /// Updates water warning icons for all spawned building views.
        /// </summary>
        public void RefreshWaterWarnings(BuildingManager buildingManager, WaterSimulationService waterSimulationService)
        {
            if (buildingManager == null || waterSimulationService == null || _viewsByAnchor.Count == 0)
            {
                return;
            }

            foreach (KeyValuePair<Vector2Int, BuildingViewBase> pair in _viewsByAnchor)
            {
                BuildingViewBase view = pair.Value;
                if (view == null)
                {
                    continue;
                }

                if (!buildingManager.TryGetBuildingEntity(pair.Key, out BuildingRuntimeEntity entity) || entity == null || !entity.IsActive)
                {
                    view.SetWaterWarningState(false);
                    continue;
                }

                if (entity.BuildingDef == null || entity.BuildingDef.WaterRole != WaterRole.Consumer)
                {
                    view.SetWaterWarningState(false);
                    continue;
                }

                BuildingWaterRuntimeState state = waterSimulationService.GetBuildingState(pair.Key);
                bool hasWaterNow = state.TankCurrentLiters > 0.001f;
                bool hasUnmetRequest = state.LastRequestedLiters > state.LastConsumedLiters + 0.001f;
                bool hasNoWaterAccess = state.WaterNetworkId == 0 && !hasWaterNow;
                bool shouldWarn = hasNoWaterAccess || (!hasWaterNow) || hasUnmetRequest;

                view.SetWaterWarningState(shouldWarn);
            }
        }

        /// <summary>
        /// Updates power warning icons for all spawned building views.
        /// </summary>
        public void RefreshPowerWarnings(BuildingManager buildingManager, PowerNetworkService powerNetworkService)
        {
            if (buildingManager == null || powerNetworkService == null || _viewsByAnchor.Count == 0)
            {
                return;
            }

            foreach (KeyValuePair<Vector2Int, BuildingViewBase> pair in _viewsByAnchor)
            {
                BuildingViewBase view = pair.Value;
                if (view == null)
                {
                    continue;
                }

                if (!buildingManager.TryGetBuildingEntity(pair.Key, out BuildingRuntimeEntity entity) || entity == null || !entity.IsActive)
                {
                    view.SetPowerWarningState(false);
                    continue;
                }

                if (entity.BuildingDef == null || !entity.BuildingDef.RequiresPower)
                {
                    view.SetPowerWarningState(false);
                    continue;
                }

                BuildingPowerRuntimeState powerState = powerNetworkService.GetBuildingState(pair.Key);
                bool hasNoPower = !powerState.IsPowered;
                bool hasInsufficientPower = powerState.RequestedPowerKw > powerState.SuppliedPowerKw + 0.0001f;
                bool shouldWarn = hasNoPower || hasInsufficientPower;
                view.SetPowerWarningState(shouldWarn);
            }
        }

        /// <summary>
        /// Updates battery charge/discharge animations for all spawned battery views.
        /// </summary>
        public void RefreshBatteryAnimations(BuildingManager buildingManager, PowerNetworkService powerNetworkService)
        {
            if (buildingManager == null || powerNetworkService == null || _viewsByAnchor.Count == 0)
            {
                return;
            }

            foreach (KeyValuePair<Vector2Int, BuildingViewBase> pair in _viewsByAnchor)
            {
                if (!(pair.Value is ElectricBatteryBuildingView batteryView))
                {
                    continue;
                }

                if (!buildingManager.TryGetBuildingEntity(pair.Key, out BuildingRuntimeEntity entity) || entity == null || !entity.IsActive)
                {
                    batteryView.SetBatteryAnimationState(false, true);
                    continue;
                }

                if (entity.BuildingDef == null || entity.BuildingDef.ObjectType != BuildObjectType.ElectricBattery)
                {
                    batteryView.SetBatteryAnimationState(false, true);
                    continue;
                }

                float chargeKwh = Mathf.Max(0f, powerNetworkService.GetBatteryChargeKwh(pair.Key));
                bool isDepleted = chargeKwh <= 0.0001f;
                bool isConnectedToNetwork = IsConnectedToPowerNetwork(entity);
                batteryView.SetBatteryAnimationState(isConnectedToNetwork, isDepleted);
            }
        }

        /// <summary>
        /// Plays one storage deposit interaction animation for the building that owns the specified footprint cell.
        /// </summary>
        public float TriggerStorageInteractionByCell(Vector2Int storageCell)
        {
            if (!TryFindStorageViewByFootprintCell(storageCell, out StorageBuildingView storageView))
            {
                return 0f;
            }

            return storageView.PlayDepositInteraction();
        }

        /// <summary>
        /// Updates oxygen warning icons for all spawned building views.
        /// </summary>
        public void RefreshOxygenWarnings(BuildingManager buildingManager, OxygenSimulationService oxygenSimulationService)
        {
            if (buildingManager == null || oxygenSimulationService == null || _viewsByAnchor.Count == 0)
            {
                return;
            }

            foreach (KeyValuePair<Vector2Int, BuildingViewBase> pair in _viewsByAnchor)
            {
                BuildingViewBase view = pair.Value;
                if (view == null)
                {
                    continue;
                }

                if (!buildingManager.TryGetBuildingEntity(pair.Key, out BuildingRuntimeEntity entity) || entity == null || !entity.IsActive)
                {
                    view.SetWaterWarningState(false);
                    continue;
                }

                if (entity.BuildingDef == null || entity.BuildingDef.OxygenRole != OxygenRole.Consumer)
                {
                    view.SetWaterWarningState(false);
                    continue;
                }

                BuildingOxygenRuntimeState state = oxygenSimulationService.GetBuildingState(pair.Key);
                bool hasOxygenNow = state.TankCurrentLiters > 0.001f;
                bool hasUnmetRequest = state.LastRequestedLiters > state.LastConsumedLiters + 0.001f;
                bool hasNoOxygenAccess = state.OxygenNetworkId == 0 && !hasOxygenNow;
                bool shouldWarn = hasNoOxygenAccess || !hasOxygenNow || hasUnmetRequest;

                // Reuse existing warning channel until dedicated oxygen warning visuals are introduced.
                view.SetWaterWarningState(shouldWarn);
            }
        }

        /// <summary>
        /// Updates light-phase driven animations for all spawned building views.
        /// </summary>
        public void RefreshLightPhaseVisuals(GameTimeService gameTimeService)
        {
            if (gameTimeService == null || _viewsByAnchor.Count == 0)
            {
                return;
            }

            bool isDay = gameTimeService.IsDay;
            foreach (KeyValuePair<Vector2Int, BuildingViewBase> pair in _viewsByAnchor)
            {
                BuildingViewBase view = pair.Value;
                if (view == null)
                {
                    continue;
                }

                view.SetLightPhaseState(isDay);
            }
        }

        /// <summary>
        /// Applies cable-build tint to all active building views and to future spawned views.
        /// </summary>
        public void SetCableBuildModeTint(Color color)
        {
            _isCableBuildTintActive = true;
            _cableBuildTintColor = color;

            foreach (KeyValuePair<Vector2Int, BuildingViewBase> pair in _viewsByAnchor)
            {
                BuildingViewBase view = pair.Value;
                if (view == null)
                {
                    continue;
                }

                view.SetModeTint(color);
            }
        }

        /// <summary>
        /// Restores original colors after cable-build mode finishes.
        /// </summary>
        public void ClearCableBuildModeTint()
        {
            if (!_isCableBuildTintActive)
            {
                return;
            }

            _isCableBuildTintActive = false;
            foreach (KeyValuePair<Vector2Int, BuildingViewBase> pair in _viewsByAnchor)
            {
                BuildingViewBase view = pair.Value;
                if (view == null)
                {
                    continue;
                }

                view.ResetModeTint();
            }
        }

        private void LogCableCrossState(string phase, Vector2Int center)
        {
            if (_gridState == null)
            {
            // Debug.LogWarning($"[CableBuild][{phase}] gridState is null.");
                return;
            }

            string c = BuildCellLog(center, "C");
            string u = BuildCellLog(center + Vector2Int.up, "U");
            string r = BuildCellLog(center + Vector2Int.right, "R");
            string d = BuildCellLog(center + Vector2Int.down, "D");
            string l = BuildCellLog(center + Vector2Int.left, "L");

            // Debug.Log($"[CableBuild][{phase}] {c} | {u} | {r} | {d} | {l}");
        }

        private string BuildCellLog(Vector2Int pos, string tag)
        {
            if (_gridState == null)
            {
                return $"{tag}=({pos.x},{pos.y}) gridState=null";
            }

            if (!_gridState.IsInside(pos.x, pos.y))
            {
                return $"{tag}=({pos.x},{pos.y}) outside";
            }

            Cell cell = _gridState.GetCell(pos.x, pos.y);
            CableVisualResolver.ResolveVisualDebug(cell.CableMask4, out CableVisualShapeId shapeId, out float rotationZ, out _);
            return $"{tag}=({pos.x},{pos.y}) hasCable={cell.HasCable} mask={cell.CableMask4} shape={shapeId} rot={rotationZ:0.##} preview={cell.IsCablePreviewVisible}";
        }

        /// <summary>
        /// Спавнит runtime view-префаб объекта по anchor и размеру footprint.
        /// </summary>
        private void SpawnBuildingView(BuildingViewBase buildingViewPrefab, Vector2Int anchorCell, Vector2Int size)
        {
            if (buildingViewPrefab == null)
            {
            // Debug.LogWarning("[Build] BuildingView prefab is not assigned.");
                return;
            }

            Vector2 anchorCenterWorld = _gridCoordinateConverter.CellToWorldCenter(anchorCell);
            float offsetX = (size.x - 1) * 0.5f * _gridState.CellSize;
            float offsetY = (size.y - 1) * 0.5f * _gridState.CellSize;
            Vector3 spawnPosition = new Vector3(anchorCenterWorld.x + offsetX, anchorCenterWorld.y + offsetY, 0f);

            BuildingViewBase view = Object.Instantiate(buildingViewPrefab, spawnPosition, Quaternion.identity, _buildingsRoot);
            view.Initialize(anchorCell, size);
            _viewsByAnchor[anchorCell] = view;
            if (_isCableBuildTintActive)
            {
                view.SetModeTint(_cableBuildTintColor);
            }

            // Проставляем локальный override проходимости по footprint этого building view.
            // Для LadderBuildingView не включаем override в Empty:
            // лестница должна оставаться лестницей для логики движения.
            if (view.IgnoreAsObstacleForPathfinding && !(view is LadderBuildingView))
            {
                for (int y = 0; y < size.y; y++)
                {
                    for (int x = 0; x < size.x; x++)
                    {
                        int gx = anchorCell.x + x;
                        int gy = anchorCell.y + y;
                        if (!_gridState.IsInside(gx, gy)) continue;

                        Cell cell = _gridState.GetCell(gx, gy);
                        cell.IgnoreObstacleForPathfinding = true;
                        _gridState.SetCell(gx, gy, cell);
                    }
                }
            }
        }

        private bool IsConnectedToPowerNetwork(BuildingRuntimeEntity entity)
        {
            if (entity == null || entity.BuildingDef == null)
            {
                return false;
            }

            Vector2Int portCell = entity.AnchorCell + entity.BuildingDef.PowerInputOffset;
            if (!_gridState.IsInside(portCell.x, portCell.y))
            {
                return false;
            }

            Cell port = _gridState.GetCell(portCell.x, portCell.y);
            return port.HasCable && port.CableNetworkId > 0;
        }

        private bool TryFindStorageViewByFootprintCell(Vector2Int storageCell, out StorageBuildingView storageView)
        {
            foreach (KeyValuePair<Vector2Int, BuildingViewBase> pair in _viewsByAnchor)
            {
                if (!(pair.Value is StorageBuildingView candidate))
                {
                    continue;
                }

                Vector2Int anchorCell = candidate.AnchorCell;
                Vector2Int size = candidate.Size;
                bool containsCell = storageCell.x >= anchorCell.x
                    && storageCell.x < anchorCell.x + size.x
                    && storageCell.y >= anchorCell.y
                    && storageCell.y < anchorCell.y + size.y;
                if (!containsCell)
                {
                    continue;
                }

                storageView = candidate;
                return true;
            }

            storageView = null;
            return false;
        }
    }
}