namespace _Project.Scripts.Systems.Oxygen
{
    [System.Flags]
    public enum OxygenDirectionMask : byte
    {
        None = 0,
        Up = 1,
        Right = 2,
        Down = 4,
        Left = 8
    }
}
