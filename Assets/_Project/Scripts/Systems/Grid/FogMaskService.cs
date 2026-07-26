using System.Collections.Generic;
using _Project.Scripts.Data.Grid;
using UnityEngine;

namespace _Project.Scripts.Systems.Grid
{
    /// <summary>
    /// Explore-only fog data: once a cell is opened, it never gets darker again.
    /// Fog around Atmosphere/Empty is revealed by distance.
    /// </summary>
    public sealed class FogMaskService
    {
        private const float FAR_ALPHA = 1f;
        private const float HALF_ALPHA = 0.5f;
        private const float OPEN_ALPHA = 0f;

        private GridState _gridState;
        private float[] _fogAlphaByCell;
        private readonly List<Vector2Int> _dirtyCells = new List<Vector2Int>();
        private float _halfDarkRadius = 2.5f;
        private float _fullDarkRadius = 5f;

        public void Initialize(GridState gridState, float halfDarkRadius, float fullDarkRadius)
        {
            _gridState = gridState;
            _fogAlphaByCell = new float[gridState.Width * gridState.Height];
            _dirtyCells.Clear();
            _halfDarkRadius = Mathf.Max(0f, halfDarkRadius);
            _fullDarkRadius = Mathf.Max(_halfDarkRadius + 0.01f, fullDarkRadius);

            // Start fully dark, then open from known open cells.
            for (int y = 0; y < _gridState.Height; y++)
            {
                for (int x = 0; x < _gridState.Width; x++)
                {
                    int index = _gridState.GetIndex(x, y);
                    _fogAlphaByCell[index] = FAR_ALPHA;
                    ref readonly Cell cell = ref _gridState.GetCell(x, y);
                    if (IsGlobalSeedType(cell.Type))
                    {
                        RevealFromOpenCell(new Vector2Int(x, y));
                    }
                }
            }
        }

        public float GetFogAlpha(int x, int y)
        {
            if (_gridState == null || !_gridState.IsInside(x, y))
            {
                return FAR_ALPHA;
            }

            return _fogAlphaByCell[_gridState.GetIndex(x, y)];
        }

        public void SetCellFog(Vector2Int cell, float alpha)
        {
            if (_gridState == null || !_gridState.IsInside(cell.x, cell.y))
            {
                return;
            }

            int index = _gridState.GetIndex(cell.x, cell.y);
            float clampedAlpha = Mathf.Clamp01(alpha);
            if (clampedAlpha >= _fogAlphaByCell[index] || Mathf.Approximately(_fogAlphaByCell[index], clampedAlpha))
            {
                // Explore-only: never darken an already explored cell.
                return;
            }

            _fogAlphaByCell[index] = clampedAlpha;
            _dirtyCells.Add(cell);
        }

        public void RevealFrom(Vector2Int originCell)
        {
            // Fog source is Atmosphere/Empty cells, not units.
        }

        public void SyncCellTypeFog(Vector2Int cell)
        {
            if (_gridState == null || !_gridState.IsInside(cell.x, cell.y))
            {
                return;
            }

            ref readonly Cell currentCell = ref _gridState.GetCell(cell.x, cell.y);
            if (currentCell.Type != CellType.Empty)
            {
                return;
            }

            RevealFromOpenCell(cell);
        }

        public IReadOnlyList<Vector2Int> ConsumeDirtyCells()
        {
            if (_dirtyCells.Count == 0)
            {
                return _dirtyCells;
            }

            List<Vector2Int> changedCells = new List<Vector2Int>(_dirtyCells);
            _dirtyCells.Clear();
            return changedCells;
        }

        private void RevealFromOpenCell(Vector2Int sourceCell)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt(sourceCell.x - _fullDarkRadius));
            int maxX = Mathf.Min(_gridState.Width - 1, Mathf.CeilToInt(sourceCell.x + _fullDarkRadius));
            int minY = Mathf.Max(0, Mathf.FloorToInt(sourceCell.y - _fullDarkRadius));
            int maxY = Mathf.Min(_gridState.Height - 1, Mathf.CeilToInt(sourceCell.y + _fullDarkRadius));

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float distance = Vector2.Distance(new Vector2(sourceCell.x, sourceCell.y), new Vector2(x, y));
                    if (distance > _fullDarkRadius)
                    {
                        continue;
                    }

                    SetCellFog(new Vector2Int(x, y), ComputeTargetAlpha(distance));
                }
            }
        }

        private float ComputeTargetAlpha(float distance)
        {
            if (distance <= 0f)
            {
                return OPEN_ALPHA;
            }

            if (distance >= _fullDarkRadius)
            {
                return FAR_ALPHA;
            }

            if (distance <= _halfDarkRadius)
            {
                float tHalf = _halfDarkRadius <= 0f ? 1f : distance / _halfDarkRadius;
                float smoothHalf = Mathf.SmoothStep(0f, 1f, tHalf);
                return Mathf.Lerp(OPEN_ALPHA, HALF_ALPHA, smoothHalf);
            }

            float range = _fullDarkRadius - _halfDarkRadius;
            float tFull = range <= 0f ? 1f : (distance - _halfDarkRadius) / range;
            float smoothFull = Mathf.SmoothStep(0f, 1f, tFull);
            return Mathf.Lerp(HALF_ALPHA, FAR_ALPHA, smoothFull);
        }

        private static bool IsGlobalSeedType(CellType cellType)
        {
            return cellType == CellType.Atmosphere;
        }

    }
}