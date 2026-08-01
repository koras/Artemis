using System;
using System.Collections.Generic;
using System.Globalization;
using _Project.Scripts.Systems.Units;
using UnityEngine;
using UnityEngine.Localization;
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

        private static readonly List<int> MissingUnitIdsBuffer = new List<int>(16);
        private static readonly Dictionary<string, string> DisplayInitialByName = new Dictionary<string, string>();
        private static readonly Dictionary<int, string> UnitIdTextByUnitId = new Dictionary<int, string>();
        private static readonly Dictionary<int, string> RosterButtonNameByUnitId = new Dictionary<int, string>();
        private static readonly Dictionary<IntPairKey, string> ProgressTitleByValue = new Dictionary<IntPairKey, string>();
        private static readonly Dictionary<IntPairKey, string> NeedBarTitleByValue = new Dictionary<IntPairKey, string>();

        private readonly VisualElement _rosterPanel;
        private readonly VisualElement _rosterListRoot;
        private readonly Dictionary<int, Button> _rosterButtons = new Dictionary<int, Button>();
        private readonly VisualElement _panel;
        private readonly Button _closeButton;
        private readonly Label _headerTitleLabel;
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
            _rosterListRoot = _rosterPanel.Q<VisualElement>("character-roster-list");
            _panel = EnsurePanelCreated(root);
            _closeButton = _panel.Q<Button>("character-diagnostics-close-btn");
            _headerTitleLabel = _panel.Q<Label>("character-diagnostics-title");
            _titleLabel = _panel.Q<Label>("character-diagnostics-selected-name");
            _nameKeyLabel = _panel.Q<Label>("character-diagnostics-name-key");
            _stateLabel = _panel.Q<Label>("character-diagnostics-state");
            _localStateLabel = _panel.Q<Label>("character-diagnostics-local-state");
            _workDecisionLabel = _panel.Q<Label>("character-diagnostics-work-decision");
            _taskBlockReasonLabel = _panel.Q<Label>("character-diagnostics-task-block-reason");
            _foodPreferencesLabel = _panel.Q<Label>("character-diagnostics-food-preferences");
            _hungerLabel = _panel.Q<Label>("character-diagnostics-hunger");
            _sleepLabel = _panel.Q<Label>("character-diagnostics-sleep");
            _moodLabel = _panel.Q<Label>("character-diagnostics-mood");
            _sleepCycleLabel = _panel.Q<Label>("character-diagnostics-sleep-cycle");
            _eatCycleLabel = _panel.Q<Label>("character-diagnostics-eat-cycle");
            _workQuotaLabel = _panel.Q<Label>("character-diagnostics-work");
            _restCycleLabel = _panel.Q<Label>("character-diagnostics-rest-cycle");
            _hungerBar = _panel.Q<ProgressBar>("character-diagnostics-hunger-bar");
            _sleepBar = _panel.Q<ProgressBar>("character-diagnostics-sleep-bar");
            _sleepCycleBar = _panel.Q<ProgressBar>("character-diagnostics-sleep-cycle-bar");
            _eatCycleBar = _panel.Q<ProgressBar>("character-diagnostics-eat-cycle-bar");
            _workBar = _panel.Q<ProgressBar>("character-diagnostics-work-bar");
            _restCycleBar = _panel.Q<ProgressBar>("character-diagnostics-rest-cycle-bar");
            _closeButton.clicked += () => SetVisible(false);
            _headerTitleLabel.text = GetLocalizedText(CharacterDiagnosticsLocalizationKeys.Title);
            _hudWindowCoordinator.Register(WINDOW_ID, () => SetVisible(false));
            SetVisible(false);
        }

        public void SetVisible(bool isVisible)
        {
            _panel.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public bool IsVisible => _panel.style.display.value == DisplayStyle.Flex;

        public void Render(IReadOnlyList<UnitDiagnosticsSnapshot> items)
        {
            _headerTitleLabel.text = GetLocalizedText(CharacterDiagnosticsLocalizationKeys.Title);

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
                icon.text = GetRosterIconText(item);

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
            _titleLabel.text = GetLocalizedDisplayName(item.DisplayName);
            _nameKeyLabel.text = GetLabeledText(
                CharacterDiagnosticsLocalizationKeys.NameKey,
                item.CharacterNameKey ?? string.Empty);
            _stateLabel.text = GetLabeledText(
                CharacterDiagnosticsLocalizationKeys.StateLabel,
                GetLocalizedText(CharacterDiagnosticsLocalizationKeys.State(item.ExecutionState)));
            _localStateLabel.text = GetLabeledText(
                CharacterDiagnosticsLocalizationKeys.LocalStateLabel,
                GetLocalizedText(CharacterDiagnosticsLocalizationKeys.LocalState(item.LocalNeedState)));
            _workDecisionLabel.text = GetLabeledText(
                CharacterDiagnosticsLocalizationKeys.WorkDecisionLabel,
                GetWorkDecisionText(item));
            _taskBlockReasonLabel.text = GetLabeledText(
                CharacterDiagnosticsLocalizationKeys.TaskBlockReasonLabel,
                GetTaskBlockReasonText(item.GlobalTaskBlockReason));
            _foodPreferencesLabel.text = GetFoodPreferencesText(item);

            int hungerToCrit = Mathf.Max(0, _criticalHunger - item.Hunger);
            int sleepToCrit = Mathf.Max(0, _criticalSleep - item.SleepDesire);

            _hungerLabel.text = GetFormattedText(
                CharacterDiagnosticsLocalizationKeys.Hunger,
                item.Hunger,
                hungerToCrit);
            _sleepLabel.text = GetFormattedText(
                CharacterDiagnosticsLocalizationKeys.SleepDesire,
                item.SleepDesire,
                sleepToCrit);
            _moodLabel.text = GetFormattedText(CharacterDiagnosticsLocalizationKeys.Mood, item.Mood);
            BindSleepCycle(item);
            BindEatCycle(item);
            BindWorkCycle(item);
            BindRestCycle(item);

            _hungerBar.lowValue = 0;
            _hungerBar.highValue = 300;
            _hungerBar.value = item.Hunger;
            _hungerBar.title = GetCachedPairText(
                NeedBarTitleByValue,
                new IntPairKey(item.Hunger, 300),
                "",
                "/",
                "");
            _sleepBar.lowValue = 0;
            _sleepBar.highValue = 300;
            _sleepBar.value = item.SleepDesire;
            _sleepBar.title = GetCachedPairText(
                NeedBarTitleByValue,
                new IntPairKey(item.SleepDesire, 300),
                "",
                "/",
                "");
        }

        private void SetEmptyState()
        {
            _titleLabel.text = GetLocalizedText(CharacterDiagnosticsLocalizationKeys.NoCharacters);
            _stateLabel.text = GetLabeledText(CharacterDiagnosticsLocalizationKeys.StateLabel, "-");
            _nameKeyLabel.text = GetLabeledText(CharacterDiagnosticsLocalizationKeys.NameKey, "-");
            _localStateLabel.text = GetLabeledText(CharacterDiagnosticsLocalizationKeys.LocalStateLabel, "-");
            _workDecisionLabel.text = GetLabeledText(CharacterDiagnosticsLocalizationKeys.WorkDecisionLabel, "-");
            _taskBlockReasonLabel.text = GetLabeledText(CharacterDiagnosticsLocalizationKeys.TaskBlockReasonLabel, "-");
            _foodPreferencesLabel.text = GetLocalizedText(CharacterDiagnosticsLocalizationKeys.FoodPreferencesLabel)
                + ": -\n"
                + GetLocalizedText(CharacterDiagnosticsLocalizationKeys.Movement)
                + ": -\n"
                + GetLocalizedText(CharacterDiagnosticsLocalizationKeys.Animation)
                + ": -";
            _hungerLabel.text = GetLabeledText(CharacterDiagnosticsLocalizationKeys.Hunger, "-");
            _sleepLabel.text = GetLabeledText(CharacterDiagnosticsLocalizationKeys.SleepDesire, "-");
            _moodLabel.text = GetLabeledText(CharacterDiagnosticsLocalizationKeys.Mood, "-");
            _sleepCycleLabel.text = GetLabeledText(CharacterDiagnosticsLocalizationKeys.SleepCycle, "-");
            _eatCycleLabel.text = GetLabeledText(CharacterDiagnosticsLocalizationKeys.EatCycle, "-");
            _workQuotaLabel.text = GetLabeledText(CharacterDiagnosticsLocalizationKeys.Work24Hours, "-");
            _restCycleLabel.text = GetLabeledText(CharacterDiagnosticsLocalizationKeys.RestCycle, "-");
            _hungerBar.value = 0;
            _sleepBar.value = 0;
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
            _sleepCycleLabel.text = GetCycleText(
                CharacterDiagnosticsLocalizationKeys.SleepCycle,
                CharacterDiagnosticsLocalizationKeys.CycleDoneLeftTotal,
                done,
                remaining,
                total);

            SetProgress(_sleepCycleBar, done, Mathf.Max(1f, total), GetProgressTitleText(done, total));
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

            _eatCycleLabel.text = GetCycleText(
                CharacterDiagnosticsLocalizationKeys.EatCycle,
                CharacterDiagnosticsLocalizationKeys.CycleDoneLeftCurrentEat,
                done,
                remaining,
                total);

            SetProgress(_eatCycleBar, done, Mathf.Max(1f, total), GetProgressTitleText(done, total));
        }

        private void BindWorkCycle(UnitDiagnosticsSnapshot item)
        {
            float total = Mathf.Max(1f, item.WorkQuotaMinutes);
            float done = Mathf.Clamp(item.WorkedMinutesWindow, 0f, total);
            float remaining = Mathf.Max(0f, total - done);
            _workQuotaLabel.text = GetCycleText(
                CharacterDiagnosticsLocalizationKeys.Work24Hours,
                CharacterDiagnosticsLocalizationKeys.CycleDoneLeftTotal,
                done,
                remaining,
                total);

            SetProgress(_workBar, done, total, GetProgressTitleText(done, total));
        }

        private void BindRestCycle(UnitDiagnosticsSnapshot item)
        {
            float total = Mathf.Max(1f, item.RestTargetMinutes);
            float done = Mathf.Clamp(item.RestElapsedMinutes, 0f, total);
            float remaining = Mathf.Max(0f, total - done);
            _restCycleLabel.text = GetCycleText(
                CharacterDiagnosticsLocalizationKeys.RestCycle,
                CharacterDiagnosticsLocalizationKeys.CycleDoneLeftTarget,
                done,
                remaining,
                total);

            SetProgress(_restCycleBar, done, total, GetProgressTitleText(done, total));
        }

        private static void ResetProgress(ProgressBar progressBar)
        {
            progressBar.lowValue = 0f;
            progressBar.highValue = 1f;
            progressBar.value = 0f;
            progressBar.title = "0/0";
        }

        private static void SetProgress(ProgressBar progressBar, float value, float maxValue, string title)
        {
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

        private static string GetRosterIconText(UnitDiagnosticsSnapshot item)
        {
            string displayName = GetLocalizedDisplayName(item.DisplayName);
            if (string.IsNullOrWhiteSpace(displayName))
            {
                if (UnitIdTextByUnitId.TryGetValue(item.UnitId, out string unitIdText))
                {
                    return unitIdText;
                }

                unitIdText = item.UnitId.ToString();
                UnitIdTextByUnitId[item.UnitId] = unitIdText;
                return unitIdText;
            }

            if (DisplayInitialByName.TryGetValue(displayName, out string initial))
            {
                return initial;
            }

            initial = char.ToUpperInvariant(displayName[0]).ToString();
            DisplayInitialByName[displayName] = initial;
            return initial;
        }

        private static string GetLocalizedDisplayName(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return displayName;
            }

            int spaceIndex = displayName.IndexOf(' ');
            int serialSeparatorIndex = displayName.LastIndexOf('-');
            if (spaceIndex <= 0 || serialSeparatorIndex <= spaceIndex + 1)
            {
                return displayName;
            }

            string prefix = displayName.Substring(0, spaceIndex);
            string suffix = displayName.Substring(spaceIndex + 1, serialSeparatorIndex - spaceIndex - 1);
            string localizedPrefix = GetLocalizedText(CharacterDiagnosticsLocalizationKeys.Prefix(prefix));
            string localizedSuffix = GetLocalizedText(CharacterDiagnosticsLocalizationKeys.Suffix(suffix));
            return localizedPrefix + " " + localizedSuffix + displayName.Substring(serialSeparatorIndex);
        }

        private static string GetCachedPairText(
            Dictionary<IntPairKey, string> cache,
            IntPairKey key,
            string prefix,
            string separator,
            string suffix)
        {
            if (cache.TryGetValue(key, out string text))
            {
                return text;
            }

            text = prefix + key.First + separator + key.Second + suffix;
            cache[key] = text;
            return text;
        }

        private static string GetWorkDecisionText(UnitDiagnosticsSnapshot item)
        {
            if (item.ExecutionState == UnitExecutionState.Moving
                || item.ExecutionState == UnitExecutionState.Working
                || item.ExecutionState == UnitExecutionState.DeliveringResource)
            {
                return GetLocalizedText(CharacterDiagnosticsLocalizationKeys.WorkDecisionYesAlready);
            }

            if (item.LocalNeedState != UnitLocalNeedState.None)
            {
                return GetFormattedText(
                    CharacterDiagnosticsLocalizationKeys.WorkDecisionNoLocalNeed,
                    GetLocalizedText(CharacterDiagnosticsLocalizationKeys.LocalState(item.LocalNeedState)));
            }

            if (item.WorkedMinutesWindow + 0.001f >= item.WorkQuotaMinutes)
            {
                return GetLocalizedText(CharacterDiagnosticsLocalizationKeys.WorkDecisionNoQuota);
            }

            return GetLocalizedText(CharacterDiagnosticsLocalizationKeys.WorkDecisionYesWillTake);
        }

        private static string GetFoodPreferencesText(UnitDiagnosticsSnapshot item)
        {
            string currentMoveSpeed = item.CurrentMoveSpeed.ToString("0.00", CultureInfo.InvariantCulture);
            string effectiveMoveSpeed = item.EffectiveMoveSpeed.ToString("0.00", CultureInfo.InvariantCulture);
            string moveLerpSpeed = item.MoveLerpSpeed.ToString("0.00", CultureInfo.InvariantCulture);
            string simulationSpeedMultiplier = item.SimulationSpeedMultiplier.ToString("0.##", CultureInfo.InvariantCulture);
            string movementAnimationSpeedMultiplier = item.MovementAnimationSpeedMultiplier.ToString("0.##", CultureInfo.InvariantCulture);
            string movementAnimationPlaybackSpeed = item.MovementAnimationPlaybackSpeed.ToString("0.##", CultureInfo.InvariantCulture);

            return GetLocalizedText(CharacterDiagnosticsLocalizationKeys.FoodPreferencesLabel)
                + ": " + item.FoodPreferencesSummary + "\n"
                + GetFormattedText(
                    CharacterDiagnosticsLocalizationKeys.Movement,
                    currentMoveSpeed,
                    effectiveMoveSpeed,
                    moveLerpSpeed) + "\n"
                + GetFormattedText(
                    CharacterDiagnosticsLocalizationKeys.Animation,
                    simulationSpeedMultiplier,
                    movementAnimationSpeedMultiplier,
                    movementAnimationPlaybackSpeed);
        }

        private static string GetProgressTitleText(float done, float total)
        {
            var key = new IntPairKey(Mathf.RoundToInt(done), Mathf.RoundToInt(total));
            return GetCachedPairText(ProgressTitleByValue, key, "", "/", "");
        }

        private static string GetCycleText(string labelKey, string suffixKey, float done, float remaining, float total)
        {
            return GetLocalizedText(labelKey)
                + ": "
                + Mathf.RoundToInt(done)
                + "/"
                + Mathf.RoundToInt(remaining)
                + "/"
                + Mathf.RoundToInt(total)
                + GetLocalizedText(suffixKey);
        }

        private static string GetTaskBlockReasonText(string reason)
        {
            if (string.IsNullOrEmpty(reason) || reason == "-")
            {
                return "-";
            }

            switch (reason)
            {
                case "idle":
                    return GetLocalizedText(CharacterDiagnosticsLocalizationKeys.TaskBlockReasonIdle);
                case "manual move active":
                    return GetLocalizedText(CharacterDiagnosticsLocalizationKeys.TaskBlockReasonManualMove);
                case "delivering resource":
                    return GetLocalizedText(CharacterDiagnosticsLocalizationKeys.TaskBlockReasonDeliveringResource);
                case "already has global task":
                    return GetLocalizedText(CharacterDiagnosticsLocalizationKeys.TaskBlockReasonAlreadyHasTask);
                case "task acquired":
                    return GetLocalizedText(CharacterDiagnosticsLocalizationKeys.TaskBlockReasonTaskAcquired);
                case "task not acquired (unknown reason)":
                    return GetLocalizedText(CharacterDiagnosticsLocalizationKeys.TaskBlockReasonUnknown);
                case "idle wandering":
                    return GetLocalizedText(CharacterDiagnosticsLocalizationKeys.TaskBlockReasonIdleWandering);
                case "idle wander settle":
                    return GetLocalizedText(CharacterDiagnosticsLocalizationKeys.TaskBlockReasonIdleWanderSettle);
                case "idle wander wait":
                    return GetLocalizedText(CharacterDiagnosticsLocalizationKeys.TaskBlockReasonIdleWanderWait);
                case "idle wander retry pause":
                    return GetLocalizedText(CharacterDiagnosticsLocalizationKeys.TaskBlockReasonIdleWanderRetryPause);
            }

            const string localNeedFlowPrefix = "local need flow: ";
            if (reason.StartsWith(localNeedFlowPrefix, StringComparison.Ordinal))
            {
                return GetFormattedText(
                    CharacterDiagnosticsLocalizationKeys.TaskBlockReasonLocalNeedFlow,
                    GetLocalizedLocalNeed(reason.Substring(localNeedFlowPrefix.Length)));
            }

            const string localNeedPrefix = "local need: ";
            if (reason.StartsWith(localNeedPrefix, StringComparison.Ordinal))
            {
                return GetFormattedText(
                    CharacterDiagnosticsLocalizationKeys.TaskBlockReasonLocalNeed,
                    GetLocalizedLocalNeed(reason.Substring(localNeedPrefix.Length)));
            }

            return reason;
        }

        private static string GetLocalizedLocalNeed(string value)
        {
            if (Enum.TryParse(value, out UnitLocalNeedState localNeedState))
            {
                return GetLocalizedText(CharacterDiagnosticsLocalizationKeys.LocalState(localNeedState));
            }

            return value;
        }

        private static string GetLabeledText(string labelKey, string value)
        {
            return GetLocalizedText(labelKey) + ": " + value;
        }

        private static string GetLocalizedText(string key)
        {
            return CharacterDiagnosticsLocalizationKeys.Localized(key).GetLocalizedString();
        }

        private static string GetFormattedText(string key, params object[] arguments)
        {
            LocalizedString localizedString = CharacterDiagnosticsLocalizationKeys.Localized(key);
            localizedString.Arguments = arguments;
            return localizedString.GetLocalizedString();
        }

        private Button GetOrCreateRosterButton(int unitId)
        {
            if (_rosterButtons.TryGetValue(unitId, out Button existingButton))
            {
                return existingButton;
            }

            var button = new Button(() => OnRosterButtonClicked(unitId))
            {
                name = GetRosterButtonName(unitId)
            };
            button.AddToClassList("character-diagnostics-item");

            var icon = new Label();
            icon.AddToClassList("character-diagnostics-icon");
            button.Add(icon);

            _rosterButtons[unitId] = button;
            return button;
        }

        private static string GetRosterButtonName(int unitId)
        {
            if (RosterButtonNameByUnitId.TryGetValue(unitId, out string name))
            {
                return name;
            }

            name = $"character-item-{unitId}";
            RosterButtonNameByUnitId[unitId] = name;
            return name;
        }

        private void OnRosterButtonClicked(int unitId)
        {
            _selectedUnitId = unitId;
            _hudWindowCoordinator.CloseAll(WINDOW_ID);
            SetVisible(true);
        }

        private void RemoveMissingRosterButtons(IReadOnlyList<UnitDiagnosticsSnapshot> items)
        {
            List<int> missingUnitIds = MissingUnitIdsBuffer;
            missingUnitIds.Clear();

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

            missingUnitIds.Clear();
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
            VisualElement existing = root.Q<VisualElement>("character-diagnostics-panel");
            if (existing != null) return existing;

            VisualTreeAsset panelTemplate = Resources.Load<VisualTreeAsset>(PANEL_TEMPLATE_PATH);
            TemplateContainer panelTree = panelTemplate.CloneTree();
            VisualElement panel = panelTree.Q<VisualElement>("character-diagnostics-panel");
            panel.RemoveFromHierarchy();
            root.Add(panel);
            return panel;
        }

        private readonly struct IntPairKey : System.IEquatable<IntPairKey>
        {
            public readonly int First;
            public readonly int Second;

            public IntPairKey(int first, int second)
            {
                First = first;
                Second = second;
            }

            public bool Equals(IntPairKey other)
            {
                return First == other.First && Second == other.Second;
            }

            public override bool Equals(object obj)
            {
                return obj is IntPairKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (First * 397) ^ Second;
                }
            }
        }

        private static VisualElement EnsureRosterPanelCreated(VisualElement root)
        {
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