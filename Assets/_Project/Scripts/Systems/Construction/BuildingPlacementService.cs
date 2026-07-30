using System.Collections.Generic;
using _Project.Scripts.Data.Construction;
using _Project.Scripts.Data.Grid;
using _Project.Scripts.Systems.Tasks;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace _Project.Scripts.Systems.Construction
{
    /// <summary>
    /// Service with placement rules and queue/cancel helpers for build tasks.
    /// </summary>
    public sealed class BuildingPlacementService
    {
        // Manager that owns build placement validation/reservations.
        private readonly BuildingManager _buildingManager;

        // Global task board used for build-task cancellation by cell.
        private readonly GlobalTaskBoardService _globalTaskBoardService;

        // Extended logs switch for build flow.
        private readonly bool _enableAiLogs;
        // Runtime-кэш tile, собранных из preview-спрайтов.
        private readonly Dictionary<BuildingDef, TileBase> _previewTilesByDef = new Dictionary<BuildingDef, TileBase>();
        private readonly Dictionary<Sprite, TileBase> _previewTilesBySprite = new Dictionary<Sprite, TileBase>();

        /// <summary>
        /// Initializes build placement service dependencies.
        /// </summary>
        public BuildingPlacementService(
            BuildingManager buildingManager,
            GlobalTaskBoardService globalTaskBoardService,
            bool enableAiLogs)
        {
            _buildingManager = buildingManager;
            _globalTaskBoardService = globalTaskBoardService;
            _enableAiLogs = enableAiLogs;
        }

        /// <summary>
        /// Returns true if the cell type is currently buildable.
        /// </summary>
        public bool CanBuildOn(CellType cellType)
        {
            return cellType == CellType.Empty
                   || cellType == CellType.Iron
                   || cellType == CellType.Titan
                   || cellType == CellType.Rogalite;
        }

        /// <summary>
        /// Safely returns preview tile for the selected building definition.
        /// </summary>
        public TileBase GetPreviewTile(BuildingDef buildingDef)
        {
            if (buildingDef == null)
            {
            // Debug.LogWarning("[Build] BuildingDef is null: preview cannot be rendered.");
                return null;
            }


            if (_previewTilesByDef.TryGetValue(buildingDef, out TileBase cachedTile))
            {
                return cachedTile;
            }

            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = buildingDef.PreviewSprite;
            _previewTilesByDef[buildingDef] = tile;
            return tile;
        }

        /// <summary>
        /// Returns a preview tile for a specific sprite variant, such as a ladder end or center part.
        /// </summary>
        public TileBase GetPreviewTile(BuildingDef buildingDef, Sprite previewSprite)
        {
            if (buildingDef == null)
            {
                return null;
            }

            if (previewSprite == buildingDef.PreviewSprite)
            {
                return GetPreviewTile(buildingDef);
            }

            if (_previewTilesBySprite.TryGetValue(previewSprite, out TileBase cachedTile))
            {
                return cachedTile;
            }

            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = previewSprite;
            _previewTilesBySprite[previewSprite] = tile;
            return tile;
        }

        /// <summary>
        /// Queues a build task in the global board.
        /// </summary>
        public bool TryQueueBuild(BuildingDef buildingDef, Vector2Int cell, int tickCounter)
        {
            if (buildingDef == null)
            {
            // Debug.LogWarning("[Build] Active BuildingDef is null.");
                return false;
            }

            bool queued = _buildingManager.TryQueueBuild(buildingDef, cell, false, tickCounter);
            if (!queued && _enableAiLogs)
            {
            // Debug.LogWarning($"[Build] Failed to queue build at ({cell.x},{cell.y}).");
            }

            return queued;
        }

        /// <summary>
        /// Queues a destruction task for an already built object.
        /// </summary>
        public bool TryQueueDestroy(Vector2Int cell, int tickCounter)
        {
            bool queued = _buildingManager.TryQueueDestroy(cell, tickCounter);
            if (!queued && _enableAiLogs)
            {
            // Debug.LogWarning($"[Destroy] Failed to queue destruction at ({cell.x},{cell.y}).");
            }

            return queued;
        }

        /// <summary>
        /// Cancels a task in the cell and releases planned area when it is a build task.
        /// </summary>
        public bool TryCancelTaskAndReleasePlannedArea(Vector2Int cell)
        {
            bool cancelled = _globalTaskBoardService.CancelTaskByCell(
                cell,
                out var cancelledBuildPayload,
                out var cancelledTaskType);
            if (!cancelled)
            {
                return false;
            }

            if (cancelledBuildPayload != null && cancelledTaskType == _Project.Scripts.Data.Tasks.UnitTaskType.BuildObject)
            {
                _buildingManager.ReleasePlannedArea(cancelledBuildPayload);
            }
            else if (cancelledBuildPayload != null && cancelledTaskType == _Project.Scripts.Data.Tasks.UnitTaskType.DestroyObject)
            {
                _buildingManager.CancelDestroy(cancelledBuildPayload);
            }

            return true;
        }

        /// <summary>
        /// Returns object footprint by anchor cell when placement is valid.
        /// </summary>
        public bool TryGetPlaceableFootprint(BuildingDef buildingDef, Vector2Int anchorCell, bool isRotated,
            List<Vector2Int> result)
        {
            return _buildingManager.TryGetPlaceableFootprint(buildingDef, anchorCell, isRotated, result);
        }

        public bool TryGetDestroyableFootprint(Vector2Int selectedCell, List<Vector2Int> result)
        {
            return _buildingManager.TryGetDestroyableFootprint(selectedCell, result);
        }

        /// <summary>
        /// Returns the footprint and anchor command cell used by the destroy tool.
        /// </summary>
        public bool TryGetDestroyToolFootprint(Vector2Int selectedCell, List<Vector2Int> result, out Vector2Int commandCell)
        {
            return _buildingManager.TryGetDestroyToolFootprint(selectedCell, result, out commandCell);
        }

        /// <summary>
        /// Возвращает true, если клетка входит в planned-область стройки.
        /// </summary>
        public int GetAvailableBuildPlanCount(BuildingDef buildingDef)
        {
            return _buildingManager.GetAvailableBuildPlanCount(buildingDef);
        }

        public bool IsPlannedCell(Vector2Int cell)
        {
            return _buildingManager.IsPlannedCell(cell);
        }

        /// <summary>
        /// Возвращает активную постройку, если клетка входит в её footprint.
        /// </summary>
        public bool TryGetActiveBuildingByCell(Vector2Int cell, out BuildingRuntimeEntity entity)
        {
            return _buildingManager.TryGetActiveBuildingByCell(cell, out entity);
        }
    }
}