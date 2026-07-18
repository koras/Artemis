using System.Collections.Generic;
using _Project.Scripts.Data.Grid;
using _Project.Scripts.Data.Pathfinding;
using _Project.Scripts.Systems.Pathfinding;
using _Project.Scripts.Systems.Grid;
using _Project.Scripts.Systems.Navigation;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace _Project.Scripts.Bootstrap.Runtime
{
    /// <summary>
    /// Isolates runtime and gizmo debug rendering from bootstrap orchestration flow.
    /// </summary>
    internal sealed class BootstrapDebugFacade
    {
        private readonly float _navigationPathGizmoPointRadius;

        public BootstrapDebugFacade(float navigationPathGizmoPointRadius)
        {
            _navigationPathGizmoPointRadius = navigationPathGizmoPointRadius;
        }

        public bool HasHoveredPowerDebugCell { get; private set; }
        public Vector2Int HoveredPowerDebugCell { get; private set; }

        public void HandlePowerDebugCellHovered(Vector2Int cell)
        {
            HasHoveredPowerDebugCell = true;
            HoveredPowerDebugCell = cell;
        }

        public void HandlePowerDebugCellHoverExited()
        {
            HasHoveredPowerDebugCell = false;
        }

        public void DrawNavigationPathGizmos(CharacterNavigationService characterNavigationService, GridCoordinateConverter gridCoordinateConverter)
        {
            if (characterNavigationService == null || gridCoordinateConverter == null)
            {
                return;
            }

            List<NavigationPathDebugSnapshot> pathSnapshots = characterNavigationService.GetDebugPathSnapshots();
            for (int i = 0; i < pathSnapshots.Count; i++)
            {
                DrawNavigationPathGizmo(pathSnapshots[i], gridCoordinateConverter);
            }
        }

        public void DrawNavigationWalkabilityGizmos(
            CharacterNavigationService characterNavigationService,
            GridCoordinateConverter gridCoordinateConverter,
            GridState gridState,
            int extraRadiusInCells)
        {
#if UNITY_EDITOR
            if (characterNavigationService == null || gridCoordinateConverter == null || gridState == null)
            {
                return;
            }

            List<NavigationPathDebugSnapshot> pathSnapshots = characterNavigationService.GetDebugPathSnapshots();
            if (pathSnapshots.Count == 0)
            {
                return;
            }

            GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
            style.alignment = TextAnchor.MiddleCenter;
            style.fontSize = 10;

            for (int i = 0; i < pathSnapshots.Count; i++)
            {
                DrawWalkabilityForSnapshot(pathSnapshots[i], gridCoordinateConverter, gridState, extraRadiusInCells, style);
            }
#endif
        }

        public void DrawNavigationPathRuntime(CharacterNavigationService characterNavigationService, GridCoordinateConverter gridCoordinateConverter)
        {
            if (characterNavigationService == null || gridCoordinateConverter == null)
            {
                return;
            }

            List<NavigationPathDebugSnapshot> pathSnapshots = characterNavigationService.GetDebugPathSnapshots();
            for (int i = 0; i < pathSnapshots.Count; i++)
            {
                NavigationPathDebugSnapshot pathSnapshot = pathSnapshots[i];
                if (pathSnapshot.Edges == null || pathSnapshot.Edges.Count == 0)
                {
                    continue;
                }

                for (int edgeIndex = 0; edgeIndex < pathSnapshot.Edges.Count; edgeIndex++)
                {
                    MovementActionEdge edge = pathSnapshot.Edges[edgeIndex];
                    Vector2 from2D = gridCoordinateConverter.CellToWorldCenter(edge.From);
                    Vector2 to2D = gridCoordinateConverter.CellToWorldCenter(edge.To);
                    Vector3 fromWorld = new Vector3(from2D.x, from2D.y, 0f);
                    Vector3 toWorld = new Vector3(to2D.x, to2D.y, 0f);
                    Color color = edgeIndex < pathSnapshot.EdgeIndex
                        ? new Color(0.35f, 0.35f, 0.35f, 1f)
                        : Color.cyan;
                    Debug.DrawLine(fromWorld, toWorld, color, 0f, false);
                }
            }
        }

        private void DrawNavigationPathGizmo(NavigationPathDebugSnapshot pathSnapshot, GridCoordinateConverter gridCoordinateConverter)
        {
            if (pathSnapshot.Edges == null || pathSnapshot.Edges.Count == 0)
            {
                return;
            }

            Color previousColor = Gizmos.color;

            for (int i = 0; i < pathSnapshot.Edges.Count; i++)
            {
                MovementActionEdge edge = pathSnapshot.Edges[i];
                Vector3 fromWorld = CellToGizmoPoint(edge.From, gridCoordinateConverter);
                Vector3 toWorld = CellToGizmoPoint(edge.To, gridCoordinateConverter);
                Gizmos.color = i < pathSnapshot.EdgeIndex
                    ? new Color(0.35f, 0.35f, 0.35f, 0.65f)
                    : new Color(0f, 1f, 1f, 0.9f);
                Gizmos.DrawLine(fromWorld, toWorld);
                Gizmos.DrawSphere(toWorld, _navigationPathGizmoPointRadius);
            }

            Gizmos.color = Color.green;
            Gizmos.DrawSphere(CellToGizmoPoint(pathSnapshot.StartCell, gridCoordinateConverter), _navigationPathGizmoPointRadius * 1.5f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(CellToGizmoPoint(pathSnapshot.GoalCell, gridCoordinateConverter), _navigationPathGizmoPointRadius * 1.8f);
            Gizmos.color = previousColor;
        }

        private static Vector3 CellToGizmoPoint(Vector2Int cell, GridCoordinateConverter gridCoordinateConverter)
        {
            Vector2 world = gridCoordinateConverter.CellToWorldCenter(cell);
            return new Vector3(world.x, world.y, 0f);
        }

#if UNITY_EDITOR
        private static void DrawWalkabilityForSnapshot(
            NavigationPathDebugSnapshot pathSnapshot,
            GridCoordinateConverter gridCoordinateConverter,
            GridState gridState,
            int extraRadiusInCells,
            GUIStyle style)
        {
            int minX = Mathf.Min(pathSnapshot.StartCell.x, pathSnapshot.GoalCell.x);
            int maxX = Mathf.Max(pathSnapshot.StartCell.x, pathSnapshot.GoalCell.x);
            int minY = Mathf.Min(pathSnapshot.StartCell.y, pathSnapshot.GoalCell.y);
            int maxY = Mathf.Max(pathSnapshot.StartCell.y, pathSnapshot.GoalCell.y);

            int margin = Mathf.Max(0, extraRadiusInCells);
            minX = Mathf.Clamp(minX - margin, 0, gridState.Width - 1);
            maxX = Mathf.Clamp(maxX + margin, 0, gridState.Width - 1);
            minY = Mathf.Clamp(minY - margin, 0, gridState.Height - 1);
            maxY = Mathf.Clamp(maxY + margin, 0, gridState.Height - 1);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    var cell = new Vector2Int(x, y);
                    bool canOccupy = ActionGraphProvider.CanOccupyCellForMovement(gridState, cell);
                    style.normal.textColor = canOccupy ? Color.green : Color.red;
                    Vector3 labelPos = CellToGizmoPoint(cell, gridCoordinateConverter) + new Vector3(0f, 0.17f, 0f);
                    Handles.Label(labelPos, canOccupy ? "1" : "0", style);
                }
            }
        }
#endif
    }
}
