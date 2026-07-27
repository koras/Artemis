using System;
using UnityEngine;

namespace _Project.Scripts.Presentation.UI
{
    [CreateAssetMenu(fileName = "HudMenuIconSet", menuName = "Artemis/UI/HUD Menu Icon Set")]
    public sealed class HudMenuIconSet : ScriptableObject
    {
        [Header("Category Buttons")]
        [SerializeField] private Sprite _bottomMenuEnergyIcon;
        [SerializeField] private Sprite _bottomMenuOxygenIcon;
        [SerializeField] private Sprite _bottomMenuWaterIcon;
        [SerializeField] private Sprite _bottomMenuModuleIcon;

        [Header("Electricity")]
        [SerializeField] private Sprite _solarPanelIcon;
        [SerializeField] private Sprite _batteryIcon;
        [SerializeField] private Sprite _buildCableIcon;
        [SerializeField] private Sprite _cancelCableIcon;

        [Header("Oxygen")]
        [SerializeField] private Sprite _oxygenStorageIcon;
        [SerializeField] private Sprite _oxygenProcessingIcon;
        [SerializeField] private Sprite _buildOxygenIcon;
        [SerializeField] private Sprite _cancelOxygenIcon;

        [Header("Water")]
        [SerializeField] private Sprite _waterReclamationIcon;
        [SerializeField] private Sprite _waterProcessingIcon;
        [SerializeField] private Sprite _buildWaterIcon;
        [SerializeField] private Sprite _cancelWaterIcon;

        [Header("Module")]
        [SerializeField] private Sprite _buildLadderIcon;
        [SerializeField] private Sprite _buildBridgeIcon;
        [SerializeField] private Sprite _buildStorageIcon;
        [SerializeField] private Sprite _regolithProcessingIcon;
        [SerializeField] private Sprite _sleepModuleIcon;
        [SerializeField] private Sprite _dinnerIcon;

        [Header("Right Utility Buttons (Optional)")]
        [SerializeField] private Sprite _destructionIcon;
        [SerializeField] private Sprite _shovelIcon;
        [SerializeField] private Sprite _cancelShovelIcon;
        [SerializeField] private Sprite _exitCableIcon;
        [SerializeField] private Sprite _exitWaterIcon;
        [SerializeField] private Sprite _exitOxygenIcon;

        [Header("Menu Button Unlocks (Optional)")]
        // По умолчанию каталог пустой, поэтому существующие кнопки видимы сразу.
        [SerializeField] private HudMenuButtonDefinition[] _menuButtonDefinitions = Array.Empty<HudMenuButtonDefinition>();

        public Sprite BottomMenuEnergyIcon => _bottomMenuEnergyIcon;
        public Sprite BottomMenuOxygenIcon => _bottomMenuOxygenIcon;
        public Sprite BottomMenuWaterIcon => _bottomMenuWaterIcon;
        public Sprite BottomMenuModuleIcon => _bottomMenuModuleIcon;
        public Sprite SolarPanelIcon => _solarPanelIcon;
        public Sprite BatteryIcon => _batteryIcon;
        public Sprite BuildCableIcon => _buildCableIcon;
        public Sprite CancelCableIcon => _cancelCableIcon;
        public Sprite OxygenStorageIcon => _oxygenStorageIcon;
        public Sprite OxygenProcessingIcon => _oxygenProcessingIcon;
        public Sprite BuildOxygenIcon => _buildOxygenIcon;
        public Sprite CancelOxygenIcon => _cancelOxygenIcon;
        public Sprite WaterReclamationIcon => _waterReclamationIcon;
        public Sprite WaterProcessingIcon => _waterProcessingIcon;
        public Sprite BuildWaterIcon => _buildWaterIcon;
        public Sprite CancelWaterIcon => _cancelWaterIcon;
        public Sprite BuildLadderIcon => _buildLadderIcon;
        public Sprite BuildBridgeIcon => _buildBridgeIcon;
        public Sprite BuildStorageIcon => _buildStorageIcon;
        public Sprite RegolithProcessingIcon => _regolithProcessingIcon;
        public Sprite SleepModuleIcon => _sleepModuleIcon;
        public Sprite DinnerIcon => _dinnerIcon;
        public Sprite DestructionIcon => _destructionIcon;
        public Sprite ShovelIcon => _shovelIcon;
        public Sprite CancelShovelIcon => _cancelShovelIcon;
        public Sprite ExitCableIcon => _exitCableIcon;
        public Sprite ExitWaterIcon => _exitWaterIcon;
        public Sprite ExitOxygenIcon => _exitOxygenIcon;
        public HudMenuButtonDefinition[] MenuButtonDefinitions => _menuButtonDefinitions;
    }
}
