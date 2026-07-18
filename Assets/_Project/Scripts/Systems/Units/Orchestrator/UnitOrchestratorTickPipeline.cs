using System;

namespace _Project.Scripts.Systems.Units.Orchestrator
{
    public sealed class UnitOrchestratorTickPipeline
    {
        private readonly Action<UnitTaskState, float, int> _processUnitAction;

        public UnitOrchestratorTickPipeline(Action<UnitTaskState, float, int> processUnitAction)
        {
            _processUnitAction = processUnitAction;
        }

        public void ProcessUnit(UnitTaskState state, float tickSeconds, int currentTick)
        {
            _processUnitAction(state, tickSeconds, currentTick);
        }
    }
}
