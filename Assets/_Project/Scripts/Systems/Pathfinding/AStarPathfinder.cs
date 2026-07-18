using System.Collections.Generic;
using _Project.Scripts.Data.Grid;
using _Project.Scripts.Data.Pathfinding;
using UnityEngine;

namespace _Project.Scripts.Systems.Pathfinding
{
 public sealed class AStarPathfinder
    {
        private readonly ActionGraphProvider _graphProvider = new ActionGraphProvider();

        public PathResult FindPath(GridState grid, PathRequest request)
        {
            // open: список клеток, которые нужно исследовать (frontier).
            var open = new List<Vector2Int> { request.Start };
            // cameFrom: для каждой достигнутой клетки храним "ребро-предок",
            // чтобы потом восстановить полный путь от Goal к Start.
            var cameFrom = new Dictionary<Vector2Int, MovementActionEdge>();
            // gScore: фактическая накопленная стоимость пути от Start до клетки.
            var gScore = new Dictionary<Vector2Int, float> { [request.Start] = 0f };
            // fScore: оценка "насколько выгодно исследовать клетку дальше":
            // f = g + h, где h — эвристика до цели.
            var fScore = new Dictionary<Vector2Int, float> { [request.Start] = Heuristic(request.Start, request.Goal) };

            while (open.Count > 0)
            {
                // Берём из open клетку с минимальным fScore.
                var current = ExtractBest(open, fScore);
                if (current == request.Goal)
                {
                    // Цель достигнута — восстанавливаем путь по cameFrom.
                    return BuildResult(cameFrom, current, request.Start);
                }

                // Генерируем все допустимые действия из текущей клетки:
                // walk/fall/climb/dig/build ladder и т.п.
                var edges = _graphProvider.BuildEdges(grid, current, request.UnitId);
                for (int i = 0; i < edges.Count; i++)
                {
                    var edge = edges[i];
                    if (edge.To == current && (edge.ActionType == MovementActionType.Dig || edge.ActionType == MovementActionType.BuildLadder))
                    {
                        // Действия "на месте" не продвигают узел в A*.
                        continue;
                    }

                    float currentG = gScore.TryGetValue(current, out var g) ? g : float.PositiveInfinity;
                    // tentative — стоимость пути через current до edge.To.
                    float tentative = currentG + edge.Cost;

                    // Релаксация ребра: принимаем новый путь, если он дешевле старого.
                    if (!gScore.TryGetValue(edge.To, out var old) || tentative < old)
                    {
                        cameFrom[edge.To] = edge;
                        gScore[edge.To] = tentative;
                        fScore[edge.To] = tentative + Heuristic(edge.To, request.Goal);

                        if (!open.Contains(edge.To))
                        {
                            open.Add(edge.To);
                        }
                    }
                }
            }

            // Если frontier опустела, но цель не достигнута — пути нет.
            return PathResult.Failed;
        }

        private static Vector2Int ExtractBest(List<Vector2Int> open, Dictionary<Vector2Int, float> fScore)
        {
            int bestIndex = 0;
            float bestValue = GetF(open[0], fScore);

            for (int i = 1; i < open.Count; i++)
            {
                float value = GetF(open[i], fScore);
                if (value < bestValue)
                {
                    bestValue = value;
                    bestIndex = i;
                }
            }

            var best = open[bestIndex];
            open.RemoveAt(bestIndex);
            return best;
        }

        private static float GetF(Vector2Int node, Dictionary<Vector2Int, float> fScore)
        {
            return fScore.TryGetValue(node, out var v) ? v : float.PositiveInfinity;
        }

        private static float Heuristic(Vector2Int a, Vector2Int b)
        {
            // Манхэттен для клеточного мира.
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }

        private static PathResult BuildResult(Dictionary<Vector2Int, MovementActionEdge> cameFrom, Vector2Int current, Vector2Int start)
        {
            var edges = new List<MovementActionEdge>();
            // Идём от Goal назад к Start по "родителям".
            while (current != start)
            {
                if (!cameFrom.TryGetValue(current, out var edge)) return PathResult.Failed;
                edges.Add(edge);
                current = edge.From;
            }

            // Развернули "назад", превращаем в порядок Start -> Goal.
            edges.Reverse();
            return new PathResult(true, edges);
        }
    }
}
