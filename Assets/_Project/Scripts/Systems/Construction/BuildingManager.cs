using System.Collections.Generic;
using _Project.Scripts.Data.Construction;
using _Project.Scripts.Data.Grid;
using _Project.Scripts.Data.Power;
using _Project.Scripts.Data.Tasks;
using _Project.Scripts.Data.Water;
using _Project.Scripts.Data.Oxygen;
using _Project.Scripts.Systems.Pathfinding;
using _Project.Scripts.Systems.Oxygen;
using _Project.Scripts.Systems.Power;
using _Project.Scripts.Systems.Resources;
using _Project.Scripts.Systems.Tasks;
using _Project.Scripts.Systems.Water;
using UnityEngine;

namespace _Project.Scripts.Systems.Construction
{
    /// <summary>
    /// Управляет постановкой стройки:
    /// - валидация места,
    /// - создание глобальной задачи,
    /// - финализация постройки.
    /// </summary>
    public sealed class BuildingManager
    {
        // Ссылка на состояние сетки мира.
        // Нужна для проверок "можно ли строить в клетке" и для финальной записи результата строительства.
        private readonly GridState _grid;

        // Глобальная доска задач.
        // Через неё создаём строительные задачи, которые потом берут в работу юниты.
        private readonly GlobalTaskBoardService _taskBoard;

        // Общее хранилище ресурсов базы: списываем стоимость стройки и возвращаем её при демонтаже.
        private readonly ResourceInventoryService _resourceInventoryService;

        // Храним якоря (anchor) уже запланированных построек.
        // Это защита от дублей: чтобы не создать вторую задачу в ту же "точку старта" объекта.
        private readonly HashSet<Vector2Int> _plannedAnchors = new HashSet<Vector2Int>();

        // Храним все клетки, уже занятые footprint'ом запланированных построек.
        // Нужен для multi-tile объектов (2x3, 3x2 и т.д.), чтобы стройки не пересекались.
        private readonly HashSet<Vector2Int> _plannedCells = new HashSet<Vector2Int>();

        // Runtime-сущности построек по якорной клетке.
        private readonly Dictionary<Vector2Int, BuildingRuntimeEntity> _buildingsByAnchor =
            new Dictionary<Vector2Int, BuildingRuntimeEntity>();

        // Быстрый поиск активной постройки по любой клетке её footprint.
        private readonly Dictionary<Vector2Int, BuildingRuntimeEntity> _activeBuildingsByCell =
            new Dictionary<Vector2Int, BuildingRuntimeEntity>();

        // Внешние точки хранения (сценные объекты), которые не проходят через pipeline строительства.
        private readonly HashSet<Vector2Int> _externalStorageCells = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> _storageDeliveryPointDedupBuffer = new HashSet<Vector2Int>();

        // Re-evaluate runtime operational conditions for active buildings every N ticks.
        private const int OperationalCheckIntervalTicks = 5;
        private int _lastOperationalCheckTick;
        private WaterSimulationService _waterSimulationService;
        private OxygenSimulationService _oxygenSimulationService;

        /// <summary>
        /// Создаёт менеджер строительства.
        /// </summary>
        /// <param name="grid">Текущее состояние сетки мира, где проверяем/применяем постройки.</param>
        /// <param name="taskBoard">Глобальная очередь задач для юнитов.</param>
        public BuildingManager(GridState grid, GlobalTaskBoardService taskBoard, ResourceInventoryService resourceInventoryService)
        {
            _grid = grid;
            _taskBoard = taskBoard;
            _resourceInventoryService = resourceInventoryService;
        }

        /// <summary>
        /// Пытается поставить задачу строительства в глобальную очередь.
        /// </summary>
        /// <param name="def">Описание объекта (размер, тики, итоговый тип клетки и т.д.).</param>
        /// <param name="anchorCell">Якорная клетка объекта (обычно левый-нижний угол footprint).</param>
        /// <param name="isRotated">Поворот объекта (меняет ширину/высоту местами).</param>
        /// <param name="currentTick">Текущий тик симуляции, чтобы записать время создания задачи.</param>
        /// <returns>
        /// True: задача успешно создана и добавлена в board.
        /// False: задача не создана (невалидный def, занято место, нельзя поставить объект и т.п.).
        /// </returns>
        public bool TryQueueBuild(BuildingDef def, Vector2Int anchorCell, bool isRotated, int currentTick)
        {
            // Нельзя создать задачу без валидной дефиниции строящегося объекта.
            if (def == null)
            {
            // Debug.LogWarning("[Build] TryQueueBuild failed: BuildingDef is null.");
                return false;
            }

            // Нельзя ставить вторую стройку в тот же anchor, если он уже зарезервирован.
            if (_plannedAnchors.Contains(anchorCell))
            {
            // Debug.LogWarning($"[Build] TryQueueBuild failed: anchor already planned at ({anchorCell.x},{anchorCell.y}).");
                return false;
            }

            // Проверяем, что объект помещается, не пересекается с planned-зонами и проходит правила размещения.
            if (!CanPlace(def, anchorCell, isRotated))
            {
            // Debug.LogWarning($"[Build] TryQueueBuild failed: CanPlace=false at ({anchorCell.x},{anchorCell.y}) for {def.ObjectType}.");
                return false;
            }

            // Preview plans also reserve capacity, so the player cannot queue more ladders than inventory can cover.
            if (GetAvailableBuildPlanCount(def) <= 0)
            {
                return false;
            }

            // Формируем payload задачи: что строим, где и какой прогресс нужен.
            var payload = new BuildTaskPayload
            {
                BuildingDef = def,
                AnchorCell = anchorCell,
                IsRotated = isRotated,
                IsExcavatingBeforeBuild = ShouldExcavateBeforeBuild(def, anchorCell, isRotated),
                RemainingClearSubtasks = 0,
                IsBuildCostPaid = false,
                RemainingBuildTicks = Mathf.Max(1, def.BuildTicks)
            };

            // Create parent build task and immediately prepare clear-task pipeline for the footprint.
            int buildTaskId = _taskBoard.CreateBuildTask(anchorCell, currentTick, payload);
            int createdClearSubtasks = _taskBoard.CreateBuildClearSubtasks(buildTaskId, payload, currentTick);
            int existingPendingDigInFootprint = _taskBoard.CountPendingDigTasksInBuildFootprint(payload) - createdClearSubtasks;
            if (existingPendingDigInFootprint < 0) existingPendingDigInFootprint = 0;

            payload.RemainingClearSubtasks = createdClearSubtasks + existingPendingDigInFootprint;
            payload.IsExcavatingBeforeBuild = payload.RemainingClearSubtasks > 0;
            
            // Резервируем footprint объекта как "planned", чтобы другие стройки не пересеклись.
            List<Vector2Int> footprint = GetFootprintCells(def, anchorCell, isRotated);
            for (int i = 0; i < footprint.Count; i++)
            {
                _plannedCells.Add(footprint[i]);
            }
            
            // Резервируем якорь как "planned", чтобы не создавать дубликаты в ту же точку.
            _plannedAnchors.Add(anchorCell);

            // Регистрируем runtime-сущность постройки и её начальный статус.
            Vector2Int size = GetSize(def, isRotated);
            _buildingsByAnchor[anchorCell] = new BuildingRuntimeEntity(
                def,
                anchorCell,
                size,
                isRotated,
                BuildingRuntimeStatus.Planned);

            return true;
        }

        /// <summary>
        /// Пытается поставить задачу демонтажа для активной постройки под указанной клеткой.
        /// </summary>
        public bool TryQueueDestroy(Vector2Int selectedCell, int currentTick)
        {
            if (!TryGetActiveBuildingByCell(selectedCell, out BuildingRuntimeEntity entity))
            {
            // Debug.LogWarning($"[Destroy] No active building at ({selectedCell.x},{selectedCell.y}).");
                return false;
            }

            if (entity.Status != BuildingRuntimeStatus.Active)
            {
                return false;
            }

            var payload = new BuildTaskPayload
            {
                BuildingDef = entity.BuildingDef,
                AnchorCell = entity.AnchorCell,
                IsRotated = entity.IsRotated,
                RemainingBuildTicks = Mathf.Max(1, entity.BuildingDef.BuildTicks)
            };

            _taskBoard.CreateDestroyTask(entity.AnchorCell, currentTick, payload);
            entity.SetStatus(BuildingRuntimeStatus.DestructionPlanned);
            return true;
        }

        /// <summary>
        /// Финализирует строительство: применяет результат в сетку и снимает planned-блокировки.
        /// </summary>
        /// <param name="payload">Данные задачи строительства (что строили, где, с каким поворотом).</param>
        public void FinalizeBuild(BuildTaskPayload payload)
        {
            Vector2Int size = GetSize(payload.BuildingDef, payload.IsRotated);

            // Записываем итоговый тип клетки во все клетки footprint объекта.
            for (int y = 0; y < size.y; y++)
            {
                for (int x = 0; x < size.x; x++)
                {
                    int gx = payload.AnchorCell.x + x;
                    int gy = payload.AnchorCell.y + y;

                    Cell cell = _grid.GetCell(gx, gy);
                    cell.BuildObjectType = payload.BuildingDef.ObjectType;
                    cell.IsOccupiedByBuilding = true;
                    // По умолчанию финальная клетка следует обычным правилам проходимости.
                    // Специальный override может быть снова включён runtime-view слоем.
                    // Bridge-like buildings explicitly mark their cells as walkable for pathfinding.
                    cell.IgnoreObstacleForPathfinding = payload.BuildingDef.IsWalkableAfterBuild;
                    _grid.SetCell(gx, gy, cell);
                }
            }

            // После успешной финализации освобождаем клетки из planned-резерва.
            List<Vector2Int> footprint = GetFootprintCells(payload.BuildingDef, payload.AnchorCell, payload.IsRotated);
            for (int i = 0; i < footprint.Count; i++)
            {
                _plannedCells.Remove(footprint[i]);
            }

            // И освобождаем якорь.
            _plannedAnchors.Remove(payload.AnchorCell);

            if (_buildingsByAnchor.TryGetValue(payload.AnchorCell, out BuildingRuntimeEntity entity))
            {
                entity.SetStatus(BuildingRuntimeStatus.Active);
                entity.SetOperational(EvaluateOperationalState(entity));
            }
            else
            {
                BuildingRuntimeEntity newEntity = new BuildingRuntimeEntity(
                    payload.BuildingDef,
                    payload.AnchorCell,
                    size,
                    payload.IsRotated,
                    BuildingRuntimeStatus.Active);
                newEntity.SetOperational(EvaluateOperationalState(newEntity));
                _buildingsByAnchor[payload.AnchorCell] = newEntity;
            }

            RegisterActiveFootprint(payload.BuildingDef, payload.AnchorCell, payload.IsRotated);
        }

        /// <summary>
        /// Финализирует демонтаж: очищает footprint и возвращает стоимость объекта в хранилище.
        /// </summary>
        public void FinalizeDestroy(BuildTaskPayload payload)
        {
            if (payload == null || payload.BuildingDef == null) return;

            List<Vector2Int> footprint = GetFootprintCells(payload.BuildingDef, payload.AnchorCell, payload.IsRotated);
            for (int i = 0; i < footprint.Count; i++)
            {
                Vector2Int cellPos = footprint[i];
                Cell cell = _grid.GetCell(cellPos.x, cellPos.y);
                cell.Type = CellType.Empty;
                cell.BuildObjectType = null;
                cell.IsOccupiedByBuilding = false;
                cell.IgnoreObstacleForPathfinding = false;
                _grid.SetCell(cellPos.x, cellPos.y, cell);
                _activeBuildingsByCell.Remove(cellPos);
            }

            if (_buildingsByAnchor.TryGetValue(payload.AnchorCell, out BuildingRuntimeEntity entity))
            {
                entity.SetStatus(BuildingRuntimeStatus.Destroyed);
                _buildingsByAnchor.Remove(payload.AnchorCell);
            }

            RefundBuildCost(payload.BuildingDef);
        }

        /// <summary>
        /// Проверяет, можно ли разместить объект в указанной позиции.
        /// </summary>
        /// <param name="def">Описание строящегося объекта.</param>
        /// <param name="anchorCell">Якорная клетка объекта.</param>
        /// <param name="isRotated">Поворот объекта.</param>
        /// <returns>True, если placement валиден; иначе False.</returns>
        private bool CanPlace(BuildingDef def, Vector2Int anchorCell, bool isRotated)
        {
            // Итоговые габариты с учетом поворота.
            Vector2Int size = GetSize(def, isRotated);

            // Проверку опоры выполняем только для объектов, которым опора действительно нужна.
            // Например, лестница с SupportRequirement.None не должна блокироваться этой веткой.
            // Bridge intentionally ignores support checks below so it can span over any cell type.
            if (def.SupportRequirement == SupportRequirement.GroundOrFloor
                && def.ObjectType != BuildObjectType.Bridge)
            {
                for (int x = 0; x < size.x; x++)
                {
                    int belowX = anchorCell.x + x;
                    int belowY = anchorCell.y - 1;
                    if (!_grid.IsInside(belowX, belowY)) return false;

                    Cell belowCell = _grid.GetCell(belowX, belowY);
                    bool isLadderBelow = belowCell.BuildObjectType.HasValue && belowCell.BuildObjectType.Value == BuildObjectType.Ladder;
                    if (belowCell.Type != CellType.Empty && !isLadderBelow) return false;
                }
            }

            for (int y = 0; y < size.y; y++)
            {
                for (int x = 0; x < size.x; x++)
                {
                    int gx = anchorCell.x + x;
                    int gy = anchorCell.y + y;
                    
                    // Нельзя строить в клетках, уже занятых planned-постройками.
                    if (_plannedCells.Contains(new Vector2Int(gx, gy))) return false;
                    
                    // Нельзя выйти за границы сетки.
                    if (!_grid.IsInside(gx, gy)) return false;
                    if (ShipLandingZoneRules.IsInsideLandingZone(_grid.Width, _grid.Height, gx, gy)) return false;

                    Cell cell = _grid.GetCell(gx, gy);
                    if (cell.IsOccupiedByBuilding) return false;
                    if (!PassesLifeModulePlacementRule(def, cell, gx, gy, size.y)) return false;
                    // Явно запрещаем ставить Bridge поверх уже существующего Bridge.
                    if (def.ObjectType == BuildObjectType.Bridge
                        && cell.BuildObjectType.HasValue
                        && cell.BuildObjectType.Value == BuildObjectType.Bridge)
                    {
                        return false;
                    }
                    if (!CanBuildOnCell(def, cell)) return false;
                }
            }

            // During placement we also validate optional 'required support below' rules from BuildingDef.
            if (!HasRequiredSupportBelow(def, anchorCell, isRotated)) return false;

            return true;
        }

        private bool ShouldExcavateBeforeBuild(BuildingDef def, Vector2Int anchorCell, bool isRotated)
        {
            if (def.ObjectType != BuildObjectType.Ladder) return false;

            Vector2Int size = GetSize(def, isRotated);
            for (int y = 0; y < size.y; y++)
            {
                for (int x = 0; x < size.x; x++)
                {
                    Cell cell = _grid.GetCell(anchorCell.x + x, anchorCell.y + y);
                    if (IsLadderExcavationCell(cell.Type)) return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Возвращает true, если в footprint постройки есть diggable-клетки,
        /// которые нужно очистить дочерними задачами до старта стройки.
        /// </summary>
        public bool HasBlockingDigCells(BuildTaskPayload payload)
        {
            if (payload == null || payload.BuildingDef == null) return false;

            Vector2Int size = GetSize(payload.BuildingDef, payload.IsRotated);
            for (int y = 0; y < size.y; y++)
            {
                for (int x = 0; x < size.x; x++)
                {
                    int gx = payload.AnchorCell.x + x;
                    int gy = payload.AnchorCell.y + y;
                    if (!_grid.IsInside(gx, gy)) continue;

                    Cell cell = _grid.GetCell(gx, gy);
                    if (CellTraversalRules.IsDiggable(cell.Type))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool CanBuildOnCell(BuildingDef def, Cell cell)
        {
            if (def.AllowedPlacementCellTypes != null && def.AllowedPlacementCellTypes.Length > 0)
            {
                return MatchesAnyPlacementType(def.AllowedPlacementCellTypes, cell.Type);
            }

            if (def.ObjectType == BuildObjectType.Ladder)
            {
                // Legacy fallback: ladder can be placed in empty or diggable resource cells.
                return cell.Type == CellType.Empty
                       || cell.Type == CellType.Iron
                       || cell.Type == CellType.Titan
                       || cell.Type == CellType.Aluminium
                       || cell.Type == CellType.Rogalite;
            }

            if (def.ObjectType == BuildObjectType.SolarPanel)
            {
                // Legacy fallback: solar panel can be placed only in atmosphere cells.
                return cell.Type == CellType.Atmosphere;
            }

            return cell.Type == CellType.Empty || cell.Type == CellType.Atmosphere;
        }

        private bool EvaluateOperationalState(BuildingRuntimeEntity entity)
        {
            if (entity == null || entity.BuildingDef == null) return false;
            return HasRequiredSupportBelow(entity.BuildingDef, entity.AnchorCell, entity.IsRotated);
        }

        private bool HasRequiredSupportBelow(BuildingDef def, Vector2Int anchorCell, bool isRotated)
        {
            bool hasCellTypeRules = def.RequiredBelowAnyCellTypes != null && def.RequiredBelowAnyCellTypes.Length > 0;
            bool hasBuildTypeRules = def.RequiredBelowAnyBuildObjectTypes != null && def.RequiredBelowAnyBuildObjectTypes.Length > 0;
            if (!hasCellTypeRules && !hasBuildTypeRules) return true;

            Vector2Int size = GetSize(def, isRotated);
            for (int x = 0; x < size.x; x++)
            {
                int belowX = anchorCell.x + x;
                int belowY = anchorCell.y - 1;
                if (!_grid.IsInside(belowX, belowY)) continue;

                Cell belowCell = _grid.GetCell(belowX, belowY);
                if (hasCellTypeRules && MatchesAnyPlacementType(def.RequiredBelowAnyCellTypes, belowCell.Type))
                {
                    return true;
                }

                if (hasBuildTypeRules
                    && belowCell.BuildObjectType.HasValue
                    && MatchesAnyBuildObjectType(def.RequiredBelowAnyBuildObjectTypes, belowCell.BuildObjectType.Value))
                {
                    return true;
                }
            }

            return false;
        }

        private bool PassesLifeModulePlacementRule(BuildingDef def, Cell cell, int cellX, int cellY, int footprintHeight)
        {
            if (def.CanBuildOnlyOnBuiltLifeModule)
            {
                // Life-module-only placement has its own geometry rules on top of built-state validation.
                if (cell.LifeModuleType != LifeModuleType.Built || cell.LifeModuleGroupId == 0)
                {
                    return false;
                }

                if (!IsLifeModuleZoneCompatibleWithHeight(def.AllowedLifeModulePlacementZone, footprintHeight))
                {
                    return false;
                }

                if (!TryGetLifeModuleGroupBounds(cell.LifeModuleGroupId, out int groupMinX, out int groupMaxX, out int groupMinY))
                {
                    return false;
                }

                if (cellX == groupMinX || cellX == groupMaxX)
                {
                    return false;
                }

                int localRow = cellY - groupMinY;
                return MatchesLifeModulePlacementZone(def.AllowedLifeModulePlacementZone, localRow);
            }

            return cell.LifeModuleType != LifeModuleType.Built
                   && cell.LifeModuleType != LifeModuleType.Preview;
        }

        private bool TryGetLifeModuleGroupBounds(int groupId, out int minX, out int maxX, out int minY)
        {
            minX = int.MaxValue;
            maxX = int.MinValue;
            minY = int.MaxValue;

            for (int y = 0; y < _grid.Height; y++)
            {
                for (int x = 0; x < _grid.Width; x++)
                {
                    Cell groupCell = _grid.GetCell(x, y);
                    if (groupCell.LifeModuleType != LifeModuleType.Built || groupCell.LifeModuleGroupId != groupId)
                    {
                        continue;
                    }

                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                }
            }

            return minX != int.MaxValue && maxX != int.MinValue && minY != int.MaxValue;
        }

        private static bool IsLifeModuleZoneCompatibleWithHeight(LifeModulePlacementZone zone, int footprintHeight)
        {
            return footprintHeight switch
            {
                3 => zone == LifeModulePlacementZone.Any,
                2 => zone == LifeModulePlacementZone.TopTwo || zone == LifeModulePlacementZone.BottomTwo,
                1 => zone == LifeModulePlacementZone.Top
                     || zone == LifeModulePlacementZone.Middle
                     || zone == LifeModulePlacementZone.Bottom,
                _ => false
            };
        }

        private static bool MatchesLifeModulePlacementZone(LifeModulePlacementZone zone, int localRow)
        {
            return zone switch
            {
                LifeModulePlacementZone.Any => localRow >= 0 && localRow <= 2,
                LifeModulePlacementZone.TopTwo => localRow == 1 || localRow == 2,
                LifeModulePlacementZone.BottomTwo => localRow == 0 || localRow == 1,
                LifeModulePlacementZone.Top => localRow == 2,
                LifeModulePlacementZone.Middle => localRow == 1,
                LifeModulePlacementZone.Bottom => localRow == 0,
                _ => false
            };
        }

        private static bool MatchesAnyPlacementType(CellType[] allowedTypes, CellType actualType)
        {
            for (int i = 0; i < allowedTypes.Length; i++)
            {
                if (allowedTypes[i] == actualType) return true;
            }

            return false;
        }

        private static bool MatchesAnyBuildObjectType(BuildObjectType[] allowedTypes, BuildObjectType actualType)
        {
            for (int i = 0; i < allowedTypes.Length; i++)
            {
                if (allowedTypes[i] == actualType) return true;
            }

            return false;
        }

        private static bool IsLadderExcavationCell(CellType cellType)
        {
            return cellType == CellType.Iron
                   || cellType == CellType.Titan
                   || cellType == CellType.Aluminium
                   || cellType == CellType.Rogalite;
        }
        
        /// <summary>
        /// Снимает локальные planned-блокировки для отменённой строительной задачи.
        /// </summary>
        /// <param name="def">Описание отменённого объекта.</param>
        /// <param name="anchorCell">Якорная клетка отменённой постройки.</param>
        /// <param name="isRotated">Поворот отменённой постройки.</param>
        public void ReleasePlannedArea(BuildTaskPayload payload)
        {
            if (payload == null || payload.BuildingDef == null) return;

            // Удаляем весь footprint из planned-клеток.
            List<Vector2Int> footprint = GetFootprintCells(payload.BuildingDef, payload.AnchorCell, payload.IsRotated);
            for (int i = 0; i < footprint.Count; i++)
            {
                _plannedCells.Remove(footprint[i]);
            }

            // Удаляем якорь.
            _plannedAnchors.Remove(payload.AnchorCell);

            if (_buildingsByAnchor.TryGetValue(payload.AnchorCell, out BuildingRuntimeEntity entity)
                && entity.Status != BuildingRuntimeStatus.Active)
            {
                entity.SetStatus(BuildingRuntimeStatus.Cancelled);
            }

            if (payload.IsBuildCostPaid)
            {
                RefundBuildCost(payload.BuildingDef);
                payload.IsBuildCostPaid = false;
            }
        }

        /// <summary>
        /// Отменяет запланированный демонтаж и возвращает постройку в активный статус.
        /// </summary>
        public void CancelDestroy(BuildTaskPayload payload)
        {
            if (payload == null) return;

            if (_buildingsByAnchor.TryGetValue(payload.AnchorCell, out BuildingRuntimeEntity entity)
                && (entity.Status == BuildingRuntimeStatus.DestructionPlanned
                    || entity.Status == BuildingRuntimeStatus.Destroying))
            {
                entity.SetStatus(BuildingRuntimeStatus.Active);
            }
        }

        /// <summary>
        /// Возвращает footprint размещения, если объект можно поставить в anchor-клетку.
        /// </summary>
        /// <param name="def">Описание объекта.</param>
        /// <param name="anchorCell">Якорная клетка (левый-нижний угол).</param>
        /// <param name="isRotated">Поворот объекта.</param>
        /// <param name="result">Буфер для клеток footprint.</param>
        /// <returns>True, если placement валиден и footprint заполнен.</returns>
        public bool TryGetPlaceableFootprint(BuildingDef def, Vector2Int anchorCell, bool isRotated, List<Vector2Int> result)
        {
            result.Clear();
            if (def == null) return false;
            if (!CanPlace(def, anchorCell, isRotated)) return false;

            List<Vector2Int> footprint = GetFootprintCells(def, anchorCell, isRotated);
            result.AddRange(footprint);
            return true;
        }

        /// <summary>
        /// Возвращает footprint активной постройки, если указанная клетка принадлежит ей.
        /// </summary>
        public bool TryGetDestroyableFootprint(Vector2Int selectedCell, List<Vector2Int> result)
        {
            result.Clear();
            if (!TryGetActiveBuildingByCell(selectedCell, out BuildingRuntimeEntity entity)) return false;
            if (entity.Status != BuildingRuntimeStatus.Active) return false;

            result.AddRange(GetFootprintCells(entity.BuildingDef, entity.AnchorCell, entity.IsRotated));
            return true;
        }

        /// <summary>
        /// Возвращает true, если клетка входит в planned-область активной стройки.
        /// </summary>
        public bool IsPlannedCell(Vector2Int cell)
        {
            return _plannedCells.Contains(cell);
        }

        /// <summary>
        /// Переводит постройку в статус InProgress по payload задачи строительства.
        /// </summary>
        public void MarkBuildInProgress(BuildTaskPayload payload)
        {
            if (payload == null) return;

            if (_buildingsByAnchor.TryGetValue(payload.AnchorCell, out BuildingRuntimeEntity entity))
            {
                entity.SetStatus(BuildingRuntimeStatus.InProgress);
                return;
            }

            Vector2Int size = GetSize(payload.BuildingDef, payload.IsRotated);
            _buildingsByAnchor[payload.AnchorCell] = new BuildingRuntimeEntity(
                payload.BuildingDef,
                payload.AnchorCell,
                size,
                payload.IsRotated,
                BuildingRuntimeStatus.InProgress);
        }

        /// <summary>
        /// Проверяет, хватает ли ресурсов для строительной задачи.
        /// </summary>
        public bool HasBuildCost(BuildTaskPayload payload)
        {
            if (payload == null || payload.BuildingDef == null) return false;
            if (payload.IsBuildCostPaid) return true;

            return HasBuildCost(payload.BuildingDef);
        }

        /// <summary>
        /// Списывает стоимость перед стартом работы по стройке.
        /// </summary>
        /// <summary>
        /// Returns how many more objects of this definition can be planned with free resources.
        /// </summary>
        public int GetAvailableBuildPlanCount(BuildingDef def)
        {
            if (def == null)
            {
                return 0;
            }

            if (def.CostItems == null || def.CostItems.Length == 0)
            {
                return int.MaxValue;
            }

            int availablePlanCount = int.MaxValue;
            for (int i = 0; i < def.CostItems.Length; i++)
            {
                BuildCostItem item = def.CostItems[i];
                if (string.IsNullOrWhiteSpace(item.ResourceId) || item.Amount <= 0)
                {
                    continue;
                }

                int inventoryAmount = _resourceInventoryService.GetAmount(item.ResourceId);
                // Unpaid build tasks already claim these resources for planning, even before the worker starts the task.
                int reservedAmount = _taskBoard.GetReservedUnpaidBuildResourceAmount(item.ResourceId);
                int freeAmount = Mathf.Max(0, inventoryAmount - reservedAmount);
                int planCountByItem = freeAmount / item.Amount;
                if (planCountByItem < availablePlanCount)
                {
                    availablePlanCount = planCountByItem;
                }
            }

            return availablePlanCount == int.MaxValue ? int.MaxValue : Mathf.Max(0, availablePlanCount);
        }

        public bool TryPayBuildCost(BuildTaskPayload payload)
        {
            if (payload == null || payload.BuildingDef == null) return false;
            if (payload.IsBuildCostPaid) return true;
            if (!TrySpendBuildCost(payload.BuildingDef)) return false;

            payload.IsBuildCostPaid = true;
            return true;
        }

        /// <summary>
        /// Переводит активную постройку в статус демонтажа.
        /// </summary>
        public void MarkDestroyInProgress(BuildTaskPayload payload)
        {
            if (payload == null) return;

            if (_buildingsByAnchor.TryGetValue(payload.AnchorCell, out BuildingRuntimeEntity entity))
            {
                entity.SetStatus(BuildingRuntimeStatus.Destroying);
            }
        }

        /// <summary>
        /// Возвращает runtime-сущность постройки по якорю, если она зарегистрирована.
        /// </summary>
        public bool TryGetBuildingEntity(Vector2Int anchorCell, out BuildingRuntimeEntity entity)
        {
            return _buildingsByAnchor.TryGetValue(anchorCell, out entity);
        }

        /// <summary>
        /// Возвращает true, если постройка по якорю перешла в активный статус.
        /// </summary>
        public bool IsBuildingActive(Vector2Int anchorCell)
        {
            return _buildingsByAnchor.TryGetValue(anchorCell, out BuildingRuntimeEntity entity)
                   && entity.IsActive;
        }

        /// <summary>
        /// Возвращает true, если постройка активна и выполняет runtime-условия работы.
        /// </summary>
        public bool IsBuildingOperational(Vector2Int anchorCell)
        {
            return _buildingsByAnchor.TryGetValue(anchorCell, out BuildingRuntimeEntity entity)
                   && entity.IsActive
                   && entity.IsOperational;
        }

        /// <summary>
        /// Периодическая переоценка работоспособности построек.
        /// </summary>
        public void Tick(int currentTick)
        {
            if (currentTick - _lastOperationalCheckTick < OperationalCheckIntervalTicks) return;
            _lastOperationalCheckTick = currentTick;

            foreach (KeyValuePair<Vector2Int, BuildingRuntimeEntity> pair in _buildingsByAnchor)
            {
                BuildingRuntimeEntity entity = pair.Value;
                if (entity == null || !entity.IsActive) continue;

                entity.SetOperational(EvaluateOperationalState(entity));
            }
        }

        /// <summary>
        /// Применяет состояние энергосети к активным постройкам.
        /// Для RequiresPower=true объект считается operational только при наличии питания.
        /// </summary>
        public void ApplyPowerStates(PowerNetworkService powerNetworkService)
        {
            if (powerNetworkService == null) return;

            foreach (KeyValuePair<Vector2Int, BuildingRuntimeEntity> pair in _buildingsByAnchor)
            {
                BuildingRuntimeEntity entity = pair.Value;
                if (entity == null || !entity.IsActive || entity.BuildingDef == null) continue;

                bool isSupportedByRuntimeRules = EvaluateOperationalState(entity);
                bool isPowerSatisfied = !entity.BuildingDef.RequiresPower || IsBuildingPowered(entity, powerNetworkService);
                bool isOxygenSatisfied = IsOxygenSatisfied(entity);
                entity.SetOperational(isSupportedByRuntimeRules && isPowerSatisfied && isOxygenSatisfied);
            }
        }

        private bool IsOxygenSatisfied(BuildingRuntimeEntity entity)
        {
            if (entity == null || entity.BuildingDef == null)
            {
                return false;
            }

            if (!entity.BuildingDef.UsesOxygenNetwork || entity.BuildingDef.OxygenRole != OxygenRole.Consumer)
            {
                return true;
            }

            if (_oxygenSimulationService == null)
            {
                return false;
            }

            return _oxygenSimulationService.HasSupplyForConsumer(entity.AnchorCell);
        }

        public bool TryGetActiveBuildingByCell(Vector2Int cell, out BuildingRuntimeEntity entity)
        {
            return _activeBuildingsByCell.TryGetValue(cell, out entity)
                   && entity != null
                   && entity.IsActive;
        }

        /// <summary>
        /// Возвращает снимок всех клеток активных хранилищ.
        /// </summary>
        public readonly struct StorageDeliveryPoint
        {
            public readonly Vector2Int StorageCell;
            public readonly Vector2Int DeliveryCell;

            public StorageDeliveryPoint(Vector2Int storageCell, Vector2Int deliveryCell)
            {
                StorageCell = storageCell;
                DeliveryCell = deliveryCell;
            }
        }

        public bool IsActiveStorageDeliveryPoint(Vector2Int storageCell, Vector2Int deliveryCell)
        {
            if (_buildingsByAnchor.TryGetValue(storageCell, out BuildingRuntimeEntity entity)
                && entity != null
                && entity.IsActive
                && entity.BuildingDef != null)
            {
                BuildObjectType objectType = entity.BuildingDef.ObjectType;
                bool isStorage = objectType == BuildObjectType.Storage || objectType == BuildObjectType.RocketData;
                if (isStorage
                    && entity.AnchorCell + entity.BuildingDef.ResourceDeliveryTargetOffset == deliveryCell)
                {
                    return true;
                }
            }

            if (storageCell != deliveryCell) return false;
            if (_externalStorageCells.Contains(storageCell)) return true;
            if (!_grid.IsInside(storageCell.x, storageCell.y)) return false;

            Cell cell = _grid.GetCell(storageCell.x, storageCell.y);
            if (!cell.BuildObjectType.HasValue) return false;

            BuildObjectType cellObjectType = cell.BuildObjectType.Value;
            bool isStorageCell = cellObjectType == BuildObjectType.Storage || cellObjectType == BuildObjectType.RocketData;
            return isStorageCell && !_activeBuildingsByCell.ContainsKey(storageCell);
        }

        public void FillActiveStorageDeliveryPoints(List<StorageDeliveryPoint> resultBuffer)
        {
            if (resultBuffer == null)
            {
                return;
            }

            resultBuffer.Clear();
            _storageDeliveryPointDedupBuffer.Clear();

            foreach (KeyValuePair<Vector2Int, BuildingRuntimeEntity> pair in _buildingsByAnchor)
            {
                BuildingRuntimeEntity entity = pair.Value;
                if (!entity.IsActive) continue;
                if (entity.BuildingDef == null) continue;
                BuildObjectType objectType = entity.BuildingDef.ObjectType;
                if (objectType != BuildObjectType.Storage && objectType != BuildObjectType.RocketData) continue;

                Vector2Int deliveryCell = entity.AnchorCell + entity.BuildingDef.ResourceDeliveryTargetOffset;
                if (!_grid.IsInside(deliveryCell.x, deliveryCell.y))
                {
                    continue;
                }

                if (_storageDeliveryPointDedupBuffer.Add(deliveryCell))
                {
                    resultBuffer.Add(new StorageDeliveryPoint(entity.AnchorCell, deliveryCell));
                }
            }

            foreach (Vector2Int externalStorageCell in _externalStorageCells)
            {
                if (_storageDeliveryPointDedupBuffer.Add(externalStorageCell))
                {
                    resultBuffer.Add(new StorageDeliveryPoint(externalStorageCell, externalStorageCell));
                }
            }

            for (int y = 0; y < _grid.Height; y++)
            {
                for (int x = 0; x < _grid.Width; x++)
                {
                    Cell cell = _grid.GetCell(x, y);
                    if (!cell.BuildObjectType.HasValue) continue;

                    BuildObjectType objectType = cell.BuildObjectType.Value;
                    if (objectType != BuildObjectType.Storage && objectType != BuildObjectType.RocketData) continue;

                    Vector2Int cellPos = new Vector2Int(x, y);
                    if (_activeBuildingsByCell.ContainsKey(cellPos))
                    {
                        continue;
                    }

                    if (_storageDeliveryPointDedupBuffer.Add(cellPos))
                    {
                        resultBuffer.Add(new StorageDeliveryPoint(cellPos, cellPos));
                    }
                }
            }
        }

        public List<StorageDeliveryPoint> GetActiveStorageDeliveryPointsSnapshot()
        {
            var result = new List<StorageDeliveryPoint>();
            FillActiveStorageDeliveryPoints(result);
            return result;
        }

        public List<Vector2Int> GetActiveStorageTargetCellsSnapshot()
        {
            var result = new List<Vector2Int>();
            var uniqueCells = new HashSet<Vector2Int>();

            foreach (KeyValuePair<Vector2Int, BuildingRuntimeEntity> pair in _buildingsByAnchor)
            {
                BuildingRuntimeEntity entity = pair.Value;
                if (!entity.IsActive) continue;
                if (entity.BuildingDef == null) continue;
                BuildObjectType objectType = entity.BuildingDef.ObjectType;
                if (objectType != BuildObjectType.Storage && objectType != BuildObjectType.RocketData) continue;

                Vector2Int targetCell = entity.AnchorCell + entity.BuildingDef.ResourceDeliveryTargetOffset;
                if (!_grid.IsInside(targetCell.x, targetCell.y))
                {
                    continue;
                }

                if (uniqueCells.Add(targetCell))
                {
                    result.Add(targetCell);
                }
            }

            foreach (Vector2Int externalStorageCell in _externalStorageCells)
            {
                if (uniqueCells.Add(externalStorageCell))
                {
                    result.Add(externalStorageCell);
                }
            }

            // Считываем storage напрямую из состояния клеток: это источник истины для доставки ресурсов.
            for (int y = 0; y < _grid.Height; y++)
            {
                for (int x = 0; x < _grid.Width; x++)
                {
                    Cell cell = _grid.GetCell(x, y);
                    if (!cell.BuildObjectType.HasValue) continue;

                    BuildObjectType objectType = cell.BuildObjectType.Value;
                    if (objectType != BuildObjectType.Storage && objectType != BuildObjectType.RocketData) continue;

                    Vector2Int cellPos = new Vector2Int(x, y);
                    if (_activeBuildingsByCell.ContainsKey(cellPos))
                    {
                        continue;
                    }

                    if (uniqueCells.Add(cellPos))
                    {
                        result.Add(cellPos);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Регистрирует внешнюю (сценную) точку хранения, не связанную с BuildTask.
        /// </summary>
        public void RegisterExternalStorageCell(Vector2Int cell)
        {
            if (!_grid.IsInside(cell.x, cell.y)) return;
            _externalStorageCells.Add(cell);

            // Для внешнего storage фиксируем тип постройки и факт занятости прямо в клетке.
            Cell cellData = _grid.GetCell(cell.x, cell.y);
            cellData.BuildObjectType = BuildObjectType.RocketData;
            cellData.IsOccupiedByBuilding = true;
            _grid.SetCell(cell.x, cell.y, cellData);
        }

        /// <summary>
        /// Возвращает снимок всех активных построек для внешних runtime-сервисов.
        /// </summary>
        public void FillActiveBuildings(List<BuildingRuntimeEntity> resultBuffer)
        {
            if (resultBuffer == null)
            {
                return;
            }

            resultBuffer.Clear();
            foreach (KeyValuePair<Vector2Int, BuildingRuntimeEntity> pair in _buildingsByAnchor)
            {
                BuildingRuntimeEntity entity = pair.Value;
                if (entity == null || !entity.IsActive) continue;
                resultBuffer.Add(entity);
            }
        }

        public List<BuildingRuntimeEntity> GetActiveBuildingsSnapshot()
        {
            var result = new List<BuildingRuntimeEntity>();
            FillActiveBuildings(result);
            return result;
        }
        
        
        /// <summary>
        /// Возвращает список клеток, которые занимает объект по anchor и ориентации.
        /// </summary>
        /// <param name="def">Описание объекта (размеры).</param>
        /// <param name="anchorCell">Якорная клетка.</param>
        /// <param name="isRotated">Поворот объекта.</param>
        /// <returns>Список всех клеток footprint объекта.</returns>
        private static List<Vector2Int> GetFootprintCells(BuildingDef def, Vector2Int anchorCell, bool isRotated)
        {
            Vector2Int size = GetSize(def, isRotated);
            var result = new List<Vector2Int>(size.x * size.y);

            for (int y = 0; y < size.y; y++)
            {
                for (int x = 0; x < size.x; x++)
                {
                    result.Add(new Vector2Int(anchorCell.x + x, anchorCell.y + y));
                }
            }

            return result;
        }

        private void RegisterActiveFootprint(BuildingDef def, Vector2Int anchorCell, bool isRotated)
        {
            if (!_buildingsByAnchor.TryGetValue(anchorCell, out BuildingRuntimeEntity entity)) return;

            List<Vector2Int> footprint = GetFootprintCells(def, anchorCell, isRotated);
            for (int i = 0; i < footprint.Count; i++)
            {
                _activeBuildingsByCell[footprint[i]] = entity;
            }
        }

        private bool TrySpendBuildCost(BuildingDef def)
        {
            if (def.CostItems == null || def.CostItems.Length == 0) return true;

            if (!HasBuildCost(def)) return false;

            for (int i = 0; i < def.CostItems.Length; i++)
            {
                BuildCostItem item = def.CostItems[i];
                _resourceInventoryService.TryRemove(item.ResourceId, item.Amount);
            }

            return true;
        }

        private bool HasBuildCost(BuildingDef def)
        {
            if (def == null) return false;
            if (def.CostItems == null || def.CostItems.Length == 0) return true;

            for (int i = 0; i < def.CostItems.Length; i++)
            {
                BuildCostItem item = def.CostItems[i];
                if (!_resourceInventoryService.Has(item.ResourceId, item.Amount)) return false;
            }

            return true;
        }

        private void RefundBuildCost(BuildingDef def)
        {
            if (def == null || def.CostItems == null) return;

            for (int i = 0; i < def.CostItems.Length; i++)
            {
                BuildCostItem item = def.CostItems[i];
                _resourceInventoryService.Add(item.ResourceId, item.Amount);
            }
        }

        /// <summary>
        /// Возвращает итоговый размер объекта с учётом поворота.
        /// </summary>
        /// <param name="def">Описание объекта.</param>
        /// <param name="isRotated">Если true, ширина и высота меняются местами.</param>
        /// <returns>Размер footprint в клетках.</returns>
        private static Vector2Int GetSize(BuildingDef def, bool isRotated)
        {
            if (!isRotated) return new Vector2Int(def.Width, def.Height);
            return new Vector2Int(def.Height, def.Width);
        }

        private static bool IsBuildingPowered(BuildingRuntimeEntity entity, PowerNetworkService powerNetworkService)
        {
            BuildingPowerRuntimeState powerState = powerNetworkService.GetBuildingState(entity.AnchorCell);
            return powerState.IsPowered;
        }

        /// <summary>
        /// Wires water simulation facade after gameplay graph composition.
        /// </summary>
        public void SetWaterSimulationService(WaterSimulationService waterSimulationService)
        {
            _waterSimulationService = waterSimulationService;
        }

        /// <summary>
        /// Wires oxygen simulation facade after gameplay graph composition.
        /// </summary>
        public void SetOxygenSimulationService(OxygenSimulationService oxygenSimulationService)
        {
            _oxygenSimulationService = oxygenSimulationService;
        }

        /// <summary>
        /// Sets producer switch for one building.
        /// </summary>
        public bool SetWaterProducerEnabled(Vector2Int anchorCell, bool isEnabled)
        {
            if (!_buildingsByAnchor.TryGetValue(anchorCell, out BuildingRuntimeEntity entity) || entity?.BuildingDef == null)
            {
                return false;
            }

            entity.SetWaterProducerEnabled(isEnabled);
            return _waterSimulationService?.SetProducerEnabled(anchorCell, isEnabled) ?? true;
        }

        /// <summary>
        /// Toggles producer switch for one building.
        /// </summary>
        public bool TryToggleWaterProducer(Vector2Int anchorCell, out bool isEnabled)
        {
            isEnabled = false;
            if (!_buildingsByAnchor.TryGetValue(anchorCell, out BuildingRuntimeEntity entity) || entity?.BuildingDef == null)
            {
                return false;
            }

            isEnabled = entity.ToggleWaterProducer();
            return _waterSimulationService?.SetProducerEnabled(anchorCell, isEnabled) ?? true;
        }

        /// <summary>
        /// Sets oxygen producer switch for one building.
        /// </summary>
        public bool SetOxygenProducerEnabled(Vector2Int anchorCell, bool isEnabled)
        {
            if (!_buildingsByAnchor.TryGetValue(anchorCell, out BuildingRuntimeEntity entity) || entity?.BuildingDef == null)
            {
                return false;
            }

            entity.SetOxygenProducerEnabled(isEnabled);
            return _oxygenSimulationService?.SetProducerEnabled(anchorCell, isEnabled) ?? true;
        }

        /// <summary>
        /// Toggles oxygen producer switch for one building.
        /// </summary>
        public bool TryToggleOxygenProducer(Vector2Int anchorCell, out bool isEnabled)
        {
            isEnabled = false;
            if (!_buildingsByAnchor.TryGetValue(anchorCell, out BuildingRuntimeEntity entity) || entity?.BuildingDef == null)
            {
                return false;
            }

            isEnabled = entity.ToggleOxygenProducer();
            return _oxygenSimulationService?.SetProducerEnabled(anchorCell, isEnabled) ?? true;
        }

        /// <summary>
        /// Tries to consume water from consumer local tank.
        /// </summary>
        public bool TryConsumeWater(Vector2Int anchorCell, float liters, out WaterConsumeResult result)
        {
            if (_waterSimulationService == null)
            {
                result = new WaterConsumeResult
                {
                    RequestedLiters = Mathf.Max(0f, liters),
                    GrantedLiters = 0f,
                    Success = false,
                    Reason = "Water simulation is not wired."
                };
                return false;
            }

            return _waterSimulationService.TryConsumeWater(anchorCell, liters, out result);
        }

        /// <summary>
        /// Tries to consume oxygen from consumer local tank.
        /// </summary>
        public bool TryConsumeOxygen(Vector2Int anchorCell, float liters, out OxygenConsumeResult result)
        {
            if (_oxygenSimulationService == null)
            {
                result = new OxygenConsumeResult
                {
                    RequestedLiters = Mathf.Max(0f, liters),
                    GrantedLiters = 0f,
                    Success = false,
                    Reason = "Oxygen simulation is not wired."
                };
                return false;
            }

            return _oxygenSimulationService.TryConsumeOxygen(anchorCell, liters, out result);
        }
    }
}