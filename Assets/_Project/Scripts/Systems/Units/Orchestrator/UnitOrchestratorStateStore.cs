using System.Collections.Generic;

namespace _Project.Scripts.Systems.Units.Orchestrator
{
    public sealed class UnitOrchestratorStateStore
    {
        public readonly Dictionary<int, UnitTaskState> StatesByUnitId = new Dictionary<int, UnitTaskState>();
        public readonly List<int> UnitOrder = new List<int>();
        public readonly Dictionary<int, Dictionary<int, float>> TaskCooldownsByUnitId = new Dictionary<int, Dictionary<int, float>>();
        public int RoundRobinStartIndex;
    }
}
