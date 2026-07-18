using UnityEngine;

namespace _Project.Scripts.Systems.Construction
{
    /// <summary>
    /// Shared rules for the ship landing zone on generated maps.
    /// </summary>
    public static class ShipLandingZoneRules
    {
        // Top band start offset from max Y for construction-forbidden landing zone.
        private const int LandingMinYOffsetFromTop = 5;
        // Corridor left bound offset from map center.
        private const int LandingMinXOffsetFromCenter = -7;
        // Corridor right bound offset from map center.
        private const int LandingMaxXOffsetFromCenter = 7;
        // Upper dig-protection rows offset from max map Y.
        private const int DigProtectionMinYOffsetFromMaxY = 5;
        private const int DigProtectionMaxYOffsetFromMaxY = 0;

        /// <summary>
        /// Returns true when the cell belongs to the reserved ship landing rectangle.
        /// </summary>
        public static bool IsInsideLandingZone(int mapWidth, int mapHeight, int x, int y)
        {
            GetBounds(mapWidth, mapHeight, out int minX, out int maxX, out int minY, out int maxY);

            return x >= minX && x <= maxX && y >= minY && y <= maxY;
        }

        /// <summary>
        /// Returns true when the cell belongs to the reserved ship landing rectangle.
        /// </summary>
        public static bool IsInsideLandingZone(int mapWidth, int mapHeight, Vector2Int cell)
        {
            return IsInsideLandingZone(mapWidth, mapHeight, cell.x, cell.y);
        }

        /// <summary>
        /// Returns landing zone bounds in grid cell coordinates.
        /// </summary>
        public static void GetBounds(int mapWidth, int mapHeight, out int minX, out int maxX, out int minY, out int maxY)
        {
            int centerX = mapWidth / 2;
            int topY = mapHeight - 1;
            minX = centerX + LandingMinXOffsetFromCenter;
            maxX = centerX + LandingMaxXOffsetFromCenter;
            minY = topY - LandingMinYOffsetFromTop;
            maxY = topY;
        }

        /// <summary>
        /// Returns true when block removal/digging is forbidden in this cell.
        /// </summary>
        public static bool IsInsideDigProtectionZone(int mapWidth, int mapHeight, int x, int y)
        {
            GetDigProtectionBounds(mapWidth, mapHeight, out int minX, out int maxX, out int minY, out int maxY);
            return x >= minX && x <= maxX && y >= minY && y <= maxY;
        }

        /// <summary>
        /// Returns true when block removal/digging is forbidden in this cell.
        /// </summary>
        public static bool IsInsideDigProtectionZone(int mapWidth, int mapHeight, Vector2Int cell)
        {
            return IsInsideDigProtectionZone(mapWidth, mapHeight, cell.x, cell.y);
        }

        /// <summary>
        /// Returns dig-protection bounds in grid cell coordinates.
        /// </summary>
        public static void GetDigProtectionBounds(int mapWidth, int mapHeight, out int minX, out int maxX, out int minY, out int maxY)
        {
            int centerX = mapWidth / 2;
            int topY = mapHeight - 1;

            minX = centerX + LandingMinXOffsetFromCenter;
            maxX = centerX + LandingMaxXOffsetFromCenter;
            minY = topY - DigProtectionMinYOffsetFromMaxY;
            maxY = topY - DigProtectionMaxYOffsetFromMaxY;
        }
    }
}
