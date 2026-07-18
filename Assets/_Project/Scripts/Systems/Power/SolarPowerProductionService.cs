using _Project.Scripts.Data.Construction;
using _Project.Scripts.Systems.Simulation;

namespace _Project.Scripts.Systems.Power
{
    /// <summary>
    /// Сервис расчёта генерации SolarPanel в зависимости от времени суток.
    /// </summary>
    public sealed class SolarPowerProductionService
    {
        private float _globalGenerationMultiplier = 1f;

        public void SetGlobalGenerationMultiplier(float multiplier)
        {
            _globalGenerationMultiplier = multiplier < 0f ? 0f : multiplier;
        }
        /// <summary>
        /// Возвращает текущую генерацию панели в кВт.
        /// </summary>
        public float GetCurrentGenerationKw(BuildingDef buildingDef, GameTimeService gameTimeService)
        {
            if (buildingDef == null || gameTimeService == null) return 0f;
            if (buildingDef.ObjectType != BuildObjectType.SolarPanel) return 0f;
            if (!gameTimeService.IsDay) return 0f;
            float baseGeneration = buildingDef.PowerGenerationKwDay > 0f ? buildingDef.PowerGenerationKwDay : 0f;
            return baseGeneration * _globalGenerationMultiplier;
        }
    }
}
