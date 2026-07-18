namespace _Project.Scripts.Data.Construction
{
    /// <summary>
    /// Runtime-статус построечного объекта.
    /// </summary>
    public enum BuildingRuntimeStatus
    {
        Planned = 0,
        InProgress = 1,
        Active = 2,
        Cancelled = 3,
        DestructionPlanned = 4,
        Destroying = 5,
        Destroyed = 6
    }
}
