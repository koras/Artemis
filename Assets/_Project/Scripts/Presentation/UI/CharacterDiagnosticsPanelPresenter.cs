using System.Collections.Generic;
using _Project.Scripts.Systems.Units;
using UnityEngine;
using UnityEngine.UIElements;

namespace _Project.Scripts.Presentation.UI
{
    /// <summary>
    /// Renders unit diagnostics panel with compact roster icons and selected unit details.
    /// </summary>
    public sealed class CharacterDiagnosticsPanelPresenter
    {
        public const string WINDOW_ID = "CharacterDiagnostics";

        private const string PANEL_TEMPLATE_PATH = "UI/CharacterDiagnosticsPanel";

        private readonly VisualElement _rosterPanel;
        private readonly VisualElement _rosterListRoot;
        private readonly Dictionary<int, Button> _rosterButtons = new Dictionary<int, Button>();
        private readonly VisualElement _panel;
        private readonly Button _closeButton;
        private readonly Label _titleLabel;
        private readonly Label _stateLabel;
        private readonly Label _nameKeyLabel;
        private readonly Label _localStateLabel;
        private readonly Label _workDecisionLabel;
        private readonly Label _taskBlockReasonLabel;
        private readonly Label _foodPreferencesLabel;
        private readonly Label _hungerLabel;
        private readonly Label _sleepLabel;
        private readonly Label _moodLabel;
        private readonly Label _sleepCycleLabel;
        private readonly Label _eatCycleLabel;
        private readonly Label _workQuotaLabel;
        private readonly Label _restCycleLabel;
        private readonly ProgressBar _hungerBar;
        private readonly ProgressBar _sleepBar;
        private readonly ProgressBar _sleepCycleBar;
        private readonly ProgressBar _eatCycleBar;
        private readonly ProgressBar _workBar;
        private readonly ProgressBar _restCycleBar;
        private readonly HudWindowCoordinator _hudWindowCoordinator;
        private readonly int _criticalHunger;
        private readonly int _criticalSleep;

        private int _selectedUnitId;

        public CharacterDiagnosticsPanelPresenter(
            VisualElement root,
            int criticalHunger,
            int criticalSleep,
            HudWindowCoordinator hudWindowCoordinator)
        {
            _hudWindowCoordinator = hudWindowCoordinator;
            _criticalHunger = Mathf.Max(1, criticalHunger);
            _criticalSleep = Mathf.Max(1, criticalSleep);
            _rosterPanel = EnsureRosterPanelCreated(root);
            _rosterListRoot = _rosterPanel?.Q<VisualElement>("character-roster-list");
            _panel = EnsurePanelCreated(root);
            _closeButton = _panel?.Q<Button>("character-diagnostics-close-btn");
            _titleLabel = _panel?.Q<Label>("character-diagnostics-selected-name");
            _nameKeyLabel = _panel?.Q<Label>("character-diagnostics-name-key");
            _stateLabel = _panel?.Q<Label>("character-diagnostics-state");
            _localStateLabel = _panel?.Q<Label>("character-diagnostics-local-state");
            _workDecisionLabel = _panel?.Q<Label>("character-diagnostics-work-decision");
            _taskBlockReasonLabel = _panel?.Q<Label>("character-diagnostics-task-block-reason");
            _foodPreferencesLabel = _panel?.Q<Label>("character-diagnostics-food-preferences");
            _hungerLabel = _panel?.Q<Label>("character-diagnostics-hunger");
            _sleepLabel = _panel?.Q<Label>("character-diagnostics-sleep");
            _moodLabel = _panel?.Q<Label>("character-diagnostics-mood");
            _sleepCycleLabel = _panel?.Q<Label>("character-diagnostics-sleep-cycle");
            _eatCycleLabel = _panel?.Q<Label>("character-diagnostics-eat-cycle");
            _workQuotaLabel = _panel?.Q<Label>("character-diagnostics-work");
            _restCycleLabel = _panel?.Q<Label>("character-diagnostics-rest-cycle");
            _hungerBar = _panel?.Q<ProgressBar>("character-diagnostics-hunger-bar");
            _sleepBar = _panel?.Q<ProgressBar>("character-diagnostics-sleep-bar");
            _sleepCycleBar = _panel?.Q<ProgressBar>("character-diagnostics-sleep-cycle-bar");
            _eatCycleBar = _panel?.Q<ProgressBar>("character-diagnostics-eat-cycle-bar");
            _workBar = _panel?.Q<ProgressBar>("character-diagnostics-work-bar");
            _restCycleBar = _panel?.Q<ProgressBar>("character-diagnostics-rest-cycle-bar");
            if (_closeButton != null)
            {
                _closeButton.clicked += () => SetVisible(false);
            }

            _hudWindowCoordinator?.Register(WINDOW_ID, () => SetVisible(false));
            SetVisible(false);
        }

        public void SetVisible(bool isVisible)
        {
            if (_panel == null) return;
            _panel.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public bool IsVisible
        {
            get
            {
                if (_panel == null)
                {
                    return false;
                }

                return _panel.style.display.value == DisplayStyle.Flex;
            }
        }

        public void Render(IReadOnlyList<UnitDiagnosticsSnapshot> items)
        {
            if (_panel == null || _rosterListRoot == null) return;

            if (items == null || items.Count == 0)
            {
                ClearRosterButtons();
                SetEmptyState();
                return;
            }

            if (_selectedUnitId != 0 && !ContainsUnit(items, _selectedUnitId))
            {
                _selectedUnitId = 0;
                SetVisible(false);
            }

            for (int i = 0; i < items.Count; i++)
            {
                UnitDiagnosticsSnapshot item = items[i];
                Button button = GetOrCreateRosterButton(item.UnitId);
                if (item.UnitId == _selectedUnitId)
                {
                    button.AddToClassList("character-diagnostics-item-selected");
                }
                else
                {
                    button.RemoveFromClassList("character-diagnostics-item-selected");
                }

                Label icon = button.Q<Label>();
                if (icon != null)
                {
                    icon.text = !string.IsNullOrWhiteSpace(item.DisplayName)
                        ? item.DisplayName.Substring(0, 1).ToUpperInvariant()
                        : item.UnitId.ToString();
                }

                // Preserve button instances between HUD refreshes so pointer down/up is not lost mid-click.
                if (button.parent != _rosterListRoot || _rosterListRoot.hierarchy.IndexOf(button) != i)
                {
                    button.RemoveFromHierarchy();
                    _rosterListRoot.Insert(i, button);
                }
            }

            RemoveMissingRosterButtons(items);

            if (_selectedUnitId == 0)
            {
                BindSelected(items[0]);
                return;
            }

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].UnitId != _selectedUnitId) continue;
                BindSelected(items[i]);
                return;
            }
        }

        private void BindSelected(UnitDiagnosticsSnapshot item)
        {
            if (_titleLabel != null) _titleLabel.text = item.DisplayName;
            if (_nameKeyLabel != null) _nameKeyLabel.text = $"NameKey: {item.CharacterNameKey}";
            if (_stateLabel != null) _stateLabel.text = $"State: {item.ExecutionState}";
            if (_localStateLabel != null) _localStateLabel.text = $"Local: {item.LocalNeedState}";
            if (_workDecisionLabel != null) _workDecisionLabel.text = $"Work decision: {BuildWorkDecisionText(item)}";
            if (_taskBlockReasonLabel != null) _taskBlockReasonLabel.text = $"Task block reason: {item.GlobalTaskBlockReason}";
            if (_foodPreferencesLabel != null)
            {
                _foodPreferencesLabel.text =
                    $"Food preferences: {item.FoodPreferencesSummary}\n" +
                    $"Movement: current {item.CurrentMoveSpeed:0.00}, max {item.EffectiveMoveSpeed:0.00}, lerp {item.MoveLerpSpeed:0.00}\n" +
                    $"Animation: sim x{item.SimulationSpeedMultiplier:0.##}, anim x{item.MovementAnimationSpeedMultiplier:0.##}, playback x{item.MovementAnimationPlaybackSpeed:0.##}";
            }

            int hungerToCrit = Mathf.Max(0, _criticalHunger - item.Hunger);
            int sleepToCrit = Mathf.Max(0, _criticalSleep - item.SleepDesire);

            if (_hungerLabel != null) _hungerLabel.text = $"Hunger: {item.Hunger}/300 | To critical: {hungerToCrit}";
            if (_sleepLabel != null) _sleepLabel.text = $"Sleep desire: {item.SleepDesire}/300 | To critical: {sleepToCrit}";
            if (_moodLabel != null) _moodLabel.text = $"Mood: {item.Mood}/100";
            if (_workQuotaLabel != null) _workQuotaLabel.text = $"Work (24h): {item.WorkedMinutesWindow:0}/{item.WorkQuotaMinutes:0} min";
            BindSleepCycle(item);
            BindEatCycle(item);
            BindWorkCycle(item);
            BindRestCycle(item);

            if (_hungerBar != null)
            {
                _hungerBar.lowValue = 0;
                _hungerBar.highValue = 300;
                _hungerBar.value = item.Hunger;
                _hungerBar.title = $"{item.Hunger}/300";
            }

            if (_sleepBar != null)
            {
                _sleepBar.lowValue = 0;
                _sleepBar.highValue = 300;
                _sleepBar.value = item.SleepDesire;
                _sleepBar.title = $"{item.SleepDesire}/300";
            }
        }

        private void SetEmptyState()
        {
            if (_titleLabel != null) _titleLabel.text = "No characters";
            if (_stateLabel != null) _stateLabel.text = "State: -";
            if (_nameKeyLabel != null) _nameKeyLabel.text = "NameKey: -";
            if (_localStateLabel != null) _localStateLabel.text = "Local: -";
            if (_workDecisionLabel != null) _workDecisionLabel.text = "Work decision: -";
            if (_taskBlockReasonLabel != null) _taskBlockReasonLabel.text = "Task block reason: -";
            if (_foodPreferencesLabel != null) _foodPreferencesLabel.text = "Food preferences: -\nMovement: -\nAnimation: -";
            if (_hungerLabel != null) _hungerLabel.text = "Hunger: -";
            if (_sleepLabel != null) _sleepLabel.text = "Sleep desire: -";
            if (_moodLabel != null) _moodLabel.text = "Mood: -";
            if (_sleepCycleLabel != null) _sleepCycleLabel.text = "Sleep cycle: -";
            if (_eatCycleLabel != null) _eatCycleLabel.text = "Eat cycle: -";
            if (_workQuotaLabel != null) _workQuotaLabel.text = "Work (24h): -";
            if (_restCycleLabel != null) _restCycleLabel.text = "Rest cycle: -";
            if (_hungerBar != null) _hungerBar.value = 0;
            if (_sleepBar != null) _sleepBar.value = 0;
            ResetProgress(_sleepCycleBar);
            ResetProgress(_eatCycleBar);
            ResetProgress(_workBar);
            ResetProgress(_restCycleBar);
        }

        private void BindSleepCycle(UnitDiagnosticsSnapshot item)
        {
            float total = Mathf.Max(0f, item.SleepTotalMinutes);
            float remaining = Mathf.Clamp(item.SleepRemainingMinutes, 0f, total);
            float done = Mathf.Max(0f, total - remaining);
            if (_sleepCycleLabel != null)
            {
                _sleepCycleLabel.text = $"Sleep cycle: {done:0}/{remaining:0}/{total:0} min (done/left/total)";
            }

            SetProgress(_sleepCycleBar, done, Mathf.Max(1f, total), $"{done:0}/{total:0}");
        }

        private void BindEatCycle(UnitDiagnosticsSnapshot item)
        {
            // Show the actual timer of the currently consumed food item instead of the generic meal target.
            float total = Mathf.Max(0f, item.EatTotalMinutes);
            float remaining = Mathf.Clamp(item.EatRemainingMinutes, 0f, total);
            float done = Mathf.Max(0f, total - remaining);
            if (total <= 0.001f)
            {
                done = 0f;
                remaining = 0f;
            }

            if (_eatCycleLabel != null)
            {
                _eatCycleLabel.text = $"Eat cycle: {done:0}/{remaining:0}/{total:0} min (done/left/current eat)";
            }

            SetProgress(_eatCycleBar, done, Mathf.Max(1f, total), $"{done:0}/{total:0}");
        }

        private void BindWorkCycle(UnitDiagnosticsSnapshot item)
        {
            float total = Mathf.Max(1f, item.WorkQuotaMinutes);
            float done = Mathf.Clamp(item.WorkedMinutesWindow, 0f, total);
            float remaining = Mathf.Max(0f, total - done);
            if (_workQuotaLabel != null)
            {
                _workQuotaLabel.text = $"Work (24h): {done:0}/{remaining:0}/{total:0} min (done/left/total)";
            }

            SetProgress(_workBar, done, total, $"{done:0}/{total:0}");
        }

        private void BindRestCycle(UnitDiagnosticsSnapshot item)
        {
            float total = Mathf.Max(1f, item.RestTargetMinutes);
            float done = Mathf.Clamp(item.RestElapsedMinutes, 0f, total);
            float remaining = Mathf.Max(0f, total - done);
            if (_restCycleLabel != null)
            {
                _restCycleLabel.text = $"Rest cycle: {done:0}/{remaining:0}/{total:0} min (done/left/target)";
            }

            SetProgress(_restCycleBar, done, total, $"{done:0}/{total:0}");
        }

        private static void ResetProgress(ProgressBar progressBar)
        {
            if (progressBar == null) return;
            progressBar.lowValue = 0f;
            progressBar.highValue = 1f;
            progressBar.value = 0f;
            progressBar.title = "0/0";
        }

        private static void SetProgress(ProgressBar progressBar, float value, float maxValue, string title)
        {
            if (progressBar == null) return;
            progressBar.lowValue = 0f;
            progressBar.highValue = Mathf.Max(1f, maxValue);
            progressBar.value = Mathf.Clamp(value, progressBar.lowValue, progressBar.highValue);
            progressBar.title = title;
        }

        private static bool ContainsUnit(IReadOnlyList<UnitDiagnosticsSnapshot> items, int unitId)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].UnitId == unitId) return true;
            }

            return false;
        }

        private static string BuildWorkDecisionText(UnitDiagnosticsSnapshot item)
        {
            if (item.ExecutionState == UnitExecutionState.Moving
                || item.ExecutionState == UnitExecutionState.Working
                || item.ExecutionState == UnitExecutionState.DeliveringResource)
            {
                return "YES (already on global task)";
            }

            if (item.LocalNeedState != UnitLocalNeedState.None)
            {
                return $"NO (local need: {item.LocalNeedState})";
            }

            if (item.WorkedMinutesWindow + 0.001f >= item.WorkQuotaMinutes)
            {
                return "NO (work quota reached)";
            }

            return "YES (will take global task)";
        }

        private Button GetOrCreateRosterButton(int unitId)
        {
            if (_rosterButtons.TryGetValue(unitId, out Button existingButton))
            {
                return existingButton;
            }

            var button = new Button(() => OnRosterButtonClicked(unitId))
            {
                name = $"character-item-{unitId}"
            };
            button.AddToClassList("character-diagnostics-item");

            var icon = new Label();
            icon.AddToClassList("character-diagnostics-icon");
            button.Add(icon);

            _rosterButtons[unitId] = button;
            return button;
        }

        private void OnRosterButtonClicked(int unitId)
        {
            _selectedUnitId = unitId;
            _hudWindowCoordinator?.CloseAll(WINDOW_ID);
            SetVisible(true);
        }

        private void RemoveMissingRosterButtons(IReadOnlyList<UnitDiagnosticsSnapshot> items)
        {
            var missingUnitIds = new List<int>();
            foreach (KeyValuePair<int, Button> pair in _rosterButtons)
            {
                if (!ContainsUnit(items, pair.Key))
                {
                    pair.Value.RemoveFromHierarchy();
                    missingUnitIds.Add(pair.Key);
                }
            }

            for (int i = 0; i < missingUnitIds.Count; i++)
            {
                _rosterButtons.Remove(missingUnitIds[i]);
            }
        }

        private void ClearRosterButtons()
        {
            foreach (KeyValuePair<int, Button> pair in _rosterButtons)
            {
                pair.Value.RemoveFromHierarchy();
            }

            _rosterButtons.Clear();
        }

        private static VisualElement EnsurePanelCreated(VisualElement root)
        {
            if (root == null) return null;

            VisualElement existing = root.Q<VisualElement>("character-diagnostics-panel");
            if (existing != null) return existing;

            VisualTreeAsset panelTemplate = Resources.Load<VisualTreeAsset>(PANEL_TEMPLATE_PATH);
            if (panelTemplate == null)
            {
                Debug.LogWarning($"[CharacterDiagnosticsPanel] Missing Resources/{PANEL_TEMPLATE_PATH}.uxml.");
                return null;
            }

            TemplateContainer panelTree = panelTemplate.CloneTree();
            VisualElement panel = panelTree.Q<VisualElement>("character-diagnostics-panel");
            if (panel == null)
            {
                Debug.LogWarning("[CharacterDiagnosticsPanel] character-diagnostics-panel was not found inside template.");
                return null;
            }

            panel.RemoveFromHierarchy();
            root.Add(panel);
            return panel;
        }

        private static VisualElement EnsureRosterPanelCreated(VisualElement root)
        {
            if (root == null) return null;

            VisualElement existing = root.Q<VisualElement>("character-roster-panel");
            if (existing != null) return existing;

            var panel = new VisualElement { name = "character-roster-panel" };
            panel.AddToClassList("character-roster-panel");

            var list = new VisualElement { name = "character-roster-list" };
            list.AddToClassList("character-diagnostics-list");
            panel.Add(list);

            root.Add(panel);
            return panel;
        }
    }
}
