using UnityEngine;
using UnityEngine.Tilemaps;

namespace _Project.Scripts.Presentation.Grid
{
    /// <summary>
    /// Stores all tilemap/tile references used by grid visual rendering.
    /// </summary>
    public sealed class GridTilemapRenderSettings : MonoBehaviour
    {
        [Header("Ground Tilemaps By Natural Type")]
        // Dedicated tilemap for Iron cells.
        [SerializeField] private Tilemap _ironTilemap;
        // Dedicated tilemap for Titan cells.
        [SerializeField] private Tilemap _titanTilemap;
        // Dedicated tilemap for aluminium cells.
        [SerializeField] private Tilemap _aluminiumTilemap;
        // Dedicated tilemap for Rogalite cells.
        [SerializeField] private Tilemap _rogaliteTilemap;
        // Dedicated tilemap for Atmosphere cells.
        [SerializeField] private Tilemap _atmosphereTilemap;
        // Dedicated tilemap for default repeating base layer.
        [SerializeField] private Tilemap _defaultTilemap;

        [Header("Natural Tiles (8x8 Repeat)")]
        // 64 tiles (indexed by x%8 + (y%8)*8) cut from one 8x8 seamless texture sheet for Iron.
        [SerializeField] private TileBase[] _ironTilesByRepeatIndex = new TileBase[64];
        // 64 tiles (indexed by x%8 + (y%8)*8) cut from one 8x8 seamless texture sheet for Titan.
        [SerializeField] private TileBase[] _titanTilesByRepeatIndex = new TileBase[64];
        // 64 tiles (indexed by x%8 + (y%8)*8) cut from one 8x8 seamless texture sheet for aluminium.
        [SerializeField] private TileBase[] _aluminiumTilesByRepeatIndex = new TileBase[64];
        // 64 tiles (indexed by x%8 + (y%8)*8) cut from one 8x8 seamless texture sheet for Rogalite.
        [SerializeField] private TileBase[] _rogaliteTilesByRepeatIndex = new TileBase[64];
        // 64 tiles (indexed by x%8 + (y%8)*8) cut from one 8x8 seamless texture sheet for Atmosphere.
        [SerializeField] private TileBase[] _atmosphereTilesByRepeatIndex = new TileBase[64];
        // 64 tiles (indexed by x%8 + (y%8)*8) cut from one 8x8 seamless texture sheet for default base layer.
        [SerializeField] private TileBase[] _defaultTilesByRepeatIndex = new TileBase[64];

        [Header("Editor Source Sprites (Optional)")]
        // Any sliced child sprite (from an 8x8 sheet) for Iron auto-mapping.
        [SerializeField] private Sprite _ironRepeatSourceSprite;
        // Any sliced child sprite (from an 8x8 sheet) for Titan auto-mapping.
        [SerializeField] private Sprite _titanRepeatSourceSprite;
        // Any sliced child sprite (from an 8x8 sheet) for aluminium auto-mapping.
        [SerializeField] private Sprite _aluminiumRepeatSourceSprite;
        // Any sliced child sprite (from an 8x8 sheet) for Rogalite auto-mapping.
        [SerializeField] private Sprite _rogaliteRepeatSourceSprite;
        // Any sliced child sprite (from an 8x8 sheet) for Atmosphere auto-mapping.
        [SerializeField] private Sprite _atmosphereRepeatSourceSprite;
        // Any sliced child sprite (from an 8x8 sheet) for default base layer auto-mapping.
        [SerializeField] private Sprite _defaultRepeatSourceSprite;

        [Header("Tilemap Dig Preview")]
        [SerializeField] private Tilemap _digPreviewTilemap;
        // Preview tile for cells where digging is allowed while LMB is held.
        [SerializeField] private TileBase _digPreviewTile;
        // Preview tile for cells where digging is blocked while LMB is held.
        [SerializeField] private TileBase _digPreviewBlockedTile;

        [Header("Tilemap Hover Highlight")]
        [SerializeField] private Tilemap _hoverHighlightTilemap;
        [SerializeField] private TileBase _hoverHighlightTile;
        [SerializeField] private TileBase _hoverHighlightDefaultTile;
        [SerializeField] private float _hoverHighlightFadeInSeconds = 0.10f;
        [SerializeField] private float _hoverHighlightFadeOutSeconds = 0.14f;

        [Header("Tilemap Dig")]
        [SerializeField] private Tilemap _digMarkerTilemap;
        [SerializeField] private TileBase _digMarkerTile;
        [SerializeField] private Tilemap _buildTaskMarkerTilemap;
        [SerializeField] private TileBase _buildTaskMarkerTile;
        [SerializeField] private TileBase _destructionMarkerTile;

        [Header("Tilemap Reserved Debug")]
        [SerializeField] private Tilemap _reservedTilemap;
        [SerializeField] private TileBase _reservedTile;

        [Header("Tilemap Protected Resource Overlay")]
        [SerializeField] private Tilemap _protectedResourceOverlayTilemap;
        [SerializeField] private TileBase _protectedResourceOverlayTile;
        [SerializeField] private TileBase _protectedResourceOverlayLeftTile;
        [SerializeField] private TileBase _protectedResourceOverlayRightTile;

        [Header("Cable")]
        // Optional: if empty, preview is rendered in CableBuiltTilemap.
        [Header("00 Cable Preview Tilemap")]
        [SerializeField] private Tilemap _cablePreviewTilemap;
        // Index is CableMask4 in range 0..15 (Up/Right/Down/Left bits).
        [Header("01 Cable Preview Tiles By Mask4")]
        [SerializeField] private TileBase[] _cablePreviewTilesByMask4 = new TileBase[16];
        [Header("07 Cable Built Tilemap")]
        [SerializeField] private Tilemap _cableBuiltTilemap;
        // Index is CableMask4 in range 0..15 (Up/Right/Down/Left bits).
        [Header("08 Cable Built Tiles By Mask4")]
        [SerializeField] private TileBase[] _cableBuiltTilesByMask4 = new TileBase[16];

        [Header("Water")]
        [SerializeField] private Tilemap _waterPreviewTilemap;
        // Index is WaterMask4 in range 0..15 (Up/Right/Down/Left bits).
        [SerializeField] private TileBase[] _waterPreviewTilesByMask4 = new TileBase[16];
        [SerializeField] private Tilemap _waterBuiltTilemap;
        // Index is WaterMask4 in range 0..15 (Up/Right/Down/Left bits).
        [SerializeField] private TileBase[] _waterBuiltTilesByMask4 = new TileBase[16];

        [Header("Oxygen")]
        [SerializeField] private Tilemap _oxygenPreviewTilemap;
        // Index is OxygenMask4 in range 0..15 (Up/Right/Down/Left bits).
        [SerializeField] private TileBase[] _oxygenPreviewTilesByMask4 = new TileBase[16];
        [SerializeField] private Tilemap _oxygenBuiltTilemap;
        // Index is OxygenMask4 in range 0..15 (Up/Right/Down/Left bits).
        [SerializeField] private TileBase[] _oxygenBuiltTilesByMask4 = new TileBase[16];

        [Header("Pipe Debug Indices")]
        [SerializeField] private bool _showPipeMaskIndexDebug;
        [SerializeField] private Color _pipeMaskIndexDebugColor = Color.white;
        [SerializeField] private int _pipeMaskIndexDebugSortingOrder = 200;
        // Дополнительный поворот для preview-кабеля (глобально для всех форм).
        // Дополнительный поворот для построенного кабеля (глобально для всех форм).
        // Точная калибровка built-кабеля по типам формы.

        [Header("Build Tiles")]
        [SerializeField] private TileBase _ladderTile;
        [SerializeField] private TileBase _emptyTile;
        [SerializeField] private Tilemap _materialTransitionTilemap;
        [SerializeField] private TileBase[] _transitionTilesByOpenMask = new TileBase[47];

        public Tilemap IronTilemap => _ironTilemap;
        public Tilemap TitanTilemap => _titanTilemap;
        public Tilemap AluminiumTilemap => _aluminiumTilemap;
        public Tilemap RogaliteTilemap => _rogaliteTilemap;
        public Tilemap AtmosphereTilemap => _atmosphereTilemap;
        public Tilemap DefaultTilemap => _defaultTilemap;

        public TileBase[] IronTilesByRepeatIndex => _ironTilesByRepeatIndex;
        public TileBase[] TitanTilesByRepeatIndex => _titanTilesByRepeatIndex;
        public TileBase[] AluminiumTilesByRepeatIndex => _aluminiumTilesByRepeatIndex;
        public TileBase[] RogaliteTilesByRepeatIndex => _rogaliteTilesByRepeatIndex;
        public TileBase[] AtmosphereTilesByRepeatIndex => _atmosphereTilesByRepeatIndex;
        public TileBase[] DefaultTilesByRepeatIndex => _defaultTilesByRepeatIndex;
        public Sprite IronRepeatSourceSprite => _ironRepeatSourceSprite;
        public Sprite TitanRepeatSourceSprite => _titanRepeatSourceSprite;
        public Sprite AluminiumRepeatSourceSprite => _aluminiumRepeatSourceSprite;
        public Sprite RogaliteRepeatSourceSprite => _rogaliteRepeatSourceSprite;
        public Sprite AtmosphereRepeatSourceSprite => _atmosphereRepeatSourceSprite;
        public Sprite DefaultRepeatSourceSprite => _defaultRepeatSourceSprite;

        public Tilemap DigPreviewTilemap => _digPreviewTilemap;
        public TileBase DigPreviewTile => _digPreviewTile;
        public TileBase DigPreviewBlockedTile => _digPreviewBlockedTile != null ? _digPreviewBlockedTile : _digPreviewTile;
        public Tilemap HoverHighlightTilemap => _hoverHighlightTilemap;
        public TileBase HoverHighlightTile => _hoverHighlightTile;
        public TileBase HoverHighlightDefaultTile => _hoverHighlightDefaultTile;
        public float HoverHighlightFadeInSeconds => _hoverHighlightFadeInSeconds;
        public float HoverHighlightFadeOutSeconds => _hoverHighlightFadeOutSeconds;
        public Tilemap DigMarkerTilemap => _digMarkerTilemap;
        public TileBase DigMarkerTile => _digMarkerTile;
        public Tilemap BuildTaskMarkerTilemap => _buildTaskMarkerTilemap;
        public TileBase BuildTaskMarkerTile => _buildTaskMarkerTile;
        public TileBase DestructionMarkerTile => _destructionMarkerTile;
        public Tilemap ReservedTilemap => _reservedTilemap;
        public TileBase ReservedTile => _reservedTile;
        public Tilemap ProtectedResourceOverlayTilemap => _protectedResourceOverlayTilemap;
        public TileBase ProtectedResourceOverlayTile => _protectedResourceOverlayTile;
        public TileBase ProtectedResourceOverlayLeftTile => _protectedResourceOverlayLeftTile != null ? _protectedResourceOverlayLeftTile : _protectedResourceOverlayTile;
        public TileBase ProtectedResourceOverlayRightTile => _protectedResourceOverlayRightTile != null ? _protectedResourceOverlayRightTile : _protectedResourceOverlayTile;
        public Tilemap CablePreviewTilemap => _cablePreviewTilemap;
        public TileBase[] CablePreviewTilesByMask4 => _cablePreviewTilesByMask4;
        public Tilemap CableBuiltTilemap => _cableBuiltTilemap;
        public TileBase[] CableBuiltTilesByMask4 => _cableBuiltTilesByMask4;
        public Tilemap WaterPreviewTilemap => _waterPreviewTilemap;
        public TileBase[] WaterPreviewTilesByMask4 => _waterPreviewTilesByMask4;
        public Tilemap WaterBuiltTilemap => _waterBuiltTilemap;
        public TileBase[] WaterBuiltTilesByMask4 => _waterBuiltTilesByMask4;
        public Tilemap OxygenPreviewTilemap => _oxygenPreviewTilemap;
        public TileBase[] OxygenPreviewTilesByMask4 => _oxygenPreviewTilesByMask4;
        public Tilemap OxygenBuiltTilemap => _oxygenBuiltTilemap;
        public TileBase[] OxygenBuiltTilesByMask4 => _oxygenBuiltTilesByMask4;
        public bool ShowPipeMaskIndexDebug => _showPipeMaskIndexDebug;
        public Color PipeMaskIndexDebugColor => _pipeMaskIndexDebugColor;
        public int PipeMaskIndexDebugSortingOrder => _pipeMaskIndexDebugSortingOrder;
        public TileBase LadderTile => _ladderTile;
        public TileBase EmptyTile => _emptyTile;
        public Tilemap MaterialTransitionTilemap => _materialTransitionTilemap;
        public TileBase[] TransitionTilesByOpenMask => _transitionTilesByOpenMask;
    }
}
