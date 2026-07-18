using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace _Project.Scripts.Presentation.UI
{
    public static class MenuButtonIconBinder
    {
        private static readonly Dictionary<string, Sprite> EmptyMap = new Dictionary<string, Sprite>();

        public static void Bind(VisualElement root, HudMenuIconSet iconSet)
        {
            if (root == null)
            {
                return;
            }

            Dictionary<string, Sprite> spriteMap = BuildSpriteMap(iconSet);
            if (spriteMap.Count == 0)
            {
                return;
            }

            foreach (var pair in spriteMap)
            {
                if (pair.Value == null)
                {
                    continue;
                }

                Button button = root.Q<Button>(pair.Key);
                if (button == null)
                {
                    continue;
                }

                button.style.backgroundImage = new StyleBackground(pair.Value);
            }
        }

        private static Dictionary<string, Sprite> BuildSpriteMap(HudMenuIconSet iconSet)
        {
            if (iconSet == null)
            {
                return EmptyMap;
            }

            var map = new Dictionary<string, Sprite>
            {
                { "bottom-menu-energy-btn", iconSet.BottomMenuEnergyIcon },
                { "bottom-menu-oxygen-btn", iconSet.BottomMenuOxygenIcon },
                { "bottom-menu-water-btn", iconSet.BottomMenuWaterIcon },
                { "bottom-menu-module-btn", iconSet.BottomMenuModuleIcon },
                { "solar-panel-btn", iconSet.SolarPanelIcon },
                { "battery-btn", iconSet.BatteryIcon },
                { "build-cable-btn", iconSet.BuildCableIcon },
                { "cancel-cable-btn", iconSet.CancelCableIcon },
                { "oxygen-storage-btn", iconSet.OxygenStorageIcon },
                { "oxygen-processing-btn", iconSet.OxygenProcessingIcon },
                { "build-oxygen-btn", iconSet.BuildOxygenIcon },
                { "cancel-oxygen-btn", iconSet.CancelOxygenIcon },
                { "water-reclamation-btn", iconSet.WaterReclamationIcon },
                { "water-processing-btn", iconSet.WaterProcessingIcon },
                { "build-water-btn", iconSet.BuildWaterIcon },
                { "cancel-water-btn", iconSet.CancelWaterIcon },
                { "build-ladder-btn", iconSet.BuildLadderIcon },
                { "build-bridge-btn", iconSet.BuildBridgeIcon },
                { "build-storage-btn", iconSet.BuildStorageIcon },
                { "regolith-processing-btn", iconSet.RegolithProcessingIcon },
                { "sleep-module-btn", iconSet.SleepModuleIcon },
                { "dinner-btn", iconSet.DinnerIcon },
                { "destruction-btn", iconSet.DestructionIcon },
                { "shovel-btn", iconSet.ShovelIcon },
                { "cancel-shovel-btn", iconSet.CancelShovelIcon },
                { "exit-cable-btn", iconSet.ExitCableIcon },
                { "exit-water-btn", iconSet.ExitWaterIcon },
                { "exit-oxygen-btn", iconSet.ExitOxygenIcon }
            };

            return map;
        }
    }
}
