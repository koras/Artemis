using UnityEngine;

namespace _Project.Scripts.Data.Power
{
    /// <summary>
    /// Runtime-состояние питания отдельной постройки.
    /// </summary>
    public struct BuildingPowerRuntimeState
    {
        public bool IsPowered;
        public float SuppliedPowerKw;
        public float RequestedPowerKw;
    }
}
