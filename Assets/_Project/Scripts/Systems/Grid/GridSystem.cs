using _Project.Scripts.Data.Grid;
using UnityEngine;

namespace _Project.Scripts.Systems.Grid
{
    /// <summary>
    /// Creates and initializes world grid data.
    ///
    /// Generation contract (developer + AI reference):
    /// 1) Top layers are counted from top to bottom (1-based row index):
    ///    - Rows 1..5: Atmosphere (always).
    ///    - Rows 6..8: Rogalite only (always).
    ///    - Rows 9..11: weighted mix
    ///      Rogalite 70%, Iron 15%, Titan 15%.
    /// 2) Rows below 11 are generated in two passes:
    ///    - Pass A (block pass): non-overlapping random blocks with per-type size ranges.
    ///      Type selection weights:
    ///      Iron 34%, Titan 16%, Aluminium 25%, Rogalite 25%.
    ///      Block size ranges:
    ///      Rogalite  rows 3..15, cols 2..10
    ///      Iron      rows 2..5,  cols 2..10
    ///      Aluminium rows 1..5,  cols 1..10
    ///      Titan     rows 2..5,  cols 1..10
    ///      Block attempts are skipped in top rows 1..11 to keep fixed layer rules deterministic.
    ///    - Pass B (fallback pass): every unfilled non-atmosphere cell is set to Titan,
    ///      except rows 6..11 where dedicated layer rules are applied.
    /// 3) Atmosphere rows are applied as the final pass to guarantee rows 1..5 are always Atmosphere.
    /// </summary>
    public sealed class GridSystem
    {
        private const int AtmosphereRows = 5;

        public GridState Create(int width, int height, int cellSize)
        {
            var grid = new GridState(width, height, cellSize);
            var occupied = new bool[width * height];
            int terrainHeight = Mathf.Max(0, height - AtmosphereRows);

            // Stage 1: place large non-overlapping natural material blocks.
            for (int y = 0; y < terrainHeight; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int rowFromTop = height - y;
                    if (rowFromTop <= 11)
                    {
                        continue;
                    }

                    int index = grid.GetIndex(x, y);
                    if (occupied[index])
                    {
                        continue;
                    }

                    if (Random.value > 0.35f)
                    {
                        continue;
                    }

                    CellType blockType = GetRandomNaturalType();
                    GetBlockSizeRange(blockType, out int minRows, out int maxRows, out int minCols, out int maxCols);
                    int blockHeight = Random.Range(minRows, maxRows + 1);
                    int blockWidth = Random.Range(minCols, maxCols + 1);

                    if (!CanPlaceBlock(x, y, blockWidth, blockHeight, width, height, terrainHeight, occupied))
                    {
                        continue;
                    }

                    PlaceBlock(grid, occupied, x, y, blockWidth, blockHeight, blockType);
                }
            }

            // Stage 2: fill every remaining free terrain cell with fallback layer rules.
            for (int y = 0; y < terrainHeight; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = grid.GetIndex(x, y);
                    if (occupied[index])
                    {
                        continue;
                    }

                    grid.SetCell(x, y, new Cell
                    {
                        Type = GetFallbackType(height, y),
                        ResourceAmount = 2,
                        Temperature = 20f
                    });

                    occupied[index] = true;
                }
            }

            // Stage 3: keep top rows as atmosphere regardless of lower pass results.
            for (int y = terrainHeight; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    grid.SetCell(x, y, new Cell
                    {
                        Type = CellType.Atmosphere,
                        ResourceAmount = 2,
                        Temperature = 20f
                    });
                }
            }

            return grid;
        }

        private static CellType GetFallbackType(int totalHeight, int y)
        {
            // Rows are counted from top to bottom (1-based):
            // 1..5 atmosphere, 6..8 Rogalite, 9..11 weighted mix.
            int rowFromTop = totalHeight - y;
            if (rowFromTop >= 6 && rowFromTop <= 8)
            {
                return CellType.Rogalite;
            }

            if (rowFromTop >= 9 && rowFromTop <= 11)
            {
                float value = Random.value;
                if (value < 0.70f)
                {
                    return CellType.Rogalite;
                }

                if (value < 0.85f)
                {
                    return CellType.Iron;
                }

                return CellType.Titan;
            }

            // Remaining uncovered cells are filled with Titan.
            return CellType.Titan;
        }

        private static bool CanPlaceBlock(
            int startX,
            int startY,
            int blockWidth,
            int blockHeight,
            int width,
            int totalHeight,
            int terrainHeight,
            bool[] occupied)
        {
            if (startX + blockWidth > width || startY + blockHeight > terrainHeight)
            {
                return false;
            }

            // Protect 6..11 top-based rows from block generation.
            int maxAllowedYForBlocks = totalHeight - 12;
            if (startY + blockHeight - 1 > maxAllowedYForBlocks)
            {
                return false;
            }

            for (int y = startY; y < startY + blockHeight; y++)
            {
                for (int x = startX; x < startX + blockWidth; x++)
                {
                    int index = y * width + x;
                    if (occupied[index])
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static void PlaceBlock(
            GridState grid,
            bool[] occupied,
            int startX,
            int startY,
            int blockWidth,
            int blockHeight,
            CellType type)
        {
            for (int y = startY; y < startY + blockHeight; y++)
            {
                for (int x = startX; x < startX + blockWidth; x++)
                {
                    grid.SetCell(x, y, new Cell
                    {
                        Type = type,
                        ResourceAmount = 2,
                        Temperature = 20f
                    });

                    occupied[grid.GetIndex(x, y)] = true;
                }
            }
        }

        private static CellType GetRandomNaturalType()
        {
            float value = Random.value;
            if (value < 0.34f)
            {
                return CellType.Iron;
            }

            if (value < 0.5f)
            {
                return CellType.Titan;
            }

            if (value < 0.75f)
            {
                return CellType.Aluminium;
            }

            return CellType.Rogalite;
        }

        private static void GetBlockSizeRange(
            CellType type,
            out int minRows,
            out int maxRows,
            out int minCols,
            out int maxCols)
        {
            switch (type)
            {
                case CellType.Rogalite:
                    minRows = 3;
                    maxRows = 15;
                    minCols = 2;
                    maxCols = 10;
                    return;

                case CellType.Iron:
                    minRows = 2;
                    maxRows = 5;
                    minCols = 2;
                    maxCols = 10;
                    return;

                case CellType.Aluminium:
                    minRows = 1;
                    maxRows = 5;
                    minCols = 1;
                    maxCols = 10;
                    return;

                case CellType.Titan:
                    minRows = 2;
                    maxRows = 5;
                    minCols = 1;
                    maxCols = 10;
                    return;

                default:
                    minRows = 1;
                    maxRows = 1;
                    minCols = 1;
                    maxCols = 1;
                    return;
            }
        }
    }
}
