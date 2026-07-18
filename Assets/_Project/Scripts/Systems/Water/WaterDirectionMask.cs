namespace _Project.Scripts.Systems.Water
{
    [System.Flags]
    public enum WaterDirectionMask : byte
    {
        None = 0,
        Up = 1,
        Right = 2,
        Down = 4,
        Left = 8
    }
}
