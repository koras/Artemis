using _Project.Scripts.Systems.Animals;
using UnityEngine;
using UnityEngine.UIElements;

namespace _Project.Scripts.Presentation.UI
{
    /// <summary>
    /// Renders the top-right diagnostics panel for a selected animal clicked in world space.
    /// </summary>
    public sealed class AnimalDiagnosticsPanelPresenter
    {
        public const string WINDOW_ID = "AnimalDiagnostics";

        private const string PANEL_TEMPLATE_PATH = "UI/AnimalDiagnosticsPanel";

        private readonly VisualElement _panel;
        private readonly Button _closeButton;
        private readonly Label _titleLabel;
        private readonly Label _speciesLabel;
        private readonly Label _cellLabel;
        private readonly Label _hungerLabel;
        private readonly ProgressBar _hungerBar;
        private readonly Label _speedLabel;
        private readonly ProgressBar _speedBar;
        private readonly VisualElement _loyaltyRow;
        private readonly Label _loyaltyLabel;
        private readonly ProgressBar _loyaltyBar;
        private readonly VisualElement _eatenPreyRow;
        private readonly Label _eatenPreyLabel;
        private readonly VisualElement _eggTimerRow;
        private readonly Label _eggTimerLabel;
        private readonly ProgressBar _eggTimerBar;
        private readonly HudWindowCoordinator _hudWindowCoordinator;

        public AnimalDiagnosticsPanelPresenter(
            VisualElement root,
            HudWindowCoordinator hudWindowCoordinator)
        {
            _hudWindowCoordinator = hudWindowCoordinator;
            _panel = EnsurePanelCreated(root);
            _closeButton = _panel?.Q<Button>("animal-diagnostics-close-btn");
            _titleLabel = _panel?.Q<Label>("animal-diagnostics-selected-name");
            _speciesLabel = _panel?.Q<Label>("animal-diagnostics-species");
            _cellLabel = _panel?.Q<Label>("animal-diagnostics-cell");
            _hungerLabel = _panel?.Q<Label>("animal-diagnostics-hunger");
            _hungerBar = _panel?.Q<ProgressBar>("animal-diagnostics-hunger-bar");
            _speedLabel = _panel?.Q<Label>("animal-diagnostics-speed");
            _speedBar = _panel?.Q<ProgressBar>("animal-diagnostics-speed-bar");
            _loyaltyRow = _panel?.Q<VisualElement>("animal-diagnostics-loyalty-row");
            _loyaltyLabel = _panel?.Q<Label>("animal-diagnostics-loyalty");
            _loyaltyBar = _panel?.Q<ProgressBar>("animal-diagnostics-loyalty-bar");
            _eatenPreyRow = _panel?.Q<VisualElement>("animal-diagnostics-eaten-prey-row");
            _eatenPreyLabel = _panel?.Q<Label>("animal-diagnostics-eaten-prey");
            _eggTimerRow = _panel?.Q<VisualElement>("animal-diagnostics-egg-row");
            _eggTimerLabel = _panel?.Q<Label>("animal-diagnostics-egg");
            _eggTimerBar = _panel?.Q<ProgressBar>("animal-diagnostics-egg-bar");
            if (_closeButton != null)
            {
                _closeButton.clicked += () => SetVisible(false);
            }

            _hudWindowCoordinator?.Register(WINDOW_ID, () => SetVisible(false));
            SetVisible(false);
        }

        public bool IsVisible => _panel != null && _panel.style.display.value == DisplayStyle.Flex;

        public void SetVisible(bool isVisible)
        {
            if (_panel == null)
            {
                return;
            }

            _panel.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void Render(AnimalDiagnosticsSnapshot snapshot)
        {
            if (_panel == null)
            {
                return;
            }

            if (_titleLabel != null)
            {
                _titleLabel.text = snapshot.DisplayName;
            }

            if (_speciesLabel != null)
            {
                _speciesLabel.text = $"Species: {snapshot.SpeciesId}";
            }

            if (_cellLabel != null)
            {
                _cellLabel.text = $"Cell: ({snapshot.CurrentCell.x}, {snapshot.CurrentCell.y})";
            }

            if (_hungerLabel != null)
            {
                _hungerLabel.text = $"Hunger: {snapshot.Hunger:0.0}/{snapshot.MaxHunger:0.0}";
            }

            if (_hungerBar != null)
            {
                SetProgress(
                    _hungerBar,
                    snapshot.Hunger,
                    snapshot.MaxHunger,
                    $"{snapshot.Hunger:0.0}/{snapshot.MaxHunger:0.0}");
            }

            if (_speedLabel != null)
            {
                _speedLabel.text = $"Speed: {snapshot.CurrentSpeed:0.00}/{snapshot.MaxMoveSpeed:0.00}";
            }

            if (_speedBar != null)
            {
                SetProgress(
                    _speedBar,
                    snapshot.CurrentSpeed,
                    snapshot.MaxMoveSpeed,
                    $"{snapshot.CurrentSpeed:0.00}/{snapshot.MaxMoveSpeed:0.00}");
            }

            if (_loyaltyRow != null)
            {
                _loyaltyRow.style.display = snapshot.HasLoyalty ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (_loyaltyLabel != null)
            {
                _loyaltyLabel.text = $"Loyalty: {Mathf.Clamp(snapshot.Loyalty, 0, 100)}/100";
            }

            if (_loyaltyBar != null)
            {
                SetProgress(
                    _loyaltyBar,
                    Mathf.Clamp(snapshot.Loyalty, 0, 100),
                    100f,
                    $"{Mathf.Clamp(snapshot.Loyalty, 0, 100)}/100");
            }

            if (_eatenPreyRow != null)
            {
                _eatenPreyRow.style.display = snapshot.TracksPreyEaten ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (_eatenPreyLabel != null)
            {
                _eatenPreyLabel.text = $"Mice eaten: {snapshot.EatenPreyCount}";
            }

            if (_eggTimerRow != null)
            {
                _eggTimerRow.style.display = snapshot.CanLayEgg ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (_eggTimerLabel != null)
            {
                if (snapshot.UsesGrowthBasedEggLaying)
                {
                    bool isEggUnlocked = snapshot.GrowthProgress >= snapshot.EggLayGrowthThreshold;
                    bool eggLimitReached = snapshot.EggsLaidCount >= snapshot.MaxEggsPerAnimal;
                    string growthPercent = $"{snapshot.GrowthProgress * 100f:0}%";
                    string thresholdPercent = $"{snapshot.EggLayGrowthThreshold * 100f:0}%";
                    if (eggLimitReached)
                    {
                        _eggTimerLabel.text = $"Eggs laid: {snapshot.EggsLaidCount}/{snapshot.MaxEggsPerAnimal}";
                    }
                    else if (!isEggUnlocked)
                    {
                        _eggTimerLabel.text = $"Egg unlock at: {growthPercent}/{thresholdPercent}";
                    }
                    else
                    {
                        _eggTimerLabel.text = $"Egg in: {Mathf.Max(0f, snapshot.EggLayRemainingGameHours):0.0} h ({snapshot.EggsLaidCount}/{snapshot.MaxEggsPerAnimal})";
                    }
                }
                else
                {
                    _eggTimerLabel.text = $"Egg in: {Mathf.Max(0f, snapshot.EggLayRemainingGameHours):0.0} h";
                }
            }

            if (_eggTimerBar != null)
            {
                if (snapshot.UsesGrowthBasedEggLaying)
                {
                    bool isEggUnlocked = snapshot.GrowthProgress >= snapshot.EggLayGrowthThreshold;
                    bool eggLimitReached = snapshot.EggsLaidCount >= snapshot.MaxEggsPerAnimal;
                    if (!isEggUnlocked || eggLimitReached)
                    {
                        // Before maturity, or after exhausting the egg cap, show lifetime progress clearly.
                        SetProgress(
                            _eggTimerBar,
                            eggLimitReached ? snapshot.EggsLaidCount : snapshot.GrowthProgress * 100f,
                            eggLimitReached ? Mathf.Max(1f, snapshot.MaxEggsPerAnimal) : 100f,
                            eggLimitReached
                                ? $"{snapshot.EggsLaidCount}/{snapshot.MaxEggsPerAnimal} eggs"
                                : $"{snapshot.GrowthProgress * 100f:0}/{snapshot.EggLayGrowthThreshold * 100f:0}%");
                    }
                    else
                    {
                        float elapsedGameHours = Mathf.Max(0f, snapshot.EggLayIntervalGameHours - snapshot.EggLayRemainingGameHours);
                        SetProgress(
                            _eggTimerBar,
                            elapsedGameHours,
                            snapshot.EggLayIntervalGameHours,
                            $"{elapsedGameHours:0.0}/{snapshot.EggLayIntervalGameHours:0.0} h");
                    }
                }
                else
                {
                    // Show incubation/lay cadence as progress toward the next egg.
                    float elapsedGameHours = Mathf.Max(0f, snapshot.EggLayIntervalGameHours - snapshot.EggLayRemainingGameHours);
                    SetProgress(
                        _eggTimerBar,
                        elapsedGameHours,
                        snapshot.EggLayIntervalGameHours,
                        $"{elapsedGameHours:0.0}/{snapshot.EggLayIntervalGameHours:0.0} h");
                }
            }
        }

        private static VisualElement EnsurePanelCreated(VisualElement root)
        {
            if (root == null)
            {
                return null;
            }

            VisualElement existing = root.Q<VisualElement>("animal-diagnostics-panel");
            if (existing != null)
            {
                return existing;
            }

            VisualTreeAsset panelTemplate = Resources.Load<VisualTreeAsset>(PANEL_TEMPLATE_PATH);
            if (panelTemplate == null)
            {
                Debug.LogWarning($"[AnimalDiagnosticsPanel] Missing Resources/{PANEL_TEMPLATE_PATH}.uxml.");
                return null;
            }

            TemplateContainer panelTree = panelTemplate.CloneTree();
            VisualElement panel = panelTree.Q<VisualElement>("animal-diagnostics-panel");
            if (panel == null)
            {
                Debug.LogWarning("[AnimalDiagnosticsPanel] animal-diagnostics-panel was not found inside template.");
                return null;
            }

            panel.RemoveFromHierarchy();
            root.Add(panel);
            return panel;
        }

        private static void SetProgress(ProgressBar progressBar, float value, float maxValue, string title)
        {
            if (progressBar == null)
            {
                return;
            }

            progressBar.lowValue = 0f;
            progressBar.highValue = Mathf.Max(0.01f, maxValue);
            progressBar.value = Mathf.Clamp(value, progressBar.lowValue, progressBar.highValue);
            progressBar.title = title;
        }
    }
}
