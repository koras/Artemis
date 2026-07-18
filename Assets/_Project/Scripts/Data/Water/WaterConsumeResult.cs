namespace _Project.Scripts.Data.Water
{
    /// <summary>
    /// Result of one explicit water consumption request.
    /// </summary>
    public struct WaterConsumeResult
    {
        public float RequestedLiters;
        public float GrantedLiters;
        public bool Success;
        public string Reason;
    }
}

