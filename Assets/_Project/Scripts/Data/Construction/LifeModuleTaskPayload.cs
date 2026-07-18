using UnityEngine;

namespace _Project.Scripts.Data.Construction
{
    /// <summary>
    /// Build-task payload for one life-module chain.
    /// </summary>
    public sealed class LifeModuleTaskPayload
    {
        public int GroupId;
        public Vector2Int AnchorCell;
        public int Width;
        public int Height;
        public bool IsPlacementValid;
        public bool IsExcavatingBeforeBuild;
        public int RemainingClearSubtasks;
        public bool IsBuildCostPaid;
        public int RemainingBuildTicks;
        public LifeModulePartPayload[] Parts;
        public Vector2Int[] OccupiedCells;
        public int[] ReplacedGroupIds;
    }
}
