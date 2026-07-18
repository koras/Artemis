using System;
using _Project.Scripts.Data.Grid;
using _Project.Scripts.Systems.Grid;
using UnityEngine;
using UnityEngine.InputSystem;

namespace _Project.Scripts.Input
{
    /// <summary>
    /// Input for grid interaction: hover, click, and drag selection by cell.
    /// </summary>
    public sealed class GridInputController
    {
        public event Action<Vector2Int> CellClicked;

        private readonly Camera _camera;
        private readonly GridState _gridState;
        private readonly GridCoordinateConverter _converter;
        public event Action LeftDragFinished;

        public event Action<Vector2Int, Vector2Int> DragRectangleChanged;
        private Vector2Int _dragStartCell;
        // Last cell visited during the current drag. Used to emit every cell on fast cursor movement.
        private Vector2Int _lastDragCell;
        private bool _isDragging;

        public event Action RightClickPressed;

        public GridInputController(Camera camera, GridState gridState, GridCoordinateConverter converter)
        {
            _camera = camera;
            _gridState = gridState;
            _converter = converter;
        }

        /// <summary>
        /// Processes mouse input for the current frame.
        /// </summary>
        public void Update()
        {
            if (_camera == null || Mouse.current == null) return;

            // Handle RMB immediately, even when the cursor is outside the grid.
            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                RightClickPressed?.Invoke();
            }

            Vector2 mouseScreen = Mouse.current.position.ReadValue();
            Vector3 mouseWorld3 = _camera.ScreenToWorldPoint(new Vector3(mouseScreen.x, mouseScreen.y, 0f));
            Vector2 mouseWorld = new Vector2(mouseWorld3.x, mouseWorld3.y);

            Vector2Int cell = _converter.WorldToCell(mouseWorld);
            bool inside = _gridState.IsInside(cell.x, cell.y);

            // On release, capture the current cell before commit so the last drag cell is not skipped.
            if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                if (_isDragging && inside && cell != _lastDragCell)
                {
                    EmitDragPath(_lastDragCell, cell, _dragStartCell);
                    _lastDragCell = cell;
                }

                if (_isDragging)
                {
                    LeftDragFinished?.Invoke();
                }

                _isDragging = false;
            }

            if (!_gridState.IsInside(cell.x, cell.y)) return;

            // Start drag selection from a valid grid cell.
            if (Mouse.current.leftButton.wasPressedThisFrame && inside)
            {
                _dragStartCell = cell;
                _lastDragCell = cell;
                _isDragging = true;

                CellClicked?.Invoke(cell);
                // Emit a 1x1 selection so a click without movement still commits one cell on release.
                DragRectangleChanged?.Invoke(cell, cell);
            }

            // While dragging, emit every intermediate cell between the previous and current cursor positions.
            if (Mouse.current.leftButton.isPressed && _isDragging && inside)
            {
                // Avoid repeated drag events while the cursor stays in the same cell.
                if (cell == _lastDragCell)
                {
                    return;
                }

                EmitDragPath(_lastDragCell, cell, _dragStartCell);
                _lastDragCell = cell;
            }
        }

        /// <summary>
        /// Cancels an active drag without committing selected cells.
        /// </summary>
        public void CancelActiveInteraction()
        {
            _isDragging = false;
        }

        /// <summary>
        /// Emits every cell between drag points so fast cursor movement does not skip preview cells.
        /// </summary>
        private void EmitDragPath(Vector2Int from, Vector2Int to, Vector2Int dragStartCell)
        {
            int x0 = from.x;
            int y0 = from.y;
            int x1 = to.x;
            int y1 = to.y;

            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);

            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                DragRectangleChanged?.Invoke(dragStartCell, new Vector2Int(x0, y0));

                if (x0 == x1 && y0 == y1)
                {
                    break;
                }

                int e2 = err * 2;
                if (e2 > -dy)
                {
                    err -= dy;
                    x0 += sx;
                }

                if (e2 < dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }
        }
    }
}
