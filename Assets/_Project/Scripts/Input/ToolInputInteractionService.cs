using System;
using System.Collections.Generic;
using _Project.Scripts.Data.Construction;
using _Project.Scripts.Data.Grid;
using _Project.Scripts.Data.Tasks;
using _Project.Scripts.Presentation.Grid;
using _Project.Scripts.Presentation.UI;
using _Project.Scripts.Systems.Construction;
using _Project.Scripts.Systems.Power;
using _Project.Scripts.Systems.Resources;
using _Project.Scripts.Systems.Tasks;
using _Project.Scripts.Systems.Units;
using _Project.Scripts.Systems.Water;
using _Project.Scripts.Systems.Oxygen;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace _Project.Scripts.Input
{
    /// <summary>
    /// Runtime interaction and preview helper.
    /// </summary>
    public sealed class ToolInputInteractionService
    {
        public event Action<ToolMode> ToolModeChanged;

        private readonly GridState _gridState;
        private readonly BuildingPlacementService _buildingPlacementService;
        private readonly GridTileVisualService _gridTileVisualService;
        private readonly ConstructionToolPanelController _constructionToolPanelController;
        private readonly Func<int> _getTickCounter;
        private readonly Func<Vector2Int, bool> _shouldBlockNeutralCellClick;
        private readonly UnitTaskOrchestratorService _unitTaskOrchestratorService;
        private readonly GlobalTaskBoardService _globalTaskBoardService;
        private readonly SceneResourceObjectService _sceneResourceObjectService;
        private readonly CablePreviewRefreshService _cablePreviewRefreshService;
        private readonly Action<Vector2Int> _onCablePreviewCleared;
        private readonly LifeModulePlacementService _lifeModulePlacementService;
        private readonly LifeModulePreviewRefreshService _lifeModulePreviewRefreshService;
        private readonly Action<LifeModuleTaskPayload> _onLifeModulePreviewCleared;
        private readonly WaterPreviewRefreshService _waterPreviewRefreshService;
        private readonly Action<Vector2Int> _onWaterPreviewCleared;
        private readonly OxygenPreviewRefreshService _oxygenPreviewRefreshService;
        private readonly Action<Vector2Int> _onOxygenPreviewCleared;
        private BuildingDef _activeBuildingDef;
        private int _selectedUnitId;

        private ToolMode _currentToolMode = ToolMode.None;

        private bool _hasHoverCell;
        private Vector2Int _hoverCell;
        private readonly HashSet<Vector2Int> _currentCursorPreviewCells = new HashSet<Vector2Int>();

        private readonly HashSet<Vector2Int> _currentPreviewCells = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> _currentCancelPreviewCells = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> _currentBuildPreviewCells = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> _currentBuildPreviewAnchors = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> _currentInvalidBuildPreviewCells = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> _currentDestructionPreviewCells = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> _currentDestructionPreviewAnchors = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> _currentCablePlanPreviewCells = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> _currentCableCancelPreviewCells = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> _currentCableDestroyPreviewCells = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> _currentLifeModuleCancelPreviewCells = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> _currentWaterPlanPreviewCells = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> _currentWaterCancelPreviewCells = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> _currentWaterDestroyPreviewCells = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> _currentOxygenPlanPreviewCells = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> _currentOxygenCancelPreviewCells = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> _currentOxygenDestroyPreviewCells = new HashSet<Vector2Int>();
        private Vector2Int _lastCableDragCell;
        private bool _hasLastCableDragCell;
        private Vector2Int _lastWaterDragCell;
        private bool _hasLastWaterDragCell;
        private Vector2Int _lastOxygenDragCell;
        private bool _hasLastOxygenDragCell;
        private LifeModuleTaskPayload _stagedLifeModulePayload;
        private readonly Dictionary<int, LifeModuleTaskPayload> _lifeModuleCancelPayloadsByGroupId = new Dictionary<int, LifeModuleTaskPayload>();
        private readonly List<Vector2Int> _footprintBuffer = new List<Vector2Int>(16);
        private const bool ENABLE_BUILD_DEBUG_LOGS = true;
        private const int CABLE_BUILD_TICKS = 1;
        private const int CABLE_DESTROY_TICKS = 1;
        private const int WATER_BUILD_TICKS = 1;
        private const int WATER_DESTROY_TICKS = 1;
        private const int OXYGEN_BUILD_TICKS = 1;
        private const int OXYGEN_DESTROY_TICKS = 1;
        private static readonly Color VALID_BUILD_PREVIEW_COLOR = Color.white;
        private static readonly Color INVALID_BUILD_PREVIEW_COLOR = new Color(1f, 0.3f, 0.3f, 0.95f);

        public ToolMode CurrentToolMode => _currentToolMode;

        /// <summary>
        /// Runtime interaction and preview helper.
        /// </summary>
        public ToolInputInteractionService(
            GridState gridState,
            BuildingPlacementService buildingPlacementService,
            GridTileVisualService gridTileVisualService,
            ConstructionToolPanelController constructionToolPanelController,
            UnitTaskOrchestratorService unitTaskOrchestratorService,
            GlobalTaskBoardService globalTaskBoardService,
            SceneResourceObjectService sceneResourceObjectService,
            CablePreviewRefreshService cablePreviewRefreshService,
            Action<Vector2Int> onCablePreviewCleared,
            LifeModulePlacementService lifeModulePlacementService,
            LifeModulePreviewRefreshService lifeModulePreviewRefreshService,
            Action<LifeModuleTaskPayload> onLifeModulePreviewCleared,
            WaterPreviewRefreshService waterPreviewRefreshService,
            Action<Vector2Int> onWaterPreviewCleared,
            OxygenPreviewRefreshService oxygenPreviewRefreshService,
            Action<Vector2Int> onOxygenPreviewCleared,
            Func<Vector2Int, bool> shouldBlockNeutralCellClick,
            Func<int> getTickCounter)
        {
            _gridState = gridState;
            _buildingPlacementService = buildingPlacementService;
            _gridTileVisualService = gridTileVisualService;
            _constructionToolPanelController = constructionToolPanelController;
            _shouldBlockNeutralCellClick = shouldBlockNeutralCellClick;
            _unitTaskOrchestratorService = unitTaskOrchestratorService;
            _globalTaskBoardService = globalTaskBoardService;
            _sceneResourceObjectService = sceneResourceObjectService;
            _cablePreviewRefreshService = cablePreviewRefreshService;
            _onCablePreviewCleared = onCablePreviewCleared;
            _lifeModulePlacementService = lifeModulePlacementService;
            _lifeModulePreviewRefreshService = lifeModulePreviewRefreshService;
            _onLifeModulePreviewCleared = onLifeModulePreviewCleared;
            _waterPreviewRefreshService = waterPreviewRefreshService;
            _onWaterPreviewCleared = onWaterPreviewCleared;
            _oxygenPreviewRefreshService = oxygenPreviewRefreshService;
            _onOxygenPreviewCleared = onOxygenPreviewCleared;
            _getTickCounter = getTickCounter;
        }

        /// <summary>
        /// Runtime interaction and preview helper.
        /// </summary>
        public void HandleCellClicked(Vector2Int cell)
        {
            LogCellClickContext(cell);

            if (_currentToolMode == ToolMode.BuildCable)
            {
                if (!_gridState.IsInside(cell.x, cell.y)) return;

                ref readonly Cell clickedCell = ref _gridState.GetCell(cell.x, cell.y);
                if (clickedCell.HasCable) return;

                if (HasExistingCablePlanAt(cell))
                {
                    return;
                }

                bool queued = _globalTaskBoardService.TryPlanCableCell(cell, _getTickCounter(), CABLE_BUILD_TICKS);
                if (!queued) return;

                _currentCablePlanPreviewCells.Add(cell);
                _cablePreviewRefreshService.RefreshAround(cell, _currentCablePlanPreviewCells);
                _cablePreviewRefreshService.RebuildAllPlanned(_currentCablePlanPreviewCells);
                return;
            }

            if (_currentToolMode == ToolMode.CancelCablePlan)
            {
                if (!_gridState.IsInside(cell.x, cell.y)) return;

                bool cancelled = _globalTaskBoardService.CancelCableTaskByCell(cell, out _, out UnitTaskType cancelledType);
                if (!cancelled || cancelledType != UnitTaskType.BuildCable) return;

                _onCablePreviewCleared?.Invoke(cell);
                _cablePreviewRefreshService.RefreshAround(cell, _currentCablePlanPreviewCells);
                _cablePreviewRefreshService.RebuildAllPlanned(_currentCablePlanPreviewCells);
                return;
            }

            if (_currentToolMode == ToolMode.CancelLifeModulePlan)
            {
                if (!_gridState.IsInside(cell.x, cell.y)) return;

                if (!_globalTaskBoardService.CancelLifeModuleTaskByCell(cell, out LifeModuleTaskPayload cancelledPayload, out UnitTaskType cancelledType)
                    || cancelledType != UnitTaskType.BuildLifeModule)
                {
                    return;
                }

                ClearLifeModuleCancelPreviewMarkers();
                _onLifeModulePreviewCleared?.Invoke(cancelledPayload);
                return;
            }

            if (_currentToolMode == ToolMode.ExitCablePlan)
            {
                if (!_gridState.IsInside(cell.x, cell.y)) return;

                ref readonly Cell clickedCableCell = ref _gridState.GetCell(cell.x, cell.y);
                if (!clickedCableCell.HasCable) return;

                bool queuedDestroy = _globalTaskBoardService.TryCreateDestroyCableTask(cell, _getTickCounter(), CABLE_DESTROY_TICKS);
                if (!queuedDestroy) return;

                _gridTileVisualService.SetDestructionMarker(cell, true);
                return;
            }

            if (_currentToolMode == ToolMode.BuildWater)
            {
                if (!_gridState.IsInside(cell.x, cell.y)) return;

                ref readonly Cell clickedCell = ref _gridState.GetCell(cell.x, cell.y);
                if (clickedCell.HasWater) return;

                if (HasExistingWaterPlanAt(cell))
                {
                    return;
                }

                bool queued = _globalTaskBoardService.TryPlanWaterCell(cell, _getTickCounter(), WATER_BUILD_TICKS);
                if (!queued) return;

                _currentWaterPlanPreviewCells.Add(cell);
                _waterPreviewRefreshService.RefreshAround(cell, _currentWaterPlanPreviewCells);
                _waterPreviewRefreshService.RebuildAllPlanned(_currentWaterPlanPreviewCells);
                return;
            }

            if (_currentToolMode == ToolMode.CancelWaterPlan)
            {
                if (!_gridState.IsInside(cell.x, cell.y)) return;

                bool cancelled = _globalTaskBoardService.CancelWaterTaskByCell(cell, out _, out UnitTaskType cancelledType);
                if (!cancelled || cancelledType != UnitTaskType.BuildWater) return;

                _onWaterPreviewCleared?.Invoke(cell);
                _waterPreviewRefreshService.RefreshAround(cell, _currentWaterPlanPreviewCells);
                _waterPreviewRefreshService.RebuildAllPlanned(_currentWaterPlanPreviewCells);
                return;
            }

            if (_currentToolMode == ToolMode.ExitWaterPlan)
            {
                if (!_gridState.IsInside(cell.x, cell.y)) return;

                ref readonly Cell clickedWaterCell = ref _gridState.GetCell(cell.x, cell.y);
                if (!clickedWaterCell.HasWater) return;

                bool queuedDestroy = _globalTaskBoardService.TryCreateDestroyWaterTask(cell, _getTickCounter(), WATER_DESTROY_TICKS);
                if (!queuedDestroy) return;

                _gridTileVisualService.SetDestructionMarker(cell, true);
                return;
            }

            if (_currentToolMode == ToolMode.BuildOxygen)
            {
                if (!_gridState.IsInside(cell.x, cell.y)) return;

                ref readonly Cell clickedCell = ref _gridState.GetCell(cell.x, cell.y);
                if (clickedCell.HasOxygen) return;

                if (HasExistingOxygenPlanAt(cell))
                {
                    return;
                }

                bool queued = _globalTaskBoardService.TryPlanOxygenCell(cell, _getTickCounter(), OXYGEN_BUILD_TICKS);
                if (!queued) return;

                _currentOxygenPlanPreviewCells.Add(cell);
                _oxygenPreviewRefreshService.RefreshAround(cell, _currentOxygenPlanPreviewCells);
                _oxygenPreviewRefreshService.RebuildAllPlanned(_currentOxygenPlanPreviewCells);
                return;
            }

            if (_currentToolMode == ToolMode.CancelOxygenPlan)
            {
                if (!_gridState.IsInside(cell.x, cell.y)) return;

                bool cancelled = _globalTaskBoardService.CancelOxygenTaskByCell(cell, out _, out UnitTaskType cancelledType);
                if (!cancelled || cancelledType != UnitTaskType.BuildOxygen) return;

                _onOxygenPreviewCleared?.Invoke(cell);
                _oxygenPreviewRefreshService.RefreshAround(cell, _currentOxygenPlanPreviewCells);
                _oxygenPreviewRefreshService.RebuildAllPlanned(_currentOxygenPlanPreviewCells);
                return;
            }

            if (_currentToolMode == ToolMode.ExitOxygenPlan)
            {
                if (!_gridState.IsInside(cell.x, cell.y)) return;

                ref readonly Cell clickedOxygenCell = ref _gridState.GetCell(cell.x, cell.y);
                if (!clickedOxygenCell.HasOxygen) return;

                bool queuedDestroy = _globalTaskBoardService.TryCreateDestroyOxygenTask(cell, _getTickCounter(), OXYGEN_DESTROY_TICKS);
                if (!queuedDestroy) return;

                _gridTileVisualService.SetDestructionMarker(cell, true);
                return;
            }

            if (_currentToolMode != ToolMode.None) return;

            if (_shouldBlockNeutralCellClick != null && _shouldBlockNeutralCellClick(cell))
            {
                return;
            }

            if (_unitTaskOrchestratorService.TryGetUnitIdAtCell(cell, out int unitId))
            {
                _selectedUnitId = unitId;
                return;
            }

            if (_selectedUnitId == 0) return;
            _unitTaskOrchestratorService.TryIssueManualMoveCommand(_selectedUnitId, cell);
        }

        /// <summary>
        /// Runtime interaction and preview helper.
        /// </summary>
        private void LogCellClickContext(Vector2Int cell)
        {
            if (!_gridState.IsInside(cell.x, cell.y))
            {
            // Debug.LogWarning($"[CellClick] cell=({cell.x},{cell.y}) is outside grid.");
                return;
            }

            ref readonly Cell cellData = ref _gridState.GetCell(cell.x, cell.y);
            bool hasUnit = _unitTaskOrchestratorService.TryGetUnitIdAtCell(cell, out int unitIdAtCell);
            bool hasTask = _globalTaskBoardService.TryGetTaskByCell(cell, out UnitTaskRecord taskAtCell);
            bool isPlannedBuildCell = _buildingPlacementService.IsPlannedCell(cell);
            bool hasActiveBuilding = _buildingPlacementService.TryGetActiveBuildingByCell(cell, out BuildingRuntimeEntity activeBuilding);

            string taskInfo = hasTask
                ? $"id={taskAtCell.TaskId}, type={taskAtCell.TaskType}, status={taskAtCell.Status}, reservedBy={taskAtCell.ReservedByUnitId}, createdAtTick={taskAtCell.CreatedAtTick}, reserveTick={taskAtCell.ReserveTick}, parentBuildTaskId={taskAtCell.ParentBuildTaskId}, buildPayload={FormatBuildPayload(taskAtCell.BuildPayload)}"
                : "none";

            string buildingInfo = hasActiveBuilding
                ? $"{activeBuilding.BuildingDef?.ObjectType} anchor=({activeBuilding.AnchorCell.x},{activeBuilding.AnchorCell.y}) size={activeBuilding.Size.x}x{activeBuilding.Size.y} status={activeBuilding.Status}"
                : "none";

            string plannedConstructionInfo = BuildPlannedConstructionInfo(isPlannedBuildCell, taskAtCell);
            int droppedPrefabCount = 0;
            string droppedPrefabSummary = "none";
            if (_sceneResourceObjectService != null)
            {
                _sceneResourceObjectService.TryBuildCellPrefabSummary(cell, out droppedPrefabCount, out droppedPrefabSummary);
            }

            Debug.Log(
            $"[CellClick] cell=({cell.x},{cell.y})\n" +
            $"cellData: type={cellData.Type}, isDigMarked={cellData.IsDigMarked}, buildObjectType={cellData.BuildObjectType}, isOccupiedByBuilding={cellData.IsOccupiedByBuilding}, reservedByUnitId={cellData.ReservedByUnitId}, ignoreObstacleForPathfinding={cellData.IgnoreObstacleForPathfinding}, temperature={cellData.Temperature:0.##}, gravityVector=({cellData.GravityVector.x},{cellData.GravityVector.y}), gravityMagnitude={cellData.GravityMagnitude:0.##}, hasCable={cellData.HasCable}, lifeModuleType={cellData.LifeModuleType}, lifeModulePartType={cellData.LifeModulePartType}, lifeModuleGroupId={cellData.LifeModuleGroupId}, lifeModulePartWidth={cellData.LifeModulePartWidth}, lifeModulePartOrder={cellData.LifeModulePartOrder}, isLifeModulePartAnchor={cellData.IsLifeModulePartAnchor}\n" +
            $"runtimeContext: toolMode={_currentToolMode}, selectedUnit={_selectedUnitId}, unitAtCell={(hasUnit ? unitIdAtCell.ToString() : "none")}, plannedBuildCell={isPlannedBuildCell}\n" +
            $"droppedPrefabs: count={droppedPrefabCount}, byType={droppedPrefabSummary}\n" +
            $"plannedConstruction: {plannedConstructionInfo}\n" +
            $"taskAtCell: {taskInfo}\n" +
            $"activeBuilding: {buildingInfo}");
        }

        /// <summary>
        /// Runtime interaction and preview helper.
        /// </summary>
        private string BuildPlannedConstructionInfo(bool isPlannedBuildCell, UnitTaskRecord taskAtCell)
        {
            if (taskAtCell != null)
            {
                if (taskAtCell.TaskType == UnitTaskType.BuildObject && taskAtCell.BuildPayload != null)
                {
                    return FormatBuildPayload(taskAtCell.BuildPayload);
                }

                if (taskAtCell.ParentBuildTaskId != 0
                    && _globalTaskBoardService.TryGetTask(taskAtCell.ParentBuildTaskId, out UnitTaskRecord parentBuildTask)
                    && parentBuildTask != null
                    && parentBuildTask.TaskType == UnitTaskType.BuildObject
                    && parentBuildTask.BuildPayload != null)
                {
                    return FormatBuildPayload(parentBuildTask.BuildPayload);
                }
            }

            if (isPlannedBuildCell)
            {
                return "planned area=true, but no linked BuildObject task found by cell";
            }

            return "none";
        }

        private static string FormatBuildPayload(BuildTaskPayload payload)
        {
            if (payload == null)
            {
                return "none";
            }

            string buildingName = payload.BuildingDef != null ? payload.BuildingDef.name : "null";
            return $"building={buildingName}, anchor=({payload.AnchorCell.x},{payload.AnchorCell.y}), rotated={payload.IsRotated}, remainingBuildTicks={payload.RemainingBuildTicks}, remainingClearSubtasks={payload.RemainingClearSubtasks}, isExcavatingBeforeBuild={payload.IsExcavatingBeforeBuild}";
        }

        /// <summary>
        /// Runtime interaction and preview helper.
        /// </summary>
        public void HandleCellHovered(Vector2Int cell)
        {
            _hoverCell = cell;
            _hasHoverCell = true;
            UpdateCursorPreview();
            if (_currentToolMode == ToolMode.BuildLifeModule)
            {
                RefreshAllPlannedLifeModulePreviews();
            }
        }

        /// <summary>
        /// Runtime interaction and preview helper.
        /// </summary>
        public void HandleCellHoverExited()
        {
            _hasHoverCell = false;
            _gridTileVisualService.ClearCursorBuildPreviewLayer();
            UpdateCursorPreview();
            if (_currentToolMode == ToolMode.BuildLifeModule)
            {
                RefreshAllPlannedLifeModulePreviews();
            }
        }

        /// <summary>
        /// Runtime interaction and preview helper.
        /// </summary>
        public void HandleToolSelectionChanged(ToolMode toolMode, BuildingDef activeBuildingDef)
        {
            // Debug.Log($"[ToolMode] prev={_currentToolMode} next={toolMode}");
            _gridTileVisualService.ClearCursorBuildPreviewLayer();
            if (toolMode == ToolMode.ExitCablePlan)
            {
                ResetCableDragStroke();
                ClearActivePreview(true);
                RefreshAllPlannedBuildPreviews();
                _currentToolMode = ToolMode.ExitCablePlan;
                _activeBuildingDef = null;
                ToolModeChanged?.Invoke(_currentToolMode);
                UpdateCursorPreview();
                RefreshAllPlannedCablePreviews();
                return;
            }

            if (toolMode == ToolMode.ExitWaterPlan)
            {
                ResetWaterDragStroke();
                ClearActivePreview(true);
                RefreshAllPlannedBuildPreviews();
                _currentToolMode = ToolMode.ExitWaterPlan;
                _activeBuildingDef = null;
                ToolModeChanged?.Invoke(_currentToolMode);
                UpdateCursorPreview();
                RefreshAllPlannedWaterPreviews();
                return;
            }

            if (toolMode == ToolMode.ExitOxygenPlan)
            {
                ResetOxygenDragStroke();
                ClearActivePreview(true);
                RefreshAllPlannedBuildPreviews();
                _currentToolMode = ToolMode.ExitOxygenPlan;
                _activeBuildingDef = null;
                ToolModeChanged?.Invoke(_currentToolMode);
                UpdateCursorPreview();
                RefreshAllPlannedOxygenPreviews();
                return;
            }

            ResetCableDragStroke();
            ResetWaterDragStroke();
            ResetOxygenDragStroke();
            ClearActivePreview(true);
            RefreshAllPlannedBuildPreviews();
            _currentToolMode = toolMode;
            _activeBuildingDef = activeBuildingDef;
            ToolModeChanged?.Invoke(_currentToolMode);
            UpdateCursorPreview();
            RefreshAllPlannedCablePreviews();
            RefreshAllPlannedLifeModulePreviews();
            RefreshAllPlannedWaterPreviews();
            RefreshAllPlannedOxygenPreviews();
        }

        /// <summary>
        /// Runtime interaction and preview helper.
        /// </summary>
        public void HandleRightClickPressed()
        {
            // Debug.Log($"[ToolMode] reset by RMB. prev={_currentToolMode}");
            _gridTileVisualService.ClearCursorBuildPreviewLayer();
            ResetCableDragStroke();
            _currentToolMode = ToolMode.None;
            _activeBuildingDef = null;
            ToolModeChanged?.Invoke(_currentToolMode);
            _selectedUnitId = 0;
            _constructionToolPanelController?.ClearActiveBuildingDef();
            ClearActivePreview(true);
            RefreshAllPlannedBuildPreviews();
            ResetWaterDragStroke();
            ResetOxygenDragStroke();
            UpdateCursorPreview();
            RefreshAllPlannedCablePreviews();
            RefreshAllPlannedLifeModulePreviews();
            RefreshAllPlannedWaterPreviews();
            RefreshAllPlannedOxygenPreviews();
        }

        /// <summary>
        /// Runtime interaction and preview helper.
        /// </summary>
        public void HandleDragRectangleChanged(Vector2Int a, Vector2Int b)
        {
            if (!IsDragSupportedToolMode()) return;

            if (_currentToolMode == ToolMode.BuildCable || _currentToolMode == ToolMode.CancelCablePlan)
            {
                if (_gridState.IsInside(b.x, b.y))
                {
                    RenderCableStrokeTo(b);
                    RefreshAllPlannedCablePreviews();
                }
                return;
            }

            if (_currentToolMode == ToolMode.BuildWater || _currentToolMode == ToolMode.CancelWaterPlan)
            {
                if (_gridState.IsInside(b.x, b.y))
                {
                    RenderWaterStrokeTo(b);
                    RefreshAllPlannedWaterPreviews();
                }
                return;
            }

            if (_currentToolMode == ToolMode.BuildOxygen || _currentToolMode == ToolMode.CancelOxygenPlan)
            {
                if (_gridState.IsInside(b.x, b.y))
                {
                    RenderOxygenStrokeTo(b);
                    RefreshAllPlannedOxygenPreviews();
                }
                return;
            }

            if (_currentToolMode == ToolMode.BuildLifeModule)
            {
                ClearActivePreview(true);
                if (_lifeModulePlacementService != null
                    && _lifeModulePlacementService.TryCreatePayloadFromDrag(a, b, out LifeModuleTaskPayload payload))
                {
                    _stagedLifeModulePayload = payload;
                }

                RefreshAllPlannedLifeModulePreviews();
                UpdateCursorPreview();
                return;
            }

            if (_currentToolMode == ToolMode.CancelLifeModulePlan)
            {
                ClearActivePreview(true);
                CollectLifeModuleCancellationPreview(a, b);
                RefreshAllPlannedLifeModulePreviews();
                UpdateCursorPreview();
                return;
            }

            ClearActivePreview(true);
            RefreshAllPlannedBuildPreviews();

            if (_currentToolMode == ToolMode.BuildLadder)
            {
                // Лестницу можно планировать только вдоль вертикали: X берём из стартовой клетки,
                // а горизонтальное смещение курсора игнорируем. Все проверки placement остаются
                // внутри RenderRectanglePreviewCell и BuildingManager, поэтому это меняет только форму выбора.
                RenderVerticalLadderPreview(a, b);
                UpdateCursorPreview();
                return;
            }

            int minX = Mathf.Min(a.x, b.x);
            int maxX = Mathf.Max(a.x, b.x);
            int minY = Mathf.Min(a.y, b.y);
            int maxY = Mathf.Max(a.y, b.y);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    Vector2Int pos = new Vector2Int(x, y);
                    ref readonly Cell cell = ref _gridState.GetCell(x, y);
                    RenderRectanglePreviewCell(pos, cell);
                }
            }

            UpdateCursorPreview();
        }

        /// <summary>
        /// Рисует preview лестницы вертикальной линией от стартовой клетки до текущей клетки.
        /// </summary>
        private void RenderVerticalLadderPreview(Vector2Int start, Vector2Int end)
        {
            int minY = Mathf.Min(start.y, end.y);
            int maxY = Mathf.Max(start.y, end.y);

            for (int y = minY; y <= maxY; y++)
            {
                Vector2Int pos = new Vector2Int(start.x, y);
                if (!_gridState.IsInside(pos.x, pos.y))
                {
                    continue;
                }

                ref readonly Cell cell = ref _gridState.GetCell(pos.x, pos.y);
                RenderRectanglePreviewCell(pos, cell, start, end);
            }
        }

        /// <summary>
        /// Runtime interaction and preview helper.
        /// </summary>
        public void HandleLeftDragFinished()
        {
            if (!IsDragSupportedToolMode()) return;
            bool shouldExitBuildModeAfterCommit = IsBuildToolMode();

            if (shouldExitBuildModeAfterCommit)
            {
                CommitBuildPreviewSelection();
            }
            else if (_currentToolMode == ToolMode.DestroyObject)
            {
                CommitDestructionPreviewSelection();
            }
            else if (_currentToolMode == ToolMode.BuildCable)
            {
                CommitCablePlanPreviewSelection();
            }
            else if (_currentToolMode == ToolMode.CancelCablePlan)
            {
                CommitCableCancelPreviewSelection();
            }
            else if (_currentToolMode == ToolMode.BuildWater)
            {
                CommitWaterPlanPreviewSelection();
            }
            else if (_currentToolMode == ToolMode.BuildLifeModule)
            {
                CommitLifeModulePlanPreviewSelection();
            }
            else if (_currentToolMode == ToolMode.CancelLifeModulePlan)
            {
                CommitLifeModuleCancelPreviewSelection();
            }
            else if (_currentToolMode == ToolMode.CancelWaterPlan)
            {
                CommitWaterCancelPreviewSelection();
            }
            else if (_currentToolMode == ToolMode.BuildOxygen)
            {
                CommitOxygenPlanPreviewSelection();
            }
            else if (_currentToolMode == ToolMode.CancelOxygenPlan)
            {
                CommitOxygenCancelPreviewSelection();
            }
            else if (_currentToolMode == ToolMode.Shovel)
            {
                CommitDigPreviewSelection();
            }
            else if (_currentToolMode == ToolMode.ShovelCancel)
            {
                CommitCancelSelectionWithTaskRelease();
            }
            else
            {
                CommitCancelSelectionLocalMarksOnly();
            }

            ClearActivePreview(false);
            if (_currentToolMode == ToolMode.Shovel || _currentToolMode == ToolMode.ShovelCancel)
            {
                _gridTileVisualService.ClearDigPreview();
                _currentCursorPreviewCells.Clear();
            }
            RefreshAllPlannedBuildPreviews();
            ResetCableDragStroke();
            ResetWaterDragStroke();
            ResetOxygenDragStroke();

            if (shouldExitBuildModeAfterCommit)
            {
                // Перед выходом из build-режима явно очищаем курсорный слой preview (ReservedTilemap).
                _gridTileVisualService.ClearCursorBuildPreviewLayer();
                _currentCursorPreviewCells.Clear();

                // После ЛКМ в режиме строительства выходим из инструмента, чтобы не оставлять курсорное preview.
                _currentToolMode = ToolMode.None;
                _activeBuildingDef = null;
                ToolModeChanged?.Invoke(_currentToolMode);
                _constructionToolPanelController?.ClearActiveBuildingDef();
            }

            UpdateCursorPreview();
            RefreshAllPlannedCablePreviews();
            RefreshAllPlannedLifeModulePreviews();
            RefreshAllPlannedWaterPreviews();
            RefreshAllPlannedOxygenPreviews();
        }

        private void RenderCableStrokeTo(Vector2Int target)
        {
            if (!_hasLastCableDragCell)
            {
                ref readonly Cell cell = ref _gridState.GetCell(target.x, target.y);
                RenderRectanglePreviewCell(target, cell);
                _lastCableDragCell = target;
                _hasLastCableDragCell = true;
                return;
            }

            Vector2Int cursor = _lastCableDragCell;
            while (cursor != target)
            {
                if (cursor.x != target.x)
                {
                    cursor.x += target.x > cursor.x ? 1 : -1;
                }
                else if (cursor.y != target.y)
                {
                    cursor.y += target.y > cursor.y ? 1 : -1;
                }

                if (_gridState.IsInside(cursor.x, cursor.y))
                {
                    ref readonly Cell cell = ref _gridState.GetCell(cursor.x, cursor.y);
                    RenderRectanglePreviewCell(cursor, cell);
                }
            }

            _lastCableDragCell = target;
            _hasLastCableDragCell = true;
        }

        private void ResetCableDragStroke()
        {
            _hasLastCableDragCell = false;
            _lastCableDragCell = default;
        }

        private void RenderWaterStrokeTo(Vector2Int target)
        {
            if (!_hasLastWaterDragCell)
            {
                ref readonly Cell cell = ref _gridState.GetCell(target.x, target.y);
                RenderRectanglePreviewCell(target, cell);
                _lastWaterDragCell = target;
                _hasLastWaterDragCell = true;
                return;
            }

            Vector2Int cursor = _lastWaterDragCell;
            while (cursor != target)
            {
                if (cursor.x != target.x)
                {
                    cursor.x += target.x > cursor.x ? 1 : -1;
                }
                else if (cursor.y != target.y)
                {
                    cursor.y += target.y > cursor.y ? 1 : -1;
                }

                if (_gridState.IsInside(cursor.x, cursor.y))
                {
                    ref readonly Cell cell = ref _gridState.GetCell(cursor.x, cursor.y);
                    RenderRectanglePreviewCell(cursor, cell);
                }
            }

            _lastWaterDragCell = target;
            _hasLastWaterDragCell = true;
        }

        private void ResetWaterDragStroke()
        {
            _hasLastWaterDragCell = false;
            _lastWaterDragCell = default;
        }

        private void RenderOxygenStrokeTo(Vector2Int target)
        {
            if (!_hasLastOxygenDragCell)
            {
                ref readonly Cell cell = ref _gridState.GetCell(target.x, target.y);
                RenderRectanglePreviewCell(target, cell);
                _lastOxygenDragCell = target;
                _hasLastOxygenDragCell = true;
                return;
            }

            Vector2Int cursor = _lastOxygenDragCell;
            while (cursor != target)
            {
                if (cursor.x != target.x)
                {
                    cursor.x += target.x > cursor.x ? 1 : -1;
                }
                else if (cursor.y != target.y)
                {
                    cursor.y += target.y > cursor.y ? 1 : -1;
                }

                if (_gridState.IsInside(cursor.x, cursor.y))
                {
                    ref readonly Cell cell = ref _gridState.GetCell(cursor.x, cursor.y);
                    RenderRectanglePreviewCell(cursor, cell);
                }
            }

            _lastOxygenDragCell = target;
            _hasLastOxygenDragCell = true;
        }

        private void ResetOxygenDragStroke()
        {
            _hasLastOxygenDragCell = false;
            _lastOxygenDragCell = default;
        }

        /// <summary>
        /// Runtime interaction and preview helper.
        /// </summary>
        private bool IsDragSupportedToolMode()
        {
            return _currentToolMode == ToolMode.Shovel
                   || _currentToolMode == ToolMode.ShovelCancel
                   || _currentToolMode == ToolMode.BuildLadder
                   || _currentToolMode == ToolMode.BuildStorage
                   || _currentToolMode == ToolMode.BuildSolarPanel
                   || _currentToolMode == ToolMode.DestroyObject
                   || _currentToolMode == ToolMode.BuildRegolithProcessingUnit
                   || _currentToolMode == ToolMode.BuildSleepModule
                   || _currentToolMode == ToolMode.BuildBattery
                   || _currentToolMode == ToolMode.BuildDinner
                   || _currentToolMode == ToolMode.BuildOxygenStorage
                   || _currentToolMode == ToolMode.BuildOxigenProcessingUnit
                   || _currentToolMode == ToolMode.BuildWaterReclamation
                   || _currentToolMode == ToolMode.BuildWaterProcessingUnit
                   || _currentToolMode == ToolMode.BuildBridge
                   || _currentToolMode == ToolMode.BuildCable
                   || _currentToolMode == ToolMode.CancelCablePlan
                   || _currentToolMode == ToolMode.BuildLifeModule
                   || _currentToolMode == ToolMode.CancelLifeModulePlan
                   || _currentToolMode == ToolMode.BuildWater
                   || _currentToolMode == ToolMode.CancelWaterPlan
                   || _currentToolMode == ToolMode.BuildOxygen
                   || _currentToolMode == ToolMode.CancelOxygenPlan;
        }

        /// <summary>
        /// Runtime interaction and preview helper.
        /// </summary>
        private bool IsBuildToolMode()
        {
            return _currentToolMode == ToolMode.BuildLadder
                   || _currentToolMode == ToolMode.BuildStorage
                   || _currentToolMode == ToolMode.BuildSolarPanel
                   || _currentToolMode == ToolMode.BuildRegolithProcessingUnit
                   || _currentToolMode == ToolMode.BuildSleepModule
                   || _currentToolMode == ToolMode.BuildBattery
                   || _currentToolMode == ToolMode.BuildDinner
                   || _currentToolMode == ToolMode.BuildOxygenStorage
                   || _currentToolMode == ToolMode.BuildOxigenProcessingUnit
                   || _currentToolMode == ToolMode.BuildWaterReclamation
                   || _currentToolMode == ToolMode.BuildWaterProcessingUnit
                   || _currentToolMode == ToolMode.BuildBridge;
        }

        /// <summary>
        /// Runtime interaction and preview helper.
        /// </summary>
        private void RenderRectanglePreviewCell(
            Vector2Int pos,
            Cell cell,
            Vector2Int? ladderLineStart = null,
            Vector2Int? ladderLineEnd = null)
        {
            if (IsBuildToolMode())
            {
                if (_activeBuildingDef == null)
                {
                    return;
                }
                // Для Bridge не показываем превью поверх уже построенного Bridge.
                if (ShouldSuppressBridgePreview(pos))
                {
                    return;
                }

                _footprintBuffer.Clear();
                bool isPlaceable = _buildingPlacementService.TryGetPlaceableFootprint(_activeBuildingDef, pos, false, _footprintBuffer);
                if (!isPlaceable)
                {
                    FillFootprintCells(_activeBuildingDef, pos, false, _footprintBuffer);
                }

                // Stop extending ladder preview when inventory-backed plan capacity has already been exhausted.
                if (isPlaceable && !CanStageBuildPreviewAnchor(pos))
                {
                    return;
                }

                if (isPlaceable)
                {
                    _currentBuildPreviewAnchors.Add(pos);
                }

                TileBase previewTile = GetBuildPreviewTile(pos, ladderLineStart, ladderLineEnd);
                if (previewTile == null) return;

                int previewWidth = Mathf.Max(1, _activeBuildingDef != null ? _activeBuildingDef.Width : 1);
                int previewHeight = Mathf.Max(1, _activeBuildingDef != null ? _activeBuildingDef.Height : 1);
                _currentBuildPreviewCells.Add(pos);
                _gridTileVisualService.SetBuildPreviewTileByAnchor(
                    pos,
                    previewTile,
                    previewWidth,
                    previewHeight,
                    isPlaceable ? VALID_BUILD_PREVIEW_COLOR : INVALID_BUILD_PREVIEW_COLOR);

                if (!isPlaceable)
                {
                    for (int i = 0; i < _footprintBuffer.Count; i++)
                    {
                        Vector2Int invalidCell = _footprintBuffer[i];
                        _currentInvalidBuildPreviewCells.Add(invalidCell);
                        _gridTileVisualService.SetDestructionMarker(invalidCell, true);
                    }
                }
                return;
            }

            if (_currentToolMode == ToolMode.DestroyObject)
            {
                _footprintBuffer.Clear();
                if (!_buildingPlacementService.TryGetDestroyToolFootprint(pos, _footprintBuffer, out Vector2Int commandCell)) return;

                for (int i = 0; i < _footprintBuffer.Count; i++)
                {
                    Vector2Int footprintCell = _footprintBuffer[i];
                    _currentDestructionPreviewCells.Add(footprintCell);
                    _gridTileVisualService.SetDestructionMarker(footprintCell, true);
                }

                _currentDestructionPreviewAnchors.Add(commandCell);

                return;
            }

            if (_currentToolMode == ToolMode.BuildCable)
            {
                if (!_gridState.IsInside(pos.x, pos.y)) return;
                ref readonly Cell cableCell = ref _gridState.GetCell(pos.x, pos.y);
                if (cableCell.HasCable) return;
                
                if (HasExistingCablePlanAt(pos))
                {
                    _currentCablePlanPreviewCells.Add(pos);
                    _cablePreviewRefreshService.RefreshAround(pos, _currentCablePlanPreviewCells);
                    return;
                }

                if (!CanStageCablePreviewCell(pos))
                {
                    return;
                }

                _currentCablePlanPreviewCells.Add(pos);
                _cablePreviewRefreshService.RefreshAround(pos, _currentCablePlanPreviewCells);
                return;
            }

            if (_currentToolMode == ToolMode.CancelCablePlan)
            {
                if (!_globalTaskBoardService.TryGetCableTaskByCell(pos, out UnitTaskRecord taskToCancel)
                    || taskToCancel == null
                    || taskToCancel.TaskType != UnitTaskType.BuildCable
                    || taskToCancel.Status == UnitTaskStatus.Completed
                    || taskToCancel.Status == UnitTaskStatus.Failed)
                {
                    return;
                }

                // Cancel immediately on hover/drag cell hit, without waiting for mouse button release.
                bool cancelled = _globalTaskBoardService.CancelCableTaskByCell(pos, out _, out UnitTaskType cancelledType);
                if (!cancelled || cancelledType != UnitTaskType.BuildCable)
                {
                    return;
                }

                _currentCablePlanPreviewCells.Remove(pos);
                _currentCableCancelPreviewCells.Remove(pos);
                _gridTileVisualService.SetDestructionMarker(pos, false);
                _onCablePreviewCleared?.Invoke(pos);
                _cablePreviewRefreshService.RefreshAround(pos, _currentCablePlanPreviewCells);
                _cablePreviewRefreshService.RebuildAllPlanned(_currentCablePlanPreviewCells);
                return;
            }

            if (_currentToolMode == ToolMode.BuildLifeModule)
            {
                return;
            }

            if (_currentToolMode == ToolMode.CancelLifeModulePlan)
            {
                if (!_globalTaskBoardService.TryGetLifeModuleTaskByCell(pos, out UnitTaskRecord taskToCancel)
                    || taskToCancel == null
                    || taskToCancel.TaskType != UnitTaskType.BuildLifeModule
                    || taskToCancel.Status == UnitTaskStatus.Completed
                    || taskToCancel.Status == UnitTaskStatus.Failed
                    || taskToCancel.LifeModulePayload == null)
                {
                    return;
                }

                TrackLifeModuleCancellationPayload(taskToCancel.LifeModulePayload);
                return;
            }

            if (_currentToolMode == ToolMode.BuildWater)
            {
                if (cell.HasWater) return;

                if (HasExistingWaterPlanAt(pos))
                {
                    _currentWaterPlanPreviewCells.Add(pos);
                    _waterPreviewRefreshService.RefreshAround(pos, _currentWaterPlanPreviewCells);
                    return;
                }

                if (!CanStageWaterPreviewCell(pos))
                {
                    return;
                }

                _currentWaterPlanPreviewCells.Add(pos);
                _waterPreviewRefreshService.RefreshAround(pos, _currentWaterPlanPreviewCells);
                return;
            }

            if (_currentToolMode == ToolMode.CancelWaterPlan)
            {
                if (!HasExistingWaterPlanAt(pos))
                {
                    return;
                }

                // Cancel immediately on hover/drag cell hit, without waiting for mouse button release.
                bool cancelled = _globalTaskBoardService.CancelWaterTaskByCell(pos, out _, out UnitTaskType cancelledType);
                if (!cancelled || cancelledType != UnitTaskType.BuildWater)
                {
                    return;
                }

                _currentWaterPlanPreviewCells.Remove(pos);
                _currentWaterCancelPreviewCells.Remove(pos);
                _gridTileVisualService.SetDestructionMarker(pos, false);
                _onWaterPreviewCleared?.Invoke(pos);
                _waterPreviewRefreshService.RefreshAround(pos, _currentWaterPlanPreviewCells);
                _waterPreviewRefreshService.RebuildAllPlanned(_currentWaterPlanPreviewCells);
                return;
            }

            if (_currentToolMode == ToolMode.BuildOxygen)
            {
                if (cell.HasOxygen) return;

                if (HasExistingOxygenPlanAt(pos))
                {
                    _currentOxygenPlanPreviewCells.Add(pos);
                    _oxygenPreviewRefreshService.RefreshAround(pos, _currentOxygenPlanPreviewCells);
                    return;
                }

                if (!CanStageOxygenPreviewCell(pos))
                {
                    return;
                }

                _currentOxygenPlanPreviewCells.Add(pos);
                _oxygenPreviewRefreshService.RefreshAround(pos, _currentOxygenPlanPreviewCells);
                return;
            }

            if (_currentToolMode == ToolMode.CancelOxygenPlan)
            {
                if (!HasExistingOxygenPlanAt(pos))
                {
                    return;
                }

                // Cancel immediately on hover/drag cell hit, without waiting for mouse button release.
                bool cancelled = _globalTaskBoardService.CancelOxygenTaskByCell(pos, out _, out UnitTaskType cancelledType);
                if (!cancelled || cancelledType != UnitTaskType.BuildOxygen)
                {
                    return;
                }

                _currentOxygenPlanPreviewCells.Remove(pos);
                _currentOxygenCancelPreviewCells.Remove(pos);
                _gridTileVisualService.SetDestructionMarker(pos, false);
                _onOxygenPreviewCleared?.Invoke(pos);
                _oxygenPreviewRefreshService.RefreshAround(pos, _currentOxygenPlanPreviewCells);
                _oxygenPreviewRefreshService.RebuildAllPlanned(_currentOxygenPlanPreviewCells);
                return;
            }

            if (_currentToolMode == ToolMode.Shovel)
            {
                _currentPreviewCells.Add(pos);
                _gridTileVisualService.SetDigPreview(pos, ResolveDigPreviewVisualKind(pos, cell));
                return;
            }

            if (!HasAnyMark(cell)) return;
            _currentCancelPreviewCells.Add(pos);
            _gridTileVisualService.SetTaskMarker(pos, false);
        }

        /// <summary>
        /// Resolves a build preview tile, including the correct ladder end/center variant.
        /// </summary>
        private TileBase GetBuildPreviewTile(BuildingDef buildingDef, Vector2Int cell, Vector2Int? ladderLineStart, Vector2Int? ladderLineEnd)
        {
            if (buildingDef == null)
            {
                return null;
            }

            if (buildingDef.ObjectType != BuildObjectType.Ladder)
            {
                return _buildingPlacementService.GetPreviewTile(buildingDef);
            }

            // A ladder object must use the specialized definition so ladder visual settings cannot leak into other buildings.
            LadderBuildingDef ladderBuildingDef = (LadderBuildingDef)buildingDef;
            bool hasLadderBelow = IsLadderForPreview(cell + Vector2Int.down, ladderLineStart, ladderLineEnd);
            bool hasLadderAbove = IsLadderForPreview(cell + Vector2Int.up, ladderLineStart, ladderLineEnd);
            Sprite previewSprite = ladderBuildingDef.ResolveLadderSprite(hasLadderBelow, hasLadderAbove, true);
            return _buildingPlacementService.GetPreviewTile(buildingDef, previewSprite);
        }

        private TileBase GetBuildPreviewTile(Vector2Int cell, Vector2Int? ladderLineStart, Vector2Int? ladderLineEnd)
        {
            return GetBuildPreviewTile(_activeBuildingDef, cell, ladderLineStart, ladderLineEnd);
        }

        /// <summary>
        /// Checks both built ladders and cells included in the current vertical preview line.
        /// </summary>
        private bool IsLadderForPreview(Vector2Int cell, Vector2Int? ladderLineStart, Vector2Int? ladderLineEnd)
        {
            if (!_gridState.IsInside(cell.x, cell.y))
            {
                return false;
            }

            ref readonly Cell gridCell = ref _gridState.GetCell(cell.x, cell.y);
            if (gridCell.BuildObjectType.HasValue && gridCell.BuildObjectType.Value == BuildObjectType.Ladder)
            {
                return true;
            }

            if (!ladderLineStart.HasValue || !ladderLineEnd.HasValue)
            {
                return false;
            }

            Vector2Int start = ladderLineStart.Value;
            Vector2Int end = ladderLineEnd.Value;
            return cell.x == start.x
                && cell.y >= Mathf.Min(start.y, end.y)
                && cell.y <= Mathf.Max(start.y, end.y);
        }

        /// <summary>
        /// Runtime interaction and preview helper.
        /// </summary>
        private void CommitBuildPreviewSelection()
        {
            foreach (Vector2Int anchorCell in _currentBuildPreviewAnchors)
            {
                bool queued = _buildingPlacementService.TryQueueBuild(
                    _activeBuildingDef,
                    anchorCell,
                    _getTickCounter());
                if (!queued) continue;

                _footprintBuffer.Clear();
                FillFootprintCells(_activeBuildingDef, anchorCell, false, _footprintBuffer);

                for (int i = 0; i < _footprintBuffer.Count; i++)
                {
                    _gridTileVisualService.SetBuildTaskMarker(_footprintBuffer[i], true);
                }

                if (ENABLE_BUILD_DEBUG_LOGS)
                {
            // Debug.Log($"[BuildDebug] Queued '{_activeBuildingDef?.name}' anchor=({anchorCell.x},{anchorCell.y}) size={_activeBuildingDef?.Width}x{_activeBuildingDef?.Height} footprintCells={_footprintBuffer.Count}");
                }
            }
        }

        /// <summary>
        /// Runtime interaction and preview helper.
        /// </summary>
        private void CommitDestructionPreviewSelection()
        {
            foreach (Vector2Int anchorCell in _currentDestructionPreviewAnchors)
            {
                // Planned constructions are cancelled by the same destroy action before the object becomes active.
                if (_buildingPlacementService.IsPlannedCell(anchorCell))
                {
                    _buildingPlacementService.TryCancelTaskAndReleasePlannedArea(anchorCell);
                    continue;
                }

                _buildingPlacementService.TryQueueDestroy(anchorCell, _getTickCounter());
            }
        }

        /// <summary>
        /// Runtime interaction and preview helper.
        /// </summary>
        private void CommitCablePlanPreviewSelection()
        {
            foreach (Vector2Int cell in _currentCablePlanPreviewCells)
            {
                if (HasExistingCablePlanAt(cell))
                {
                    _cablePreviewRefreshService.RefreshAround(cell, _currentCablePlanPreviewCells);
                    continue;
                }

                bool queued = _globalTaskBoardService.TryPlanCableCell(cell, _getTickCounter(), CABLE_BUILD_TICKS);
                if (!queued)
                {
                    _cablePreviewRefreshService.ClearCellState(cell);
                    _gridTileVisualService.SetCablePreview(cell, false, 0);
                    continue;
                }

                _cablePreviewRefreshService.RefreshAround(cell, _currentCablePlanPreviewCells);
            }
        }

        /// <summary>
        /// Runtime interaction and preview helper.
        /// </summary>
        private void CommitCableCancelPreviewSelection()
        {
            foreach (Vector2Int cell in _currentCableCancelPreviewCells)
            {
                bool cancelled = _globalTaskBoardService.CancelCableTaskByCell(cell, out _, out UnitTaskType cancelledType);
                _gridTileVisualService.SetDestructionMarker(cell, false);
                if (!cancelled || cancelledType != UnitTaskType.BuildCable)
                {
                    continue;
                }

                _onCablePreviewCleared?.Invoke(cell);
            }
        }

        private void CommitLifeModulePlanPreviewSelection()
        {
            if (_stagedLifeModulePayload == null || _lifeModulePlacementService == null)
            {
                return;
            }

            if (_lifeModulePlacementService.TryQueueBuild(_stagedLifeModulePayload, _getTickCounter(), out _))
            {
                _stagedLifeModulePayload = null;
            }

            RefreshAllPlannedLifeModulePreviews();
        }

        private void CommitLifeModuleCancelPreviewSelection()
        {
            foreach (KeyValuePair<int, LifeModuleTaskPayload> pair in _lifeModuleCancelPayloadsByGroupId)
            {
                LifeModuleTaskPayload payload = pair.Value;
                if (payload?.OccupiedCells == null || payload.OccupiedCells.Length == 0)
                {
                    continue;
                }

                if (!_globalTaskBoardService.CancelLifeModuleTaskByCell(
                        payload.OccupiedCells[0],
                        out LifeModuleTaskPayload cancelledPayload,
                        out UnitTaskType cancelledType)
                    || cancelledType != UnitTaskType.BuildLifeModule)
                {
                    continue;
                }

                _onLifeModulePreviewCleared?.Invoke(cancelledPayload);
            }

            ClearLifeModuleCancelPreviewMarkers();
            RefreshAllPlannedLifeModulePreviews();
        }

        private void CommitWaterPlanPreviewSelection()
        {
            foreach (Vector2Int cell in _currentWaterPlanPreviewCells)
            {
                if (HasExistingWaterPlanAt(cell))
                {
                    _waterPreviewRefreshService.RefreshAround(cell, _currentWaterPlanPreviewCells);
                    continue;
                }

                bool queued = _globalTaskBoardService.TryPlanWaterCell(cell, _getTickCounter(), WATER_BUILD_TICKS);
                if (!queued)
                {
                    _waterPreviewRefreshService.ClearCellState(cell);
                    _gridTileVisualService.SetWaterPreview(cell, false, 0);
                    continue;
                }

                _waterPreviewRefreshService.RefreshAround(cell, _currentWaterPlanPreviewCells);
            }
        }

        private void CommitWaterCancelPreviewSelection()
        {
            foreach (Vector2Int cell in _currentWaterCancelPreviewCells)
            {
                bool cancelled = _globalTaskBoardService.CancelWaterTaskByCell(cell, out _, out UnitTaskType cancelledType);
                _gridTileVisualService.SetDestructionMarker(cell, false);
                if (!cancelled || cancelledType != UnitTaskType.BuildWater)
                {
                    continue;
                }

                _onWaterPreviewCleared?.Invoke(cell);
            }
        }

        private void CommitOxygenPlanPreviewSelection()
        {
            foreach (Vector2Int cell in _currentOxygenPlanPreviewCells)
            {
                if (HasExistingOxygenPlanAt(cell))
                {
                    _oxygenPreviewRefreshService.RefreshAround(cell, _currentOxygenPlanPreviewCells);
                    continue;
                }

                bool queued = _globalTaskBoardService.TryPlanOxygenCell(cell, _getTickCounter(), OXYGEN_BUILD_TICKS);
                if (!queued)
                {
                    _oxygenPreviewRefreshService.ClearCellState(cell);
                    _gridTileVisualService.SetOxygenPreview(cell, false, 0);
                    continue;
                }

                _oxygenPreviewRefreshService.RefreshAround(cell, _currentOxygenPlanPreviewCells);
            }
        }

        private void CommitOxygenCancelPreviewSelection()
        {
            foreach (Vector2Int cell in _currentOxygenCancelPreviewCells)
            {
                bool cancelled = _globalTaskBoardService.CancelOxygenTaskByCell(cell, out _, out UnitTaskType cancelledType);
                _gridTileVisualService.SetDestructionMarker(cell, false);
                if (!cancelled || cancelledType != UnitTaskType.BuildOxygen)
                {
                    continue;
                }

                _onOxygenPreviewCleared?.Invoke(cell);
            }
        }

        /// <summary>
        /// Runtime interaction and preview helper.
        /// </summary>
        private void CommitDigPreviewSelection()
        {
            foreach (Vector2Int pos in _currentPreviewCells)
            {
                if (ShipLandingZoneRules.IsInsideDigProtectionZone(_gridState.Width, _gridState.Height, pos)) continue;
                Cell cell = _gridState.GetCell(pos.x, pos.y);
                if (!CanDig(cell.Type) || cell.IsDigMarked) continue;

                cell.IsDigMarked = true;
                _gridState.SetCell(pos.x, pos.y, cell);
                _globalTaskBoardService.TryActivateDigTaskIfReachable(pos, _getTickCounter());
                _gridTileVisualService.SetTaskMarker(pos, true);
            }
        }

        
        /// <summary>
        /// Runtime interaction and preview helper.
        /// </summary>
        private void RefreshAllPlannedCablePreviews()
        {
            _cablePreviewRefreshService.RebuildAllPlanned(_currentCablePlanPreviewCells);
        }

        private bool CanStageCablePreviewCell(Vector2Int cellPos)
        {
            if (_currentCablePlanPreviewCells.Contains(cellPos))
            {
                return true;
            }

            if (HasOpenCableTaskAt(cellPos))
            {
                return true;
            }

            int stagedNewCableCells = CountStagedNewCablePreviewCells();
            int availableCablePlans = _globalTaskBoardService.GetAvailableCableBuildPlanCount();
            return stagedNewCableCells < availableCablePlans;
        }

        private int CountStagedNewCablePreviewCells()
        {
            int count = 0;
            foreach (Vector2Int stagedCell in _currentCablePlanPreviewCells)
            {
                if (HasExistingCablePlanAt(stagedCell))
                {
                    continue;
                }

                count++;
            }

            return count;
        }

        private bool HasOpenCableTaskAt(Vector2Int cellPos)
        {
            return _globalTaskBoardService.TryGetCableTaskByCell(cellPos, out UnitTaskRecord task)
                   && task != null
                   && task.TaskType == UnitTaskType.BuildCable
                   && task.Status != UnitTaskStatus.Completed
                   && task.Status != UnitTaskStatus.Failed;
        }

        private bool HasExistingCablePlanAt(Vector2Int cellPos)
        {
            if (!_gridState.IsInside(cellPos.x, cellPos.y))
            {
                return false;
            }

            ref readonly Cell cell = ref _gridState.GetCell(cellPos.x, cellPos.y);
            if (cell.IsCableMarked)
            {
                return true;
            }

            return HasOpenCableTaskAt(cellPos);
        }

        private void RefreshAllPlannedLifeModulePreviews()
        {
            _lifeModulePreviewRefreshService?.RebuildPreview(GetActiveLifeModulePreviewPayload());
        }

        private LifeModuleTaskPayload GetActiveLifeModulePreviewPayload()
        {
            if (_stagedLifeModulePayload != null)
            {
                return _stagedLifeModulePayload;
            }

            if (_currentToolMode != ToolMode.BuildLifeModule || !_hasHoverCell || _lifeModulePlacementService == null)
            {
                return null;
            }

            if (!_gridState.IsInside(_hoverCell.x, _hoverCell.y))
            {
                return null;
            }

            // Before the drag starts we still want a live M1 cursor preview at the hovered cell.
            if (_lifeModulePlacementService.TryCreatePayloadFromDrag(_hoverCell, _hoverCell, out LifeModuleTaskPayload cursorPayload))
            {
                return cursorPayload;
            }

            return null;
        }

        private void RefreshAllPlannedWaterPreviews()
        {
            _waterPreviewRefreshService.RebuildAllPlanned(_currentWaterPlanPreviewCells);
        }

        private bool CanStageWaterPreviewCell(Vector2Int cellPos)
        {
            if (_currentWaterPlanPreviewCells.Contains(cellPos))
            {
                return true;
            }

            if (HasOpenWaterTaskAt(cellPos))
            {
                return true;
            }

            int stagedNewWaterCells = CountStagedNewWaterPreviewCells();
            int availableWaterPlans = _globalTaskBoardService.GetAvailableWaterBuildPlanCount();
            return stagedNewWaterCells < availableWaterPlans;
        }

        private int CountStagedNewWaterPreviewCells()
        {
            int count = 0;
            foreach (Vector2Int stagedCell in _currentWaterPlanPreviewCells)
            {
                if (HasExistingWaterPlanAt(stagedCell))
                {
                    continue;
                }

                count++;
            }

            return count;
        }

        private bool HasOpenWaterTaskAt(Vector2Int cellPos)
        {
            return _globalTaskBoardService.TryGetWaterTaskByCell(cellPos, out UnitTaskRecord task)
                   && task != null
                   && task.TaskType == UnitTaskType.BuildWater
                   && task.Status != UnitTaskStatus.Completed
                   && task.Status != UnitTaskStatus.Failed;
        }

        private bool HasExistingWaterPlanAt(Vector2Int cellPos)
        {
            if (!_gridState.IsInside(cellPos.x, cellPos.y))
            {
                return false;
            }

            ref readonly Cell cell = ref _gridState.GetCell(cellPos.x, cellPos.y);
            if (cell.IsWaterMarked)
            {
                return true;
            }

            return HasOpenWaterTaskAt(cellPos);
        }

        private void RefreshAllPlannedOxygenPreviews()
        {
            _oxygenPreviewRefreshService.RebuildAllPlanned(_currentOxygenPlanPreviewCells);
        }

        private bool CanStageOxygenPreviewCell(Vector2Int cellPos)
        {
            if (_currentOxygenPlanPreviewCells.Contains(cellPos))
            {
                return true;
            }

            if (HasOpenOxygenTaskAt(cellPos))
            {
                return true;
            }

            int stagedNewOxygenCells = CountStagedNewOxygenPreviewCells();
            int availableOxygenPlans = _globalTaskBoardService.GetAvailableOxygenBuildPlanCount();
            return stagedNewOxygenCells < availableOxygenPlans;
        }

        private int CountStagedNewOxygenPreviewCells()
        {
            int count = 0;
            foreach (Vector2Int stagedCell in _currentOxygenPlanPreviewCells)
            {
                if (HasExistingOxygenPlanAt(stagedCell))
                {
                    continue;
                }

                count++;
            }

            return count;
        }

        private bool HasOpenOxygenTaskAt(Vector2Int cellPos)
        {
            return _globalTaskBoardService.TryGetOxygenTaskByCell(cellPos, out UnitTaskRecord task)
                   && task != null
                   && task.TaskType == UnitTaskType.BuildOxygen
                   && task.Status != UnitTaskStatus.Completed
                   && task.Status != UnitTaskStatus.Failed;
        }

        private bool HasExistingOxygenPlanAt(Vector2Int cellPos)
        {
            if (!_gridState.IsInside(cellPos.x, cellPos.y))
            {
                return false;
            }

            ref readonly Cell cell = ref _gridState.GetCell(cellPos.x, cellPos.y);
            if (cell.IsOxygenMarked)
            {
                return true;
            }

            return HasOpenOxygenTaskAt(cellPos);
        }

        /// <summary>
        /// Runtime interaction and preview helper.
        /// </summary>
        private bool CanStageBuildPreviewAnchor(Vector2Int anchorCell)
        {
            if (!UsesResourceLimitedBuildPreview(_activeBuildingDef))
            {
                return true;
            }

            if (_currentBuildPreviewAnchors.Contains(anchorCell))
            {
                return true;
            }

            int availableBuildPlans = _buildingPlacementService.GetAvailableBuildPlanCount(_activeBuildingDef);
            if (availableBuildPlans <= 0)
            {
                return false;
            }

            return _currentBuildPreviewAnchors.Count < availableBuildPlans;
        }

        private static bool UsesResourceLimitedBuildPreview(BuildingDef buildingDef)
        {
            return buildingDef != null
                   && buildingDef.ObjectType == BuildObjectType.Ladder;
        }

        private void RefreshAllPlannedBuildPreviews()
        {
            var activeTasks = _globalTaskBoardService.GetActiveTasksSnapshot();
            for (int i = 0; i < activeTasks.Count; i++)
            {
                UnitTaskRecord task = activeTasks[i];
                if (task == null || task.TaskType != UnitTaskType.BuildObject) continue;

                BuildTaskPayload payload = task.BuildPayload;
                if (payload == null || payload.BuildingDef == null) continue;

                TileBase previewTile = GetBuildPreviewTile(payload.BuildingDef, payload.AnchorCell, null, null);
                if (previewTile == null) continue;

                int previewWidth = Mathf.Max(1, payload.IsRotated ? payload.BuildingDef.Height : payload.BuildingDef.Width);
                int previewHeight = Mathf.Max(1, payload.IsRotated ? payload.BuildingDef.Width : payload.BuildingDef.Height);
                _gridTileVisualService.SetBuildPreviewTileByAnchor(
                    payload.AnchorCell,
                    previewTile,
                    previewWidth,
                    previewHeight,
                    VALID_BUILD_PREVIEW_COLOR);
            }
        }
        
        
        
        /// <summary>
        /// Runtime interaction and preview helper.
        /// </summary>
        private void CommitCancelSelectionWithTaskRelease()
        {
            foreach (Vector2Int pos in _currentCancelPreviewCells)
            {
                bool cancelled = _buildingPlacementService.TryCancelTaskAndReleasePlannedArea(pos);
                if (!cancelled) continue;

                Cell cell = _gridState.GetCell(pos.x, pos.y);
                if (cell.IsDigMarked)
                {
                    cell.IsDigMarked = false;
                    _gridState.SetCell(pos.x, pos.y, cell);
                }

                _gridTileVisualService.SetTaskMarker(pos, false);
                _gridTileVisualService.SetBuildTaskMarker(pos, false);
            }
        }

        /// <summary>
        /// Runtime interaction and preview helper.
        /// </summary>
        private void CommitCancelSelectionLocalMarksOnly()
        {
            foreach (Vector2Int pos in _currentCancelPreviewCells)
            {
                Cell cell = _gridState.GetCell(pos.x, pos.y);
                if (!HasAnyMark(cell)) continue;

                ClearAllMarks(ref cell);
                _gridState.SetCell(pos.x, pos.y, cell);
                _gridTileVisualService.SetTaskMarker(pos, false);
            }
        }

        /// <summary>
        /// Runtime interaction and preview helper.
        /// </summary>
        private void ClearActivePreview(bool restoreCancelMarkers)
        {
            foreach (Vector2Int cell in _currentPreviewCells)
            {
                _gridTileVisualService.SetDigPreview(cell, false);
            }
            _currentPreviewCells.Clear();

            if (restoreCancelMarkers)
            {
                foreach (Vector2Int cell in _currentCancelPreviewCells)
                {
                    _gridTileVisualService.SetTaskMarker(cell, true);
                }
            }
            _currentCancelPreviewCells.Clear();

            foreach (Vector2Int cell in _currentBuildPreviewCells)
            {
                // Не затираем preview уже запланированной стройки:
                // planned-слой должен жить до завершения/отмены задачи.
                if (_buildingPlacementService.IsPlannedCell(cell))
                {
                    continue;
                }

                _gridTileVisualService.SetBuildPreviewTile(cell, null);
            }
            _currentBuildPreviewCells.Clear();
            _currentBuildPreviewAnchors.Clear();
            foreach (Vector2Int cell in _currentInvalidBuildPreviewCells)
            {
                _gridTileVisualService.SetDestructionMarker(cell, false);
            }
            _currentInvalidBuildPreviewCells.Clear();

            if (restoreCancelMarkers)
            {
                foreach (Vector2Int cell in _currentDestructionPreviewCells)
                {
                    _gridTileVisualService.SetDestructionMarker(cell, false);
                }
            }
            _currentDestructionPreviewCells.Clear();
            _currentDestructionPreviewAnchors.Clear();

            foreach (Vector2Int cell in _currentCablePlanPreviewCells)
            {
                ref readonly Cell gridCell = ref _gridState.GetCell(cell.x, cell.y);
                bool hasOpenCableTask = _globalTaskBoardService.TryGetCableTaskByCell(cell, out UnitTaskRecord task)
                    && task != null
                    && task.TaskType == UnitTaskType.BuildCable
                    && task.Status != UnitTaskStatus.Completed
                    && task.Status != UnitTaskStatus.Failed;
                if (gridCell.IsCableMarked || hasOpenCableTask)
                {
                    continue;
                }

                _cablePreviewRefreshService.ClearCellState(cell);
                _gridTileVisualService.SetCablePreview(cell, false, 0);
            }
            _currentCablePlanPreviewCells.Clear();

            foreach (Vector2Int cell in _currentCableCancelPreviewCells)
            {
                _gridTileVisualService.SetDestructionMarker(cell, false);
            }
            _currentCableCancelPreviewCells.Clear();

            _stagedLifeModulePayload = null;
            _lifeModulePreviewRefreshService?.RebuildPreview();
            ClearLifeModuleCancelPreviewMarkers();

            foreach (Vector2Int cell in _currentWaterPlanPreviewCells)
            {
                ref readonly Cell gridCell = ref _gridState.GetCell(cell.x, cell.y);
                bool hasOpenWaterTask = _globalTaskBoardService.TryGetWaterTaskByCell(cell, out UnitTaskRecord task)
                    && task != null
                    && task.TaskType == UnitTaskType.BuildWater
                    && task.Status != UnitTaskStatus.Completed
                    && task.Status != UnitTaskStatus.Failed;
                if (gridCell.IsWaterMarked || hasOpenWaterTask)
                {
                    continue;
                }

                _waterPreviewRefreshService.ClearCellState(cell);
                _gridTileVisualService.SetWaterPreview(cell, false, 0);
            }
            _currentWaterPlanPreviewCells.Clear();

            foreach (Vector2Int cell in _currentWaterCancelPreviewCells)
            {
                _gridTileVisualService.SetDestructionMarker(cell, false);
            }
            _currentWaterCancelPreviewCells.Clear();

            foreach (Vector2Int cell in _currentOxygenPlanPreviewCells)
            {
                ref readonly Cell gridCell = ref _gridState.GetCell(cell.x, cell.y);
                bool hasOpenOxygenTask = _globalTaskBoardService.TryGetOxygenTaskByCell(cell, out UnitTaskRecord task)
                    && task != null
                    && task.TaskType == UnitTaskType.BuildOxygen
                    && task.Status != UnitTaskStatus.Completed
                    && task.Status != UnitTaskStatus.Failed;
                if (gridCell.IsOxygenMarked || hasOpenOxygenTask)
                {
                    continue;
                }

                _oxygenPreviewRefreshService.ClearCellState(cell);
                _gridTileVisualService.SetOxygenPreview(cell, false, 0);
            }
            _currentOxygenPlanPreviewCells.Clear();

            foreach (Vector2Int cell in _currentOxygenCancelPreviewCells)
            {
                _gridTileVisualService.SetDestructionMarker(cell, false);
            }
            _currentOxygenCancelPreviewCells.Clear();

            foreach (Vector2Int cursorCell in _currentCursorPreviewCells)
            {
                if (IsBuildToolMode())
                {
                    _gridTileVisualService.SetCursorBuildPreviewTile(cursorCell, null);
                }
                else if (_currentToolMode == ToolMode.DestroyObject)
                {
                    _gridTileVisualService.SetDestructionMarker(cursorCell, false);
                }
                else if (_currentToolMode == ToolMode.BuildCable)
                {
                    _cablePreviewRefreshService.HideIfNotPlanned(cursorCell, _currentCablePlanPreviewCells);
                }
                else if (_currentToolMode == ToolMode.CancelCablePlan)
                {
                    _gridTileVisualService.SetDestructionMarker(cursorCell, false);
                }
                else if (_currentToolMode == ToolMode.BuildWater)
                {
                    _waterPreviewRefreshService.HideIfNotPlanned(cursorCell, _currentWaterPlanPreviewCells);
                }
                else if (_currentToolMode == ToolMode.CancelWaterPlan)
                {
                    _gridTileVisualService.SetDestructionMarker(cursorCell, false);
                }
                else if (_currentToolMode == ToolMode.BuildOxygen)
                {
                    _oxygenPreviewRefreshService.HideIfNotPlanned(cursorCell, _currentOxygenPlanPreviewCells);
                }
                else if (_currentToolMode == ToolMode.CancelOxygenPlan)
                {
                    _gridTileVisualService.SetDestructionMarker(cursorCell, false);
                }
                else
                {
                    _gridTileVisualService.SetDigPreview(cursorCell, false);
                }
            }
            _currentCursorPreviewCells.Clear();
        }

        /// <summary>
        /// Runtime interaction and preview helper.
        /// </summary>
        private bool CanShowCursorPreview(Vector2Int pos)
        {
            if (_currentToolMode != ToolMode.Shovel
                && _currentToolMode != ToolMode.BuildLadder
                && _currentToolMode != ToolMode.BuildStorage
                && _currentToolMode != ToolMode.BuildSolarPanel
                && _currentToolMode != ToolMode.BuildRegolithProcessingUnit
                && _currentToolMode != ToolMode.BuildSleepModule
                && _currentToolMode != ToolMode.BuildBattery
                && _currentToolMode != ToolMode.BuildDinner
                && _currentToolMode != ToolMode.BuildOxygenStorage
                && _currentToolMode != ToolMode.BuildOxigenProcessingUnit
                && _currentToolMode != ToolMode.BuildWaterReclamation
                && _currentToolMode != ToolMode.BuildWaterProcessingUnit
                && _currentToolMode != ToolMode.BuildBridge
                && _currentToolMode != ToolMode.BuildCable
                && _currentToolMode != ToolMode.CancelCablePlan
                && _currentToolMode != ToolMode.CancelLifeModulePlan
                && _currentToolMode != ToolMode.BuildWater
                && _currentToolMode != ToolMode.CancelWaterPlan
                && _currentToolMode != ToolMode.BuildOxygen
                && _currentToolMode != ToolMode.CancelOxygenPlan
                && _currentToolMode != ToolMode.DestroyObject) return false;

            ref readonly Cell cell = ref _gridState.GetCell(pos.x, pos.y);

            if (IsBuildToolMode())
            {
                return _gridState.IsInside(pos.x, pos.y)
                       && _activeBuildingDef != null
                       && CanStageBuildPreviewAnchor(pos);
            }

            if (_currentToolMode == ToolMode.DestroyObject)
            {
                _footprintBuffer.Clear();
                return _buildingPlacementService.TryGetDestroyToolFootprint(pos, _footprintBuffer, out _);
            }

            if (_currentToolMode == ToolMode.BuildCable)
            {
                if (!_gridState.IsInside(pos.x, pos.y)) return false;
                if (cell.HasCable) return false;
                if (HasExistingCablePlanAt(pos))
                {
                    return false;
                }
                return CanStageCablePreviewCell(pos);
            }

            if (_currentToolMode == ToolMode.CancelCablePlan)
            {
                return _globalTaskBoardService.TryGetCableTaskByCell(pos, out UnitTaskRecord task)
                       && task != null
                       && task.TaskType == UnitTaskType.BuildCable
                       && task.Status != UnitTaskStatus.Completed
                       && task.Status != UnitTaskStatus.Failed;
            }

            if (_currentToolMode == ToolMode.CancelLifeModulePlan)
            {
                return _globalTaskBoardService.TryGetLifeModuleTaskByCell(pos, out UnitTaskRecord task)
                       && task != null
                       && task.TaskType == UnitTaskType.BuildLifeModule
                       && task.Status != UnitTaskStatus.Completed
                       && task.Status != UnitTaskStatus.Failed;
            }

            if (_currentToolMode == ToolMode.BuildWater)
            {
                if (!_gridState.IsInside(pos.x, pos.y)) return false;
                if (cell.HasWater) return false;
                if (HasExistingWaterPlanAt(pos))
                {
                    return false;
                }
                return CanStageWaterPreviewCell(pos);
            }

            if (_currentToolMode == ToolMode.CancelWaterPlan)
            {
                return HasExistingWaterPlanAt(pos);
            }

            if (_currentToolMode == ToolMode.BuildOxygen)
            {
                if (!_gridState.IsInside(pos.x, pos.y)) return false;
                if (cell.HasOxygen) return false;
                if (HasExistingOxygenPlanAt(pos))
                {
                    return false;
                }
                return CanStageOxygenPreviewCell(pos);
            }

            if (_currentToolMode == ToolMode.CancelOxygenPlan)
            {
                return HasExistingOxygenPlanAt(pos);
            }

            if (_currentToolMode == ToolMode.Shovel)
            {
                if (!_gridState.IsInside(pos.x, pos.y))
                {
                    return false;
                }

                return ResolveDigPreviewVisualKind(pos, cell) == DigPreviewVisualKind.Allowed;
            }

            if (!CanDig(cell.Type)) return false;
            if (ShipLandingZoneRules.IsInsideDigProtectionZone(_gridState.Width, _gridState.Height, pos)) return false;
            if (cell.IsDigMarked) return false;
            return true;
        }

        /// <summary>
        /// Runtime interaction and preview helper.
        /// </summary>
        private void UpdateCursorPreview()
        {
            foreach (Vector2Int cursorCell in _currentCursorPreviewCells)
            {
                if (IsBuildToolMode())
                {
                    _gridTileVisualService.SetCursorBuildPreviewTile(cursorCell, null);
                }
                else if (_currentToolMode == ToolMode.DestroyObject)
                {
                    _gridTileVisualService.SetDestructionMarker(cursorCell, false);
                }
                else if (_currentToolMode == ToolMode.BuildCable)
                {
                    _cablePreviewRefreshService.HideIfNotPlanned(cursorCell, _currentCablePlanPreviewCells);
                }
                else if (_currentToolMode == ToolMode.CancelCablePlan)
                {
                    _gridTileVisualService.SetDestructionMarker(cursorCell, false);
                }
                else if (_currentToolMode == ToolMode.CancelLifeModulePlan)
                {
                    _gridTileVisualService.SetDestructionMarker(cursorCell, false);
                }
                else if (_currentToolMode == ToolMode.BuildWater)
                {
                    _waterPreviewRefreshService.HideIfNotPlanned(cursorCell, _currentWaterPlanPreviewCells);
                }
                else if (_currentToolMode == ToolMode.CancelWaterPlan)
                {
                    _gridTileVisualService.SetDestructionMarker(cursorCell, false);
                }
                else if (_currentToolMode == ToolMode.BuildOxygen)
                {
                    _oxygenPreviewRefreshService.HideIfNotPlanned(cursorCell, _currentOxygenPlanPreviewCells);
                }
                else if (_currentToolMode == ToolMode.CancelOxygenPlan)
                {
                    _gridTileVisualService.SetDestructionMarker(cursorCell, false);
                }
                else
                {
                    _gridTileVisualService.SetDigPreview(cursorCell, false);
                }
            }
            _currentCursorPreviewCells.Clear();
            foreach (Vector2Int invalidCell in _currentInvalidBuildPreviewCells)
            {
                _gridTileVisualService.SetDestructionMarker(invalidCell, false);
            }
            _currentInvalidBuildPreviewCells.Clear();

            if (!_hasHoverCell) return;
            if (_currentPreviewCells.Count > 0
                || _currentCancelPreviewCells.Count > 0
                || _currentBuildPreviewCells.Count > 0
                || _currentDestructionPreviewCells.Count > 0) return;
            if (!CanShowCursorPreview(_hoverCell)) return;

            if (IsBuildToolMode())
            {
                if (_activeBuildingDef == null)
                {
                    return;
                }
                // Для Bridge не показываем курсорное превью на клетке с активным Bridge.
                if (ShouldSuppressBridgePreview(_hoverCell))
                {
                    _gridTileVisualService.SetCursorBuildPreviewTile(_hoverCell, null);
                    return;
                }

                // На already-planned клетке не рисуем курсорный preview во втором слое.
                if (_buildingPlacementService.IsPlannedCell(_hoverCell))
                {
                    _gridTileVisualService.SetCursorBuildPreviewTile(_hoverCell, null);
                    return;
                }

                TileBase previewTile = GetBuildPreviewTile(_hoverCell, null, null);
                if (previewTile == null) return;

                _footprintBuffer.Clear();
                bool isPlaceable = _buildingPlacementService.TryGetPlaceableFootprint(_activeBuildingDef, _hoverCell, false, _footprintBuffer);
                if (!isPlaceable)
                {
                    FillFootprintCells(_activeBuildingDef, _hoverCell, false, _footprintBuffer);
                }

                int previewWidth = Mathf.Max(1, _activeBuildingDef != null ? _activeBuildingDef.Width : 1);
                int previewHeight = Mathf.Max(1, _activeBuildingDef != null ? _activeBuildingDef.Height : 1);
                _currentCursorPreviewCells.Add(_hoverCell);
                _gridTileVisualService.SetCursorBuildPreviewTileByAnchor(
                    _hoverCell,
                    previewTile,
                    previewWidth,
                    previewHeight,
                    isPlaceable ? VALID_BUILD_PREVIEW_COLOR : INVALID_BUILD_PREVIEW_COLOR);

                if (!isPlaceable)
                {
                    for (int i = 0; i < _footprintBuffer.Count; i++)
                    {
                        Vector2Int invalidCell = _footprintBuffer[i];
                        _currentInvalidBuildPreviewCells.Add(invalidCell);
                        _gridTileVisualService.SetDestructionMarker(invalidCell, true);
                    }
                }

                if (ENABLE_BUILD_DEBUG_LOGS)
                {
                    //    Debug.Log($"[BuildDebug] Hover '{_activeBuildingDef?.name}' anchor=({_hoverCell.x},{_hoverCell.y}) size={_activeBuildingDef?.Width}x{_activeBuildingDef?.Height} footprintCells={_footprintBuffer.Count}");
                }
            }
            else
            {
                if (_currentToolMode == ToolMode.DestroyObject)
                {
                    _footprintBuffer.Clear();
                    if (!_buildingPlacementService.TryGetDestroyToolFootprint(_hoverCell, _footprintBuffer, out _)) return;

                    for (int i = 0; i < _footprintBuffer.Count; i++)
                    {
                        Vector2Int cell = _footprintBuffer[i];
                        _gridTileVisualService.SetDestructionMarker(cell, true);
                        _currentCursorPreviewCells.Add(cell);
                    }

                    return;
                }

                if (_currentToolMode == ToolMode.BuildCable)
                {
                    _cablePreviewRefreshService.RefreshAt(_hoverCell, _currentCablePlanPreviewCells);
                    _currentCursorPreviewCells.Add(_hoverCell);
                    return;
                }

                if (_currentToolMode == ToolMode.CancelCablePlan)
                {
                    if (_globalTaskBoardService.TryGetCableTaskByCell(_hoverCell, out UnitTaskRecord task)
                        && task != null
                        && task.TaskType == UnitTaskType.BuildCable
                        && task.Status != UnitTaskStatus.Completed
                        && task.Status != UnitTaskStatus.Failed)
                    {
                        _gridTileVisualService.SetDestructionMarker(_hoverCell, true);
                        _currentCursorPreviewCells.Add(_hoverCell);
                    }
                    return;
                }

                if (_currentToolMode == ToolMode.CancelLifeModulePlan)
                {
                    if (_globalTaskBoardService.TryGetLifeModuleTaskByCell(_hoverCell, out UnitTaskRecord task)
                        && task != null
                        && task.TaskType == UnitTaskType.BuildLifeModule
                        && task.Status != UnitTaskStatus.Completed
                        && task.Status != UnitTaskStatus.Failed
                        && task.LifeModulePayload != null
                        && task.LifeModulePayload.OccupiedCells != null)
                    {
                        for (int i = 0; i < task.LifeModulePayload.OccupiedCells.Length; i++)
                        {
                            Vector2Int occupiedCell = task.LifeModulePayload.OccupiedCells[i];
                            _gridTileVisualService.SetDestructionMarker(occupiedCell, true);
                            _currentCursorPreviewCells.Add(occupiedCell);
                        }
                    }

                    return;
                }

                if (_currentToolMode == ToolMode.BuildWater)
                {
                    _waterPreviewRefreshService.RefreshAt(_hoverCell, _currentWaterPlanPreviewCells);
                    _currentCursorPreviewCells.Add(_hoverCell);
                    return;
                }

                if (_currentToolMode == ToolMode.CancelWaterPlan)
                {
                    if (_globalTaskBoardService.TryGetWaterTaskByCell(_hoverCell, out UnitTaskRecord task)
                        && task != null
                        && task.TaskType == UnitTaskType.BuildWater
                        && task.Status != UnitTaskStatus.Completed
                        && task.Status != UnitTaskStatus.Failed)
                    {
                        _gridTileVisualService.SetDestructionMarker(_hoverCell, true);
                        _currentCursorPreviewCells.Add(_hoverCell);
                    }
                    return;
                }

                if (_currentToolMode == ToolMode.BuildOxygen)
                {
                    _oxygenPreviewRefreshService.RefreshAt(_hoverCell, _currentOxygenPlanPreviewCells);
                    _currentCursorPreviewCells.Add(_hoverCell);
                    return;
                }

                if (_currentToolMode == ToolMode.CancelOxygenPlan)
                {
                    if (_globalTaskBoardService.TryGetOxygenTaskByCell(_hoverCell, out UnitTaskRecord task)
                        && task != null
                        && task.TaskType == UnitTaskType.BuildOxygen
                        && task.Status != UnitTaskStatus.Completed
                        && task.Status != UnitTaskStatus.Failed)
                    {
                        _gridTileVisualService.SetDestructionMarker(_hoverCell, true);
                        _currentCursorPreviewCells.Add(_hoverCell);
                    }
                    return;
                }

                ref readonly Cell hoveredCell = ref _gridState.GetCell(_hoverCell.x, _hoverCell.y);
                _gridTileVisualService.SetDigPreview(_hoverCell, ResolveDigPreviewVisualKind(_hoverCell, hoveredCell));
                _currentCursorPreviewCells.Add(_hoverCell);
            }
        }

        /// <summary>
        /// Chooses which dig preview tile should be shown for the current shovel cell.
        /// </summary>
        private DigPreviewVisualKind ResolveDigPreviewVisualKind(Vector2Int cellPos, Cell cell)
        {
            if (ShipLandingZoneRules.IsInsideDigProtectionZone(_gridState.Width, _gridState.Height, cellPos))
            {
                return DigPreviewVisualKind.Blocked;
            }

            if (!CanDig(cell.Type) || cell.IsDigMarked)
            {
                return DigPreviewVisualKind.Blocked;
            }

            return DigPreviewVisualKind.Allowed;
        }

        private static bool CanDig(CellType type)
        {
            return type == CellType.Iron || type == CellType.Titan || type == CellType.Aluminium || type == CellType.Rogalite;
        }

        /// <summary>
        /// Runtime interaction and preview helper.
        /// </summary>
        private static bool HasAnyMark(Cell cell)
        {
            return cell.IsDigMarked;
        }

        /// <summary>
        /// Runtime interaction and preview helper.
        /// </summary>
        private static void ClearAllMarks(ref Cell cell)
        {
            cell.IsDigMarked = false;
        }

        /// <summary>
        /// Runtime interaction and preview helper.
        /// </summary>
        private static void FillFootprintCells(BuildingDef buildingDef, Vector2Int anchorCell, bool isRotated, List<Vector2Int> result)
        {
            result.Clear();
            if (buildingDef == null) return;

            int width = isRotated ? buildingDef.Height : buildingDef.Width;
            int height = isRotated ? buildingDef.Width : buildingDef.Height;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    result.Add(new Vector2Int(anchorCell.x + x, anchorCell.y + y));
                }
            }
        }

        private bool ShouldSuppressBridgePreview(Vector2Int anchorCell)
        {
            if (_activeBuildingDef == null || _activeBuildingDef.ObjectType != BuildObjectType.Bridge)
            {
                return false;
            }

            if (!_buildingPlacementService.TryGetActiveBuildingByCell(anchorCell, out BuildingRuntimeEntity activeBuilding)
                || activeBuilding?.BuildingDef == null)
            {
                return false;
            }

            return activeBuilding.BuildingDef.ObjectType == BuildObjectType.Bridge;
        }

        private void CollectLifeModuleCancellationPreview(Vector2Int a, Vector2Int b)
        {
            ClearLifeModuleCancelPreviewMarkers();

            int minX = Mathf.Min(a.x, b.x);
            int maxX = Mathf.Max(a.x, b.x);
            int minY = Mathf.Min(a.y, b.y);
            int maxY = Mathf.Max(a.y, b.y);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (!_gridState.IsInside(x, y))
                    {
                        continue;
                    }

                    Vector2Int cell = new Vector2Int(x, y);
                    if (!_globalTaskBoardService.TryGetLifeModuleTaskByCell(cell, out UnitTaskRecord task)
                        || task == null
                        || task.TaskType != UnitTaskType.BuildLifeModule
                        || task.Status == UnitTaskStatus.Completed
                        || task.Status == UnitTaskStatus.Failed
                        || task.LifeModulePayload == null)
                    {
                        continue;
                    }

                    TrackLifeModuleCancellationPayload(task.LifeModulePayload);
                }
            }
        }

        private void TrackLifeModuleCancellationPayload(LifeModuleTaskPayload payload)
        {
            if (payload == null || payload.OccupiedCells == null || payload.GroupId == 0)
            {
                return;
            }

            if (_lifeModuleCancelPayloadsByGroupId.ContainsKey(payload.GroupId))
            {
                return;
            }

            _lifeModuleCancelPayloadsByGroupId[payload.GroupId] = payload;
            for (int i = 0; i < payload.OccupiedCells.Length; i++)
            {
                Vector2Int cell = payload.OccupiedCells[i];
                _currentLifeModuleCancelPreviewCells.Add(cell);
                _gridTileVisualService.SetDestructionMarker(cell, true);
            }
        }

        private void ClearLifeModuleCancelPreviewMarkers()
        {
            foreach (Vector2Int cell in _currentLifeModuleCancelPreviewCells)
            {
                _gridTileVisualService.SetDestructionMarker(cell, false);
            }

            _currentLifeModuleCancelPreviewCells.Clear();
            _lifeModuleCancelPayloadsByGroupId.Clear();
        }
    }
}
