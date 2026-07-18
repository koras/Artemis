using System.Collections.Generic;
using UnityEngine;

namespace _Project.Scripts.Data.Water
{
    /// <summary>
    /// Snapshot of last recalculated water simulation state.
    /// </summary>
    public sealed class WaterNetworkSnapshot
    {
        public readonly Dictionary<Vector2Int, BuildingWaterRuntimeState> BuildingStates;
        public readonly float TotalProducedLiters;
        public readonly float TotalConsumedLiters;

        public WaterNetworkSnapshot(
            Dictionary<Vector2Int, BuildingWaterRuntimeState> buildingStates,
            float totalProducedLiters,
            float totalConsumedLiters)
        {
            BuildingStates = buildingStates;
            TotalProducedLiters = totalProducedLiters;
            TotalConsumedLiters = totalConsumedLiters;
        }
    }
}

