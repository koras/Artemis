using UnityEngine;

namespace _Project.Scripts.Data.Construction
{
    /// <summary>
    /// One visual/logical part inside a life-module chain.
    /// </summary>
    public sealed class LifeModulePartPayload
    {
        public LifeModulePartType PartType;
        public Vector2Int AnchorCell;
        public byte Width;
        public byte Height;
        public byte Order;
    }
}
