using System;
using _Project.Scripts.Data.Grid;
using UnityEngine;


namespace _Project.Scripts.Systems.Grid
{
    /// <summary>
    /// Отслеживает клетку под курсором и шлёт события только при изменении.
    /// </summary>
    public sealed class GridHoverSystem
    {
        public event Action<Vector2Int> CellHovered;
        public event Action CellHoverExited;

        private readonly GridState _gridState;
        private readonly GridCoordinateConverter _converter;

        private bool _hasHoveredCell;
        private Vector2Int _currentCell;

        public GridHoverSystem(GridState gridState, GridCoordinateConverter converter)
        {
            _gridState = gridState;
            _converter = converter;
        }

        /// <summary>
        /// Clears current hover state when world input is blocked by UI.
        /// </summary>
        public void ClearHover()
        {
            if (!_hasHoveredCell)
            {
                return;
            }

            _hasHoveredCell = false;
            CellHoverExited?.Invoke();
        }

        /// <summary>
        /// Updates hovered grid cell once per MonoBehaviour.Update().
        /// </summary>
        public void UpdateHover(Vector2 worldMousePosition)
        {
            Vector2Int cell = _converter.WorldToCell(worldMousePosition);
            bool isInside = _gridState.IsInside(cell.x, cell.y);

            if (!isInside)
            {
                if (_hasHoveredCell)
                {
                    _hasHoveredCell = false;
                    CellHoverExited?.Invoke();
                }

                return;
            }

            // Событие только если клетка реально изменилась.
            if (!_hasHoveredCell || cell != _currentCell)
            {
                _hasHoveredCell = true;
                _currentCell = cell;
                CellHovered?.Invoke(_currentCell);
            }
        }
    }
}