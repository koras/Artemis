using _Project.Scripts.Data.Construction;

namespace _Project.Scripts.Data.Water
{
    /// <summary>
    /// Runtime water state of a single active building.
    /// </summary>
    public struct BuildingWaterRuntimeState
    {
        public WaterRole Role;
        public int WaterNetworkId;
        public bool IsProducerEnabled;
        public float TankCurrentLiters;
        public float TankCapacityLiters;
        public float LastProducedLiters;
        public float LastReceivedLiters;
        public float LastRequestedLiters;
        public float LastConsumedLiters;
    }
}
