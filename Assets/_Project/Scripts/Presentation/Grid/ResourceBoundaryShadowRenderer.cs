using _Project.Scripts.Data.Grid;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace _Project.Scripts.Presentation.Grid
{
    /// <summary>
    /// Maintains a resource mask and mirrors resource tiles into the boundary overlay.
    /// The overlay shader draws only the boundary, leaving ResourceTilemap untouched.
    /// </summary>
    public sealed class ResourceBoundaryShadowRenderer
    {
        private const string ShaderName = "_Project/Resource Boundary Wave";
        private const string ResourceMaskProperty = "_ResourceMask";
        private const string GridSizeProperty = "_GridSize";
        private const string SmoothingProperty = "_Smoothing";
        private const string BorderInsetProperty = "_BorderInset";
        private const string WaveColorProperty = "_WaveColor";
        private const string WaveThicknessProperty = "_WaveThickness";
        private const string WaveAmplitudeProperty = "_WaveAmplitude";
        private const string WaveFrequencyProperty = "_WaveFrequency";

        private readonly Tilemap _resourceTilemap;
        private readonly Tilemap _shaderBoardTileMap;
        private readonly TilemapRenderer _tilemapRenderer;
        private TileBase _solidOverlayTile;
        private float _smoothing;
        private float _borderInset;
        private Color _waveColor;
        private float _waveThickness;
        private float _waveAmplitude;
        private float _waveFrequency;

        private Texture2D _resourceMask;
        private Color32[] _maskPixels;
        private int _width;
        private int _height;

        public ResourceBoundaryShadowRenderer(
            Tilemap resourceTilemap,
            Tilemap shaderBoardTileMap,
            float smoothing,
            float borderInset,
            Color waveColor,
            float waveThickness,
            float waveAmplitude,
            float waveFrequency)
        {
            _resourceTilemap = resourceTilemap;
            _shaderBoardTileMap = shaderBoardTileMap;
            _tilemapRenderer = shaderBoardTileMap != null ? shaderBoardTileMap.GetComponent<TilemapRenderer>() : null;
            _smoothing = smoothing;
            _borderInset = borderInset;
            _waveColor = waveColor;
            _waveThickness = waveThickness;
            _waveAmplitude = waveAmplitude;
            _waveFrequency = waveFrequency;

            if (_shaderBoardTileMap == null || _tilemapRenderer == null)
            {
                return;
            }

            // Use a full rectangular tile for the overlay. Resource sprites can have
            // tight/custom meshes, which would clip the wave into circles and gaps.
            Tile overlayTile = ScriptableObject.CreateInstance<Tile>();
            overlayTile.name = "ResourceBoundaryOverlayTile (Runtime)";
            overlayTile.sprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
            _solidOverlayTile = overlayTile;

            Material material = new Material(Shader.Find(ShaderName))
            {
                name = "ResourceBoundaryShadow (Runtime)"
            };
            _tilemapRenderer.material = material;
        }

        public void RenderFull(GridState grid)
        {
            if (_tilemapRenderer == null)
            {
                return;
            }

            _width = grid.Width;
            _height = grid.Height;
            EnsureMask();
            _shaderBoardTileMap.ClearAllTiles();

            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    SetPixel(x, y, IsProtectedResourceCellType(grid.GetCell(x, y).Type));
                }
            }

            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    UpdateOverlayTile(x, y);
                }
            }

            ApplyMask();
        }

        public void SetCell(Vector2Int cell, CellType cellType)
        {
            if (_resourceMask == null || cell.x < 0 || cell.x >= _width || cell.y < 0 || cell.y >= _height)
            {
                return;
            }

            SetPixel(cell.x, cell.y, IsProtectedResourceCellType(cellType));

            // A changed cell can affect the boundary geometry in all eight neighbours.
            for (int y = cell.y - 1; y <= cell.y + 1; y++)
            {
                for (int x = cell.x - 1; x <= cell.x + 1; x++)
                {
                    UpdateOverlayTile(x, y);
                }
            }

            ApplyMask();
        }

        public void UpdateSettings(
            float smoothing,
            float borderInset,
            Color waveColor,
            float waveThickness,
            float waveAmplitude,
            float waveFrequency)
        {
            _smoothing = smoothing;
            _borderInset = borderInset;
            _waveColor = waveColor;
            _waveThickness = waveThickness;
            _waveAmplitude = waveAmplitude;
            _waveFrequency = waveFrequency;

            if (_tilemapRenderer == null)
            {
                return;
            }

            Material material = _tilemapRenderer.material;
            material.SetFloat(SmoothingProperty, _smoothing);
            material.SetFloat(BorderInsetProperty, _borderInset);
            material.SetColor(WaveColorProperty, _waveColor);
            material.SetFloat(WaveThicknessProperty, _waveThickness);
            material.SetFloat(WaveAmplitudeProperty, _waveAmplitude);
            material.SetFloat(WaveFrequencyProperty, _waveFrequency);
        }

        private void EnsureMask()
        {
            if (_resourceMask != null && _resourceMask.width == _width && _resourceMask.height == _height)
            {
                return;
            }

            Object.Destroy(_resourceMask);
            _resourceMask = new Texture2D(_width, _height, TextureFormat.Alpha8, false)
            {
                name = "ResourceBoundaryMask (Runtime)",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            _maskPixels = new Color32[_width * _height];
        }

        private void SetPixel(int x, int y, bool isResource)
        {
            _maskPixels[y * _width + x] = isResource ? Color.white : Color.clear;
        }

        private void ApplyMask()
        {
            _resourceMask.SetPixels32(_maskPixels);
            _resourceMask.Apply(false, false);
            Material material = _tilemapRenderer.material;
            material.SetTexture(ResourceMaskProperty, _resourceMask);
            material.SetVector(GridSizeProperty, new Vector4(_width, _height, 0f, 0f));
            material.SetFloat(SmoothingProperty, _smoothing);
            material.SetFloat(BorderInsetProperty, _borderInset);
            material.SetColor(WaveColorProperty, _waveColor);
            material.SetFloat(WaveThicknessProperty, _waveThickness);
            material.SetFloat(WaveAmplitudeProperty, _waveAmplitude);
            material.SetFloat(WaveFrequencyProperty, _waveFrequency);
        }

        private void UpdateOverlayTile(int x, int y)
        {
            if (x < 0 || x >= _width || y < 0 || y >= _height)
            {
                return;
            }

            Vector3Int position = new Vector3Int(x, y, 0);
            if (!ShouldRenderOverlayCell(x, y))
            {
                _shaderBoardTileMap.SetTile(position, null);
                return;
            }

            _shaderBoardTileMap.SetTile(position, _solidOverlayTile);
        }

        private bool ShouldRenderOverlayCell(int x, int y)
        {
            if (IsResourceAt(x, y))
            {
                return true;
            }

            for (int offsetY = -1; offsetY <= 1; offsetY++)
            {
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    if (IsResourceAt(x + offsetX, y + offsetY))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool IsResourceAt(int x, int y)
        {
            return x >= 0
                   && x < _width
                   && y >= 0
                   && y < _height
                   && _maskPixels[y * _width + x].a > 0;
        }

        private static bool IsProtectedResourceCellType(CellType cellType)
        {
            return cellType == CellType.Iron
                   || cellType == CellType.Titan
                   || cellType == CellType.Aluminium
                   || cellType == CellType.Rogalite;
        }
    }
}
