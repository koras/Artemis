using System;
using System.Collections;
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
        private readonly NavigationReachabilityMapBuilder _reachabilityMapBuilder;

        private static readonly List<NavigationPathDebugSnapshot> _debugPathSnapshotsBuffered = new List<NavigationPathDebugSnapshot>();
        private static readonly Dictionary<int, List<MovementActionEdge>> _debugPathEdgesByUnitIdBuffered = new Dictionary<int, List<MovementActionEdge>>();

        // Кэш текущего пути на юнита, чтобы не пересчитывать без необходимости.
        private readonly Dictionary<int, CachedPath> _pathsByUnitId = new Dictionary<int, CachedPath>();
        private readonly Dictionary<int, CachedReachabilityMap> _reachabilityMapsByUnitId = new Dictionary<int, CachedReachabilityMap>(128);

        public CharacterNavigationService(GridState grid)
        {
            _grid = grid;
            _pathfinder = new AStarPathfinder();
            _reachabilityMapBuilder = new NavigationReachabilityMapBuilder(grid.Width, grid.Height);
        }

        public bool TryBuildPath(int unitId, Vector2Int from, Vector2Int to, out PathResult path)
        {
            path = _pathfinder.FindPath(_grid, new PathRequest(unitId, from, to));
            // Сохраняем последний рассчитанный путь для gizmo-отладки маршрутов.
            _pathsByUnitId[unitId] = new CachedPath(from, to, path, 0, _grid.NavigationRevision);
            return path.Success;
        }

        public bool TryBuildPathToClosestReachableCell(
            int unitId,
            Vector2Int from,
            Vector2Int requestedCell,
            int maxDistanceFromStart,
            out Vector2Int reachableCell,
            out PathResult path)
        {
            path = _pathfinder.FindPathToClosestReachable(
                _grid,
                new PathRequest(unitId, from, requestedCell),
                maxDistanceFromStart,
                out reachableCell);
            _pathsByUnitId[unitId] = new CachedPath(from, reachableCell, path, 0, _grid.NavigationRevision);
            return path.Success;
        }

        /// <summary>
        /// Checks reachability through a cached traversal from the unit's current cell without building a path.
        /// </summary>
        public bool CanReachCell(int unitId, Vector2Int from, Vector2Int to)
        {
            if (!_grid.IsInside(from.x, from.y) || !_grid.IsInside(to.x, to.y))
            {
                return false;
            }

            if (from == to)
            {
                return true;
            }

            if (!_reachabilityMapsByUnitId.TryGetValue(unitId, out CachedReachabilityMap cached))
            {
                cached = new CachedReachabilityMap(_grid.Width * _grid.Height);
                _reachabilityMapsByUnitId[unitId] = cached;
            }

            if (!cached.IsValid
                || cached.StartCell != from
                || cached.NavigationRevision != _grid.NavigationRevision)
            {
                _reachabilityMapBuilder.FillReachableCells(_grid, unitId, from, cached.ReachableCells);
                cached.StartCell = from;
                cached.NavigationRevision = _grid.NavigationRevision;
                cached.IsValid = true;
            }

            return cached.ReachableCells[_grid.GetIndex(to.x, to.y)];
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
                cached = new CachedPath(currentCell, goalCell, fresh, 0, _grid.NavigationRevision);
                _pathsByUnitId[unitId] = cached;
                return cached;
            }

            bool goalChanged = cached.GoalCell != goalCell;
            bool startMoved = cached.StartCell != currentCell && cached.EdgeIndex == 0;
            bool pathConsumed = cached.Path.Success && cached.EdgeIndex >= cached.Path.Edges.Count;
            // Изменение проходимости клеток делает сохранённый маршрут недействительным.
            bool navigationChanged = cached.NavigationRevision != _grid.NavigationRevision;

            if (goalChanged || startMoved || navigationChanged || !cached.Path.Success || pathConsumed)
            {
                PathResult fresh = _pathfinder.FindPath(_grid, new PathRequest(unitId, currentCell, goalCell));
                cached = new CachedPath(currentCell, goalCell, fresh, 0, _grid.NavigationRevision);
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
            public int NavigationRevision;

            public CachedPath(
                Vector2Int startCell,
                Vector2Int goalCell,
                PathResult path,
                int edgeIndex,
                int navigationRevision)
            {
                StartCell = startCell;
                GoalCell = goalCell;
                Path = path;
                EdgeIndex = edgeIndex;
                NavigationRevision = navigationRevision;
            }
        }

        private sealed class CachedReachabilityMap
        {
            public readonly BitArray ReachableCells;
            public Vector2Int StartCell;
            public int NavigationRevision;
            public bool IsValid;

            public CachedReachabilityMap(int cellCount)
            {
                ReachableCells = new BitArray(cellCount);
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

    internal sealed class NavigationReachabilityMapBuilder
    {
        private readonly ActionGraphProvider _graphProvider = new ActionGraphProvider();
        private readonly int[] _frontierCellIndices;
        private readonly int _gridWidth;

        public NavigationReachabilityMapBuilder(int gridWidth, int gridHeight)
        {
            if (gridWidth <= 0) throw new ArgumentOutOfRangeException(nameof(gridWidth));
            if (gridHeight <= 0) throw new ArgumentOutOfRangeException(nameof(gridHeight));

            _gridWidth = gridWidth;
            _frontierCellIndices = new int[gridWidth * gridHeight];
        }

        public void FillReachableCells(GridState grid, int unitId, Vector2Int start, BitArray reachableCells)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (reachableCells == null) throw new ArgumentNullException(nameof(reachableCells));
            if (reachableCells.Length != _frontierCellIndices.Length)
            {
                throw new ArgumentException("Reachability map size must match the grid size.", nameof(reachableCells));
            }

            reachableCells.SetAll(false);
            if (!grid.IsInside(start.x, start.y))
            {
                return;
            }

            int frontierHead = 0;
            int frontierTail = 0;
            int startIndex = grid.GetIndex(start.x, start.y);
            reachableCells[startIndex] = true;
            _frontierCellIndices[frontierTail++] = startIndex;

            while (frontierHead < frontierTail)
            {
                int currentIndex = _frontierCellIndices[frontierHead++];
                var current = new Vector2Int(currentIndex % _gridWidth, currentIndex / _gridWidth);
                List<MovementActionEdge> edges = _graphProvider.BuildEdges(grid, current, unitId);

                for (int i = 0; i < edges.Count; i++)
                {
                    Vector2Int destination = edges[i].To;
                    if (destination == current || !grid.IsInside(destination.x, destination.y))
                    {
                        continue;
                    }

                    int destinationIndex = grid.GetIndex(destination.x, destination.y);
                    if (reachableCells[destinationIndex])
                    {
                        continue;
                    }

                    reachableCells[destinationIndex] = true;
                    _frontierCellIndices[frontierTail++] = destinationIndex;
                }
            }
        }
    }
}
