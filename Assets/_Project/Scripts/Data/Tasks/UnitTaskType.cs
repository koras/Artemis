namespace _Project.Scripts.Data.Tasks
{
    /// <summary>
    /// Типы задач, которые может выполнять юнит.
    /// </summary>
    public enum UnitTaskType
    {
        DigCell = 0,
        Eat = 1,
        Sleep = 2,
        BuildObject = 3,
        ClearBuildCell = 4,
        DestroyObject = 5,
        BuildCable = 6,
        DestroyCable = 7,
        DeliverDroppedResource = 8,
        BuildWater = 9,
        DestroyWater = 10,
        BuildOxygen = 11,
        DestroyOxygen = 12,
        BuildLifeModule = 13
    }
}
