using _Project.Scripts.Data.Grid;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace _Project.Scripts.Presentation.Grid
{
    /// <summary>
    /// Maintains a resource-type mask and mirrors protected resource tiles into the boundary overlay.
    /// The overlay shader draws only boundaries between different protected resources.
    /// </summary>
    public sealed class ResourceBoundaryShadowRenderer
    {
        private const string ShaderName = "_Project/Protected Resource Transition";
        private const string LegacyShaderName = "_Project/Resource Boundary Wave";
        private const string DarkenShaderName = "_Project/Resource Darkening";
        private const string ResourceMaskProperty = "_ResourceMask";
        private const string GridSizeProperty = "_GridSize";
        private const string SmoothingProperty = "_Smoothing";
        private const string BorderInsetProperty = "_BorderInset";
        private const string LineOffsetProperty = "_LineOffset";
        private const string LegacyWaveColorProperty = "_WaveColor";
        private const string LegacyWaveThicknessProperty = "_WaveThickness";
        private const string LineColorProperty = "_LineColor";
        private const string LineThicknessProperty = "_LineThickness";
        private const string CornerRadiusProperty = "_CornerRadius";
        private const string WaveAmplitudeProperty = "_WaveAmplitude";
        private const string WaveFrequencyProperty = "_WaveFrequency";
        private const string DarkenColorProperty = "_DarkenColor";
        private const string DarkenAmountProperty = "_DarkenAmount";
        private const string BoundaryInsetPixelsProperty = "_BoundaryInsetPixels";
        private const string TransitionPixelsProperty = "_TransitionPixels";
        private const string PixelsPerTileProperty = "_PixelsPerTile";

        private readonly Tilemap _resourceTilemap;
        private readonly Tilemap _shaderBoardTileMap;
        private readonly TilemapRenderer _tilemapRenderer;
        private readonly bool _useProtectedResourceTransitionShader;
        private readonly bool _renderResourceCellsOnly;
        private TileBase _solidOverlayTile;
        private float _smoothing;
        private float _borderInset;
        private Color _waveColor;
        private float _waveThickness;
        private float _waveAmplitude;
        private float _waveFrequency;
        private Color _darkenColor;
        private float _darkenAmount;
        private float _boundaryInsetPixels;
        private float _transitionPixels;
        private float _pixelsPerTile;

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
            float waveFrequency,
            bool useProtectedResourceTransitionShader,
            bool renderResourceCellsOnly = false,
            Color darkenColor = default,
            float darkenAmount = 0.65f,
            float boundaryInsetPixels = 50f,
            float transitionPixels = 20f,
            float pixelsPerTile = 256f)
        {
            _resourceTilemap = resourceTilemap;
            _shaderBoardTileMap = shaderBoardTileMap;
            _tilemapRenderer = shaderBoardTileMap != null ? shaderBoardTileMap.GetComponent<TilemapRenderer>() : null;
            _useProtectedResourceTransitionShader = useProtectedResourceTransitionShader;
            _renderResourceCellsOnly = renderResourceCellsOnly;
            _smoothing = smoothing;
            _borderInset = borderInset;
            _waveColor = waveColor;
            _waveThickness = waveThickness;
            _waveAmplitude = waveAmplitude;
            _waveFrequency = waveFrequency;
            _darkenColor = darkenColor == default ? Color.black : darkenColor;
            _darkenAmount = darkenAmount;
            _boundaryInsetPixels = boundaryInsetPixels;
            _transitionPixels = transitionPixels;
            _pixelsPerTile = pixelsPerTile;

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

            string shaderName = _renderResourceCellsOnly
                ? DarkenShaderName
                : (_useProtectedResourceTransitionShader ? ShaderName : LegacyShaderName);
            Material material = new Material(Shader.Find(shaderName))
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
                    SetPixel(x, y, GetProtectedResourceId(grid.GetCell(x, y).Type));
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

            SetPixel(cell.x, cell.y, GetProtectedResourceId(cellType));

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
            material.SetFloat(LineOffsetProperty, _borderInset);
            material.SetColor(LineColorProperty, _waveColor);
            material.SetFloat(LineThicknessProperty, _waveThickness);
            material.SetFloat(CornerRadiusProperty, _waveThickness + _smoothing);
            material.SetColor(LegacyWaveColorProperty, _waveColor);
            material.SetFloat(LegacyWaveThicknessProperty, _waveThickness);
            material.SetFloat(WaveAmplitudeProperty, _waveAmplitude);
            material.SetFloat(WaveFrequencyProperty, _waveFrequency);
            material.SetColor(DarkenColorProperty, _darkenColor);
            material.SetFloat(DarkenAmountProperty, _darkenAmount);
            material.SetFloat(BoundaryInsetPixelsProperty, _boundaryInsetPixels);
            material.SetFloat(TransitionPixelsProperty, _transitionPixels);
            material.SetFloat(PixelsPerTileProperty, _pixelsPerTile);
        }

        public void UpdateDarkeningSettings(
            Color darkenColor,
            float darkenAmount,
            float boundaryInsetPixels,
            float transitionPixels,
            float pixelsPerTile)
        {
            _darkenColor = darkenColor;
            _darkenAmount = darkenAmount;
            _boundaryInsetPixels = boundaryInsetPixels;
            _transitionPixels = transitionPixels;
            _pixelsPerTile = pixelsPerTile;

            if (_tilemapRenderer == null)
            {
                return;
            }

            Material material = _tilemapRenderer.material;
            material.SetColor(DarkenColorProperty, _darkenColor);
            material.SetFloat(DarkenAmountProperty, _darkenAmount);
            material.SetFloat(BoundaryInsetPixelsProperty, _boundaryInsetPixels);
            material.SetFloat(TransitionPixelsProperty, _transitionPixels);
            material.SetFloat(PixelsPerTileProperty, _pixelsPerTile);
        }

        private void EnsureMask()
        {
            if (_resourceMask != null && _resourceMask.width == _width && _resourceMask.height == _height)
            {
                return;
            }

            Object.Destroy(_resourceMask);
            _resourceMask = new Texture2D(_width, _height, TextureFormat.RGBA32, false)
            {
                name = "ResourceBoundaryMask (Runtime)",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            _maskPixels = new Color32[_width * _height];
        }

        private void SetPixel(int x, int y, byte resourceId)
        {
            byte encodedId = (byte)(resourceId * 255 / 4);
            byte resourceFlag = resourceId > 0 ? (byte)255 : (byte)0;
            _maskPixels[y * _width + x] = new Color32(encodedId, 0, 0, resourceFlag);
        }

        private void ApplyMask()
        {
            _resourceMask.SetPixelData(_maskPixels, 0);
            _resourceMask.Apply(false, false);
            Material material = _tilemapRenderer.material;
            material.SetTexture(ResourceMaskProperty, _resourceMask);
            material.SetVector(GridSizeProperty, new Vector4(_width, _height, 0f, 0f));
            material.SetFloat(SmoothingProperty, _smoothing);
            material.SetFloat(BorderInsetProperty, _borderInset);
            material.SetFloat(LineOffsetProperty, _borderInset);
            material.SetColor(LineColorProperty, _waveColor);
            material.SetFloat(LineThicknessProperty, _waveThickness);
            material.SetFloat(CornerRadiusProperty, _waveThickness + _smoothing);
            material.SetColor(LegacyWaveColorProperty, _waveColor);
            material.SetFloat(LegacyWaveThicknessProperty, _waveThickness);
            material.SetFloat(WaveAmplitudeProperty, _waveAmplitude);
            material.SetFloat(WaveFrequencyProperty, _waveFrequency);
            material.SetColor(DarkenColorProperty, _darkenColor);
            material.SetFloat(DarkenAmountProperty, _darkenAmount);
            material.SetFloat(BoundaryInsetPixelsProperty, _boundaryInsetPixels);
            material.SetFloat(TransitionPixelsProperty, _transitionPixels);
            material.SetFloat(PixelsPerTileProperty, _pixelsPerTile);
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
            if (_renderResourceCellsOnly)
            {
                return IsResourceAt(x, y);
            }

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

        private static byte GetProtectedResourceId(CellType cellType)
        {
            // Stable IDs are stored in the R8 mask so the shader can compare resource types.
            switch (cellType)
            {
                case CellType.Iron:
                    return 1;
                case CellType.Titan:
                    return 2;
                case CellType.Aluminium:
                    return 3;
                case CellType.Rogalite:
                    return 4;
                default:
                    return 0;
            }
        }
    }
}
