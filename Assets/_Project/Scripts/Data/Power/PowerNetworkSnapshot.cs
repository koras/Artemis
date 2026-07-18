using System.Collections.Generic;
using UnityEngine;

namespace _Project.Scripts.Data.Power
{
    /// <summary>
    /// Снимок последнего пересчёта энергосети.
    /// </summary>
    public sealed class PowerNetworkSnapshot
    {
        public readonly Dictionary<Vector2Int, BuildingPowerRuntimeState> BuildingStates;
        public readonly float TotalGenerationKw;
        public readonly float TotalDemandKw;

        public PowerNetworkSnapshot(Dictionary<Vector2Int, BuildingPowerRuntimeState> buildingStates, float totalGenerationKw, float totalDemandKw)
        {
            BuildingStates = buildingStates;
            TotalGenerationKw = totalGenerationKw;
            TotalDemandKw = totalDemandKw;
        }
    }
}
