using System;
using _Project.Scripts.Data.Grid;
using _Project.Scripts.Systems.Grid;
using UnityEngine;
using UnityEngine.InputSystem;

namespace _Project.Scripts.Input
{
    /// <summary>
    /// Coordinates runtime input: camera, grid clicks/drag, and hover state.
    /// </summary>
    public sealed class GameInputRuntimeCoordinator
    {
        // Camera panning and zoom controller.
        private readonly CameraInputController _cameraInputController;
        // Grid interaction controller for click, drag, and right-click.
        private readonly GridInputController _gridInputController;
        // Hover tracking over the grid.
        private readonly GridHoverSystem _gridHoverSystem;

        // Camera used to project mouse position into world space.
        private readonly Camera _camera;

        public event Action<Vector2Int> CellClicked;
        public event Action<Vector2Int, Vector2Int> DragRectangleChanged;
        public event Action RightClickPressed;
        public event Action LeftDragFinished;
        public event Action<Vector2Int> CellHovered;
        public event Action CellHoverExited;

        /// <summary>
        /// Creates the runtime input coordinator.
        /// </summary>
        public GameInputRuntimeCoordinator(
            Camera camera,
            float cameraMoveSpeed,
            Transform cameraMovementTarget,
            Vector2 gridOrigin,
            GridState gridState,
            GridCoordinateConverter gridCoordinateConverter)
        {
            _camera = camera;
            // When Cinemachine owns the camera transform, move the follow target instead.
            _cameraInputController = new CameraInputController(
                camera,
                cameraMoveSpeed,
                cameraMovementTarget,
                gridOrigin,
                gridState.Width,
                gridState.Height,
                gridState.CellSize);
            _gridInputController = new GridInputController(camera, gridState, gridCoordinateConverter);
            _gridHoverSystem = new GridHoverSystem(gridState, gridCoordinateConverter);

            _gridInputController.CellClicked += HandleCellClicked;
            _gridInputController.DragRectangleChanged += HandleDragRectangleChanged;
            _gridInputController.RightClickPressed += HandleRightClickPressed;
            _gridInputController.LeftDragFinished += HandleLeftDragFinished;

            _gridHoverSystem.CellHovered += HandleCellHovered;
            _gridHoverSystem.CellHoverExited += HandleCellHoverExited;
        }

        /// <summary>
        /// Per-frame update for camera, grid input, and hover state.
        /// </summary>
        public void Update(bool isWorldInputBlocked)
        {
            if (_camera == null) return;
            if (Mouse.current == null) return;

            if (isWorldInputBlocked)
            {
                _cameraInputController.Update();
                _gridInputController.CancelActiveInteraction();
                _gridHoverSystem.ClearHover();
                return;
            }

            _cameraInputController.Update();

            // While the middle mouse button is held, mouse dragging must not trigger grid interactions.
            if (_cameraInputController.IsMiddleMouseHeld)
            {
                _gridInputController.CancelActiveInteraction();
                _gridHoverSystem.ClearHover();
                return;
            }

            Vector2 mouseScreen = Mouse.current.position.ReadValue();
            Vector3 mouseWorld3 = _camera.ScreenToWorldPoint(new Vector3(mouseScreen.x, mouseScreen.y, 0f));
            Vector2 mouseWorld2 = new Vector2(mouseWorld3.x, mouseWorld3.y);

            _gridInputController.Update();
            _gridHoverSystem.UpdateHover(mouseWorld2);
        }

        /// <summary>
        /// Unsubscribes internal handlers. Call this from the owner's OnDestroy.
        /// </summary>
        public void Dispose()
        {
            _gridInputController.CellClicked -= HandleCellClicked;
            _gridInputController.DragRectangleChanged -= HandleDragRectangleChanged;
            _gridInputController.RightClickPressed -= HandleRightClickPressed;
            _gridInputController.LeftDragFinished -= HandleLeftDragFinished;

            _gridHoverSystem.CellHovered -= HandleCellHovered;
            _gridHoverSystem.CellHoverExited -= HandleCellHoverExited;
        }

        /// <summary>
        /// Forwards the clicked cell event.
        /// </summary>
        private void HandleCellClicked(Vector2Int cell)
        {
            CellClicked?.Invoke(cell);
        }

        /// <summary>
        /// Forwards the drag rectangle update.
        /// </summary>
        private void HandleDragRectangleChanged(Vector2Int from, Vector2Int to)
        {
            DragRectangleChanged?.Invoke(from, to);
        }

        /// <summary>
        /// Forwards the right-click event.
        /// </summary>
        private void HandleRightClickPressed()
        {
            RightClickPressed?.Invoke();
        }

        /// <summary>
        /// Forwards the drag-finished event.
        /// </summary>
        private void HandleLeftDragFinished()
        {
            LeftDragFinished?.Invoke();
        }

        /// <summary>
        /// Forwards the hovered cell event.
        /// </summary>
        private void HandleCellHovered(Vector2Int cell)
        {
            CellHovered?.Invoke(cell);
        }

        /// <summary>
        /// Forwards the hover-exit event.
        /// </summary>
        private void HandleCellHoverExited()
        {
            CellHoverExited?.Invoke();
        }
    }
}
