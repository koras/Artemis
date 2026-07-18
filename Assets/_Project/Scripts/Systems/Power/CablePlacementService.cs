using _Project.Scripts.Data.Grid;
using UnityEngine;

namespace _Project.Scripts.Systems.Power
{
    /// <summary>
    /// Сервис прокладки/удаления кабеля в отдельном слое клетки.
    /// </summary>
    public sealed class CablePlacementService
    {
        private static readonly Vector2Int[] Directions =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left
        };

        /// <summary>
        /// Пробует поставить кабель в клетку и обновляет маски соседей.
        /// </summary>
        public bool TryPlaceCable(GridState gridState, Vector2Int cell)
        {
            if (gridState == null || !gridState.IsInside(cell.x, cell.y))
            {
                Debug.LogWarning($"[CablePlace] Skip: invalid grid/cell at ({cell.x},{cell.y}).");
                return false;
            }

            Cell current = gridState.GetCell(cell.x, cell.y);
            if (current.HasCable)
            {
                Debug.Log($"[CablePlace] Skip: cable already exists at ({cell.x},{cell.y}). mask={ToMask4(current.CableMask4)}");
                return false;
            }

            current.HasCable = true;
            gridState.SetCell(cell.x, cell.y, current);
            RecalculateMask(gridState, cell);
            Cell centerAfterMask = gridState.GetCell(cell.x, cell.y);
            Debug.Log($"[CablePlace] Center updated ({cell.x},{cell.y}) hasCable={centerAfterMask.HasCable} mask={ToMask4(centerAfterMask.CableMask4)}");

            for (int i = 0; i < Directions.Length; i++)
            {
                Vector2Int neighbor = cell + Directions[i];
                if (!gridState.IsInside(neighbor.x, neighbor.y)) continue;
                Cell neighborBeforeMask = gridState.GetCell(neighbor.x, neighbor.y);
                RecalculateMask(gridState, neighbor);
                Cell neighborAfterMask = gridState.GetCell(neighbor.x, neighbor.y);
                Debug.Log(
                    $"[CablePlace] Neighbor updated ({neighbor.x},{neighbor.y}) " +
                    $"hasCable={neighborAfterMask.HasCable} " +
                    $"mask={ToMask4(neighborBeforeMask.CableMask4)}->{ToMask4(neighborAfterMask.CableMask4)}");
            }

            Debug.Log($"[CablePlace] Completed placement at ({cell.x},{cell.y}).");
            return true;
        }

        private static string ToMask4(byte mask)
        {
            return System.Convert.ToString(mask & 0x0F, 2).PadLeft(4, '0');
        }

        /// <summary>
        /// Полностью пересчитывает маски построенных кабелей по всей сетке.
        /// Используется как глобальная синхронизация после изменения топологии сети.
        /// </summary>
        public void RecalculateAllCableMasks(GridState gridState)
        {
            if (gridState == null)
            {
                return;
            }

            for (int y = 0; y < gridState.Height; y++)
            {
                for (int x = 0; x < gridState.Width; x++)
                {
                    RecalculateMask(gridState, new Vector2Int(x, y));
                }
            }
        }

        /// <summary>
        /// Пробует удалить кабель из клетки и обновляет маски соседей.
        /// </summary>
        public bool TryRemoveCable(GridState gridState, Vector2Int cell)
        {
            if (gridState == null || !gridState.IsInside(cell.x, cell.y)) return false;

            Cell current = gridState.GetCell(cell.x, cell.y);
            if (!current.HasCable) return false;

            current.HasCable = false;
            current.CableMask4 = 0;
            current.CableNetworkId = 0;
            gridState.SetCell(cell.x, cell.y, current);

            for (int i = 0; i < Directions.Length; i++)
            {
                Vector2Int neighbor = cell + Directions[i];
                if (!gridState.IsInside(neighbor.x, neighbor.y)) continue;
                RecalculateMask(gridState, neighbor);
            }

            return true;
        }

        private static void RecalculateMask(GridState gridState, Vector2Int cell)
        {
            // Если клетка вне границ сетки, пересчёт для неё невозможен.
            if (!gridState.IsInside(cell.x, cell.y)) return;
            // Читаем текущее состояние клетки, чтобы обновить маску кабеля.
            Cell source = gridState.GetCell(cell.x, cell.y);
            // Если в клетке нет построенного кабеля, маска должна быть сброшена.
            if (!source.HasCable)
            {
                // Сбрасываем только если там был старый ненулевой след маски.
                if (source.CableMask4 != 0)
                {
                    // Обнуляем маску направлений для непостроенной клетки.
                    source.CableMask4 = 0;
                    // Сохраняем обновлённое состояние клетки в grid.
                    gridState.SetCell(cell.x, cell.y, source);
                }
                // На непостроенной клетке дальнейший расчёт соседей не нужен.
                return;
            }

            // Стартовое значение маски: ни одного подключения.
            byte mask = 0;
            // Если сверху есть кабель, добавляем флаг Up в маску.
            if (HasCable(gridState, cell + Vector2Int.up)) mask |= (byte)CableDirectionMask.Up;
            // Если справа есть кабель, добавляем флаг Right в маску.
            if (HasCable(gridState, cell + Vector2Int.right)) mask |= (byte)CableDirectionMask.Right;
            // Если снизу есть кабель, добавляем флаг Down в маску.
            if (HasCable(gridState, cell + Vector2Int.down)) mask |= (byte)CableDirectionMask.Down;
            // Если слева есть кабель, добавляем флаг Left в маску.
            if (HasCable(gridState, cell + Vector2Int.left)) mask |= (byte)CableDirectionMask.Left;

            // Записываем итоговую 4-битную маску подключений в клетку.
            source.CableMask4 = mask;
            // Фиксируем результат пересчёта в grid-состоянии.
            gridState.SetCell(cell.x, cell.y, source);
        }

        private static bool HasCable(GridState gridState, Vector2Int cell)
        {
            if (!gridState.IsInside(cell.x, cell.y)) return false;
            bool hasCable = gridState.GetCell(cell.x, cell.y).HasCable;
            if (hasCable)
            {
                Debug.Log($"в такой ячейке {cell.x}*{cell.y} есть cable");
            }
            return hasCable;
        }
    }
}
