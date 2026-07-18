using UnityEngine;

namespace _Project.Scripts.Systems.Grid
{
    /// <summary>
    /// ����������� ��������� ����� world � grid.
    /// </summary>
    public sealed class GridCoordinateConverter
    {
        private readonly Vector2 _origin;
        private readonly float _cellSize;

        public GridCoordinateConverter(Vector2 origin, float cellSize)
        {
            _origin = origin;
            _cellSize = cellSize;
        }

        /// <summary> 
        /// </summary>
        public Vector2Int WorldToCell(Vector2 worldPosition)
        {
            int x = Mathf.FloorToInt((worldPosition.x - _origin.x) / _cellSize);
            int y = Mathf.FloorToInt((worldPosition.y - _origin.y) / _cellSize);
            return new Vector2Int(x, y);
        }

        /// <summary>
        /// ����� ������ � world-����������� (������ ��� ���������).
        /// </summary>
        public Vector2 CellToWorldCenter(Vector2Int cell)
        {
            float worldX = _origin.x + (cell.x + 0.5f) * _cellSize;
            float worldY = _origin.y + (cell.y + 0.5f) * _cellSize;
            return new Vector2(worldX, worldY);
        }
    }
}
