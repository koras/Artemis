namespace _Project.Scripts.Data.Power
{
    /// <summary>
    /// Ребро между двумя узлами энергосети.
    /// </summary>
    public readonly struct PowerEdge
    {
        public readonly int FromNodeId;
        public readonly int ToNodeId;

        public PowerEdge(int fromNodeId, int toNodeId)
        {
            FromNodeId = fromNodeId;
            ToNodeId = toNodeId;
        }
    }
}
