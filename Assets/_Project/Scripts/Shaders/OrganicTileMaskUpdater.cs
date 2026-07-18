using UnityEngine;
using UnityEngine.Tilemaps;

namespace _Project.Scripts.Shaders
{
    public class OrganicTileMaskUpdater : MonoBehaviour
    {
        [SerializeField] private Tilemap tilemap;

        private const int UP = 1;
        private const int RIGHT = 2;
        private const int DOWN = 4;
        private const int LEFT = 8;

        public void RefreshAll()
        {
            if (tilemap == null)
            {
            // Debug.LogWarning("[OrganicClip] RefreshAll skipped: tilemap is not assigned.");
                return;
            }

            BoundsInt bounds = tilemap.cellBounds;
            int paintedTiles = 0;
            int sampleMask = -1;
            Vector3Int samplePos = default;

            foreach (Vector3Int pos in bounds.allPositionsWithin)
            {
                if (!tilemap.HasTile(pos))
                    continue;

                int mask = 0;

                if (tilemap.HasTile(pos + Vector3Int.up))
                    mask |= UP;

                if (tilemap.HasTile(pos + Vector3Int.right))
                    mask |= RIGHT;

                if (tilemap.HasTile(pos + Vector3Int.down))
                    mask |= DOWN;

                if (tilemap.HasTile(pos + Vector3Int.left))
                    mask |= LEFT;

                float encodedMask = mask / 15f;

                tilemap.SetTileFlags(pos, TileFlags.None);
                tilemap.SetColor(pos, new Color(encodedMask, 1f, 1f, 1f));
                paintedTiles++;

                if (sampleMask < 0)
                {
                    sampleMask = mask;
                    samplePos = pos;
                }
            }

            if (sampleMask >= 0)
            {
            // Debug.Log($"[OrganicClip] RefreshAll done tilemap={tilemap.name} paintedTiles={paintedTiles} sampleCell=({samplePos.x},{samplePos.y}) sampleMask={sampleMask}");
            }
            else
            {
            // Debug.Log($"[OrganicClip] RefreshAll done tilemap={tilemap.name} paintedTiles=0 (no tiles found in bounds).");
            }
        }
    }
}
