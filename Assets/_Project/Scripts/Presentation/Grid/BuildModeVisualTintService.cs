using System.Collections.Generic;
using _Project.Scripts.Input;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace _Project.Scripts.Presentation.Grid
{
    /// <summary>
    /// Tints selected tilemaps and buildings while the player is in cable, water, or oxygen build mode.
    /// </summary>
    public sealed class BuildModeVisualTintService
    {
        private static readonly Color32 CableBuildModeColor = new Color32(0x74, 0x74, 0x74, 0xFF);
        private static readonly HashSet<string> SharedTilemapNames = new HashSet<string>
        {
            "ResourceTilemap",
            "TilemapDefault",
            "TilemapFerrum",
            "TilemapTitan",
            "TilemapAluminium",
            "TilemapRogalite",
            "TilemapAtmosphere",
            "TilemapHg3",
            "DigMarkersTilemap",
            "DigPreviewTilemap",
            "ReservedTilemap",
            "LifeModulePreviewTilemap",
            "LifeModuleBuiltTilemap",
            "TilemapHoverHighlight"
        };
        private static readonly HashSet<string> CableModeTilemapNames = new HashSet<string>
        {
            "WaterBuiltTilemap",
            "WaterPreviewTilemap",
            "OxygenPreviewTilemap",
            "OxygenBuildTilemap",
        };
        private static readonly HashSet<string> WaterModeTilemapNames = new HashSet<string>
        {
            "CablePreviewTilemap",
            "CableBuiltTilemap",
            "OxygenPreviewTilemap",
            "OxygenBuildTilemap"
        };
        private static readonly HashSet<string> OxygenModeTilemapNames = new HashSet<string>
        {
            "WaterPreviewTilemap",
            "WaterBuiltTilemap",
            "CablePreviewTilemap",
            "CableBuiltTilemap"
        };

        private readonly ConstructionDigVisualCallbackService _constructionDigVisualCallbackService;
        private readonly Dictionary<Tilemap, Color> _originalTilemapColors = new Dictionary<Tilemap, Color>();
        private readonly Dictionary<Tilemap, HashSet<BuildModeTintType>> _tilemapModes = new Dictionary<Tilemap, HashSet<BuildModeTintType>>();
        private BuildModeTintType _activeModeTintType;

        public BuildModeVisualTintService(
            GridTilemapRenderSettings gridTilemapRenderSettings,
            ConstructionDigVisualCallbackService constructionDigVisualCallbackService)
        {
            _constructionDigVisualCallbackService = constructionDigVisualCallbackService;
            CacheTargetTilemaps(gridTilemapRenderSettings);
        }

        public void HandleToolModeChanged(ToolMode toolMode)
        {
            BuildModeTintType nextModeTintType = ResolveModeTintType(toolMode);
            if (_activeModeTintType == nextModeTintType)
            {
                return;
            }

            _activeModeTintType = nextModeTintType;
            if (_activeModeTintType != BuildModeTintType.None)
            {
                ApplyBuildModeTint(_activeModeTintType);
                return;
            }

            RestoreOriginalColors();
        }

        private void ApplyBuildModeTint(BuildModeTintType modeTintType)
        {
            foreach (KeyValuePair<Tilemap, HashSet<BuildModeTintType>> pair in _tilemapModes)
            {
                Tilemap tilemap = pair.Key;
                if (tilemap == null)
                {
                    continue;
                }

                tilemap.color = pair.Value.Contains(modeTintType)
                    ? CableBuildModeColor
                    : _originalTilemapColors[tilemap];
            }

            _constructionDigVisualCallbackService?.SetCableBuildModeTint(CableBuildModeColor);
        }

        private void RestoreOriginalColors()
        {
            foreach (KeyValuePair<Tilemap, Color> pair in _originalTilemapColors)
            {
                if (pair.Key == null)
                {
                    continue;
                }

                pair.Key.color = pair.Value;
            }

            _constructionDigVisualCallbackService?.ClearCableBuildModeTint();
        }

        private void CacheTargetTilemaps(GridTilemapRenderSettings gridTilemapRenderSettings)
        {
            if (gridTilemapRenderSettings != null)
            {
                RegisterTilemap(gridTilemapRenderSettings.ResourceTilemap, BuildModeTintType.Cable, BuildModeTintType.Water, BuildModeTintType.Oxygen);
                RegisterTilemap(gridTilemapRenderSettings.DefaultTilemap, BuildModeTintType.Cable, BuildModeTintType.Water, BuildModeTintType.Oxygen);
                RegisterTilemap(gridTilemapRenderSettings.DigMarkerTilemap, BuildModeTintType.Cable, BuildModeTintType.Water, BuildModeTintType.Oxygen);
                RegisterTilemap(gridTilemapRenderSettings.DigPreviewTilemap, BuildModeTintType.Cable, BuildModeTintType.Water, BuildModeTintType.Oxygen);
                RegisterTilemap(gridTilemapRenderSettings.ReservedTilemap, BuildModeTintType.Cable, BuildModeTintType.Water, BuildModeTintType.Oxygen);
                RegisterTilemap(gridTilemapRenderSettings.CablePreviewTilemap, BuildModeTintType.Water, BuildModeTintType.Oxygen);
                RegisterTilemap(gridTilemapRenderSettings.CableBuiltTilemap, BuildModeTintType.Water, BuildModeTintType.Oxygen);
                RegisterTilemap(gridTilemapRenderSettings.WaterBuiltTilemap, BuildModeTintType.Cable, BuildModeTintType.Oxygen);
                RegisterTilemap(gridTilemapRenderSettings.WaterPreviewTilemap, BuildModeTintType.Cable, BuildModeTintType.Oxygen);
                RegisterTilemap(gridTilemapRenderSettings.OxygenPreviewTilemap, BuildModeTintType.Cable, BuildModeTintType.Water);
                RegisterTilemap(gridTilemapRenderSettings.OxygenBuiltTilemap, BuildModeTintType.Cable, BuildModeTintType.Water);
                RegisterTilemap(gridTilemapRenderSettings.HoverHighlightTilemap, BuildModeTintType.Cable, BuildModeTintType.Water, BuildModeTintType.Oxygen);
            }

            Tilemap[] sceneTilemaps = Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < sceneTilemaps.Length; i++)
            {
                Tilemap tilemap = sceneTilemaps[i];
                if (tilemap == null)
                {
                    continue;
                }

                if (SharedTilemapNames.Contains(tilemap.name))
                {
                    RegisterTilemap(tilemap, BuildModeTintType.Cable, BuildModeTintType.Water, BuildModeTintType.Oxygen);
                    continue;
                }

                if (CableModeTilemapNames.Contains(tilemap.name))
                {
                    RegisterTilemap(tilemap, BuildModeTintType.Cable);
                    continue;
                }

                if (WaterModeTilemapNames.Contains(tilemap.name))
                {
                    RegisterTilemap(tilemap, BuildModeTintType.Water);
                    continue;
                }

                if (OxygenModeTilemapNames.Contains(tilemap.name))
                {
                    RegisterTilemap(tilemap, BuildModeTintType.Oxygen);
                }
            }
        }

        private void RegisterTilemap(Tilemap tilemap, params BuildModeTintType[] modeTintTypes)
        {
            if (tilemap == null)
            {
                return;
            }

            if (!_originalTilemapColors.ContainsKey(tilemap))
            {
                _originalTilemapColors[tilemap] = tilemap.color;
            }

            if (!_tilemapModes.TryGetValue(tilemap, out HashSet<BuildModeTintType> registeredModes))
            {
                registeredModes = new HashSet<BuildModeTintType>();
                _tilemapModes[tilemap] = registeredModes;
            }

            for (int i = 0; i < modeTintTypes.Length; i++)
            {
                registeredModes.Add(modeTintTypes[i]);
            }
        }

        private static BuildModeTintType ResolveModeTintType(ToolMode toolMode)
        {
            return toolMode switch
            {
                ToolMode.BuildCable => BuildModeTintType.Cable,
                ToolMode.BuildWater => BuildModeTintType.Water,
                ToolMode.BuildOxygen => BuildModeTintType.Oxygen,
                _ => BuildModeTintType.None
            };
        }

        private enum BuildModeTintType
        {
            None = 0,
            Cable = 1,
            Water = 2,
            Oxygen = 3
        }
    }
}
