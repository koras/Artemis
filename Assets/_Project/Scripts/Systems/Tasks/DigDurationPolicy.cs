using _Project.Scripts.Data.Grid;

namespace _Project.Scripts.Systems.Tasks
{
    /// <summary>
    /// Правило длительности копки по типу породы.
    /// </summary>
    public sealed class DigDurationPolicy
    {
        public float GetSeconds(CellType cellType)
        {
            switch (cellType)
            {
                case CellType.Iron: return 1.1f;
                case CellType.Titan: return 1.1f;
                case CellType.Aluminium: return 1.1f;
                case CellType.Rogalite: return 1.1f;
                default: return 1.1f;
            }
        }
    }
}
