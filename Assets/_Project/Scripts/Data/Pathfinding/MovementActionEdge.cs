using UnityEngine;

namespace _Project.Scripts.Data.Pathfinding
{
    public readonly struct MovementActionEdge
    {
        public readonly Vector2Int From;
        public readonly Vector2Int To;
        public readonly MovementActionType ActionType;
        public readonly float Cost;

        public MovementActionEdge(Vector2Int from, Vector2Int to, MovementActionType actionType, float cost)
        {
            From = from;
            To = to;
            ActionType = actionType;
            Cost = cost;
        }
    }
}
