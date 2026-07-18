namespace _Project.Scripts.Data.Oxygen
{
    /// <summary>
    /// Result of one explicit oxygen consumption request.
    /// </summary>
    public struct OxygenConsumeResult
    {
        public float RequestedLiters;
        public float GrantedLiters;
        public bool Success;
        public string Reason;
    }
}
