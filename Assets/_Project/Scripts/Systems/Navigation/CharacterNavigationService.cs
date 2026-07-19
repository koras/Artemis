using System.Collections.Generic;
using _Project.Scripts.Data.Grid;
using _Project.Scripts.Data.Pathfinding;
using _Project.Scripts.Systems.Pathfinding;
using UnityEngine;
using UnityEngine.Pool;

namespace _Project.Scripts.Systems.Navigation
{
    /// <summary>
    /// Навигация юнитов: построение пути и пошаговое движение по нему.
    /// </summary>
    public sealed class CharacterNavigationService
    {
        private readonly GridState _grid;
        private readonly AStarPathfinder _pathfinder;

        private static readonly List<NavigationPathDebugSnapshot> _debugPathSnapshotsBuffered = new List<NavigationPathDebugSnapshot>();
        private static readonly Dictionary<int, List<MovementActionEdge>> _debugPathEdgesByUnitIdBuffered = new Dictionary<int, List<MovementActionEdge>>();

        // Кэш текущего пути на юнита, чтобы не пересчитывать без необходимости.
        private readonly Dictionary<int, CachedPath> _pathsByUnitId = new Dictionary<int, CachedPath>();

        public CharacterNavigationService(GridState grid)
        {
            _grid = grid;
            _pathfinder = new AStarPathfinder();
        }

        public bool TryBuildPath(int unitId, Vector2Int from, Vector2Int to, out PathResult path)
        {
            path = _pathfinder.FindPath(_grid, new PathRequest(unitId, from, to));
            // Сохраняем последний рассчитанный путь для gizmo-отладки маршрутов.
            _pathsByUnitId[unitId] = new CachedPath(from, to, path, 0);
            return path.Success;
        }

        public void ClearPath(int unitId)
        {
            _pathsByUnitId.Remove(unitId);
            if (_debugPathEdgesByUnitIdBuffered.TryGetValue(unitId, out List<MovementActionEdge> edges))
            {
                _debugPathEdgesByUnitIdBuffered.Remove(unitId);
                ListPool<MovementActionEdge>.Release(edges);
            }
        }

        public List<NavigationPathDebugSnapshot> GetDebugPathSnapshots()
        {
            _debugPathSnapshotsBuffered.Clear();

            foreach (KeyValuePair<int, CachedPath> pair in _pathsByUnitId)
            {
                CachedPath cached = pair.Value;
                if (!cached.Path.Success) continue;

                if (!_debugPathEdgesByUnitIdBuffered.TryGetValue(pair.Key, out List<MovementActionEdge> edges))
                {
                    edges = ListPool<MovementActionEdge>.Get();
                    _debugPathEdgesByUnitIdBuffered[pair.Key] = edges;
                }

                edges.Clear();
                for (int i = 0; i < cached.Path.Edges.Count; i++)
                {
                    edges.Add(cached.Path.Edges[i]);
                }

                _debugPathSnapshotsBuffered.Add(new NavigationPathDebugSnapshot(
                    pair.Key,
                    cached.StartCell,
                    cached.GoalCell,
                    cached.EdgeIndex,
                    edges));
            }

            return _debugPathSnapshotsBuffered;
        }


        public NavigationStepResult TryStep(
            int unitId,
            ref Vector2Int currentCell,
            Vector2Int goalCell,
            out Vector2Int fromCell,
            out Vector2Int toCell,
            out MovementActionType actionType)
        {
            fromCell = currentCell;
            toCell = currentCell;
            actionType = MovementActionType.Wait;

            if (currentCell == goalCell) return NavigationStepResult.Arrived;

            CachedPath cached = GetOrRebuildPath(unitId, currentCell, goalCell);
            if (!cached.Path.Success) return NavigationStepResult.Blocked;

            if (cached.EdgeIndex >= cached.Path.Edges.Count) return NavigationStepResult.Arrived;

            MovementActionEdge edge = cached.Path.Edges[cached.EdgeIndex];
            if (edge.To == currentCell)
            {
                // Защита от "нулевых" шагов, которые не двигают юнита.
                cached.EdgeIndex++;
                _pathsByUnitId[unitId] = cached;
                return NavigationStepResult.Blocked;
            }

            currentCell = edge.To;
            fromCell = edge.From;
            toCell = edge.To;
            actionType = edge.ActionType;
            cached.EdgeIndex++;
            _pathsByUnitId[unitId] = cached;
            return NavigationStepResult.Stepped;
        }

        private CachedPath GetOrRebuildPath(int unitId, Vector2Int currentCell, Vector2Int goalCell)
        {
            if (!_pathsByUnitId.TryGetValue(unitId, out CachedPath cached))
            {
                PathResult fresh = _pathfinder.FindPath(_grid, new PathRequest(unitId, currentCell, goalCell));
                cached = new CachedPath(currentCell, goalCell, fresh, 0);
                _pathsByUnitId[unitId] = cached;
                return cached;
            }

            bool goalChanged = cached.GoalCell != goalCell;
            bool startMoved = cached.StartCell != currentCell && cached.EdgeIndex == 0;
            bool pathConsumed = cached.Path.Success && cached.EdgeIndex >= cached.Path.Edges.Count;

            if (goalChanged || startMoved || !cached.Path.Success || pathConsumed)
            {
                PathResult fresh = _pathfinder.FindPath(_grid, new PathRequest(unitId, currentCell, goalCell));
                cached = new CachedPath(currentCell, goalCell, fresh, 0);
                _pathsByUnitId[unitId] = cached;
            }

            return cached;
        }

        private struct CachedPath
        {
            public Vector2Int StartCell;
            public Vector2Int GoalCell;
            public PathResult Path;
            public int EdgeIndex;

            public CachedPath(Vector2Int startCell, Vector2Int goalCell, PathResult path, int edgeIndex)
            {
                StartCell = startCell;
                GoalCell = goalCell;
                Path = path;
                EdgeIndex = edgeIndex;
            }
        }
    }

    /// <summary>
    /// Результат одного навигационного шага.
    /// </summary>
    public enum NavigationStepResult
    {
        Stepped = 0,
        Blocked = 1,
        Arrived = 2
    }

    public readonly struct NavigationPathDebugSnapshot
    {
        public readonly int UnitId;
        public readonly Vector2Int StartCell;
        public readonly Vector2Int GoalCell;
        public readonly int EdgeIndex;
        public readonly IReadOnlyList<MovementActionEdge> Edges;

        public NavigationPathDebugSnapshot(
            int unitId,
            Vector2Int startCell,
            Vector2Int goalCell,
            int edgeIndex,
            IReadOnlyList<MovementActionEdge> edges)
        {
            UnitId = unitId;
            StartCell = startCell;
            GoalCell = goalCell;
            EdgeIndex = edgeIndex;
            Edges = edges;
        }
    }
}