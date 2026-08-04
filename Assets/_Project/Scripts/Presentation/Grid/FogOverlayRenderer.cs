using System.Collections.Generic;
using _Project.Scripts.Data.Grid;
using _Project.Scripts.Systems.Grid;
using UnityEngine;

namespace _Project.Scripts.Presentation.Grid
{
    /// <summary>
    /// Renders fog as a single overlay sprite driven by a per-cell mask texture.
    /// </summary>
    public sealed class FogOverlayRenderer
    {
        private const string FOG_MASK_TEXTURE = "_FogMaskTex";

        private readonly GridState _gridState;
        private readonly SpriteRenderer _overlayRenderer;
        private readonly Texture2D _fogMaskTexture;

        public FogOverlayRenderer(
            GridState gridState,
            Vector2 gridOrigin,
            int cellSize,
            SpriteRenderer overlayRenderer,
            Material fogMaterial)
        {
            _gridState = gridState;
            _overlayRenderer = overlayRenderer;
            if (_overlayRenderer == null || fogMaterial == null)
            {
                return;
            }

            _fogMaskTexture = new Texture2D(_gridState.Width, _gridState.Height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            // Runtime material instance, so changing mask does not affect other renderers.
            Material runtimeMaterial = new Material(fogMaterial);
            runtimeMaterial.SetTexture(FOG_MASK_TEXTURE, _fogMaskTexture);
            runtimeMaterial.SetVector("_FogMaskSize", new Vector4(_gridState.Width, _gridState.Height, 0f, 0f));
            runtimeMaterial.SetVector("_GridSize", new Vector4(_gridState.Width, _gridState.Height, 0f, 0f));
            runtimeMaterial.SetFloat("_CellSize", cellSize);
            runtimeMaterial.SetVector("_GridOrigin", new Vector4(gridOrigin.x, gridOrigin.y, 0f, 0f));

            Rect spriteRect = new Rect(0f, 0f, _gridState.Width, _gridState.Height);
            // 1 mask pixel == 1 grid cell; world cell size is applied via transform scale.
            Sprite fogSprite = Sprite.Create(_fogMaskTexture, spriteRect, new Vector2(0f, 0f), 1f);
            _overlayRenderer.sprite = fogSprite;
            _overlayRenderer.material = runtimeMaterial;
            _overlayRenderer.transform.position = new Vector3(gridOrigin.x, gridOrigin.y, -0.5f);
            _overlayRenderer.transform.localScale = new Vector3(cellSize, cellSize, 1f);
        }

        public void ApplyFull(FogMaskService fogMaskService)
        {
            if (_fogMaskTexture == null || fogMaskService == null)
            {
                return;
            }

            for (int y = 0; y < _gridState.Height; y++)
            {
                for (int x = 0; x < _gridState.Width; x++)
                {
                    SetMaskPixel(x, y, fogMaskService.GetFogAlpha(x, y));
                }
            }

            _fogMaskTexture.Apply(false, false);
        }

        public void ApplyDelta(IReadOnlyList<Vector2Int> changedCells, FogMaskService fogMaskService)
        {
            if (_fogMaskTexture == null || fogMaskService == null || changedCells == null || changedCells.Count == 0)
            {
                return;
            }

            for (int i = 0; i < changedCells.Count; i++)
            {
                Vector2Int cell = changedCells[i];
                if (!_gridState.IsInside(cell.x, cell.y))
                {
                    continue;
                }

                SetMaskPixel(cell.x, cell.y, fogMaskService.GetFogAlpha(cell.x, cell.y));
            }

            _fogMaskTexture.Apply(false, false);
        }

        private void SetMaskPixel(int x, int y, float alpha)
        {
            float clampedAlpha = Mathf.Clamp01(alpha);
            _fogMaskTexture.SetPixel(x, y, new Color(clampedAlpha, clampedAlpha, clampedAlpha, clampedAlpha));
        }
    }
}
