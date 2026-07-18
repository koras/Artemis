namespace _Project.Scripts.Systems.Power
{
    [System.Flags]
    public enum CableDirectionMask : byte
    {
        None = 0,
        Up = 1,
        Right = 2,
        Down = 4,
        Left = 8
    }
}
