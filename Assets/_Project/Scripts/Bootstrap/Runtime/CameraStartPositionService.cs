using UnityEngine;

namespace _Project.Scripts.Bootstrap.Runtime
{
    /// <summary>
    /// Calculates and applies the initial camera anchor for the scene.
    /// </summary>
    public static class CameraStartPositionService
    {
        /// <summary>
        /// Centers the camera over the grid and configures the orthographic view.
        /// </summary>
        public static void Apply(Camera camera, Transform followTarget, int width, int height, int cellSize, Vector2 gridOrigin)
        {
            if (camera == null) return;

            float worldWidth = width * cellSize;
            float centerX = gridOrigin.x + worldWidth * 0.5f;
            float surfaceY = gridOrigin.y + (height - 18) * cellSize;
            float cameraY = surfaceY + 10f * cellSize;

            if (followTarget != null)
            {
                Vector3 followTargetPosition = followTarget.position;
                followTarget.position = new Vector3(centerX, cameraY, followTargetPosition.z);
            }
            else
            {
                Vector3 camPos = camera.transform.position;
                camera.transform.position = new Vector3(centerX, cameraY, camPos.z);
            }

            camera.orthographic = true;
            camera.orthographicSize = 7f;
        }
    }
}
