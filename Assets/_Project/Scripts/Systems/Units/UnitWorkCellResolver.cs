using _Project.Scripts.Data.Grid;
using _Project.Scripts.Data.Tasks;
using _Project.Scripts.Data.Construction;
using _Project.Scripts.Systems.Navigation;
using _Project.Scripts.Systems.Pathfinding;
using System.Collections.Generic;
using UnityEngine;

namespace _Project.Scripts.Systems.Units
{
    /// <summary>
    /// Вычисляет достижимые клетки для перемещения и рабочие клетки вокруг целевой задачи.
    /// </summary>
    public sealed class UnitWorkCellResolver
    {
        private readonly GridState _grid;
        private readonly CharacterNavigationService _navigation;
        private readonly int _taskWorkMaxDistance;
        private const int MAX_DIAGONAL_TASK_WORK_DISTANCE = 1;

        /// <summary>
        /// Создаёт резолвер клеток и принимает зависимости для работы с сеткой и навигацией.
        /// </summary>
        // Method UnitWorkCellResolver: executes the UnitWorkCellResolver workflow.
        public UnitWorkCellResolver(GridState grid, CharacterNavigationService navigation, int taskWorkMaxDistance)
        {
            _grid = grid;
            _navigation = navigation;
            _taskWorkMaxDistance = taskWorkMaxDistance;
        }

        /// <summary>
        /// Ищет ближайшую достижимую клетку к точке запроса (или саму точку, если путь есть).
        /// </summary>
        // Method TryFindNearestReachableCell: executes the TryFindNearestReachableCell workflow.
        public bool TryFindNearestReachableCell(int unitId, Vector2Int unitCell, Vector2Int requestedCell, out Vector2Int reachableCell)
        {
            reachableCell = unitCell;

            if (_grid.IsInside(requestedCell.x, requestedCell.y)
                && _navigation.TryBuildPath(unitId, unitCell, requestedCell, out _))
            {
                reachableCell = requestedCell;
                return true;
            }

            int maxRadius = Mathf.Max(_grid.Width, _grid.Height);
            for (int radius = 1; radius <= maxRadius; radius++)
            {
                bool foundCandidateOnRadius = false;

                for (int x = requestedCell.x - radius; x <= requestedCell.x + radius; x++)
                {
                    if (TryUseReachableCandidate(unitId, unitCell, x, requestedCell.y - radius, ref reachableCell)) return true;
                    if (TryUseReachableCandidate(unitId, unitCell, x, requestedCell.y + radius, ref reachableCell)) return true;
                    foundCandidateOnRadius = true;
                }

                for (int y = requestedCell.y - radius + 1; y <= requestedCell.y + radius - 1; y++)
                {
                    if (TryUseReachableCandidate(unitId, unitCell, requestedCell.x - radius, y, ref reachableCell)) return true;
                    if (TryUseReachableCandidate(unitId, unitCell, requestedCell.x + radius, y, ref reachableCell)) return true;
                    foundCandidateOnRadius = true;
                }

                if (!foundCandidateOnRadius) break;
            }

            return false;
        }

        /// <summary>
        /// Подбирает рабочую клетку для задачи, в которую юнит сможет дойти и из которой сможет работать по цели.
        /// </summary>
        // Method TryFindWorkCell: executes the TryFindWorkCell workflow.
        public bool TryFindWorkCell(int unitId, Vector2Int unitCell, UnitTaskRecord task, out Vector2Int workCell)
        {
            workCell = unitCell;
            float bestDistance = float.PositiveInfinity;

            if (task.TaskType == UnitTaskType.BuildObject)
            {
                TryUseWorkCellCandidate(unitId, unitCell, task.TargetCell, ref bestDistance, ref workCell);
            }

            for (int diagonalDistance = 1; diagonalDistance <= MAX_DIAGONAL_TASK_WORK_DISTANCE; diagonalDistance++)
            {
                TryUseTaskDiagonalWorkCellCandidate(
                    unitId,
                    unitCell,
                    task,
                    new Vector2Int(task.TargetCell.x + diagonalDistance, task.TargetCell.y + diagonalDistance),
                    ref bestDistance,
                    ref workCell);
                TryUseTaskDiagonalWorkCellCandidate(
                    unitId,
                    unitCell,
                    task,
                    new Vector2Int(task.TargetCell.x + diagonalDistance, task.TargetCell.y - diagonalDistance),
                    ref bestDistance,
                    ref workCell);
                TryUseTaskDiagonalWorkCellCandidate(
                    unitId,
                    unitCell,
                    task,
                    new Vector2Int(task.TargetCell.x - diagonalDistance, task.TargetCell.y + diagonalDistance),
                    ref bestDistance,
                    ref workCell);
                TryUseTaskDiagonalWorkCellCandidate(
                    unitId,
                    unitCell,
                    task,
                    new Vector2Int(task.TargetCell.x - diagonalDistance, task.TargetCell.y - diagonalDistance),
                    ref bestDistance,
                    ref workCell);
            }

            for (int distanceToTarget = 1; distanceToTarget <= _taskWorkMaxDistance; distanceToTarget++)
            {
                TryUseTaskWorkCellCandidate(
                    unitId,
                    unitCell,
                    new Vector2Int(task.TargetCell.x + distanceToTarget, task.TargetCell.y),
                    task.TargetCell,
                    ref bestDistance,
                    ref workCell);
                TryUseTaskWorkCellCandidate(
                    unitId,
                    unitCell,
                    new Vector2Int(task.TargetCell.x - distanceToTarget, task.TargetCell.y),
                    task.TargetCell,
                    ref bestDistance,
                    ref workCell);
                TryUseTaskWorkCellCandidate(
                    unitId,
                    unitCell,
                    new Vector2Int(task.TargetCell.x, task.TargetCell.y + distanceToTarget),
                    task.TargetCell,
                    ref bestDistance,
                    ref workCell);
                TryUseTaskWorkCellCandidate(
                    unitId,
                    unitCell,
                    new Vector2Int(task.TargetCell.x, task.TargetCell.y - distanceToTarget),
                    task.TargetCell,
                    ref bestDistance,
                    ref workCell);
            }

            return !float.IsInfinity(bestDistance) && !float.IsNaN(bestDistance);
        }

        /// <summary>
        /// Возвращает человеко-понятную причину, почему для задачи не нашлась рабочая клетка.
        /// </summary>
        // Method ExplainWhyNoWorkCell: executes the ExplainWhyNoWorkCell workflow.
        public string ExplainWhyNoWorkCell(int unitId, Vector2Int unitCell, UnitTaskRecord task)
        {
            if (task == null) return "task is missing";

            bool hasWorkRelationCandidate = false;
            bool hasInsideGridCandidate = false;
            bool hasWalkableCandidate = false;
            bool hasPathCandidate = false;
            var sampleRejects = new List<string>();

            void EvaluateCandidate(Vector2Int candidate, Vector2Int targetCell)
            {
                if (!CanTaskWorkWithTargetFromCell(task, candidate, targetCell))
                {
                    return;
                }
                hasWorkRelationCandidate = true;

                if (!_grid.IsInside(candidate.x, candidate.y))
                {
                    if (sampleRejects.Count < 3) sampleRejects.Add($"({candidate.x},{candidate.y}) outside grid");
                    return;
                }
                hasInsideGridCandidate = true;

                if (candidate != unitCell)
                {
                    if (!IsWorkCellWalkable(candidate))
                    {
                        if (sampleRejects.Count < 3) sampleRejects.Add($"({candidate.x},{candidate.y}) not walkable");
                        return;
                    }
                    hasWalkableCandidate = true;

                    if (!_navigation.TryBuildPath(unitId, unitCell, candidate, out _))
                    {
                        if (sampleRejects.Count < 3) sampleRejects.Add($"({candidate.x},{candidate.y}) no path");
                        return;
                    }
                    hasPathCandidate = true;
                }
                else
                {
                    hasWalkableCandidate = true;
                    hasPathCandidate = true;
                }
            }

            if (task.TaskType == UnitTaskType.BuildObject)
            {
                EvaluateCandidate(task.TargetCell, task.TargetCell);
            }

            for (int diagonalDistance = 1; diagonalDistance <= MAX_DIAGONAL_TASK_WORK_DISTANCE; diagonalDistance++)
            {
                EvaluateCandidate(new Vector2Int(task.TargetCell.x + diagonalDistance, task.TargetCell.y + diagonalDistance), task.TargetCell);
                EvaluateCandidate(new Vector2Int(task.TargetCell.x + diagonalDistance, task.TargetCell.y - diagonalDistance), task.TargetCell);
                EvaluateCandidate(new Vector2Int(task.TargetCell.x - diagonalDistance, task.TargetCell.y + diagonalDistance), task.TargetCell);
                EvaluateCandidate(new Vector2Int(task.TargetCell.x - diagonalDistance, task.TargetCell.y - diagonalDistance), task.TargetCell);
            }

            for (int distanceToTarget = 1; distanceToTarget <= _taskWorkMaxDistance; distanceToTarget++)
            {
                EvaluateCandidate(new Vector2Int(task.TargetCell.x + distanceToTarget, task.TargetCell.y), task.TargetCell);
                EvaluateCandidate(new Vector2Int(task.TargetCell.x - distanceToTarget, task.TargetCell.y), task.TargetCell);
                EvaluateCandidate(new Vector2Int(task.TargetCell.x, task.TargetCell.y + distanceToTarget), task.TargetCell);
                EvaluateCandidate(new Vector2Int(task.TargetCell.x, task.TargetCell.y - distanceToTarget), task.TargetCell);
            }

            if (!hasWorkRelationCandidate)
            {
                return $"no valid work positions relative to target (target=({task.TargetCell.x},{task.TargetCell.y}))";
            }

            if (!hasInsideGridCandidate)
            {
                return "all candidate work cells are outside the grid";
            }

            if (!hasWalkableCandidate)
            {
                string sample = sampleRejects.Count > 0 ? string.Join("; ", sampleRejects) : "no valid standing cell";
                return $"no cell where the unit can stand: {sample}";
            }

            if (!hasPathCandidate)
            {
                string sample = sampleRejects.Count > 0 ? string.Join("; ", sampleRejects) : "path to work cells cannot be built";
                return $"no path to work cells: {sample}";
            }

            return "work cell was not selected due to candidate ranking constraints";
        }

        /// <summary>
        /// Ищет клетку доставки вокруг storage, с которой юнит сможет выполнить операцию передачи ресурса.
        /// </summary>
        // Method TryFindDeliveryCellForStorage: executes the TryFindDeliveryCellForStorage workflow.
        public bool TryFindDeliveryCellForStorage(int unitId, Vector2Int unitCell, Vector2Int storageCell, out Vector2Int deliveryCell)
        {
            deliveryCell = unitCell;
            float bestDistance = float.PositiveInfinity;

            for (int distanceToStorage = 1; distanceToStorage <= _taskWorkMaxDistance; distanceToStorage++)
            {
                int minX = storageCell.x - distanceToStorage;
                int maxX = storageCell.x + distanceToStorage;
                int minY = storageCell.y - distanceToStorage;
                int maxY = storageCell.y + distanceToStorage;

                for (int x = minX; x <= maxX; x++)
                {
                    TryUseTaskWorkCellCandidate(unitId, unitCell, new Vector2Int(x, minY), storageCell, ref bestDistance, ref deliveryCell);
                    TryUseTaskWorkCellCandidate(unitId, unitCell, new Vector2Int(x, maxY), storageCell, ref bestDistance, ref deliveryCell);
                }

                for (int y = minY + 1; y <= maxY - 1; y++)
                {
                    TryUseTaskWorkCellCandidate(unitId, unitCell, new Vector2Int(minX, y), storageCell, ref bestDistance, ref deliveryCell);
                    TryUseTaskWorkCellCandidate(unitId, unitCell, new Vector2Int(maxX, y), storageCell, ref bestDistance, ref deliveryCell);
                }
            }

            return !float.IsInfinity(bestDistance) && !float.IsNaN(bestDistance);
        }

        /// <summary>
        /// Возвращает клетку-источник ресурса, склад которой подходит для delivery-задачи к цели.
        /// </summary>
        // Method ExplainWhyNoDeliveryCellForStorage: executes the ExplainWhyNoDeliveryCellForStorage workflow.
        public string ExplainWhyNoDeliveryCellForStorage(int unitId, Vector2Int unitCell, Vector2Int storageCell)
        {
            bool hasWorkRelationCandidate = false;
            bool hasInsideGridCandidate = false;
            bool hasWalkableCandidate = false;
            bool hasPathCandidate = false;
            var sampleRejects = new List<string>();

            void EvaluateCandidate(Vector2Int candidate)
            {
                if (!CanWorkWithTargetFromCell(candidate, storageCell))
                {
                    return;
                }
                hasWorkRelationCandidate = true;

                if (!_grid.IsInside(candidate.x, candidate.y))
                {
                    if (sampleRejects.Count < 3) sampleRejects.Add($"({candidate.x},{candidate.y}) outside grid");
                    return;
                }
                hasInsideGridCandidate = true;

                if (candidate != unitCell)
                {
                    if (!IsWorkCellWalkable(candidate))
                    {
                        if (sampleRejects.Count < 3) sampleRejects.Add($"({candidate.x},{candidate.y}) not standable");
                        return;
                    }
                    hasWalkableCandidate = true;

                    if (!_navigation.TryBuildPath(unitId, unitCell, candidate, out _))
                    {
                        if (sampleRejects.Count < 3) sampleRejects.Add($"({candidate.x},{candidate.y}) no path");
                        return;
                    }
                    hasPathCandidate = true;
                }
                else
                {
                    hasWalkableCandidate = true;
                    hasPathCandidate = true;
                }
            }

            for (int distanceToStorage = 1; distanceToStorage <= _taskWorkMaxDistance; distanceToStorage++)
            {
                int minX = storageCell.x - distanceToStorage;
                int maxX = storageCell.x + distanceToStorage;
                int minY = storageCell.y - distanceToStorage;
                int maxY = storageCell.y + distanceToStorage;

                for (int x = minX; x <= maxX; x++)
                {
                    EvaluateCandidate(new Vector2Int(x, minY));
                    EvaluateCandidate(new Vector2Int(x, maxY));
                }

                for (int y = minY + 1; y <= maxY - 1; y++)
                {
                    EvaluateCandidate(new Vector2Int(minX, y));
                    EvaluateCandidate(new Vector2Int(maxX, y));
                }
            }

            if (!hasWorkRelationCandidate)
            {
                return "no candidate cells satisfy work-distance relation to storage";
            }

            if (!hasInsideGridCandidate)
            {
                return "all delivery candidates are outside grid";
            }

            if (!hasWalkableCandidate)
            {
                string sample = sampleRejects.Count > 0 ? string.Join("; ", sampleRejects) : "no standable candidate cell";
                return $"no standable delivery cell: {sample}";
            }

            if (!hasPathCandidate)
            {
                string sample = sampleRejects.Count > 0 ? string.Join("; ", sampleRejects) : "path to delivery cells cannot be built";
                return $"no path to any delivery cell: {sample}";
            }

            return "delivery cell not selected due to candidate ranking constraints";
        }

        /// <summary>
        /// Проверяет, можно ли начать работу по задаче из текущей клетки юнита.
        /// </summary>
        // Method CanStartWorkFromCell: executes the CanStartWorkFromCell workflow.
        public bool CanStartWorkFromCell(Vector2Int unitCell, UnitTaskRecord task)
        {
            if (task == null) return false;

            if (IsDirectVerticalNeighbor(unitCell, task.TargetCell))
            {
                return true;
            }

            if (task != null
                && task.TaskType == UnitTaskType.BuildObject
                && unitCell == task.TargetCell)
            {
                return true;
            }

            return CanTaskWorkWithTargetFromCell(task, unitCell, task.TargetCell);
        }

        /// <summary>
        /// Проверяет, достижима ли рабочая дистанция от unitCell до targetCell по правилам взаимодействия.
        /// </summary>
        // Method CanWorkWithTargetFromCell: executes the CanWorkWithTargetFromCell workflow.
        public bool CanWorkWithTargetFromCell(Vector2Int unitCell, Vector2Int targetCell)
        {
            if (IsDirectVerticalNeighbor(unitCell, targetCell))
            {
                return true;
            }

            Vector2Int delta = targetCell - unitCell;
            int distance = Mathf.Abs(delta.x) + Mathf.Abs(delta.y);
            if (distance == 1) return true;
            if (distance > _taskWorkMaxDistance) return false;
            if (delta.x != 0 && delta.y != 0) return false;

            Vector2Int step = new Vector2Int(
                delta.x == 0 ? 0 : delta.x / Mathf.Abs(delta.x),
                delta.y == 0 ? 0 : delta.y / Mathf.Abs(delta.y));

            for (int i = 1; i < distance; i++)
            {
                Vector2Int betweenCellPos = unitCell + step * i;
                if (!_grid.IsInside(betweenCellPos.x, betweenCellPos.y)) return false;

                Cell betweenCell = _grid.GetCell(betweenCellPos.x, betweenCellPos.y);
                if (!IsAirCell(betweenCell)) return false;
            }

            return true;
        }

        /// <summary>
        /// Проверяет, можно ли стоять в клетке как в рабочей позиции.
        /// </summary>
        // Method IsWorkCellWalkable: executes the IsWorkCellWalkable workflow.
        public bool IsWorkCellWalkable(Vector2Int cellPos)
        {
            Cell cell = _grid.GetCell(cellPos.x, cellPos.y);
            Vector2Int down = MovementSupportRules.GetDownDirection(cell);
            return MovementSupportRules.IsCellStandableForMovement(_grid, cellPos, down);
#if false
            // Bridge для получения временно зарезервированных ресурсов для текущей задачи.
            return IsBridgeCell(supportCell) || !IsAirCell(supportCell);
#endif
        }

        /// <summary>
        /// Возвращает клетку-поставщик из 8 соседей вокруг targetCell для build-задачи.
        /// </summary>
        // Method GetBuildNeighborDiagnostics: executes the GetBuildNeighborDiagnostics workflow.
        public string GetBuildNeighborDiagnostics(int unitId, Vector2Int unitCell, Vector2Int targetCell)
        {
            var neighbors = new List<string>(8);
            for (int dy = 1; dy >= -1; dy--)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;

                    Vector2Int cellPos = new Vector2Int(targetCell.x + dx, targetCell.y + dy);
                    if (!_grid.IsInside(cellPos.x, cellPos.y))
                    {
                        neighbors.Add($"({cellPos.x},{cellPos.y}) outside grid");
                        continue;
                    }

                    Cell cell = _grid.GetCell(cellPos.x, cellPos.y);
                    bool walkable = IsWorkCellWalkable(cellPos);
                    bool canPath = cellPos == unitCell || _navigation.TryBuildPath(unitId, unitCell, cellPos, out _);
                    bool canWorkCardinal = CanWorkWithTargetFromCell(cellPos, targetCell);
                    bool canWorkDiagonalBuild = CanWorkDiagonallyWithTarget(cellPos, targetCell);

                    neighbors.Add(
                        $"({cellPos.x},{cellPos.y}) type={cell.Type}, dig={cell.IsDigMarked}, walk={walkable}, path={canPath}, cardinal={canWorkCardinal}, diagTask={canWorkDiagonalBuild}");
                }
            }

            return string.Join("; ", neighbors);
        }

        /// <summary>
        /// Возвращает эффективный тип клетки для движения с учётом флага IgnoreObstacleForPathfinding.
        /// </summary>
        // Method GetMovementCellType: executes the GetMovementCellType workflow.
        public static CellType GetMovementCellType(Cell cell)
        {
            return MovementSupportRules.GetMovementCellType(cell);
        }

        // Method IsLadderCell: executes the IsLadderCell workflow.
        private static bool IsLadderCell(Cell cell)
        {
            return MovementSupportRules.IsLadderCell(cell);
        }

        // Method IsBridgeCell: executes the IsBridgeCell workflow.
        private static bool IsBridgeCell(Cell cell)
        {
            return MovementSupportRules.IsBridgeCell(cell);
        }

        /// <summary>
        /// Определяет, считается ли клетка "воздухом" для проверок прохода/опоры.
        /// </summary>
        // Method IsAirCell: executes the IsAirCell workflow.
        public static bool IsAirCell(Cell cell)
        {
            return MovementSupportRules.IsAirCell(cell);
#if false
            if (IsBridgeCell(cell))
            {
                return false;
            }

            CellType effectiveType = GetMovementCellType(cell);
            return effectiveType == CellType.Empty || effectiveType == CellType.Atmosphere;
#endif
        }

        /// <summary>
        /// Проверяет одного кандидата в ближайшую достижимую клетку.
        /// </summary>
        // Method TryUseReachableCandidate: executes the TryUseReachableCandidate workflow.
        private bool TryUseReachableCandidate(int unitId, Vector2Int unitCell, int x, int y, ref Vector2Int reachableCell)
        {
            if (!_grid.IsInside(x, y)) return false;

            Vector2Int candidate = new Vector2Int(x, y);
            if (!_navigation.TryBuildPath(unitId, unitCell, candidate, out _)) return false;

            reachableCell = candidate;
            return true;
        }

        /// <summary>
        /// Добавляет кандидата рабочей клетки в расчёт, если из него можно работать по targetCell.
        /// </summary>
        // Method TryUseTaskWorkCellCandidate: executes the TryUseTaskWorkCellCandidate workflow.
        private void TryUseTaskWorkCellCandidate(
            int unitId,
            Vector2Int unitCell,
            Vector2Int candidate,
            Vector2Int targetCell,
            ref float bestDistance,
            ref Vector2Int workCell)
        {
            if (!CanWorkWithTargetFromCell(candidate, targetCell)) return;
            TryUseWorkCellCandidate(unitId, unitCell, candidate, ref bestDistance, ref workCell);
        }

        // Method TryUseTaskDiagonalWorkCellCandidate: executes the TryUseTaskDiagonalWorkCellCandidate workflow.
        private void TryUseTaskDiagonalWorkCellCandidate(
            int unitId,
            Vector2Int unitCell,
            UnitTaskRecord task,
            Vector2Int candidate,
            ref float bestDistance,
            ref Vector2Int workCell)
        {
            if (!CanTaskWorkWithTargetFromCell(task, candidate, task.TargetCell)) return;
            TryUseWorkCellCandidate(unitId, unitCell, candidate, ref bestDistance, ref workCell);
        }

        /// <summary>
        /// Проверяет кандидата рабочей клетки на валидность и обновляет лучший результат по расстоянию.
        /// </summary>
        // Method TryUseWorkCellCandidate: executes the TryUseWorkCellCandidate workflow.
        private bool TryUseWorkCellCandidate(
            int unitId,
            Vector2Int unitCell,
            Vector2Int candidate,
            ref float bestDistance,
            ref Vector2Int workCell)
        {
            if (!_grid.IsInside(candidate.x, candidate.y)) return false;
            if (candidate != unitCell)
            {
                if (!IsWorkCellWalkable(candidate)) return false;
                if (!_navigation.TryBuildPath(unitId, unitCell, candidate, out _)) return false;
            }

            float distance = Mathf.Abs(candidate.x - unitCell.x) + Mathf.Abs(candidate.y - unitCell.y);
            if (distance >= bestDistance) return false;

            bestDistance = distance;
            workCell = candidate;
            return true;
        }


        /// <summary>
        /// Разрешает старт стройки по диагонали на соседней клетке с проверкой угла.
        /// </summary>
        // Method CanStartBuildFromDiagonalNeighbor: executes the CanStartBuildFromDiagonalNeighbor workflow.
        private bool CanStartBuildFromDiagonalNeighbor(Vector2Int unitCell, Vector2Int targetCell)
        {
            int dx = targetCell.x - unitCell.x;
            int dy = targetCell.y - unitCell.y;
            if (Mathf.Abs(dx) != 1 || Mathf.Abs(dy) != 1) return false;
            if (!_grid.IsInside(unitCell.x, unitCell.y)) return false;
            if (!_grid.IsInside(targetCell.x, targetCell.y)) return false;

            Cell unit = _grid.GetCell(unitCell.x, unitCell.y);
            Cell target = _grid.GetCell(targetCell.x, targetCell.y);
            if (!IsAirCell(unit) || unit.IsDigMarked) return false;
            if (!IsAirCell(target) || target.IsDigMarked) return false;

            Vector2Int cornerA = new Vector2Int(unitCell.x, targetCell.y);
            Vector2Int cornerB = new Vector2Int(targetCell.x, unitCell.y);
            if (!_grid.IsInside(cornerA.x, cornerA.y) || !_grid.IsInside(cornerB.x, cornerB.y)) return false;

            Cell cornerCellA = _grid.GetCell(cornerA.x, cornerA.y);
            Cell cornerCellB = _grid.GetCell(cornerB.x, cornerB.y);
            if (cornerCellA.IsDigMarked || cornerCellB.IsDigMarked) return false;

            bool cornerAFree = cornerCellA.Type == CellType.Empty || cornerCellA.Type == CellType.Atmosphere;
            bool cornerBFree = cornerCellB.Type == CellType.Empty || cornerCellB.Type == CellType.Atmosphere;
            // Для стройки из доступных ресурсов ищем "ближайший склад".
            return cornerAFree || cornerBFree;
        }

        /// <summary>
        /// Проверяет, что targetCell находится рядом с одной из клеток пути для unitCell.
        /// </summary>
        // Method IsDirectVerticalNeighbor: executes the IsDirectVerticalNeighbor workflow.
        private static bool IsDirectVerticalNeighbor(Vector2Int unitCell, Vector2Int targetCell)
        {
            return unitCell.x == targetCell.x && Mathf.Abs(unitCell.y - targetCell.y) == 1;
        }

        /// <summary>
        /// Разрешает dig/build по диагонали только с соседней клетки или через одну клетку.
        /// </summary>
        // Method CanTaskWorkWithTargetFromCell: executes the CanTaskWorkWithTargetFromCell workflow.
        private bool CanTaskWorkWithTargetFromCell(UnitTaskRecord task, Vector2Int unitCell, Vector2Int targetCell)
        {
            if (CanWorkWithTargetFromCell(unitCell, targetCell))
            {
                return true;
            }

            if (!AllowsDiagonalTaskWork(task))
            {
                return false;
            }

            return CanWorkDiagonallyWithTarget(unitCell, targetCell);
        }

        // Method AllowsDiagonalTaskWork: executes the AllowsDiagonalTaskWork workflow.
        private static bool AllowsDiagonalTaskWork(UnitTaskRecord task)
        {
            if (task == null)
            {
                return false;
            }

            return task.TaskType == UnitTaskType.DigCell
                   || task.TaskType == UnitTaskType.BuildObject
                   || task.TaskType == UnitTaskType.ClearBuildCell;
        }

        // Method CanWorkDiagonallyWithTarget: executes the CanWorkDiagonallyWithTarget workflow.
        private bool CanWorkDiagonallyWithTarget(Vector2Int unitCell, Vector2Int targetCell)
        {
            int dx = targetCell.x - unitCell.x;
            int dy = targetCell.y - unitCell.y;
            int diagonalDistance = Mathf.Abs(dx);
            if (diagonalDistance != Mathf.Abs(dy)) return false;
            if (diagonalDistance < 1 || diagonalDistance > MAX_DIAGONAL_TASK_WORK_DISTANCE) return false;
            if (!_grid.IsInside(unitCell.x, unitCell.y)) return false;
            if (!_grid.IsInside(targetCell.x, targetCell.y)) return false;

            Cell unit = _grid.GetCell(unitCell.x, unitCell.y);
            if (!IsAirCell(unit) || unit.IsDigMarked) return false;

            // Allow only the immediate diagonal work corner-case so units can keep
            // digging/building around a one-cell pit without reopening diagonal travel.
            Vector2Int step = new Vector2Int(dx / diagonalDistance, dy / diagonalDistance);
            Vector2Int current = unitCell;

            for (int i = 0; i < diagonalDistance; i++)
            {
                Vector2Int next = current + step;
                if (!HasOpenDiagonalStep(current, next)) return false;

                if (i < diagonalDistance - 1)
                {
                    Cell betweenDiagonalCell = _grid.GetCell(next.x, next.y);
                    if (!IsAirCell(betweenDiagonalCell) || betweenDiagonalCell.IsDigMarked) return false;
                }

                current = next;
            }

            return true;
        }

        // Method HasOpenDiagonalStep: executes the HasOpenDiagonalStep workflow.
        private bool HasOpenDiagonalStep(Vector2Int fromCell, Vector2Int toCell)
        {
            Vector2Int cornerA = new Vector2Int(fromCell.x, toCell.y);
            Vector2Int cornerB = new Vector2Int(toCell.x, fromCell.y);
            if (!_grid.IsInside(cornerA.x, cornerA.y) || !_grid.IsInside(cornerB.x, cornerB.y)) return false;

            Cell cornerCellA = _grid.GetCell(cornerA.x, cornerA.y);
            Cell cornerCellB = _grid.GetCell(cornerB.x, cornerB.y);
            if (cornerCellA.IsDigMarked || cornerCellB.IsDigMarked) return false;

            bool cornerAFree = cornerCellA.Type == CellType.Empty || cornerCellA.Type == CellType.Atmosphere;
            bool cornerBFree = cornerCellB.Type == CellType.Empty || cornerCellB.Type == CellType.Atmosphere;
            return cornerAFree || cornerBFree;
        }
    }
}