using UnityEngine;

namespace _Project.Scripts.Data.Pathfinding
{
    public readonly struct PathRequest
    {
        public readonly int UnitId;
        public readonly Vector2Int Start;
        public readonly Vector2Int Goal;

        public PathRequest(int unitId, Vector2Int start, Vector2Int goal)
        {
            UnitId = unitId;
            Start = start;
            Goal = goal;
        }
    }
}
