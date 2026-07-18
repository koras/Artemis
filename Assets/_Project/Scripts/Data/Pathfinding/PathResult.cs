using System.Collections.Generic;

namespace _Project.Scripts.Data.Pathfinding
{
    public sealed class PathResult
    {
        public bool Success { get; }
        public IReadOnlyList<MovementActionEdge> Edges { get; }

        public PathResult(bool success, IReadOnlyList<MovementActionEdge> edges)
        {
            Success = success;
            Edges = edges;
        }

        public static PathResult Failed => new PathResult(false, new List<MovementActionEdge>());
    }
}
