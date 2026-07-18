using _Project.Scripts.Data.Grid;

namespace _Project.Scripts.Systems.Simulation
{
    /// <summary>
    /// Performs one simulation tick over the grid state.
    /// Current demo rule: each cell temperature moves toward 18.
    /// </summary>
    public sealed class SimulationSystem
    {
        public int TickNumber { get; private set; }

        public void Tick(GridState gridState)
        {
            var cells = gridState.GetRawCells();

            // Demo rule: temperature in each cell converges to 18.
            for (int i = 0; i < cells.Length; i++)
            {
                var cell = cells[i];

                if (cell.Temperature > 18f)
                {
                    cell.Temperature -= 0.1f;
                }
                else if (cell.Temperature < 18f)
                {
                    cell.Temperature += 0.1f;
                }

                cells[i] = cell;
            }

            TickNumber++;
        }
    }
}
