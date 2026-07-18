using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace _Project.Scripts.Input
{
    /// <summary>
    /// Camera panning and zoom input.
    /// </summary>
    public sealed class CameraInputController
    {
        private const float MIN_ORTHOGRAPHIC_SIZE = 5f;
        private const float MAX_ORTHOGRAPHIC_SIZE = 8f;
        private const float ZOOM_SPEED = 0.5f;

        private readonly Camera _camera;
        private readonly float _moveSpeed;
        private readonly Transform _movementTarget;
        private readonly CinemachineBrain _cinemachineBrain;
        private readonly Vector2 _gridOrigin;
        private readonly float _worldWidth;
        private readonly float _worldHeight;

        public CameraInputController(
            Camera camera,
            float moveSpeed,
            Transform movementTarget,
            Vector2 gridOrigin,
            int gridWidth,
            int gridHeight,
            int cellSize)
        {
            _camera = camera;
            _moveSpeed = moveSpeed;
            _movementTarget = movementTarget;
            _cinemachineBrain = camera != null ? camera.GetComponent<CinemachineBrain>() : null;
            _gridOrigin = gridOrigin;
            _worldWidth = gridWidth * cellSize;
            _worldHeight = gridHeight * cellSize;
        }

        /// <summary>
        /// Call every frame from Update.
        /// </summary>
        public void Update()
        {
            if (_camera == null)
            {
                return;
            }

            UpdateZoom();
            UpdateMovement();
            ClampViewToGrid();
        }

        /// <summary>
        /// Adjusts orthographic zoom with the mouse wheel.
        /// </summary>
        private void UpdateZoom()
        {
            if (Mouse.current == null)
            {
                return;
            }

            float scrollDelta = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Approximately(scrollDelta, 0f))
            {
                return;
            }

            float currentSize = GetCurrentOrthographicSize();
            float nextSize = Mathf.Clamp(currentSize - scrollDelta * ZOOM_SPEED, MIN_ORTHOGRAPHIC_SIZE, MAX_ORTHOGRAPHIC_SIZE);
            if (Mathf.Approximately(currentSize, nextSize))
            {
                return;
            }

            ApplyZoomAroundCursor(currentSize, nextSize, Mouse.current.position.ReadValue());
        }

        /// <summary>
        /// Moves the camera anchor on the XY plane with WASD.
        /// </summary>
        private void UpdateMovement()
        {
            if (Keyboard.current == null)
            {
                return;
            }

            Vector2 move = Vector2.zero;
            if (Keyboard.current.wKey.isPressed) move.y += 1f;
            if (Keyboard.current.sKey.isPressed) move.y -= 1f;
            if (Keyboard.current.aKey.isPressed) move.x -= 1f;
            if (Keyboard.current.dKey.isPressed) move.x += 1f;

            if (move.sqrMagnitude > 1f)
            {
                move.Normalize();
            }

            // Camera input should stay responsive even when simulation speed changes via Time.timeScale.
            Vector3 delta = new Vector3(move.x, move.y, 0f) * (_moveSpeed * Time.unscaledDeltaTime);
            Transform movementTransform = _movementTarget != null ? _movementTarget : _camera.transform;
            movementTransform.position += delta;
        }

        private void ClampViewToGrid()
        {
            Transform movementTransform = _movementTarget != null ? _movementTarget : _camera.transform;
            float orthographicSize = GetCurrentOrthographicSize();
            float verticalHalfExtent = orthographicSize;
            float horizontalHalfExtent = orthographicSize * _camera.aspect;

            float minCenterX = _gridOrigin.x + horizontalHalfExtent;
            float maxCenterX = _gridOrigin.x + _worldWidth - horizontalHalfExtent;
            float minCenterY = _gridOrigin.y + verticalHalfExtent;
            float maxCenterY = _gridOrigin.y + _worldHeight - verticalHalfExtent;

            // If the viewport is larger than the grid, keep the camera centered on that axis.
            float clampedX = minCenterX <= maxCenterX
                ? Mathf.Clamp(movementTransform.position.x, minCenterX, maxCenterX)
                : _gridOrigin.x + (_worldWidth * 0.5f);
            float clampedY = minCenterY <= maxCenterY
                ? Mathf.Clamp(movementTransform.position.y, minCenterY, maxCenterY)
                : _gridOrigin.y + (_worldHeight * 0.5f);

            movementTransform.position = new Vector3(clampedX, clampedY, movementTransform.position.z);
        }

        private float GetCurrentOrthographicSize()
        {
            if (_cinemachineBrain?.ActiveVirtualCamera is CinemachineCamera cinemachineCamera)
            {
                return cinemachineCamera.Lens.OrthographicSize;
            }

            return _camera.orthographicSize;
        }

        private void ApplyZoomAroundCursor(float currentSize, float nextSize, Vector2 mouseScreenPosition)
        {
            Transform movementTransform = _movementTarget != null ? _movementTarget : _camera.transform;
            Vector2 viewportPoint = _camera.ScreenToViewportPoint(mouseScreenPosition);
            Vector2 viewportOffset = viewportPoint - new Vector2(0.5f, 0.5f);

            float currentHalfWidth = currentSize * _camera.aspect;
            float nextHalfWidth = nextSize * _camera.aspect;
            Vector2 worldOffsetBeforeZoom = new Vector2(
                viewportOffset.x * currentHalfWidth * 2f,
                viewportOffset.y * currentSize * 2f);
            Vector2 worldOffsetAfterZoom = new Vector2(
                viewportOffset.x * nextHalfWidth * 2f,
                viewportOffset.y * nextSize * 2f);
            Vector2 worldOffsetDelta = worldOffsetBeforeZoom - worldOffsetAfterZoom;

            movementTransform.position += new Vector3(worldOffsetDelta.x, worldOffsetDelta.y, 0f);

            if (_cinemachineBrain?.ActiveVirtualCamera is CinemachineCamera cinemachineCamera)
            {
                LensSettings lens = cinemachineCamera.Lens;
                lens.OrthographicSize = nextSize;
                cinemachineCamera.Lens = lens;
                return;
            }

            _camera.orthographicSize = nextSize;
        }
    }
}
