using _Project.Scripts.Data.Grid;
using _Project.Scripts.Systems.Construction;
using _Project.Scripts.Systems.Units;
using UnityEngine;
using System;

namespace _Project.Scripts.Systems.Tasks
{
    /// <summary>
    /// Executes dig-task work for units.
    /// </summary>
    public sealed class TaskExecutionService
    {
        // World grid state used to read/update target cells.
        private readonly GridState _grid;

        // Policy that returns dig duration by source cell type.
        private readonly DigDurationPolicy _digDurationPolicy;

        // Enables optional debug logs.
        private readonly bool _enableLogs;

        // Visual callbacks for cell updates after dig completion.
        private readonly Action<Vector2Int> _onDigCompletedVisual;
        private readonly Action<Vector2Int> _onCellEmptied;

        /// <summary>
        /// Creates a task execution service.
        /// </summary>
        /// <param name="grid">World grid state.</param>
        /// <param name="digDurationPolicy">Dig duration rules by cell type.</param>
        /// <param name="enableLogs">Debug log switch.</param>
        public TaskExecutionService(
            GridState grid,
            DigDurationPolicy digDurationPolicy,
            bool enableLogs,
            Action<Vector2Int> onDigCompletedVisual,
            Action<Vector2Int> onCellEmptied)
        {
            _grid = grid;
            _digDurationPolicy = digDurationPolicy;
            _enableLogs = enableLogs;
            _onDigCompletedVisual = onDigCompletedVisual;
            _onCellEmptied = onCellEmptied;
        }

        /// <summary>
        /// Starts dig work for a unit and initializes work timer/state.
        /// </summary>
        /// <param name="unitState">Mutable unit task runtime state.</param>
        /// <param name="targetCellType">Type of the cell being dug.</param>
        /// <returns>True when dig work is started.</returns>
        public bool TryStartDig(ref UnitTaskState unitState, CellType targetCellType)
        {
            // Resolve dig duration for the current material.
            float duration = _digDurationPolicy.GetSeconds(targetCellType);

            // Move unit into work state and set timer.
            unitState.RemainingWorkSeconds = duration;
            unitState.State = UnitExecutionState.Working;

            if (_enableLogs)
            {
                // Debug.Log($"[UnitAI] unit={unitState.UnitId} started dig cell=({unitState.CurrentTaskTargetCell.x},{unitState.CurrentTaskTargetCell.y}) remaining={duration:0.0}s");
            }

            return true;
        }

        /// <summary>
        /// Executes one dig tick and applies result when work completes.
        /// </summary>
        /// <param name="unitState">Mutable unit task runtime state.</param>
        /// <param name="tickSeconds">Tick duration in seconds.</param>
        /// <returns>
        /// False when work is still in progress.
        /// True when dig step is completed (or target became unavailable/protected).
        /// </returns>
        public bool TickDig(ref UnitTaskState unitState, float tickSeconds)
        {
            // Advance remaining work time.
            unitState.RemainingWorkSeconds -= tickSeconds;

            if (_enableLogs)
            {
                // Clamp value for cleaner debug output.
                float left = Mathf.Max(0f, unitState.RemainingWorkSeconds);
                // Debug.Log($"[UnitAI] unit={unitState.UnitId} dig progress remaining={left:0.0}s");
            }

            // Keep working until timer reaches zero.
            if (unitState.RemainingWorkSeconds > 0f) return false;

            // Work finished: resolve target cell.
            Vector2Int target = unitState.CurrentTaskTargetCell;

            // If target is out of bounds, complete without applying changes.
            if (!_grid.IsInside(target.x, target.y)) return true;

            // Block dig in the protected top band.
            if (ShipLandingZoneRules.IsInsideDigProtectionZone(_grid.Width, _grid.Height, target))
            {
                Cell protectedCell = _grid.GetCell(target.x, target.y);
                if (protectedCell.IsDigMarked)
                {
                    protectedCell.IsDigMarked = false;
                    _grid.SetCell(target.x, target.y, protectedCell);
                }

                return true;
            }

            // Apply dig result to the target cell.
            Cell cell = _grid.GetCell(target.x, target.y);
            cell.Type = CellType.Empty;
            cell.ResourceAmount = 0;
            cell.BuildObjectType = null;
            cell.IsDigMarked = false;
            _grid.SetCell(target.x, target.y, cell);
            _onCellEmptied?.Invoke(target);

            // Notify visuals that the cell has been changed.
            _onDigCompletedVisual?.Invoke(target);
            return true;
        }
    }
}
