using _Project.Scripts.Data.Grid;
using UnityEngine;

namespace _Project.Scripts.Systems.Water
{
    /// <summary>
    /// Сервис прокладки/удаления кабеля в отдельном слое клетки.
    /// </summary>
    public sealed class WaterPlacementService
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
        public bool TryPlaceWater(GridState gridState, Vector2Int cell)
        {
            if (gridState == null || !gridState.IsInside(cell.x, cell.y))
            {
                Debug.LogWarning($"[WaterPlace] Skip: invalid grid/cell at ({cell.x},{cell.y}).");
                return false;
            }

            Cell current = gridState.GetCell(cell.x, cell.y);
            if (current.HasWater)
            {
                Debug.Log($"[WaterPlace] Skip: Water already exists at ({cell.x},{cell.y}). mask={ToMask4(current.WaterMask4)}");
                return false;
            }

            current.HasWater = true;
            gridState.SetCell(cell.x, cell.y, current);
            RecalculateMask(gridState, cell);
            ref readonly Cell centerAfterMask = ref gridState.GetCell(cell.x, cell.y);
            Debug.Log($"[WaterPlace] Center updated ({cell.x},{cell.y}) hasWater={centerAfterMask.HasWater} mask={ToMask4(centerAfterMask.WaterMask4)}");

            for (int i = 0; i < Directions.Length; i++)
            {
                Vector2Int neighbor = cell + Directions[i];
                if (!gridState.IsInside(neighbor.x, neighbor.y)) continue;
                ref readonly Cell neighborBeforeMask = ref gridState.GetCell(neighbor.x, neighbor.y);
                RecalculateMask(gridState, neighbor);
                ref readonly Cell neighborAfterMask = ref gridState.GetCell(neighbor.x, neighbor.y);
                Debug.Log(
                    $"[WaterPlace] Neighbor updated ({neighbor.x},{neighbor.y}) " +
                    $"hasWater={neighborAfterMask.HasWater} " +
                    $"mask={ToMask4(neighborBeforeMask.WaterMask4)}->{ToMask4(neighborAfterMask.WaterMask4)}");
            }

            Debug.Log($"[WaterPlace] Completed placement at ({cell.x},{cell.y}).");
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
        public void RecalculateAllWaterMasks(GridState gridState)
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
        public bool TryRemoveWater(GridState gridState, Vector2Int cell)
        {
            if (gridState == null || !gridState.IsInside(cell.x, cell.y)) return false;

            Cell current = gridState.GetCell(cell.x, cell.y);
            if (!current.HasWater) return false;

            current.HasWater = false;
            current.WaterMask4 = 0;
            current.WaterNetworkId = 0;
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
            if (!source.HasWater)
            {
                // Сбрасываем только если там был старый ненулевой след маски.
                if (source.WaterMask4 != 0)
                {
                    // Обнуляем маску направлений для непостроенной клетки.
                    source.WaterMask4 = 0;
                    // Сохраняем обновлённое состояние клетки в grid.
                    gridState.SetCell(cell.x, cell.y, source);
                }
                // На непостроенной клетке дальнейший расчёт соседей не нужен.
                return;
            }

            // Стартовое значение маски: ни одного подключения.
            byte mask = 0;
            // Если сверху есть кабель, добавляем флаг Up в маску.
            if (HasWater(gridState, cell + Vector2Int.up)) mask |= (byte)WaterDirectionMask.Up;
            // Если справа есть кабель, добавляем флаг Right в маску.
            if (HasWater(gridState, cell + Vector2Int.right)) mask |= (byte)WaterDirectionMask.Right;
            // Если снизу есть кабель, добавляем флаг Down в маску.
            if (HasWater(gridState, cell + Vector2Int.down)) mask |= (byte)WaterDirectionMask.Down;
            // Если слева есть кабель, добавляем флаг Left в маску.
            if (HasWater(gridState, cell + Vector2Int.left)) mask |= (byte)WaterDirectionMask.Left;

            // Записываем итоговую 4-битную маску подключений в клетку.
            source.WaterMask4 = mask;
            // Фиксируем результат пересчёта в grid-состоянии.
            gridState.SetCell(cell.x, cell.y, source);
        }

        private static bool HasWater(GridState gridState, Vector2Int cell)
        {
            if (!gridState.IsInside(cell.x, cell.y)) return false;
            ref readonly Cell current = ref gridState.GetCell(cell.x, cell.y);
            bool hasWater = current.HasWater;
            if (hasWater)
            {
                Debug.Log($"в такой ячейке {cell.x}*{cell.y} есть Water");
            }
            return hasWater;
        }
    }
}