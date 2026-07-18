using System.Collections.Generic;
using UnityEngine;

namespace _Project.Scripts.Data.Oxygen
{
    /// <summary>
    /// Snapshot of last recalculated oxygen simulation state.
    /// </summary>
    public sealed class OxygenNetworkSnapshot
    {
        public readonly Dictionary<Vector2Int, BuildingOxygenRuntimeState> BuildingStates;
        public readonly float TotalProducedLiters;
        public readonly float TotalConsumedLiters;

        public OxygenNetworkSnapshot(
            Dictionary<Vector2Int, BuildingOxygenRuntimeState> buildingStates,
            float totalProducedLiters,
            float totalConsumedLiters)
        {
            BuildingStates = buildingStates;
            TotalProducedLiters = totalProducedLiters;
            TotalConsumedLiters = totalConsumedLiters;
        }
    }
}
