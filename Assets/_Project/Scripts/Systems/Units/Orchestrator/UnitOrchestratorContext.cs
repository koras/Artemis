using System;
using _Project.Scripts.Data.Grid;
using _Project.Scripts.Systems.Construction;
using _Project.Scripts.Systems.Grid;
using _Project.Scripts.Systems.Navigation;
using _Project.Scripts.Systems.Tasks;
using UnityEngine;

namespace _Project.Scripts.Systems.Units.Orchestrator
{
    public sealed class UnitOrchestratorContext
    {
        public GridState Grid;
        public GlobalTaskBoardService TaskBoard;
        public CharacterNavigationService Navigation;
        public GridCoordinateConverter GridCoordinateConverter;
        public BuildingManager BuildingManager;
        public UnitWorkCellResolver WorkCellResolver;
        public Action<Vector2Int> OnUnitCellChanged;

        public float ManualMoveNoProgressTimeoutSeconds;
        public float DailyWorkQuotaMinutes;
        public float DailyRestTargetMinutes;
        public float MealDurationMinutes;
    }
}
