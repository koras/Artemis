using _Project.Scripts.Data.Grid;

namespace _Project.Scripts.Systems.Pathfinding
{
    
    /**
     * правила проходимости и гравитационных осей
     */
    public static class CellTraversalRules
    {
        public static bool IsSolid(CellType type)
        {
            return type == CellType.Iron || type == CellType.Titan || type == CellType.Aluminium || type == CellType.Rogalite;
        }

        public static bool IsWalkable(CellType type)
        {
            return type == CellType.Empty || type == CellType.Atmosphere;
        }

        public static bool IsDiggable(CellType type)
        {
            return type == CellType.Iron || type == CellType.Titan || type == CellType.Aluminium || type == CellType.Rogalite;
        }

        public static bool IsBuildable(CellType type)
        {
            return type == CellType.Empty || IsDiggable(type);
        }
    }
}
