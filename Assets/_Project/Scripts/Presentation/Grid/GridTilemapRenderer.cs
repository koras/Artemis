using _Project.Scripts.Data.Grid;
using _Project.Scripts.Data.Construction;
using System.Collections.Generic;
using _Project.Scripts.Systems.Construction;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace _Project.Scripts.Presentation.Grid
{
    public sealed class GridTilemapRenderer
    {
        private const int RepeatGridSize = 8;
        private const int RepeatTileCount = RepeatGridSize * RepeatGridSize;
        private const int TransitionTileCount = 47;
        private const bool ENABLE_LIFE_MODULE_DEBUG_LOGS = true;
        private static readonly Color DigPreviewColor = new Color32(0xFF, 0xC8, 0x5A, 0xFF);
        private static readonly Color DigPreviewBlockedColor = new Color(1f, 0.3f, 0.3f, 0.95f);

        private readonly Tilemap _resourceTilemap;
        private readonly Tilemap _defaultTilemap;

        private readonly TileBase[] _ironTilesByRepeatIndex;
        private readonly TileBase[] _titanTilesByRepeatIndex;
        private readonly TileBase[] _aluminiumTilesByRepeatIndex;
        private readonly TileBase[] _rogaliteTilesByRepeatIndex;
        private readonly TileBase[] _atmosphereTilesByRepeatIndex;
        private readonly TileBase[] _defaultTilesByRepeatIndex;

        private readonly Tilemap _digMarkerTilemap;
        private readonly TileBase _digMarkerTile;
        private readonly Tilemap _buildTaskMarkerTilemap;
        private readonly TileBase _buildTaskMarkerTile;
        private readonly TileBase _destructionMarkerTile;
        private readonly Tilemap _reservedTilemap;
        private readonly TileBase _reservedTile;
        private readonly Tilemap _protectedResourceOverlayTilemap;
        private readonly TileBase _protectedResourceOverlayTile;
        private readonly TileBase _protectedResourceOverlayLeftTile;
        private readonly TileBase _protectedResourceOverlayRightTile;
        private readonly Tilemap _materialTransitionTilemap;
        private readonly TileBase[] _transitionTilesByOpenMask;
        private readonly Dictionary<Vector2Int, int> _materialTransitionIndexByCell = new Dictionary<Vector2Int, int>();

        private readonly Tilemap _digPreviewTilemap;
        private readonly TileBase _digPreviewTile;
        private readonly TileBase _digPreviewBlockedTile;
        private readonly Tilemap _hoverHighlightTilemap;
        private readonly TileBase _hoverHighlightActiveTile;
        private readonly TileBase _hoverHighlightDefaultTile;
        private readonly HashSet<Vector2Int> _hoverHighlightCells = new HashSet<Vector2Int>();
        private readonly Tilemap _cablePreviewTilemap;
        private readonly TileBase[] _cablePreviewTilesByMask4;
        private readonly Tilemap _cableBuiltTilemap;
        private readonly TileBase[] _cableBuiltTilesByMask4;
        private readonly Tilemap _waterPreviewTilemap;
        private readonly TileBase[] _waterPreviewTilesByMask4;
        private readonly Tilemap _waterBuiltTilemap;
        private readonly TileBase[] _waterBuiltTilesByMask4;
        private readonly Tilemap _oxygenPreviewTilemap;
        private readonly TileBase[] _oxygenPreviewTilesByMask4;
        private readonly Tilemap _oxygenBuiltTilemap;
        private readonly TileBase[] _oxygenBuiltTilesByMask4;
        private readonly Tilemap _lifeModulePreviewTilemap;
        private readonly Tilemap _lifeModuleBuiltTilemap;
        private readonly Dictionary<Sprite, TileBase> _lifeModuleTilesBySprite = new Dictionary<Sprite, TileBase>();
        private readonly LifeModuleVisualCatalog _lifeModuleVisualCatalog;
        private readonly bool _showPipeMaskIndexDebug;
        private readonly Color _pipeMaskIndexDebugColor;
        private readonly int _pipeMaskIndexDebugSortingOrder;
        private readonly Dictionary<Vector2Int, TextMesh> _pipeMaskDebugLabelsByCell = new Dictionary<Vector2Int, TextMesh>();
        private readonly Transform _pipeMaskDebugRoot;

        public GridTilemapRenderer(
            Tilemap resourceTilemap,
            Tilemap defaultTilemap,
            TileBase[] ironTilesByRepeatIndex,
            TileBase[] titanTilesByRepeatIndex,
            TileBase[] aluminiumTilesByRepeatIndex,
            TileBase[] rogaliteTilesByRepeatIndex,
            TileBase[] atmosphereTilesByRepeatIndex,
            TileBase[] defaultTilesByRepeatIndex,
            Tilemap digPreviewTilemap,
            TileBase digPreviewTile,
            TileBase digPreviewBlockedTile,
            Tilemap hoverHighlightTilemap,
            TileBase hoverHighlightActiveTile,
            TileBase hoverHighlightDefaultTile,
            Tilemap digMarkerTilemap,
            TileBase digMarkerTile,
            Tilemap buildTaskMarkerTilemap,
            TileBase buildTaskMarkerTile,
            TileBase destructionMarkerTile,
            Tilemap reservedTilemap,
            TileBase reservedTile,
            Tilemap protectedResourceOverlayTilemap,
            TileBase protectedResourceOverlayTile,
            TileBase protectedResourceOverlayLeftTile,
            TileBase protectedResourceOverlayRightTile,
            Tilemap cablePreviewTilemap,
            TileBase[] cablePreviewTilesByMask4,
            Tilemap cableBuiltTilemap,
            TileBase[] cableBuiltTilesByMask4,
            Tilemap waterPreviewTilemap,
            TileBase[] waterPreviewTilesByMask4,
            Tilemap waterBuiltTilemap,
            TileBase[] waterBuiltTilesByMask4,
            Tilemap oxygenPreviewTilemap,
            TileBase[] oxygenPreviewTilesByMask4,
            Tilemap oxygenBuiltTilemap,
            TileBase[] oxygenBuiltTilesByMask4,
            bool showPipeMaskIndexDebug,
            Color pipeMaskIndexDebugColor,
            int pipeMaskIndexDebugSortingOrder,
            Tilemap materialTransitionTilemap,
            TileBase[] transitionTilesByOpenMask)
        {
            _resourceTilemap = resourceTilemap;
            _defaultTilemap = defaultTilemap;

            _ironTilesByRepeatIndex = EnsureTileArray(ironTilesByRepeatIndex);
            _titanTilesByRepeatIndex = EnsureTileArray(titanTilesByRepeatIndex);
            _aluminiumTilesByRepeatIndex = EnsureTileArray(aluminiumTilesByRepeatIndex);
            _rogaliteTilesByRepeatIndex = EnsureTileArray(rogaliteTilesByRepeatIndex);
            _atmosphereTilesByRepeatIndex = EnsureTileArray(atmosphereTilesByRepeatIndex);
            _defaultTilesByRepeatIndex = EnsureTileArray(defaultTilesByRepeatIndex);

            _digPreviewTilemap = digPreviewTilemap;
            _digPreviewTile = digPreviewTile;
            _digPreviewBlockedTile = digPreviewBlockedTile != null ? digPreviewBlockedTile : digPreviewTile;
            _hoverHighlightTilemap = hoverHighlightTilemap;
            _hoverHighlightActiveTile = hoverHighlightActiveTile;
            _hoverHighlightDefaultTile = hoverHighlightDefaultTile != null ? hoverHighlightDefaultTile : hoverHighlightActiveTile;

            _digMarkerTilemap = digMarkerTilemap;
            _digMarkerTile = digMarkerTile;
            _buildTaskMarkerTilemap = buildTaskMarkerTilemap;
            _buildTaskMarkerTile = buildTaskMarkerTile;
            _destructionMarkerTile = destructionMarkerTile != null ? destructionMarkerTile : digMarkerTile;
            _reservedTilemap = reservedTilemap;
            _reservedTile = reservedTile;
            _protectedResourceOverlayTilemap = protectedResourceOverlayTilemap;
            _protectedResourceOverlayTile = protectedResourceOverlayTile;
            _protectedResourceOverlayLeftTile = protectedResourceOverlayLeftTile != null ? protectedResourceOverlayLeftTile : protectedResourceOverlayTile;
            _protectedResourceOverlayRightTile = protectedResourceOverlayRightTile != null ? protectedResourceOverlayRightTile : protectedResourceOverlayTile;
            _cablePreviewTilemap = cablePreviewTilemap;
            _cablePreviewTilesByMask4 = EnsureMask4TileArray(cablePreviewTilesByMask4);
            _cableBuiltTilemap = cableBuiltTilemap;
            _cableBuiltTilesByMask4 = EnsureMask4TileArray(cableBuiltTilesByMask4);
            _waterPreviewTilemap = waterPreviewTilemap;
            _waterPreviewTilesByMask4 = EnsureMask4TileArray(waterPreviewTilesByMask4);
            _waterBuiltTilemap = waterBuiltTilemap;
            _waterBuiltTilesByMask4 = EnsureMask4TileArray(waterBuiltTilesByMask4);
            _oxygenPreviewTilemap = oxygenPreviewTilemap;
            _oxygenPreviewTilesByMask4 = EnsureMask4TileArray(oxygenPreviewTilesByMask4);
            _oxygenBuiltTilemap = oxygenBuiltTilemap;
            _oxygenBuiltTilesByMask4 = EnsureMask4TileArray(oxygenBuiltTilesByMask4);
            _lifeModuleVisualCatalog = Resources.Load<LifeModuleVisualCatalog>("LifeModule/LifeModuleVisualCatalog");
            _lifeModulePreviewTilemap = EnsureOverlayTilemap(
                "LifeModulePreviewTilemap",
                _cablePreviewTilemap != null ? _cablePreviewTilemap : _digPreviewTilemap,
                1);
            _lifeModuleBuiltTilemap = EnsureOverlayTilemap(
                "LifeModuleBuiltTilemap",
                _cableBuiltTilemap != null ? _cableBuiltTilemap : _digMarkerTilemap,
                1);
            _showPipeMaskIndexDebug = showPipeMaskIndexDebug;
            _pipeMaskIndexDebugColor = pipeMaskIndexDebugColor;
            _pipeMaskIndexDebugSortingOrder = pipeMaskIndexDebugSortingOrder;
            _pipeMaskDebugRoot = CreatePipeMaskDebugRoot();
            _materialTransitionTilemap = materialTransitionTilemap;
            _transitionTilesByOpenMask = EnsureTransitionTileArray(transitionTilesByOpenMask);
        }

        public void SetDigPreview(int x, int y, DigPreviewVisualKind previewKind)
        {
            Vector3Int position = new Vector3Int(x, y, 0);
            TileBase previewTile = previewKind switch
            {
                DigPreviewVisualKind.Allowed => _digPreviewTile,
                DigPreviewVisualKind.Blocked => _digPreviewBlockedTile,
                _ => null
            };

            Color previewColor = previewKind switch
            {
                DigPreviewVisualKind.Allowed => DigPreviewColor,
                DigPreviewVisualKind.Blocked => DigPreviewBlockedColor,
                _ => Color.white
            };

            _digPreviewTilemap.SetTile(position, previewTile);
            _digPreviewTilemap.SetTileFlags(position, TileFlags.None);
            _digPreviewTilemap.SetColor(position, previewColor);
        }

        public void SetHoverHighlightActive(int x, int y, float alpha)
        {
            SetHoverHighlightTile(x, y, _hoverHighlightActiveTile, alpha);
        }

        public void SetHoverHighlightDefault(int x, int y, float alpha)
        {
            SetHoverHighlightTile(x, y, _hoverHighlightDefaultTile, alpha);
        }

        private void SetHoverHighlightTile(int x, int y, TileBase tile, float alpha)
        {
            if (_hoverHighlightTilemap == null || tile == null)
            {
                return;
            }

            Vector3Int position = new Vector3Int(x, y, 0);
            Vector2Int key = new Vector2Int(x, y);
            if (!_hoverHighlightCells.Contains(key))
            {
                _hoverHighlightTilemap.SetTile(position, tile);
                _hoverHighlightTilemap.SetTileFlags(position, TileFlags.None);
                _hoverHighlightCells.Add(key);
            }
            else if (_hoverHighlightTilemap.GetTile(position) != tile)
            {
                _hoverHighlightTilemap.SetTile(position, tile);
                _hoverHighlightTilemap.SetTileFlags(position, TileFlags.None);
            }

            Color color = _hoverHighlightTilemap.GetColor(position);
            color.a = Mathf.Clamp01(alpha);
            _hoverHighlightTilemap.SetColor(position, color);
        }

        public void ClearHoverHighlight(int x, int y)
        {
            if (_hoverHighlightTilemap == null)
            {
                return;
            }

            Vector3Int position = new Vector3Int(x, y, 0);
            _hoverHighlightTilemap.SetTile(position, null);
            _hoverHighlightTilemap.SetColor(position, Color.white);
            _hoverHighlightCells.Remove(new Vector2Int(x, y));
        }

        public void SetCustomPreviewTile(int x, int y, TileBase previewTile)
        {
            Vector3Int cellPosition = new Vector3Int(x, y, 0);
            _digPreviewTilemap.SetTile(cellPosition, previewTile);
            _digPreviewTilemap.SetTransformMatrix(cellPosition, Matrix4x4.identity);
            _digPreviewTilemap.SetTileFlags(cellPosition, TileFlags.None);
            _digPreviewTilemap.SetColor(cellPosition, Color.white);
        }

        public void SetCustomPreviewTileByAnchor(int x, int y, TileBase previewTile, int width, int height)
        {
            Vector3Int cellPosition = new Vector3Int(x, y, 0);
            _digPreviewTilemap.SetTile(cellPosition, previewTile);
            _digPreviewTilemap.SetTransformMatrix(cellPosition, Matrix4x4.identity);
            _digPreviewTilemap.SetTileFlags(cellPosition, TileFlags.None);
            _digPreviewTilemap.SetColor(cellPosition, Color.white);

            if (previewTile == null)
            {
                return;
            }

            float offsetX = (Mathf.Max(1, width) - 1) * 0.5f;
            float offsetY = (Mathf.Max(1, height) - 1) * 0.5f;
            Matrix4x4 matrix = Matrix4x4.TRS(new Vector3(offsetX, offsetY, 0f), Quaternion.identity, Vector3.one);
            _digPreviewTilemap.SetTransformMatrix(cellPosition, matrix);
        }

        public void SetCustomPreviewTileColor(int x, int y, Color color)
        {
            Vector3Int cellPosition = new Vector3Int(x, y, 0);
            _digPreviewTilemap.SetTileFlags(cellPosition, TileFlags.None);
            _digPreviewTilemap.SetColor(cellPosition, color);
        }

        public void SetBuildPreviewTile(int x, int y, TileBase previewTile)
        {
            SetCustomPreviewTile(x, y, previewTile);
        }

        public void SetBuildPreviewTileByAnchor(int x, int y, TileBase previewTile, int width, int height)
        {
            SetCustomPreviewTileByAnchor(x, y, previewTile, width, height);
        }

        public void SetBuildPreviewTileColor(int x, int y, Color color)
        {
            SetCustomPreviewTileColor(x, y, color);
        }

        public void SetCursorBuildPreviewTile(int x, int y, TileBase previewTile)
        {
            Tilemap targetTilemap = _reservedTilemap != null ? _reservedTilemap : _digPreviewTilemap;
            if (targetTilemap == null)
            {
                return;
            }

            Vector3Int cellPosition = new Vector3Int(x, y, 0);
            targetTilemap.SetTile(cellPosition, previewTile);
            targetTilemap.SetTransformMatrix(cellPosition, Matrix4x4.identity);
            targetTilemap.SetTileFlags(cellPosition, TileFlags.None);
            targetTilemap.SetColor(cellPosition, Color.white);
        }

        public void SetCursorBuildPreviewTileByAnchor(int x, int y, TileBase previewTile, int width, int height)
        {
            Tilemap targetTilemap = _reservedTilemap != null ? _reservedTilemap : _digPreviewTilemap;
            if (targetTilemap == null)
            {
                return;
            }

            Vector3Int cellPosition = new Vector3Int(x, y, 0);
            targetTilemap.SetTile(cellPosition, previewTile);
            targetTilemap.SetTransformMatrix(cellPosition, Matrix4x4.identity);
            targetTilemap.SetTileFlags(cellPosition, TileFlags.None);
            targetTilemap.SetColor(cellPosition, Color.white);

            if (previewTile == null)
            {
                return;
            }

            float offsetX = (Mathf.Max(1, width) - 1) * 0.5f;
            float offsetY = (Mathf.Max(1, height) - 1) * 0.5f;
            Matrix4x4 matrix = Matrix4x4.TRS(new Vector3(offsetX, offsetY, 0f), Quaternion.identity, Vector3.one);
            targetTilemap.SetTransformMatrix(cellPosition, matrix);
        }

        public void SetCursorBuildPreviewTileColor(int x, int y, Color color)
        {
            Tilemap targetTilemap = _reservedTilemap != null ? _reservedTilemap : _digPreviewTilemap;
            if (targetTilemap == null)
            {
                return;
            }

            Vector3Int cellPosition = new Vector3Int(x, y, 0);
            targetTilemap.SetTileFlags(cellPosition, TileFlags.None);
            targetTilemap.SetColor(cellPosition, color);
        }

        public void ClearCursorBuildPreviewLayer()
        {
            if (_reservedTilemap != null)
            {
                _reservedTilemap.ClearAllTiles();
            }
        }

        public void ClearDigPreview()
        {
            _digPreviewTilemap.ClearAllTiles();
        }

        public void SetGroundCell(int x, int y, CellType type)
        {
            Vector3Int position = new Vector3Int(x, y, 0);
            SetDefaultCell(position, x, y);

            TileBase tile = GetNaturalTileForCell(type, x, y);
            if (_resourceTilemap != null)
            {
                _resourceTilemap.SetTile(position, tile);
            }
        }

        public void SetCustomMarkerTile(int x, int y, TileBase tile)
        {
            Vector3Int cellPosition = new Vector3Int(x, y, 0);
            _digMarkerTilemap.SetTile(cellPosition, tile);
            _digMarkerTilemap.SetTransformMatrix(cellPosition, Matrix4x4.identity);
        }

        public void SetCustomMarkerTileByAnchor(int x, int y, TileBase tile, int width, int height)
        {
            Vector3Int cellPosition = new Vector3Int(x, y, 0);
            _digMarkerTilemap.SetTile(cellPosition, tile);
            _digMarkerTilemap.SetTransformMatrix(cellPosition, Matrix4x4.identity);

            if (tile == null)
            {
                return;
            }

            float offsetX = (Mathf.Max(1, width) - 1) * 0.5f;
            float offsetY = (Mathf.Max(1, height) - 1) * 0.5f;
            Matrix4x4 matrix = Matrix4x4.TRS(new Vector3(offsetX, offsetY, 0f), Quaternion.identity, Vector3.one);
            _digMarkerTilemap.SetTransformMatrix(cellPosition, matrix);
        }

        public void RenderFull(GridState grid)
        {
            _resourceTilemap?.ClearAllTiles();
            _defaultTilemap?.ClearAllTiles();
            _protectedResourceOverlayTilemap?.ClearAllTiles();

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    SetDefaultCell(new Vector3Int(x, y, 0), x, y);

                    CellType type = grid.GetCell(x, y).Type;
                    TileBase tile = GetNaturalTileForCell(type, x, y);
                    if (_resourceTilemap == null || tile == null)
                    {
                        continue;
                    }

                    _resourceTilemap.SetTile(new Vector3Int(x, y, 0), tile);
                }
            }

            RenderProtectedResourceOverlay(grid);
        }

        private void RenderProtectedResourceOverlay(GridState grid)
        {
            if (_protectedResourceOverlayTilemap == null || _protectedResourceOverlayTile == null || grid == null)
            {
                return;
            }

            ShipLandingZoneRules.GetDigProtectionBounds(grid.Width, grid.Height, out int minX, out int maxX, out int minY, out _);

            for (int x = minX; x <= maxX; x++)
            {
                if (!grid.IsInside(x, minY))
                {
                    continue;
                }

                CellType cellType = grid.GetCell(x, minY).Type;
                if (!IsProtectedResourceCellType(cellType))
                {
                    continue;
                }

                TileBase overlayTile = ResolveProtectedResourceOverlayTile(x, minX, maxX);
                _protectedResourceOverlayTilemap.SetTile(new Vector3Int(x, minY, 0), overlayTile);
            }
        }

        private TileBase ResolveProtectedResourceOverlayTile(int x, int minX, int maxX)
        {
            if (x == minX)
            {
                return _protectedResourceOverlayLeftTile;
            }

            if (x == maxX)
            {
                return _protectedResourceOverlayRightTile;
            }

            return _protectedResourceOverlayTile;
        }

        private static bool IsProtectedResourceCellType(CellType cellType)
        {
            return cellType == CellType.Iron
                   || cellType == CellType.Titan
                   || cellType == CellType.Aluminium
                   || cellType == CellType.Rogalite;
        }

        public void SetDigMarker(int x, int y, bool marked)
        {
            Vector3Int position = new Vector3Int(x, y, 0);
            _digMarkerTilemap.SetTile(position, marked ? _digMarkerTile : null);
            // DigMarkersTilemap is shared by several marker types, so each shovel write
            // must fully normalize the cell state and not rely on previous transforms.
            _digMarkerTilemap.SetTransformMatrix(position, Matrix4x4.identity);
            _digMarkerTilemap.SetTileFlags(position, TileFlags.None);
            // Keep committed shovel markers in the sprite's original color.
            _digMarkerTilemap.SetColor(position, Color.white);
        }

        public void SetBuildTaskMarker(int x, int y, bool marked)
        {
            if (_buildTaskMarkerTilemap == null || _buildTaskMarkerTile == null)
            {
                return;
            }

            _buildTaskMarkerTilemap.SetTile(new Vector3Int(x, y, 0), marked ? _buildTaskMarkerTile : null);
        }

        public void SetDestructionMarker(int x, int y, bool marked)
        {
            Vector3Int position = new Vector3Int(x, y, 0);
            _digMarkerTilemap.SetTile(position, marked ? _destructionMarkerTile : null);
            // Destruction preview also lives on DigMarkersTilemap, so clear stale transforms/colors.
            _digMarkerTilemap.SetTransformMatrix(position, Matrix4x4.identity);
            _digMarkerTilemap.SetTileFlags(position, TileFlags.None);
            _digMarkerTilemap.SetColor(position, Color.white);
        }

        public void SetReservedDebugMarker(int x, int y, bool marked)
        {
            if (_reservedTilemap == null)
            {
                return;
            }

            Vector3Int position = new Vector3Int(x, y, 0);
            _reservedTilemap.SetTile(position, marked ? _reservedTile : null);
            _reservedTilemap.SetTileFlags(position, TileFlags.None);
            _reservedTilemap.SetColor(position, Color.white);
        }

        public void SetReservedDebugMarker(int x, int y, bool marked, Color color)
        {
            if (_reservedTilemap == null)
            {
                return;
            }

            Vector3Int position = new Vector3Int(x, y, 0);
            _reservedTilemap.SetTile(position, marked ? _reservedTile : null);
            _reservedTilemap.SetTileFlags(position, TileFlags.None);
            _reservedTilemap.SetColor(position, marked ? color : Color.white);
        }

        public void ClearReservedDebugMarkers()
        {
            if (_reservedTilemap == null)
            {
                return;
            }

            _reservedTilemap.ClearAllTiles();
        }

        public void SetCablePreview(int x, int y, bool visible, byte cableMask4)
        {
            // Preview всегда рисуем только в отдельном preview-слое.
            // Это исключает перетирание/наложение финального built-визуала.
            Tilemap targetTilemap = _cablePreviewTilemap;
            if (targetTilemap == null)
            {
                return;
            }

            Vector3Int pos = new Vector3Int(x, y, 0);
            if (!visible)
            {
                targetTilemap.SetTile(pos, null);
                targetTilemap.SetTransformMatrix(pos, Matrix4x4.identity);
                return;
            }

            TileBase tile = ResolveCableTileByMask4(cableMask4, true);
            targetTilemap.SetTile(pos, tile);
            targetTilemap.SetTileFlags(pos, TileFlags.None);
            // Cable tile is selected by full mask index, so rotation is not applied.
            targetTilemap.SetTransformMatrix(pos, Matrix4x4.identity);
        }

        /// <summary>
        /// Полностью очищает preview-слой кабелей.
        /// Используется перед полным пересчетом planned-кабелей.
        /// </summary>
        public void ClearCablePreview()
        {
            if (_cablePreviewTilemap == null)
            {
                return;
            }

            _cablePreviewTilemap.ClearAllTiles();
        }

        public void SetCableBuilt(int x, int y, bool visible, byte cableMask4)
        {
            if (_cableBuiltTilemap == null)
            {
                return;
            }

            Vector3Int pos = new Vector3Int(x, y, 0);
            if (!visible)
            {
                _cableBuiltTilemap.SetTile(pos, null);
                _cableBuiltTilemap.SetTransformMatrix(pos, Matrix4x4.identity);
                return;
            }

            // Для построенного кабеля очищаем preview только в клетке, где реально есть built-тайл.
            if (_cablePreviewTilemap != null && _cablePreviewTilemap != _cableBuiltTilemap)
            {
                _cablePreviewTilemap.SetTile(pos, null);
                _cablePreviewTilemap.SetTransformMatrix(pos, Matrix4x4.identity);
            }

            TileBase tile = ResolveCableTileByMask4(cableMask4, false);
            _cableBuiltTilemap.SetTile(pos, tile);
            _cableBuiltTilemap.SetTileFlags(pos, TileFlags.None);
            // Cable tile is selected by full mask index, so rotation is not applied.
            _cableBuiltTilemap.SetTransformMatrix(pos, Matrix4x4.identity);
        }

        public void SetWaterPreview(int x, int y, bool visible, byte waterMask4)
        {
            Vector3Int pos = new Vector3Int(x, y, 0);
            Tilemap targetTilemap = _waterPreviewTilemap;
            if (targetTilemap == null)
            {
                targetTilemap = _waterBuiltTilemap;
            }

            if (targetTilemap == null)
            {
                return;
            }

            if (!visible)
            {
                targetTilemap.SetTile(pos, null);
                targetTilemap.SetTransformMatrix(pos, Matrix4x4.identity);
                ClearPipeMaskDebugIndex(x, y);
                return;
            }

            int maskIndex = waterMask4 & 0x0F;
            TileBase tile = ResolveWaterTileByMask4(waterMask4, true);
            targetTilemap.SetTile(pos, tile);
            targetTilemap.SetTileFlags(pos, TileFlags.None);
            // Water tiles are unique for each mask, so transform must stay identity.
            targetTilemap.SetTransformMatrix(pos, Matrix4x4.identity);
            SetPipeMaskDebugIndex(x, y, maskIndex);
        }

        public void ClearWaterPreview()
        {
            if (_waterPreviewTilemap == null)
            {
                return;
            }

            _waterPreviewTilemap.ClearAllTiles();
            ClearAllPipeMaskDebugIndices();
        }

        public void SetWaterBuilt(int x, int y, bool visible, byte waterMask4)
        {
            if (_waterBuiltTilemap == null)
            {
                return;
            }

            Vector3Int pos = new Vector3Int(x, y, 0);
            if (!visible)
            {
                _waterBuiltTilemap.SetTile(pos, null);
                _waterBuiltTilemap.SetTransformMatrix(pos, Matrix4x4.identity);
                ClearPipeMaskDebugIndex(x, y);
                return;
            }

            if (_waterPreviewTilemap != null && _waterPreviewTilemap != _waterBuiltTilemap)
            {
                _waterPreviewTilemap.SetTile(pos, null);
                _waterPreviewTilemap.SetTransformMatrix(pos, Matrix4x4.identity);
            }

            int maskIndex = waterMask4 & 0x0F;
            TileBase tile = ResolveWaterTileByMask4(waterMask4, false);
            _waterBuiltTilemap.SetTile(pos, tile);
            _waterBuiltTilemap.SetTileFlags(pos, TileFlags.None);
            // Water tiles are unique for each mask, so transform must stay identity.
            _waterBuiltTilemap.SetTransformMatrix(pos, Matrix4x4.identity);
            SetPipeMaskDebugIndex(x, y, maskIndex);
        }

        public void SetOxygenPreview(int x, int y, bool visible, byte oxygenMask4)
        {
            Vector3Int pos = new Vector3Int(x, y, 0);
            Tilemap targetTilemap = _oxygenPreviewTilemap;
            if (targetTilemap == null)
            {
                targetTilemap = _oxygenBuiltTilemap;
            }

            if (targetTilemap == null)
            {
                return;
            }

            if (!visible)
            {
                targetTilemap.SetTile(pos, null);
                targetTilemap.SetTransformMatrix(pos, Matrix4x4.identity);
                ClearPipeMaskDebugIndex(x, y);
                return;
            }

            int maskIndex = oxygenMask4 & 0x0F;
            TileBase tile = ResolveOxygenTileByMask4(oxygenMask4, true);
            targetTilemap.SetTile(pos, tile);
            targetTilemap.SetTileFlags(pos, TileFlags.None);
            targetTilemap.SetTransformMatrix(pos, Matrix4x4.identity);
            SetPipeMaskDebugIndex(x, y, maskIndex);
        }

        public void ClearOxygenPreview()
        {
            if (_oxygenPreviewTilemap == null)
            {
                return;
            }

            _oxygenPreviewTilemap.ClearAllTiles();
            ClearAllPipeMaskDebugIndices();
        }

        public void SetOxygenBuilt(int x, int y, bool visible, byte oxygenMask4)
        {
            if (_oxygenBuiltTilemap == null)
            {
                return;
            }

            Vector3Int pos = new Vector3Int(x, y, 0);
            if (!visible)
            {
                _oxygenBuiltTilemap.SetTile(pos, null);
                _oxygenBuiltTilemap.SetTransformMatrix(pos, Matrix4x4.identity);
                ClearPipeMaskDebugIndex(x, y);
                return;
            }

            if (_oxygenPreviewTilemap != null && _oxygenPreviewTilemap != _oxygenBuiltTilemap)
            {
                _oxygenPreviewTilemap.SetTile(pos, null);
                _oxygenPreviewTilemap.SetTransformMatrix(pos, Matrix4x4.identity);
            }

            int maskIndex = oxygenMask4 & 0x0F;
            TileBase tile = ResolveOxygenTileByMask4(oxygenMask4, false);
            _oxygenBuiltTilemap.SetTile(pos, tile);
            _oxygenBuiltTilemap.SetTileFlags(pos, TileFlags.None);
            _oxygenBuiltTilemap.SetTransformMatrix(pos, Matrix4x4.identity);
            SetPipeMaskDebugIndex(x, y, maskIndex);
        }

        public void SetLifeModulePreview(int x, int y, LifeModulePartType partType, int width, int height, Color color)
        {
            SetLifeModulePart(_lifeModulePreviewTilemap, x, y, partType, width, height, true, color);
        }

        public void SetLifeModuleBuilt(int x, int y, LifeModulePartType partType, int width, int height)
        {
            SetLifeModulePart(_lifeModuleBuiltTilemap, x, y, partType, width, height, false, Color.white);
        }

        public void ClearLifeModulePreview()
        {
            _lifeModulePreviewTilemap?.ClearAllTiles();
        }

        public void ClearLifeModuleBuilt()
        {
            _lifeModuleBuiltTilemap?.ClearAllTiles();
        }

        private TileBase ResolveCableTileByMask4(byte cableMask4, bool isPreview)
        {
            int index = cableMask4 & 0x0F;
            TileBase[] tilesByMask = isPreview ? _cablePreviewTilesByMask4 : _cableBuiltTilesByMask4;
            return tilesByMask[index];
        }

        private TileBase ResolveWaterTileByMask4(byte waterMask4, bool isPreview)
        {
            int index = waterMask4 & 0x0F;
            TileBase[] tilesByMask = isPreview ? _waterPreviewTilesByMask4 : _waterBuiltTilesByMask4;
            return tilesByMask[index];
        }

        private TileBase ResolveOxygenTileByMask4(byte oxygenMask4, bool isPreview)
        {
            int index = oxygenMask4 & 0x0F;
            TileBase[] tilesByMask = isPreview ? _oxygenPreviewTilesByMask4 : _oxygenBuiltTilesByMask4;
            return tilesByMask[index];
        }

        private void SetLifeModulePart(Tilemap tilemap, int x, int y, LifeModulePartType partType, int width, int height, bool isPreview, Color color)
        {
            if (tilemap == null || _lifeModuleVisualCatalog == null || partType == LifeModulePartType.None)
            {
                return;
            }

            Sprite sprite = ResolveLifeModuleSprite(partType, isPreview);
            TileBase tile = ResolveLifeModuleTile(sprite);
            if (tile == null)
            {
                return;
            }

            Vector3Int cellPosition = new Vector3Int(x, y, 0);
            tilemap.SetTile(cellPosition, tile);
            tilemap.SetTileFlags(cellPosition, TileFlags.None);
            tilemap.SetColor(cellPosition, color);

            float pixelsPerUnit = sprite != null && sprite.pixelsPerUnit > 0f ? sprite.pixelsPerUnit : 1f;
            float nativeWidth = sprite != null ? sprite.rect.width / pixelsPerUnit : 1f;
            float nativeHeight = sprite != null ? sprite.rect.height / pixelsPerUnit : 1f;
            int targetWidth = Mathf.Max(1, width);
            int targetHeight = Mathf.Max(1, height);
            float scaleX = targetWidth / (float)nativeWidth;
            float scaleY = targetHeight / (float)nativeHeight;
            // Tilemap places the sprite pivot at the cell center.
            // Convert the imported sprite pivot into world-cell units so preview alignment
            // follows the actual slice pivot set in Unity.
            float pivotXUnits = sprite != null ? (sprite.pivot.x / pixelsPerUnit) * scaleX : 0.5f;
            float pivotYUnits = sprite != null ? (sprite.pivot.y / pixelsPerUnit) * scaleY : 0.5f;
            float offsetX = pivotXUnits - 0.5f;
            float offsetY = pivotYUnits - 0.5f;
            Matrix4x4 matrix = Matrix4x4.TRS(
                new Vector3(offsetX, offsetY, 0f),
                Quaternion.identity,
                new Vector3(scaleX, scaleY, 1f));
            tilemap.SetTransformMatrix(cellPosition, matrix);

            if (ENABLE_LIFE_MODULE_DEBUG_LOGS && sprite != null)
            {
              //  Vector3 tileAnchor = tilemap.tileAnchor;
              //  Debug.Log(
                  //  $"[LifeModuleRender] tilemap={tilemap.name} preview={isPreview} part={partType} cell=({x},{y}) " +
                 //   $"size={targetWidth}x{targetHeight} sprite={sprite.name} rect={sprite.rect.width}x{sprite.rect.height} " +
                 //   $"ppu={pixelsPerUnit:0.###} pivotPx=({sprite.pivot.x:0.###},{sprite.pivot.y:0.###}) " +
                 //   $"pivotUnits=({pivotXUnits:0.###},{pivotYUnits:0.###}) tileAnchor=({tileAnchor.x:0.###},{tileAnchor.y:0.###},{tileAnchor.z:0.###}) " +
               //     $"offset=({offsetX:0.###},{offsetY:0.###}) scale=({scaleX:0.###},{scaleY:0.###})");
            }
        }

        private TileBase ResolveLifeModuleTile(Sprite sprite)
        {
            if (sprite == null)
            {
                return null;
            }

            if (_lifeModuleTilesBySprite.TryGetValue(sprite, out TileBase cachedTile))
            {
                return cachedTile;
            }

            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            _lifeModuleTilesBySprite[sprite] = tile;
            return tile;
        }

        private Sprite ResolveLifeModuleSprite(LifeModulePartType partType, bool isPreview)
        {
            if (_lifeModuleVisualCatalog == null)
            {
                return null;
            }

            return partType switch
            {
                LifeModulePartType.Left => isPreview ? _lifeModuleVisualCatalog.PreviewLeftSprite : _lifeModuleVisualCatalog.BuiltLeftSprite,
                LifeModulePartType.Middle1 => isPreview ? _lifeModuleVisualCatalog.PreviewMiddle1Sprite : null,
                LifeModulePartType.Middle2 => isPreview ? _lifeModuleVisualCatalog.PreviewMiddle2Sprite : _lifeModuleVisualCatalog.BuiltMiddle2Sprite,
                LifeModulePartType.Middle3 => isPreview ? _lifeModuleVisualCatalog.PreviewMiddle3Sprite : _lifeModuleVisualCatalog.BuiltMiddle3Sprite,
                LifeModulePartType.Middle4 => isPreview ? _lifeModuleVisualCatalog.PreviewMiddle4Sprite : _lifeModuleVisualCatalog.BuiltMiddle4Sprite,
                LifeModulePartType.Middle5 => isPreview ? _lifeModuleVisualCatalog.PreviewMiddle5Sprite : _lifeModuleVisualCatalog.BuiltMiddle5Sprite,
                LifeModulePartType.Right => isPreview ? _lifeModuleVisualCatalog.PreviewRightSprite : _lifeModuleVisualCatalog.BuiltRightSprite,
                _ => null
            };
        }

        public void SetMaterialTransitionMask(int x, int y, int openMask)
        {
            if (_materialTransitionTilemap == null)
            {
                return;
            }

            if (openMask < 0 || openMask >= _transitionTilesByOpenMask.Length)
            {
                _materialTransitionTilemap.SetTile(new Vector3Int(x, y, 0), null);
                _materialTransitionIndexByCell.Remove(new Vector2Int(x, y));
                return;
            }

            _materialTransitionTilemap.SetTile(new Vector3Int(x, y, 0), _transitionTilesByOpenMask[openMask]);
            _materialTransitionIndexByCell[new Vector2Int(x, y)] = openMask;
        }

        public void ClearMaterialTransition(int x, int y)
        {
            if (_materialTransitionTilemap == null)
            {
                return;
            }

            _materialTransitionTilemap.SetTile(new Vector3Int(x, y, 0), null);
            _materialTransitionIndexByCell.Remove(new Vector2Int(x, y));
        }

        public bool TryGetMaterialTransitionIndex(int x, int y, out int transitionIndex)
        {
            return _materialTransitionIndexByCell.TryGetValue(new Vector2Int(x, y), out transitionIndex);
        }

        private TileBase GetNaturalTileForCell(CellType type, int x, int y)
        {
            int repeatIndex = GetRepeatIndex8x8(x, y);

            return type switch
            {
                CellType.Iron => _ironTilesByRepeatIndex[repeatIndex],
                CellType.Titan => _titanTilesByRepeatIndex[repeatIndex],
                CellType.Aluminium => GetAluminiumTile(repeatIndex),
                CellType.Rogalite => _rogaliteTilesByRepeatIndex[repeatIndex],
                CellType.Atmosphere => _atmosphereTilesByRepeatIndex[repeatIndex],
                _ => null
            };
        }

        private TileBase GetAluminiumTile(int repeatIndex)
        {
            TileBase tile = _aluminiumTilesByRepeatIndex[repeatIndex];
            return tile != null ? tile : _titanTilesByRepeatIndex[repeatIndex];
        }

        private void SetDefaultCell(Vector3Int position, int x, int y)
        {
            if (_defaultTilemap == null)
            {
                return;
            }

            int repeatIndex = GetRepeatIndex8x8(x, y);
            TileBase defaultTile = _defaultTilesByRepeatIndex[repeatIndex];
            if (defaultTile != null)
            {
                _defaultTilemap.SetTile(position, defaultTile);
            }
        }

        private static int GetRepeatIndex8x8(int x, int y)
        {
            int modX = Mathf.Abs(x % RepeatGridSize);
            int modY = Mathf.Abs(y % RepeatGridSize);
            return modX + (modY * RepeatGridSize);
        }

        private static TileBase[] EnsureTileArray(TileBase[] tiles)
        {
            if (tiles != null && tiles.Length == RepeatTileCount)
            {
                return tiles;
            }

            var result = new TileBase[RepeatTileCount];
            if (tiles == null)
            {
                return result;
            }

            int count = Mathf.Min(tiles.Length, result.Length);
            for (int i = 0; i < count; i++)
            {
                result[i] = tiles[i];
            }

            return result;
        }

        private static TileBase[] EnsureTransitionTileArray(TileBase[] tiles)
        {
            if (tiles != null && tiles.Length == TransitionTileCount)
            {
                return tiles;
            }

            var result = new TileBase[TransitionTileCount];
            if (tiles == null || tiles.Length == 0)
            {
                return result;
            }

            // Backward compatibility for old 16-mask setup (Up/Right/Down/Left open mask).
            if (tiles.Length == 16)
            {
                int[] validBlobMasks =
                {
                    0, 1, 4, 5, 7, 16, 17, 20, 21, 23, 28, 29, 31, 64, 65, 68, 69, 71, 80, 81, 84, 85, 87, 92, 93, 95, 112, 113, 116, 117, 119, 124, 125, 127, 193, 197, 199, 209, 213, 215, 221, 223, 241, 245, 247, 253, 255
                };

                for (int i = 0; i < validBlobMasks.Length; i++)
                {
                    int mask = validBlobMasks[i];
                    int oldOpenMask = 0;

                    // old mask bits: up=1, right=2, down=4, left=8 (open side).
                    if ((mask & 1) == 0) oldOpenMask |= 1;
                    if ((mask & 4) == 0) oldOpenMask |= 2;
                    if ((mask & 16) == 0) oldOpenMask |= 4;
                    if ((mask & 64) == 0) oldOpenMask |= 8;

                    result[i] = tiles[oldOpenMask];
                }

                return result;
            }

            int count = Mathf.Min(tiles.Length, result.Length);
            for (int i = 0; i < count; i++)
            {
                result[i] = tiles[i];
            }

            return result;
        }

        private static TileBase[] EnsureMask4TileArray(TileBase[] tiles)
        {
            const int requiredLength = 16;
            if (tiles != null && tiles.Length == requiredLength)
            {
                return tiles;
            }

            var result = new TileBase[requiredLength];
            if (tiles == null)
            {
                return result;
            }

            int count = Mathf.Min(tiles.Length, requiredLength);
            for (int i = 0; i < count; i++)
            {
                result[i] = tiles[i];
            }

            return result;
        }

        private static Tilemap EnsureOverlayTilemap(string name, Tilemap fallbackTilemap, int sortingOrderOffset)
        {
            if (fallbackTilemap == null)
            {
                return null;
            }

            Transform parent = fallbackTilemap.transform.parent;
            if (parent != null)
            {
                Transform existing = parent.Find(name);
                if (existing != null)
                {
                    Tilemap existingTilemap = existing.GetComponent<Tilemap>();
                    if (existingTilemap != null)
                    {
                        existingTilemap.tileAnchor = fallbackTilemap.tileAnchor;
                        existingTilemap.orientation = fallbackTilemap.orientation;
                    }

                    return existingTilemap;
                }
            }

            var gameObject = new GameObject(name);
            if (parent != null)
            {
                gameObject.transform.SetParent(parent, false);
            }

            var tilemap = gameObject.AddComponent<Tilemap>();
            tilemap.tileAnchor = fallbackTilemap.tileAnchor;
            tilemap.orientation = fallbackTilemap.orientation;
            var renderer = gameObject.AddComponent<TilemapRenderer>();
            TilemapRenderer fallbackRenderer = fallbackTilemap.GetComponent<TilemapRenderer>();
            if (fallbackRenderer != null)
            {
                renderer.sortOrder = fallbackRenderer.sortOrder;
                renderer.sortingLayerID = fallbackRenderer.sortingLayerID;
                renderer.sortingOrder = fallbackRenderer.sortingOrder + sortingOrderOffset;
            }

            return tilemap;
        }

        private Transform CreatePipeMaskDebugRoot()
        {
            if (!_showPipeMaskIndexDebug)
            {
                return null;
            }

            return CreateDebugRoot("PipeMaskDebugLabels");
        }

        private static Transform CreateDebugRoot(string rootName)
        {
            var root = new GameObject(rootName);
            return root.transform;
        }

        private void SetPipeMaskDebugIndex(int x, int y, int maskIndex)
        {
            if (!_showPipeMaskIndexDebug || _pipeMaskDebugRoot == null)
            {
                return;
            }

            Vector2Int cell = new Vector2Int(x, y);
            if (!_pipeMaskDebugLabelsByCell.TryGetValue(cell, out TextMesh label) || label == null)
            {
                var go = new GameObject($"PipeMask_{x}_{y}");
                go.transform.SetParent(_pipeMaskDebugRoot, false);
                label = go.AddComponent<TextMesh>();
                label.characterSize = 0.2f;
                label.fontSize = 20;
                label.anchor = TextAnchor.MiddleCenter;
                label.alignment = TextAlignment.Center;
                label.color = _pipeMaskIndexDebugColor;
                var renderer = go.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.sortingOrder = _pipeMaskIndexDebugSortingOrder;
                }

                _pipeMaskDebugLabelsByCell[cell] = label;
            }

            label.text = maskIndex.ToString();
            label.transform.position = new Vector3(x + 0.5f, y + 0.5f, 0f);
        }

        private void ClearPipeMaskDebugIndex(int x, int y)
        {
            Vector2Int cell = new Vector2Int(x, y);
            if (!_pipeMaskDebugLabelsByCell.TryGetValue(cell, out TextMesh label))
            {
                return;
            }

            _pipeMaskDebugLabelsByCell.Remove(cell);
            if (label != null)
            {
                Object.Destroy(label.gameObject);
            }
        }

        private void ClearAllPipeMaskDebugIndices()
        {
            if (_pipeMaskDebugLabelsByCell.Count == 0)
            {
                return;
            }

            foreach (TextMesh label in _pipeMaskDebugLabelsByCell.Values)
            {
                if (label != null)
                {
                    Object.Destroy(label.gameObject);
                }
            }

            _pipeMaskDebugLabelsByCell.Clear();
        }

    }
}
