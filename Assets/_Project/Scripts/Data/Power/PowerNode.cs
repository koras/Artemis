using UnityEngine;

namespace _Project.Scripts.Data.Power
{
    /// <summary>
    /// Узел энергосети: либо точка кабеля, либо порт постройки.
    /// </summary>
    public readonly struct PowerNode
    {
        public readonly int Id;
        public readonly Vector2Int Cell;
        public readonly Vector2Int AnchorCell;
        public readonly bool IsBuildingNode;

        public PowerNode(int id, Vector2Int cell, Vector2Int anchorCell, bool isBuildingNode)
        {
            Id = id;
            Cell = cell;
            AnchorCell = anchorCell;
            IsBuildingNode = isBuildingNode;
        }
    }
}
