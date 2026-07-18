using _Project.Scripts.Data.Grid;
using UnityEngine;

namespace _Project.Scripts.Presentation.Grid
{
    /// <summary>
    /// Рисует слой стыков материалов через отдельный tilemap и индекс 47-тайлового blob-набора.
    /// </summary>
    public sealed class MaterialTransitionOverlayService
    {
        // Биты 8-соседней маски связности (same-material).
        private const int N = 1;
        private const int NE = 2;
        private const int E = 4;
        private const int SE = 8;
        private const int S = 16;
        private const int SW = 32;
        private const int W = 64;
        private const int NW = 128;

        // Валидные 47 blob-масок после фильтрации диагоналей.
        private static readonly int[] ValidBlobMasks =
        {
            0, 1, 4, 5, 7, 16, 17, 20, 21, 23, 28, 29, 31, 64, 65, 68, 69, 71, 80, 81, 84, 85, 87, 92, 93, 95, 112, 113, 116, 117, 119, 124, 125, 127, 193, 197, 199, 209, 213, 215, 221, 223, 241, 245, 247, 253, 255
        };

        private static readonly System.Collections.Generic.Dictionary<int, int> BlobMaskToIndex = BuildBlobMaskToIndex();

        private readonly GridState _gridState;
        private readonly GridTileVisualService _gridTileVisualService;

        public MaterialTransitionOverlayService(GridState gridState, GridTileVisualService gridTileVisualService)
        {
            _gridState = gridState;
            _gridTileVisualService = gridTileVisualService;
        }

        /// <summary>
        /// Полный пересчёт слоя стыков для всей сетки.
        /// </summary>
        public void RefreshAll()
        {
            for (int y = 0; y < _gridState.Height; y++)
            {
                for (int x = 0; x < _gridState.Width; x++)
                {
                    RefreshCell(new Vector2Int(x, y));
                }
            }
        }

        /// <summary>
        /// Пересчитывает клетку и 4 соседей, чтобы стык сразу обновлялся после изменения одной клетки.
        /// </summary>
        public void RefreshAround(Vector2Int centerCell)
        {
            RefreshCell(centerCell);
            RefreshCell(centerCell + Vector2Int.up);
            RefreshCell(centerCell + Vector2Int.right);
            RefreshCell(centerCell + Vector2Int.down);
            RefreshCell(centerCell + Vector2Int.left);
        }

        /// <summary>
        /// Пересчитывает прямоугольную область вокруг центра (включая центр).
        /// Нужен для локального обновления transition-слоя без полного RefreshAll.
        /// </summary>
        public void RefreshArea(Vector2Int centerCell, int radius)
        {
            int minX = Mathf.Max(0, centerCell.x - radius);
            int maxX = Mathf.Min(_gridState.Width - 1, centerCell.x + radius);
            int minY = Mathf.Max(0, centerCell.y - radius);
            int maxY = Mathf.Min(_gridState.Height - 1, centerCell.y + radius);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    RefreshCell(new Vector2Int(x, y));
                }
            }
        }

        private void RefreshCell(Vector2Int cell)
        {
            if (!_gridState.IsInside(cell.x, cell.y))
            {
                return;
            }

            CellType currentType = _gridState.GetCell(cell.x, cell.y).Type;
            if (!IsTransitionMaterial(currentType))
            {
                _gridTileVisualService.ClearMaterialTransition(cell);
                return;
            }

            int connectedMask = ComputeConnectedMask8(cell, currentType);
            int filteredBlobMask = FilterBlobMaskDiagonals(connectedMask);
            if (!BlobMaskToIndex.TryGetValue(filteredBlobMask, out int transitionTileIndex))
            {
                transitionTileIndex = 0;
            }

            _gridTileVisualService.SetMaterialTransitionMask(cell, transitionTileIndex);
        }

        private int ComputeConnectedMask8(Vector2Int cell, CellType currentType)
        {
            int mask = 0;

            if (IsConnected(cell, Vector2Int.up, currentType)) mask |= N;
            if (IsConnected(cell, new Vector2Int(1, 1), currentType)) mask |= NE;
            if (IsConnected(cell, Vector2Int.right, currentType)) mask |= E;
            if (IsConnected(cell, new Vector2Int(1, -1), currentType)) mask |= SE;
            if (IsConnected(cell, Vector2Int.down, currentType)) mask |= S;
            if (IsConnected(cell, new Vector2Int(-1, -1), currentType)) mask |= SW;
            if (IsConnected(cell, Vector2Int.left, currentType)) mask |= W;
            if (IsConnected(cell, new Vector2Int(-1, 1), currentType)) mask |= NW;

            return mask;
        }

        private static int FilterBlobMaskDiagonals(int connectedMask)
        {
            bool n = (connectedMask & N) != 0;
            bool e = (connectedMask & E) != 0;
            bool s = (connectedMask & S) != 0;
            bool w = (connectedMask & W) != 0;

            int filteredMask = 0;
            if (n) filteredMask |= N;
            if (e) filteredMask |= E;
            if (s) filteredMask |= S;
            if (w) filteredMask |= W;

            if (n && e && (connectedMask & NE) != 0) filteredMask |= NE;
            if (e && s && (connectedMask & SE) != 0) filteredMask |= SE;
            if (s && w && (connectedMask & SW) != 0) filteredMask |= SW;
            if (w && n && (connectedMask & NW) != 0) filteredMask |= NW;

            return filteredMask;
        }

        private bool IsConnected(Vector2Int cell, Vector2Int dir, CellType currentType)
        {
            Vector2Int neighbor = cell + dir;
            if (!_gridState.IsInside(neighbor.x, neighbor.y))
            {
                return false;
            }

            CellType neighborType = _gridState.GetCell(neighbor.x, neighbor.y).Type;
            return AreSameTransitionMaterial(currentType, neighborType);
        }

        private static bool AreSameTransitionMaterial(CellType currentType, CellType neighborType)
        {
            if (currentType == neighborType)
            {
                return true;
            }

            return false;
        }

        private static System.Collections.Generic.Dictionary<int, int> BuildBlobMaskToIndex()
        {
            var result = new System.Collections.Generic.Dictionary<int, int>(ValidBlobMasks.Length);
            for (int i = 0; i < ValidBlobMasks.Length; i++)
            {
                result[ValidBlobMasks[i]] = i;
            }

            return result;
        }

        // Материалы, на которых показываем стык.
        private static bool IsTransitionMaterial(CellType cellType)
        {
            return cellType == CellType.Iron
                   || cellType == CellType.Titan
                   || cellType == CellType.Aluminium
                   || cellType == CellType.Atmosphere
                   || cellType == CellType.Empty
                   || cellType == CellType.Rogalite;
        }
    }
}