using _Project.Scripts.Data.Construction;

namespace _Project.Scripts.Data.Oxygen
{
    /// <summary>
    /// Runtime oxygen state of a single active building.
    /// </summary>
    public struct BuildingOxygenRuntimeState
    {
        public OxygenRole Role;
        public int OxygenNetworkId;
        public bool IsProducerEnabled;
        public float TankCurrentLiters;
        public float TankCapacityLiters;
        public float LastProducedLiters;
        public float LastReceivedLiters;
        public float LastRequestedLiters;
        public float LastConsumedLiters;
    }
}
