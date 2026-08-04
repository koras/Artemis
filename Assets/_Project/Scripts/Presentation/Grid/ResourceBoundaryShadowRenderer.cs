using _Project.Scripts.Data.Grid;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace _Project.Scripts.Presentation.Grid
{
    /// <summary>
    /// Maintains a small resource mask consumed by the ResourceTilemap shader.
    /// The shader uses neighbouring texels to leave a soft inset at resource boundaries.
    /// </summary>
    public sealed class ResourceBoundaryShadowRenderer
    {
        private const string ShaderName = "_Project/Resource Boundary Shadow";
        private const string ResourceMaskProperty = "_ResourceMask";
        private const string GridSizeProperty = "_GridSize";
        private const string SmoothingProperty = "_Smoothing";
        private const string BorderInsetProperty = "_BorderInset";
        private const string WaveAmplitudeProperty = "_WaveAmplitude";

        private readonly Tilemap _resourceTilemap;
        private readonly TilemapRenderer _tilemapRenderer;
        private readonly float _smoothing;
        private readonly float _borderInset;
        private readonly float _waveAmplitude;

        private Texture2D _resourceMask;
        private Color32[] _maskPixels;
        private int _width;
        private int _height;

        public ResourceBoundaryShadowRenderer(
            Tilemap resourceTilemap,
            float smoothing,
            float borderInset,
            float waveAmplitude)
        {
            _resourceTilemap = resourceTilemap;
            _tilemapRenderer = resourceTilemap != null ? resourceTilemap.GetComponent<TilemapRenderer>() : null;
            _smoothing = smoothing;
            _borderInset = borderInset;
            _waveAmplitude = waveAmplitude;

            if (_resourceTilemap == null || _tilemapRenderer == null)
            {
                return;
            }

            Material material = new Material(Shader.Find(ShaderName))
            {
                name = "ResourceBoundaryShadow (Runtime)"
            };
            _tilemapRenderer.material = material;
        }

        public void RenderFull(GridState grid)
        {
            _width = grid.Width;
            _height = grid.Height;
            EnsureMask();

            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    SetPixel(x, y, IsProtectedResourceCellType(grid.GetCell(x, y).Type));
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
            ApplyMask();
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
            material.SetFloat(WaveAmplitudeProperty, _waveAmplitude);
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
