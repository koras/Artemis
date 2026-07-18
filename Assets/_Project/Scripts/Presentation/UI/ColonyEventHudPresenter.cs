using _Project.Scripts.Data.ColonyEvents;
using _Project.Scripts.Systems.ColonyEvents;
using UnityEngine;
using UnityEngine.UIElements;

namespace _Project.Scripts.Presentation.UI
{
    /// <summary>
    /// Shows the currently active Sol event in the HUD.
    /// </summary>
    public sealed class ColonyEventHudPresenter
    {
        private const string PANEL_TEMPLATE_PATH = "UI/Mode/ColonyEventBanner";

        private readonly ColonyEventService _colonyEventService;
        private readonly VisualElement _panel;
        private readonly Label _titleLabel;
        private readonly Label _descriptionLabel;
        private readonly Button _acknowledgeButton;

        private ColonyEventDefinition _currentDefinition;
        private bool _isDismissed;

        public ColonyEventHudPresenter(VisualElement root, ColonyEventService colonyEventService)
        {
            _colonyEventService = colonyEventService;
            _panel = EnsurePanelCreated(root);
            _titleLabel = _panel?.Q<Label>("colony-event-title");
            _descriptionLabel = _panel?.Q<Label>("colony-event-description");
            _acknowledgeButton = _panel?.Q<Button>("colony-event-ack-btn");

            _acknowledgeButton?.RegisterCallback<ClickEvent>(OnDismissClicked);

            if (_colonyEventService != null)
            {
                _colonyEventService.CurrentEventChanged += OnCurrentEventChanged;
                Render(_colonyEventService.CurrentEvent);
            }
            else
            {
                SetVisible(false);
            }
        }

        public void Dispose()
        {
            if (_colonyEventService != null)
            {
                _colonyEventService.CurrentEventChanged -= OnCurrentEventChanged;
            }

            _acknowledgeButton?.UnregisterCallback<ClickEvent>(OnDismissClicked);
        }

        private void OnCurrentEventChanged(ColonyEventDefinition definition)
        {
            Render(definition);
        }

        private void Render(ColonyEventDefinition definition)
        {
            if (_panel == null)
            {
                return;
            }

            _currentDefinition = definition;
            _isDismissed = false;

            if (definition == null)
            {
                SetVisible(false);
                return;
            }

            _titleLabel.text = definition.Title;
            _descriptionLabel.text = definition.Description;
            SetVisible(true);
        }

        private void OnDismissClicked(ClickEvent _)
        {
            if (_currentDefinition == null)
            {
                return;
            }

            _isDismissed = true;
            SetVisible(false);
        }

        private void SetVisible(bool isVisible)
        {
            if (_panel == null)
            {
                return;
            }

            _panel.style.display = isVisible && !_isDismissed ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static VisualElement EnsurePanelCreated(VisualElement root)
        {
            if (root == null)
            {
                return null;
            }

            VisualElement existingPanel = root.Q<VisualElement>("colony-event-banner");
            if (existingPanel != null)
            {
                return existingPanel;
            }

            VisualTreeAsset panelTemplate = Resources.Load<VisualTreeAsset>(PANEL_TEMPLATE_PATH);
            if (panelTemplate == null)
            {
                Debug.LogWarning($"[ColonyEventHudPresenter] Colony event banner template not found at Resources/{PANEL_TEMPLATE_PATH}.uxml");
                return null;
            }

            VisualElement panelTree = panelTemplate.Instantiate();
            VisualElement instantiatedPanel = panelTree.Q<VisualElement>("colony-event-banner");
            if (instantiatedPanel == null)
            {
                Debug.LogWarning("[ColonyEventHudPresenter] colony-event-banner was not found inside template.");
                return null;
            }

            instantiatedPanel.RemoveFromHierarchy();
            root.Add(instantiatedPanel);
            return instantiatedPanel;
        }
    }
}
