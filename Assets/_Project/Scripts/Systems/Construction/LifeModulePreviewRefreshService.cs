using System.Collections.Generic;
using _Project.Scripts.Data.Construction;
using _Project.Scripts.Data.Grid;
using _Project.Scripts.Data.Tasks;
using _Project.Scripts.Presentation.Grid;
using _Project.Scripts.Systems.Tasks;
using UnityEngine;

namespace _Project.Scripts.Systems.Construction
{
    /// <summary>
    /// Rebuilds life-module preview and built visuals from payloads/grid state.
    /// </summary>
    public sealed class LifeModulePreviewRefreshService
    {
        private static readonly Color LIFE_MODULE_VALID_DEBUG_COLOR = new Color(0.15f, 0.95f, 0.35f, 0.85f);
        private static readonly Color LIFE_MODULE_INVALID_DEBUG_COLOR = new Color(1f, 0.25f, 0.25f, 0.9f);
        private readonly GridState _gridState;
        private readonly GridTileVisualService _gridTileVisualService;
        private readonly GlobalTaskBoardService _globalTaskBoardService;
        private readonly HashSet<Vector2Int> _debugPreviewCells = new HashSet<Vector2Int>();

        public LifeModulePreviewRefreshService(
            GridState gridState,
            GridTileVisualService gridTileVisualService,
            GlobalTaskBoardService globalTaskBoardService)
        {
            _gridState = gridState;
            _gridTileVisualService = gridTileVisualService;
            _globalTaskBoardService = globalTaskBoardService;
        }

        public void RebuildPreview(LifeModuleTaskPayload stagedPayload = null)
        {
            ClearDebugPreviewMarkers();
            _gridTileVisualService.ClearLifeModulePreview();

            if (stagedPayload != null)
            {
                RenderPayload(stagedPayload, true);
            }

            var tasks = _globalTaskBoardService.GetActiveTasksSnapshot();
            for (int i = 0; i < tasks.Count; i++)
            {
                UnitTaskRecord task = tasks[i];
                if (task == null || task.TaskType != UnitTaskType.BuildLifeModule || task.LifeModulePayload?.Parts == null)
                {
                    continue;
                }

                RenderPayload(task.LifeModulePayload, true);
            }
        }

        public void RebuildBuilt()
        {
            _gridTileVisualService.ClearLifeModuleBuilt();
            for (int y = 0; y < _gridState.Height; y++)
            {
                for (int x = 0; x < _gridState.Width; x++)
                {
                    ref readonly Cell cell = ref _gridState.GetCell(x, y);
                    if (cell.LifeModuleType != LifeModuleType.Built || !cell.IsLifeModulePartAnchor)
                    {
                        continue;
                    }

                    _gridTileVisualService.SetLifeModuleBuilt(
                        new Vector2Int(x, y),
                        cell.LifeModulePartType,
                        Mathf.Max(1, cell.LifeModulePartWidth),
                        3);
                }
            }
        }

        private void RenderPayload(LifeModuleTaskPayload payload, bool isPreview)
        {
            if (payload == null)
            {
                return;
            }

            RenderParts(payload.Parts, isPreview, payload.IsPlacementValid);
            if (isPreview)
            {
                RenderDebugPreviewCells(payload.OccupiedCells, payload.IsPlacementValid);
            }
        }

        private void RenderParts(IReadOnlyList<LifeModulePartPayload> parts, bool isPreview, bool payloadIsValid = true)
        {
            if (parts == null)
            {
                return;
            }

            for (int i = 0; i < parts.Count; i++)
            {
                LifeModulePartPayload part = parts[i];
                if (isPreview)
                {
                    Color previewColor = payloadIsValid ? Color.white : LIFE_MODULE_INVALID_DEBUG_COLOR;
                    _gridTileVisualService.SetLifeModulePreview(part.AnchorCell, part.PartType, part.Width, part.Height, previewColor);
                    continue;
                }

                _gridTileVisualService.SetLifeModuleBuilt(part.AnchorCell, part.PartType, part.Width, part.Height);
            }
        }

        private void RenderDebugPreviewCells(IReadOnlyList<Vector2Int> occupiedCells, bool isPlacementValid)
        {
            if (occupiedCells == null)
            {
                return;
            }

            for (int i = 0; i < occupiedCells.Count; i++)
            {
                Vector2Int cell = occupiedCells[i];
                _debugPreviewCells.Add(cell);
                _gridTileVisualService.SetReservedDebugOverlayCell(
                    cell,
                    true,
                    isPlacementValid ? LIFE_MODULE_VALID_DEBUG_COLOR : LIFE_MODULE_INVALID_DEBUG_COLOR);
            }
        }

        private void ClearDebugPreviewMarkers()
        {
            foreach (Vector2Int cell in _debugPreviewCells)
            {
                _gridTileVisualService.SetReservedDebugOverlayCell(cell, false);
            }

            _debugPreviewCells.Clear();
        }
    }
}