using _Project.Scripts.Data.Construction;
using UnityEngine;

namespace _Project.Scripts.Data.Grid
{
    /// <summary>
    /// Grid cell material/type.
    /// </summary>
    public enum CellType
    {
        Empty = 0,
        Iron = 1,
        Titan = 2,
        Rogalite = 3,
        Aluminium = 11,
        Atmosphere = 4,
    }
    
    

    /// <summary>
    /// Runtime data of a single grid cell.
    /// Pure data container (no MonoBehaviour).
    /// </summary>
    public struct Cell
    {
        public bool IsDigMarked;
        public CellType Type;
        public int ResourceAmount;

        // Built object type in this cell. Null means no building object is present.
        public BuildObjectType? BuildObjectType;

        // True when the cell is occupied by an active building.
        public bool IsOccupiedByBuilding;

        public float Temperature;

        // Allows this cell to be treated as walkable for pathfinding
        // even if its CellType is usually non-walkable.
        public bool IgnoreObstacleForPathfinding;

        // Final gravity data for this cell.
        public Vector2Int GravityVector;
        public float GravityMagnitude;

        // Reservation owner for tasks (dig/build), used to prevent unit conflicts.
        public int ReservedByUnitId;

        // Cable/power overlay data.
        public bool IsCableMarked;
        public bool HasCable;
        public byte CableMask4;
        public int CableNetworkId;

        // Debug/visual cable shape state.
        public byte CableVisualShapeId;
        public float CableRotationZ;

        // Built cable layer state.
        public byte CableBuiltShapeId;
        public float CableBuiltRotationZ;

        // Cable preview layer state.
        public bool IsCablePreviewVisible;
        public byte CablePreviewMask4;
        public byte CablePreviewShapeId;
        public float CablePreviewRotationZ;

        // Water/pipes overlay data.
        public bool HasWater;
        public byte WaterMask4;
        public int WaterNetworkId;

        // Debug/visual water shape state.
        public byte WaterVisualShapeId;
        public float WaterRotationZ;

        // Built water layer state.
        public byte WaterBuiltShapeId;
        public float WaterBuiltRotationZ;

        // Water preview layer state.
        public bool IsWaterPreviewVisible;
        public byte WaterPreviewMask4;
        public byte WaterPreviewShapeId;
        public float WaterPreviewRotationZ;

        // Oxygen/pipes overlay data.
        public bool IsWaterMarked;
        public bool IsOxygenMarked;
        public bool HasOxygen;
        public byte OxygenMask4;
        public int OxygenNetworkId;

        // Debug/visual oxygen shape state.
        public byte OxygenVisualShapeId;
        public float OxygenRotationZ;

        // Built oxygen layer state.
        public byte OxygenBuiltShapeId;
        public float OxygenBuiltRotationZ;

        // Oxygen preview layer state.
        public bool IsOxygenPreviewVisible;
        public byte OxygenPreviewMask4;
        public byte OxygenPreviewShapeId;
        public float OxygenPreviewRotationZ;

        // Life-module overlay data.
        public LifeModuleType LifeModuleType;
        public LifeModulePartType LifeModulePartType;
        public int LifeModuleGroupId;
        public byte LifeModulePartWidth;
        public byte LifeModulePartOrder;
        public bool IsLifeModulePartAnchor;
    }
}
