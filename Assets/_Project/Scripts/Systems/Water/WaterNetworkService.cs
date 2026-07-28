using System.Collections.Generic;
using _Project.Scripts.Data.Grid;
using UnityEngine;

namespace _Project.Scripts.Systems.Water
{
    /// <summary>
    /// Recalculates connected components of the built water pipe graph.
    /// </summary>
    public sealed class WaterNetworkService
    {
        // Пересчёт сети временно отключён, но код оставлен для последующего включения.
        private static readonly bool NetworkRecalculationDisabled = true;

        private readonly GridState _gridState;
        private int _lastComponentCount;

        public WaterNetworkService(GridState gridState)
        {
            _gridState = gridState;
        }

        public void Recalculate()
        {
            if (NetworkRecalculationDisabled)
            {
                return;
            }
            if (_gridState == null)
            {
                return;
            }

            var componentByCell = new Dictionary<Vector2Int, int>();
            int componentId = 1;

            for (int y = 0; y < _gridState.Height; y++)
            {
                for (int x = 0; x < _gridState.Width; x++)
                {
                    var cellPos = new Vector2Int(x, y);
                    Cell cell = _gridState.GetCell(x, y);
                    if (!cell.HasWater || componentByCell.ContainsKey(cellPos))
                    {
                        continue;
                    }

                    var queue = new Queue<Vector2Int>();
                    queue.Enqueue(cellPos);
                    componentByCell[cellPos] = componentId;

                    while (queue.Count > 0)
                    {
                        Vector2Int current = queue.Dequeue();
                        foreach (Vector2Int neighbor in EnumerateNeighbors4(current))
                        {
                            if (!_gridState.IsInside(neighbor.x, neighbor.y) || componentByCell.ContainsKey(neighbor))
                            {
                                continue;
                            }

                            ref readonly Cell neighborCell = ref _gridState.GetCell(neighbor.x, neighbor.y);
                            if (!neighborCell.HasWater)
                            {
                                continue;
                            }

                            componentByCell[neighbor] = componentId;
                            queue.Enqueue(neighbor);
                        }
                    }

                    componentId++;
                }
            }

            for (int y = 0; y < _gridState.Height; y++)
            {
                for (int x = 0; x < _gridState.Width; x++)
                {
                    var cellPos = new Vector2Int(x, y);
                    Cell cell = _gridState.GetCell(x, y);
                    cell.WaterNetworkId = componentByCell.TryGetValue(cellPos, out int id) ? id : 0;
                    _gridState.SetCell(x, y, cell);
                }
            }

            int componentCount = componentId - 1;
            if (componentCount != _lastComponentCount)
            {
                Debug.Log($"[WaterNetwork] Pipe components changed: {_lastComponentCount} -> {componentCount}.");
                _lastComponentCount = componentCount;
            }
        }

        private static IEnumerable<Vector2Int> EnumerateNeighbors4(Vector2Int cell)
        {
            yield return cell + Vector2Int.up;
            yield return cell + Vector2Int.right;
            yield return cell + Vector2Int.down;
            yield return cell + Vector2Int.left;
        }
    }
}
