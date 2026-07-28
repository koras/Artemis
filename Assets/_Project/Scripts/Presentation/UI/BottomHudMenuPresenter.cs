using UnityEngine.UIElements;
using UnityEngine;

namespace _Project.Scripts.Presentation.UI
{
    public sealed class BottomHudMenuPresenter
    {
        public const string WINDOW_ID = "BottomHudPopup";

        private const string EnergyPopupTemplatePath = "UI/Mode/BottomPopupEnergy";
        private const string OxygenPopupTemplatePath = "UI/Mode/BottomPopupOxygen";
        private const string WaterPopupTemplatePath = "UI/Mode/BottomPopupWater";
        private const string ModulePopupTemplatePath = "UI/Mode/BottomPopupModule";
        private const string DecorationPopupTemplatePath = "UI/Mode/BottomPopupDecoration";

        private readonly Button _energyButton;
        private readonly Button _oxygenButton;
        private readonly Button _waterButton;
        private readonly Button _moduleButton;
        private readonly Button _decorationButton;
        private readonly VisualElement _toolPanel;
        private readonly VisualElement _energyPopup;
        private readonly VisualElement _oxygenPopup;
        private readonly VisualElement _waterPopup;
        private readonly VisualElement _modulePopup;
        private readonly VisualElement _decorationPopup;
        private readonly HudWindowCoordinator _hudWindowCoordinator;

        public BottomHudMenuPresenter(VisualElement root, HudWindowCoordinator hudWindowCoordinator)
        {
            _hudWindowCoordinator = hudWindowCoordinator;
            _energyButton = root?.Q<Button>("bottom-menu-energy-btn");
            _oxygenButton = root?.Q<Button>("bottom-menu-oxygen-btn");
            _waterButton = root?.Q<Button>("bottom-menu-water-btn");
            _moduleButton = root?.Q<Button>("bottom-menu-module-btn");
            _decorationButton = root?.Q<Button>("bottom-menu-decoration-btn");
            _toolPanel = root?.Q<VisualElement>("tool-panel");

            _energyPopup = EnsurePopupCreated(root, "bottom-popup-energy", EnergyPopupTemplatePath);
            _oxygenPopup = EnsurePopupCreated(root, "bottom-popup-oxygen", OxygenPopupTemplatePath);
            _waterPopup = EnsurePopupCreated(root, "bottom-popup-water", WaterPopupTemplatePath);
            _modulePopup = EnsurePopupCreated(root, "bottom-popup-module", ModulePopupTemplatePath);
            _decorationPopup = EnsurePopupCreated(root, "bottom-popup-decoration", DecorationPopupTemplatePath);
            _hudWindowCoordinator?.Register(WINDOW_ID, CloseAll);

            _energyButton?.RegisterCallback<ClickEvent>(_ => TogglePopup(_energyPopup));
            _oxygenButton?.RegisterCallback<ClickEvent>(_ => TogglePopup(_oxygenPopup));
            _waterButton?.RegisterCallback<ClickEvent>(_ => TogglePopup(_waterPopup));
            _moduleButton?.RegisterCallback<ClickEvent>(_ => TogglePopup(_modulePopup));
            _decorationButton?.RegisterCallback<ClickEvent>(_ => TogglePopup(_decorationPopup));
        }

        private void TogglePopup(VisualElement targetPopup)
        {
            bool shouldOpen = targetPopup != null && !targetPopup.ClassListContains("open");
            CloseAll();

            if (shouldOpen)
            {
                _hudWindowCoordinator?.CloseAll(WINDOW_ID);
                targetPopup.AddToClassList("open");
            }
        }

        private void CloseAll()
        {
            _energyPopup?.RemoveFromClassList("open");
            _oxygenPopup?.RemoveFromClassList("open");
            _waterPopup?.RemoveFromClassList("open");
            _modulePopup?.RemoveFromClassList("open");
            _decorationPopup?.RemoveFromClassList("open");
        }

        private VisualElement EnsurePopupCreated(VisualElement root, string popupName, string templatePath)
        {
            if (root == null)
            {
                return null;
            }

            VisualElement existingPopup = root.Q<VisualElement>(popupName);
            if (existingPopup != null)
            {
                return existingPopup;
            }

            if (_toolPanel == null)
            {
                return null;
            }

            VisualTreeAsset template = Resources.Load<VisualTreeAsset>(templatePath);
            if (template == null)
            {
                Debug.LogWarning($"[BottomHudMenuPresenter] Popup template not found at Resources/{templatePath}.uxml");
                return null;
            }

            VisualElement tree = template.Instantiate();
            VisualElement popup = tree.Q<VisualElement>(popupName);
            if (popup == null)
            {
                Debug.LogWarning($"[BottomHudMenuPresenter] Popup '{popupName}' was not found inside template {templatePath}.");
                return null;
            }

            popup.RemoveFromHierarchy();
            _toolPanel.Insert(0, popup);
            return popup;
        }
    }
}
