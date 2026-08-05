using _Project.Scripts.Data.Grid;
using _Project.Scripts.Data.Construction;
using System;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace _Project.Scripts.Presentation.Grid
{
    public enum DigPreviewVisualKind
    {
        None = 0,
        Allowed = 1,
        Blocked = 2
    }

    /// <summary>
    /// Encapsulates all operations with grid tilemap renderer.
    /// </summary>
    public sealed class GridTileVisualService
    {
        private readonly GridTilemapRenderer _gridTilemapRenderer;

        public GridTileVisualService(GridTilemapRenderSettings settings)
        {
            _gridTilemapRenderer = new GridTilemapRenderer(
                settings.ResourceTilemap,
                settings.ShaderBoardTileMap,
                settings.DefaultTilemap,
                settings.IronTilesByRepeatIndex,
                settings.TitanTilesByRepeatIndex,
                settings.AluminiumTilesByRepeatIndex,
                settings.RogaliteTilesByRepeatIndex,
                settings.AtmosphereTilesByRepeatIndex,
                settings.DefaultTilesByRepeatIndex,
                settings.DigPreviewTilemap,
                settings.DigPreviewTile,
                settings.DigPreviewBlockedTile,
                settings.HoverHighlightTilemap,
                settings.HoverHighlightTile,
                settings.HoverHighlightDefaultTile,
                settings.DigMarkerTilemap,
                settings.DigMarkerTile,
                settings.BuildTaskMarkerTilemap,
                settings.BuildTaskMarkerTile,
                settings.DestructionMarkerTile,
                settings.ReservedTilemap,
                settings.ReservedTile,
                settings.ProtectedResourceOverlayTilemap,
                settings.ProtectedResourceOverlayTile,
                settings.ProtectedResourceOverlayLeftTile,
                settings.ProtectedResourceOverlayRightTile,
                settings.CablePreviewTilemap,
                settings.CablePreviewTilesByMask4,
                settings.CableBuiltTilemap,
                settings.CableBuiltTilesByMask4,
                settings.WaterPreviewTilemap,
                settings.WaterPreviewTilesByMask4,
                settings.WaterBuiltTilemap,
                settings.WaterBuiltTilesByMask4,
                settings.OxygenPreviewTilemap,
                settings.OxygenPreviewTilesByMask4,
                settings.OxygenBuiltTilemap,
                settings.OxygenBuiltTilesByMask4,
                settings.ShowPipeMaskIndexDebug,
                settings.PipeMaskIndexDebugColor,
                settings.PipeMaskIndexDebugSortingOrder,
                settings.MaterialTransitionShaderTilemap,
                settings.MaterialTransitionTilemap,
                settings.TransitionTilesByOpenMask,
                settings.DigLineTilemap,
                settings.ResourceShadowSmoothing,
                settings.ResourceShadowBorderInset,
                settings.ResourceShadowWaveColor,
                settings.ResourceTransitionLineColor,
                settings.ResourceShadowWaveThickness,
                settings.ResourceShadowWaveAmplitude,
                settings.ResourceShadowWaveFrequency,
                settings.ResourceDarkeningColor,
                settings.ResourceDarkeningAmount,
                settings.ResourceDarkeningBoundaryInsetPixels,
                settings.ResourceDarkeningTransitionPixels,
                settings.ResourceDarkeningPixelsPerTile);

            settings.ResourceBoundarySettingsChanged += OnResourceBoundarySettingsChanged;

            void OnResourceBoundarySettingsChanged()
            {
                _gridTilemapRenderer.UpdateResourceBoundarySettings(
                    settings.ResourceShadowSmoothing,
                    settings.ResourceShadowBorderInset,
                    settings.ResourceShadowWaveColor,
                    settings.ResourceTransitionLineColor,
                    settings.ResourceShadowWaveThickness,
                    settings.ResourceShadowWaveAmplitude,
                    settings.ResourceShadowWaveFrequency,
                    settings.ResourceDarkeningColor,
                    settings.ResourceDarkeningAmount,
                    settings.ResourceDarkeningBoundaryInsetPixels,
                    settings.ResourceDarkeningTransitionPixels,
                    settings.ResourceDarkeningPixelsPerTile);
            }
        }

        public void RenderFull(GridState gridState)
        {
            _gridTilemapRenderer.RenderFull(gridState);
        }

        public void SetDigPreview(Vector2Int cell, DigPreviewVisualKind previewKind)
        {
            _gridTilemapRenderer.SetDigPreview(cell.x, cell.y, previewKind);
        }

        public void SetDigPreview(Vector2Int cell, bool isVisible)
        {
            _gridTilemapRenderer.SetDigPreview(
                cell.x,
                cell.y,
                isVisible ? DigPreviewVisualKind.Allowed : DigPreviewVisualKind.None);
        }

        public void SetTaskMarker(Vector2Int cell, bool isVisible)
        {
            _gridTilemapRenderer.SetDigMarker(cell.x, cell.y, isVisible);
        }

        public void ClearDigPreview()
        {
            _gridTilemapRenderer.ClearDigPreview();
        }

        public void SetBuildTaskMarker(Vector2Int cell, bool isVisible)
        {
            _gridTilemapRenderer.SetBuildTaskMarker(cell.x, cell.y, isVisible);
        }

        public void SetHoverHighlight(Vector2Int cell, float alpha)
        {
            _gridTilemapRenderer.SetHoverHighlightActive(cell.x, cell.y, alpha);
        }

        public void SetHoverHighlightDefault(Vector2Int cell, float alpha)
        {
            _gridTilemapRenderer.SetHoverHighlightDefault(cell.x, cell.y, alpha);
        }

        public void ClearHoverHighlight(Vector2Int cell)
        {
            _gridTilemapRenderer.ClearHoverHighlight(cell.x, cell.y);
        }

        public void SetDestructionMarker(Vector2Int cell, bool isVisible)
        {
            _gridTilemapRenderer.SetDestructionMarker(cell.x, cell.y, isVisible);
        }

        public void SetBuildPreviewTile(Vector2Int cell, TileBase previewTile)
        {
            _gridTilemapRenderer.SetBuildPreviewTile(cell.x, cell.y, previewTile);
        }

        public void SetBuildPreviewTile(Vector2Int cell, TileBase previewTile, Color color)
        {
            _gridTilemapRenderer.SetBuildPreviewTile(cell.x, cell.y, previewTile);
            _gridTilemapRenderer.SetBuildPreviewTileColor(cell.x, cell.y, color);
        }

        public void SetBuildPreviewTileByAnchor(Vector2Int anchorCell, TileBase previewTile, int width, int height)
        {
            _gridTilemapRenderer.SetBuildPreviewTileByAnchor(anchorCell.x, anchorCell.y, previewTile, width, height);
        }

        public void SetBuildPreviewTileByAnchor(Vector2Int anchorCell, TileBase previewTile, int width, int height, Color color)
        {
            _gridTilemapRenderer.SetBuildPreviewTileByAnchor(anchorCell.x, anchorCell.y, previewTile, width, height);
            _gridTilemapRenderer.SetBuildPreviewTileColor(anchorCell.x, anchorCell.y, color);
        }

        public void SetCursorBuildPreviewTile(Vector2Int cell, TileBase previewTile)
        {
            _gridTilemapRenderer.SetCursorBuildPreviewTile(cell.x, cell.y, previewTile);
        }

        public void SetCursorBuildPreviewTileByAnchor(Vector2Int anchorCell, TileBase previewTile, int width, int height)
        {
            _gridTilemapRenderer.SetCursorBuildPreviewTileByAnchor(anchorCell.x, anchorCell.y, previewTile, width, height);
        }

        public void SetCursorBuildPreviewTileByAnchor(Vector2Int anchorCell, TileBase previewTile, int width, int height, Color color)
        {
            _gridTilemapRenderer.SetCursorBuildPreviewTileByAnchor(anchorCell.x, anchorCell.y, previewTile, width, height);
            _gridTilemapRenderer.SetCursorBuildPreviewTileColor(anchorCell.x, anchorCell.y, color);
        }

        public void ClearCursorBuildPreviewLayer()
        {
            _gridTilemapRenderer.ClearCursorBuildPreviewLayer();
        }

        public void SetBuiltMarkerTile(Vector2Int cell, TileBase builtTile)
        {
            _gridTilemapRenderer.SetCustomMarkerTile(cell.x, cell.y, builtTile);
        }

        public void SetBuiltMarkerTileByAnchor(Vector2Int anchorCell, TileBase builtTile, int width, int height)
        {
            _gridTilemapRenderer.SetCustomMarkerTileByAnchor(anchorCell.x, anchorCell.y, builtTile, width, height);
        }

        public void SetGroundByCellType(Vector2Int cell, CellType cellType)
        {
            _gridTilemapRenderer.SetGroundCell(cell.x, cell.y, cellType);
        }

        public void RenderReservedDebugOverlay(GridState gridState, Func<Vector2Int, bool> isCellReservedOrBuilt)
        {
            _gridTilemapRenderer.ClearReservedDebugMarkers();
            if (isCellReservedOrBuilt == null)
            {
                return;
            }

            for (int y = 0; y < gridState.Height; y++)
            {
                for (int x = 0; x < gridState.Width; x++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    if (!isCellReservedOrBuilt(cell))
                    {
                        continue;
                    }

                    _gridTilemapRenderer.SetReservedDebugMarker(x, y, true);
                }
            }
        }

        public void SetReservedDebugOverlayCell(Vector2Int cell, bool isReservedOrBuilt)
        {
            _gridTilemapRenderer.SetReservedDebugMarker(cell.x, cell.y, isReservedOrBuilt);
        }

        public void SetReservedDebugOverlayCell(Vector2Int cell, bool isReservedOrBuilt, Color color)
        {
            _gridTilemapRenderer.SetReservedDebugMarker(cell.x, cell.y, isReservedOrBuilt, color);
        }

        public void SetCablePreview(Vector2Int cell, bool isVisible, byte cableMask4)
        {
            _gridTilemapRenderer.SetCablePreview(cell.x, cell.y, isVisible, cableMask4);
        }

        public void SetCableBuilt(Vector2Int cell, bool isVisible, byte cableMask4)
        {
            _gridTilemapRenderer.SetCableBuilt(cell.x, cell.y, isVisible, cableMask4);
        }

        /// <summary>
        /// Очищает весь preview-слой кабелей.
        /// </summary>
        public void ClearCablePreview()
        {
            _gridTilemapRenderer.ClearCablePreview();
        }

        public void SetWaterPreview(Vector2Int cell, bool isVisible, byte waterMask4)
        {
            _gridTilemapRenderer.SetWaterPreview(cell.x, cell.y, isVisible, waterMask4);
        }

        public void SetWaterBuilt(Vector2Int cell, bool isVisible, byte waterMask4)
        {
            _gridTilemapRenderer.SetWaterBuilt(cell.x, cell.y, isVisible, waterMask4);
        }

        public void ClearWaterPreview()
        {
            _gridTilemapRenderer.ClearWaterPreview();
        }

        public void SetOxygenPreview(Vector2Int cell, bool isVisible, byte oxygenMask4)
        {
            _gridTilemapRenderer.SetOxygenPreview(cell.x, cell.y, isVisible, oxygenMask4);
        }

        public void SetOxygenBuilt(Vector2Int cell, bool isVisible, byte oxygenMask4)
        {
            _gridTilemapRenderer.SetOxygenBuilt(cell.x, cell.y, isVisible, oxygenMask4);
        }

        public void ClearOxygenPreview()
        {
            _gridTilemapRenderer.ClearOxygenPreview();
        }

        public void SetLifeModulePreview(Vector2Int anchorCell, LifeModulePartType partType, int width, int height)
        {
            _gridTilemapRenderer.SetLifeModulePreview(anchorCell.x, anchorCell.y, partType, width, height, Color.white);
        }

        public void SetLifeModulePreview(Vector2Int anchorCell, LifeModulePartType partType, int width, int height, Color color)
        {
            _gridTilemapRenderer.SetLifeModulePreview(anchorCell.x, anchorCell.y, partType, width, height, color);
        }

        public void SetLifeModuleBuilt(Vector2Int anchorCell, LifeModulePartType partType, int width, int height)
        {
            _gridTilemapRenderer.SetLifeModuleBuilt(anchorCell.x, anchorCell.y, partType, width, height);
        }

        public void ClearLifeModulePreview()
        {
            _gridTilemapRenderer.ClearLifeModulePreview();
        }

        public void ClearLifeModuleBuilt()
        {
            _gridTilemapRenderer.ClearLifeModuleBuilt();
        }

        public void SetMaterialTransitionMask(Vector2Int cell, int openMask)
        {
            _gridTilemapRenderer.SetMaterialTransitionMask(cell.x, cell.y, openMask);
        }

        public void ClearMaterialTransition(Vector2Int cell)
        {
            _gridTilemapRenderer.ClearMaterialTransition(cell.x, cell.y);
        }

        public bool TryGetMaterialTransitionIndex(Vector2Int cell, out int transitionIndex)
        {
            return _gridTilemapRenderer.TryGetMaterialTransitionIndex(cell.x, cell.y, out transitionIndex);
        }
    }
}
