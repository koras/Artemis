using System;
using System.Collections.Generic;
using _Project.Scripts.Data.Offers;
using _Project.Scripts.Presentation.UI;
using _Project.Scripts.Systems.Offers;
using UnityEngine;
using UnityEngine.UIElements;

namespace _Project.Scripts.Presentation.UI.Offers
{
    /// <summary>
    /// UI офферов: таблицы, уведомление о новых офферах и карточка деталей.
    /// </summary>
    public sealed class OfferPanelPresenter : IDisposable
    {
        public const string WINDOW_ID = "Offer";

        private enum OfferViewMode
        {
            NewOffers = 0,
            Accepted = 1
        }

        private const string PANEL_TEMPLATE_PATH = "UI/Mode/OffersPanel";
        private const string TOOLTIP_TEMPLATE_PATH = "UI/Mode/OfferTooltipPanel";

        private readonly OfferSystemService _offerSystemService;
        private readonly HudWindowCoordinator _hudWindowCoordinator;
        private readonly VisualElement _root;
        private readonly Button _openButton;
        private Button _closeButton;
        private Button _newOffersTabButton;
        private Button _acceptedTabButton;
        private VisualElement _panel;
        private VisualElement _availableSection;
        private VisualElement _activeSection;
        private Label _goldLabel;
        private ScrollView _availableList;
        private ScrollView _activeList;
        private readonly VisualTreeAsset _panelTemplate;
        private readonly VisualTreeAsset _tooltipTemplate;

        private readonly VisualElement _tooltipPanel;
        private readonly Image _tooltipPortrait;
        private readonly Label _tooltipCustomerName;
        private readonly Label _tooltipCompanyName;
        private readonly Label _tooltipCompanyDescription;
        private readonly Label _tooltipTitle;
        private readonly Label _tooltipDescription;
        private readonly Label _tooltipRequirements;
        private readonly Label _tooltipDeadline;
        private readonly Button _tooltipAcceptButton;
        private readonly Button _tooltipCloseButton;
        private readonly Button _tooltipHeaderCloseButton;
        private readonly Button _tooltipRejectButton;

        private readonly HashSet<string> _knownAvailableOfferIds = new HashSet<string>();
        private readonly HashSet<string> _unreadOfferIds = new HashSet<string>();
        private IVisualElementScheduledItem _blinkSchedule;
        private bool _blinkOn;
        private OfferRuntimeRecord _selectedOffer;
        private OfferViewMode _activeViewMode = OfferViewMode.NewOffers;

        public OfferPanelPresenter(
            VisualElement root,
            OfferSystemService offerSystemService,
            HudWindowCoordinator hudWindowCoordinator)
        {
            _root = root;
            _offerSystemService = offerSystemService;
            _hudWindowCoordinator = hudWindowCoordinator;
            _openButton = root?.Q<Button>("offers-open-btn");
            _panelTemplate = Resources.Load<VisualTreeAsset>(PANEL_TEMPLATE_PATH);
            _tooltipTemplate = Resources.Load<VisualTreeAsset>(TOOLTIP_TEMPLATE_PATH);
            EnsurePanelReady();
            EnsureTooltipReady();

            _tooltipPanel = FindElement<VisualElement>(root, "offer-tooltip-panel", "offer-tooltip-panel");
            _tooltipPortrait = FindElement<Image>(root, "offer-tooltip-portrait", "offer-tooltip-portrait");
            _tooltipCustomerName = FindElement<Label>(root, "offer-tooltip-customer-name", "offer-tooltip-customer-name");
            _tooltipCompanyName = FindElement<Label>(root, "offer-tooltip-company-name", "offer-tooltip-company-name");
            _tooltipCompanyDescription = FindElement<Label>(root, "offer-tooltip-company-description", "offer-tooltip-company-description");
            _tooltipTitle = FindElement<Label>(root, "offer-tooltip-title", "offer-tooltip-title");
            _tooltipDescription = FindElement<Label>(root, "offer-tooltip-description", "offer-tooltip-description");
            _tooltipRequirements = FindElement<Label>(root, "offer-tooltip-requirements", "offer-tooltip-requirements");
            _tooltipDeadline = FindElement<Label>(root, "offer-tooltip-deadline", "offer-tooltip-deadline");
            _tooltipAcceptButton = FindElement<Button>(root, "offer-tooltip-accept-btn");
            _tooltipCloseButton = FindElement<Button>(root, "offer-tooltip-close-btn");
            _tooltipHeaderCloseButton = FindElement<Button>(root, "offer-tooltip-header-close-btn");
            _tooltipRejectButton = FindElement<Button>(root, "offer-tooltip-reject-btn");

            // Держим кнопку OFFERS в правой части верхней панели HUD.
            VisualElement hudRightControls = root?.Q<VisualElement>("hud-right-controls");
            if (hudRightControls != null && _openButton != null)
            {
                _openButton.text = "OFFERS";
                _openButton.RemoveFromHierarchy();
                hudRightControls.Add(_openButton);
            }

            if (_panel != null) _panel.style.display = DisplayStyle.None;
            if (_tooltipPanel != null) _tooltipPanel.style.display = DisplayStyle.None;
            _hudWindowCoordinator?.Register(WINDOW_ID, CloseAllUi);

            _openButton?.RegisterCallback<ClickEvent>(OnOpenClicked);
            _tooltipCloseButton?.RegisterCallback<ClickEvent>(OnTooltipCloseClicked);
            _tooltipHeaderCloseButton?.RegisterCallback<ClickEvent>(OnTooltipCloseClicked);
            _tooltipAcceptButton?.RegisterCallback<ClickEvent>(OnTooltipAcceptClicked);
            _tooltipRejectButton?.RegisterCallback<ClickEvent>(OnTooltipRejectClicked);

            if (_offerSystemService != null)
            {
                _offerSystemService.StateChanged += OnOfferStateChanged;
            }

            Render();
        }

        public void Dispose()
        {
            _hudWindowCoordinator?.SetBlockingWindowOpen(WINDOW_ID, false);
            _openButton?.UnregisterCallback<ClickEvent>(OnOpenClicked);
            _closeButton?.UnregisterCallback<ClickEvent>(OnCloseClicked);
            _newOffersTabButton?.UnregisterCallback<ClickEvent>(OnNewOffersTabClicked);
            _acceptedTabButton?.UnregisterCallback<ClickEvent>(OnAcceptedTabClicked);
            _tooltipCloseButton?.UnregisterCallback<ClickEvent>(OnTooltipCloseClicked);
            _tooltipHeaderCloseButton?.UnregisterCallback<ClickEvent>(OnTooltipCloseClicked);
            _tooltipAcceptButton?.UnregisterCallback<ClickEvent>(OnTooltipAcceptClicked);
            _tooltipRejectButton?.UnregisterCallback<ClickEvent>(OnTooltipRejectClicked);

            if (_offerSystemService != null)
            {
                _offerSystemService.StateChanged -= OnOfferStateChanged;
            }

            _blinkSchedule?.Pause();
            _blinkSchedule = null;
        }

        public void Render()
        {
            if (_goldLabel != null)
            {
                _goldLabel.text = $"Gold: {(_offerSystemService != null ? _offerSystemService.Gold : 0)}";
            }

            DetectNewOffers();
            UpdateOpenButtonBlink();
            UpdateViewStates();
            RenderAvailable();
            RenderActive();
            RenderTooltip();
        }

        private void RenderAvailable()
        {
            if (_availableList == null) return;
            _availableList.Clear();
            _availableList.Add(BuildTableHeader(false));

            if (_offerSystemService == null || _offerSystemService.AvailableOffers.Count == 0)
            {
                _availableList.Add(new Label("No new offers.") { name = "offer-empty-row" });
                return;
            }

            for (int i = 0; i < _offerSystemService.AvailableOffers.Count; i++)
            {
                _availableList.Add(BuildOfferRow(_offerSystemService.AvailableOffers[i], false));
            }
        }

        private void RenderActive()
        {
            if (_activeList == null) return;
            _activeList.Clear();
            _activeList.Add(BuildTableHeader(true));

            if (_offerSystemService == null || _offerSystemService.ActiveOffers.Count == 0)
            {
                _activeList.Add(new Label("No accepted tasks.") { name = "offer-empty-row" });
                return;
            }

            for (int i = 0; i < _offerSystemService.ActiveOffers.Count; i++)
            {
                _activeList.Add(BuildOfferRow(_offerSystemService.ActiveOffers[i], true));
            }
        }

        private VisualElement BuildOfferRow(OfferRuntimeRecord record, bool isActive)
        {
            OfferDefinition definition = record.Definition;
            bool isReservedForShipment = record.IsReservedForShipment;
            int goldReward = _offerSystemService != null ? _offerSystemService.GetGoldReward(record) : definition.GoldReward;

            var row = new VisualElement();
            row.AddToClassList("offer-table-row");

            OfferStageProgressSnapshot stageProgress = _offerSystemService.GetStageProgress(record);
            string stagePrefix = stageProgress != null
                ? $"S{stageProgress.StageIndex + 1}/{Mathf.Max(1, stageProgress.TotalStages)}"
                : "S1/1";

            row.Add(BuildTableCell($"{definition.Title} [{definition.Category}] {stagePrefix}", "offer-col-title"));
            row.Add(BuildTableCell(definition.Customer.FullName, "offer-col-customer"));
            row.Add(BuildTableCell(goldReward.ToString(), "offer-col-gold"));
            row.Add(BuildTableCell($"+{definition.ReputationReward}", "offer-col-reputation"));
            row.Add(BuildTableCell(BuildStageSummary(stageProgress, definition), "offer-col-requirements"));
            string deadlineText = record.DeadlineSol.HasValue ? $"{record.DeadlineSol.Value} sol" : "-";
            if (isActive && record.ShipmentMissionTarget > 0)
            {
                deadlineText = $"{deadlineText} / M#{record.ShipmentMissionTarget}";
            }

            row.Add(BuildTableCell(deadlineText, "offer-col-deadline"));
           // row.Add(BuildTableCell(record.Source.ToString(), "offer-col-source"));
            row.Add(BuildTableCell(BuildCooldownCellText(definition), "offer-col-cooldown"));

            var actions = new VisualElement();
            actions.AddToClassList("offer-table-cell");
            actions.AddToClassList("offer-col-actions");
            actions.Add(new Button(() => OpenTooltip(record)) { text = "Details" });
            if (isActive)
            {
                if (_offerSystemService.CurrentStageHasDeliverObjectives(record))
                {
                    if (isReservedForShipment)
                    {
                        actions.Add(new Button(() => _offerSystemService.CancelOfferReservation(record.RuntimeId)) { text = "Unreserve" });
                    }
                    else
                    {
                        actions.Add(new Button(() => _offerSystemService.TryReserveOfferForNextMission(record.RuntimeId, 0)) { text = "Reserve" });
                    }
                }
            }
            else
            {
                actions.Add(new Button(() => _offerSystemService.AcceptOffer(record.RuntimeId)) { text = "Accept" });
            }

            row.Add(actions);
            return row;
        }

        private static VisualElement BuildTableHeader(bool isActive)
        {
            var header = new VisualElement();
            header.AddToClassList("offer-table-header");
            header.Add(BuildTableCell("TASK", "offer-col-title"));
            header.Add(BuildTableCell("CUSTOMER", "offer-col-customer"));
            header.Add(BuildTableCell("G", "offer-col-gold"));
            header.Add(BuildTableCell("REP", "offer-col-reputation"));
            header.Add(BuildTableCell("RESOURCES", "offer-col-requirements"));
            header.Add(BuildTableCell("DEADLINE", "offer-col-deadline"));
         //   header.Add(BuildTableCell("SOURCE", "offer-col-source"));
            header.Add(BuildTableCell("COOLDOWN", "offer-col-cooldown"));
            header.Add(BuildTableCell(isActive ? "ACTIONS" : "DECISION", "offer-col-actions-title"));
            return header;
        }

        private static Label BuildTableCell(string text, string className)
        {
            var label = new Label(text);
            label.AddToClassList("offer-table-cell");
            label.AddToClassList(className);
            return label;
        }

        private void OpenTooltip(OfferRuntimeRecord record)
        {
            _selectedOffer = record;
            if (_tooltipPanel != null) _tooltipPanel.style.display = DisplayStyle.Flex;
            RefreshBlockingWindowState();
            RenderTooltip();
        }

        private void RenderTooltip()
        {
            if (_tooltipPanel == null || _selectedOffer == null) return;
            if (_tooltipPortrait == null || _tooltipCustomerName == null || _tooltipCompanyName == null ||
                _tooltipCompanyDescription == null || _tooltipTitle == null || _tooltipDescription == null ||
                _tooltipRequirements == null || _tooltipDeadline == null || _tooltipAcceptButton == null ||
                _tooltipRejectButton == null) return;

            OfferDefinition definition = _selectedOffer.Definition;
            if (definition == null || definition.Customer == null)
            {
                _tooltipPanel.style.display = DisplayStyle.None;
                return;
            }

            _tooltipPortrait.sprite = definition.Customer.KindPortrait;
            _tooltipCustomerName.text = definition.Customer.FullName;
            _tooltipCompanyName.text = definition.Customer.CompanyName;
            _tooltipCompanyDescription.text = definition.Customer.CompanyDescription;
            _tooltipTitle.text = definition.Title;
            _tooltipDescription.text = definition.Description;
            OfferStageProgressSnapshot stageProgress = _offerSystemService.GetStageProgress(_selectedOffer);
            bool hasDeliverObjectives = _offerSystemService.CurrentStageHasDeliverObjectives(_selectedOffer);
            string shipmentRuleText = hasDeliverObjectives
                ? "Shipment rule: only 100% reserve counts. Partial reserve fails the contract."
                : "Stage rule: this step resolves from base state and story progression.";
            string chainText = BuildChainText(definition);
            string fastBonusText = BuildFastBonusText(_selectedOffer);
            _tooltipRequirements.text =
                $"Category: {definition.Category}\n" +
                $"Type: {definition.Archetype}\n" +
                $"{BuildStageTooltip(stageProgress, definition)}\n" +
                $"{chainText}\n" +
                $"{fastBonusText}\n" +
                $"{shipmentRuleText}";
            int cooldownRemainingMinutes = _offerSystemService.GetCooldownRemainingMinutes(definition);
            string cooldownText = cooldownRemainingMinutes > 0
                ? $"{cooldownRemainingMinutes} min"
                : "ready";
            _tooltipDeadline.text = BuildDebugMetaText(_selectedOffer, cooldownText);

            bool isAvailable = ContainsRuntimeId(_offerSystemService.AvailableOffers, _selectedOffer.RuntimeId);
            bool isActive = ContainsRuntimeId(_offerSystemService.ActiveOffers, _selectedOffer.RuntimeId);

            _tooltipAcceptButton.style.display = isAvailable ? DisplayStyle.Flex : DisplayStyle.None;
            _tooltipRejectButton.style.display = isAvailable ? DisplayStyle.Flex : DisplayStyle.None;
            if (isActive)
            {
                if (hasDeliverObjectives)
                {
                    _tooltipAcceptButton.text = _selectedOffer.IsReservedForShipment ? "Unreserve" : "Reserve";
                    _tooltipAcceptButton.style.display = DisplayStyle.Flex;
                }
                else
                {
                    _tooltipAcceptButton.style.display = DisplayStyle.None;
                }
                _tooltipRejectButton.style.display = DisplayStyle.None;
            }
            else
            {
                _tooltipAcceptButton.text = "Accept";
            }
        }

        private void DetectNewOffers()
        {
            if (_offerSystemService == null) return;
            var currentAvailableIds = new HashSet<string>();
            for (int i = 0; i < _offerSystemService.AvailableOffers.Count; i++)
            {
                string runtimeId = _offerSystemService.AvailableOffers[i].RuntimeId;
                currentAvailableIds.Add(runtimeId);
                if (!_knownAvailableOfferIds.Contains(runtimeId)) _unreadOfferIds.Add(runtimeId);
            }

            _knownAvailableOfferIds.Clear();
            foreach (string runtimeId in currentAvailableIds) _knownAvailableOfferIds.Add(runtimeId);
            _unreadOfferIds.RemoveWhere(runtimeId => !currentAvailableIds.Contains(runtimeId));
        }

        private void UpdateOpenButtonBlink()
        {
            if (_openButton == null) return;
            if (_unreadOfferIds.Count <= 0)
            {
                _blinkSchedule?.Pause();
                _openButton.RemoveFromClassList("blink");
                _blinkOn = false;
                return;
            }

            if (_blinkSchedule == null)
            {
                _blinkSchedule = _openButton.schedule.Execute(() =>
                {
                    _blinkOn = !_blinkOn;
                    if (_blinkOn) _openButton.AddToClassList("blink");
                    else _openButton.RemoveFromClassList("blink");
                }).Every(450);
            }
            else
            {
                _blinkSchedule.Resume();
            }
        }

        private static string BuildRequirementsText(OfferResourceAmount[] requirements)
        {
            if (requirements == null || requirements.Length == 0) return "-";
            string result = string.Empty;
            for (int i = 0; i < requirements.Length; i++)
            {
                if (i > 0) result += ", ";
                result += $"{requirements[i].Amount} {requirements[i].ResourceId}";
            }

            return result;
        }

        private static string BuildStageSummary(OfferStageProgressSnapshot stageProgress, OfferDefinition definition)
        {
            if (stageProgress == null || stageProgress.Objectives == null || stageProgress.Objectives.Length == 0)
            {
                return BuildRequirementsText(definition?.CompletionRequirements);
            }

            int completed = 0;
            for (int i = 0; i < stageProgress.Objectives.Length; i++)
            {
                if (stageProgress.Objectives[i] != null && stageProgress.Objectives[i].IsCompleted)
                {
                    completed++;
                }
            }

            return $"{stageProgress.Stage?.Title ?? "Stage"} {completed}/{stageProgress.Objectives.Length}";
        }

        private string BuildCooldownCellText(OfferDefinition definition)
        {
            if (definition == null)
            {
                return "-";
            }

            if (!definition.IsRepeatable)
            {
                return "single";
            }

            int cooldownRemainingMinutes = _offerSystemService.GetCooldownRemainingMinutes(definition);
            return cooldownRemainingMinutes > 0 ? $"{cooldownRemainingMinutes}m" : "ready";
        }

        private static bool ContainsRuntimeId(IReadOnlyList<OfferRuntimeRecord> list, string runtimeId)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].RuntimeId == runtimeId) return true;
            }

            return false;
        }

        private static T FindElement<T>(VisualElement root, string name, string className = null) where T : VisualElement
        {
            if (root == null) return null;
            T byName = root.Q<T>(name);
            if (byName != null) return byName;
            if (string.IsNullOrWhiteSpace(className)) return null;
            return root.Q<T>(className: className);
        }

        private static string BuildDebugMetaText(OfferRuntimeRecord record, string cooldownText)
        {
            string deadlineText = record.DeadlineSol.HasValue
                ? $"Mining deadline: until sol {record.DeadlineSol.Value}"
                : "Mining deadline: unlimited";
            string fastReserveState = record.FastReserveBonusGranted ? "claimed" : "pending";
            return $"{deadlineText}\nSource: {record.Source}\nCooldown: {cooldownText}\nFast reserve: {fastReserveState}";
        }

        private static string BuildChainText(OfferDefinition definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.ChainId) || definition.ChainStep <= 0)
            {
                return "Chain: standalone";
            }

            return $"Chain: {definition.ChainId} step {definition.ChainStep}";
        }

        private string BuildFastBonusText(OfferRuntimeRecord record)
        {
            if (record?.Definition == null || record.Definition.FastReserveBonusGold <= 0)
            {
                return "Fast bonus: none";
            }

            if (record.FastReserveBonusGranted)
            {
                return $"Fast bonus: +{record.Definition.FastReserveBonusGold} gold secured";
            }

            bool isWindowOpen = _offerSystemService != null && _offerSystemService.IsFastReserveWindowOpen(record);
            return isWindowOpen
                ? $"Fast bonus: +{record.Definition.FastReserveBonusGold} gold if accepted now"
                : "Fast bonus: expired";
        }

        private static string BuildStageTooltip(OfferStageProgressSnapshot stageProgress, OfferDefinition definition)
        {
            if (stageProgress == null)
            {
                return $"Resources: {BuildRequirementsText(definition?.CompletionRequirements)}";
            }

            string title = stageProgress.Stage != null
                ? $"Stage {stageProgress.StageIndex + 1}/{Mathf.Max(1, stageProgress.TotalStages)}: {stageProgress.Stage.Title}"
                : $"Stage {stageProgress.StageIndex + 1}/{Mathf.Max(1, stageProgress.TotalStages)}";
            string description = stageProgress.Stage != null && !string.IsNullOrWhiteSpace(stageProgress.Stage.Description)
                ? stageProgress.Stage.Description
                : string.Empty;
            string objectives = BuildObjectiveLines(stageProgress.Objectives);

            if (string.IsNullOrWhiteSpace(description))
            {
                return $"{title}\n{objectives}";
            }

            return $"{title}\n{description}\n{objectives}";
        }

        private static string BuildObjectiveLines(OfferObjectiveProgressSnapshot[] objectives)
        {
            if (objectives == null || objectives.Length == 0)
            {
                return "No objectives.";
            }

            string result = "Objectives:";
            for (int i = 0; i < objectives.Length; i++)
            {
                OfferObjectiveProgressSnapshot objective = objectives[i];
                if (objective?.Objective == null)
                {
                    continue;
                }

                string description = !string.IsNullOrWhiteSpace(objective.Objective.Description)
                    ? objective.Objective.Description
                    : objective.Objective.ObjectiveType.ToString();
                string progress = objective.TargetValue > 0
                    ? $"{objective.CurrentValue}/{objective.TargetValue}"
                    : (objective.IsCompleted ? "done" : "pending");
                string marker = objective.IsCompleted ? "[x]" : "[ ]";
                result += $"\n{marker} {description} ({progress})";
            }

            return result;
        }

        private void OnOpenClicked(ClickEvent _)
        {
            EnsurePanelReady();
            if (_panel == null) return;
            _activeViewMode = OfferViewMode.NewOffers;
            _hudWindowCoordinator?.CloseAll(WINDOW_ID);
            _panel.style.display = DisplayStyle.Flex;
            _unreadOfferIds.Clear();
            UpdateOpenButtonBlink();
            RefreshBlockingWindowState();
            Render();
        }

        private void OnCloseClicked(ClickEvent _)
        {
            CloseAllUi();
        }

        private void OnNewOffersTabClicked(ClickEvent _)
        {
            _activeViewMode = OfferViewMode.NewOffers;
            Render();
        }

        private void OnAcceptedTabClicked(ClickEvent _)
        {
            _activeViewMode = OfferViewMode.Accepted;
            Render();
        }

        private void OnTooltipCloseClicked(ClickEvent _)
        {
            if (_tooltipPanel == null) return;
            _tooltipPanel.style.display = DisplayStyle.None;
            _selectedOffer = null;
            RefreshBlockingWindowState();
        }

        private void OnTooltipAcceptClicked(ClickEvent _)
        {
            if (_selectedOffer == null) return;
            bool isAvailable = ContainsRuntimeId(_offerSystemService.AvailableOffers, _selectedOffer.RuntimeId);
            bool isActive = ContainsRuntimeId(_offerSystemService.ActiveOffers, _selectedOffer.RuntimeId);

            if (isAvailable) _offerSystemService.AcceptOffer(_selectedOffer.RuntimeId);
            else if (isActive)
            {
                if (_selectedOffer.IsReservedForShipment) _offerSystemService.CancelOfferReservation(_selectedOffer.RuntimeId);
                else _offerSystemService.TryReserveOfferForNextMission(_selectedOffer.RuntimeId, 0);
            }

            if (_tooltipPanel != null) _tooltipPanel.style.display = DisplayStyle.None;
            _selectedOffer = null;
            RefreshBlockingWindowState();
        }

        private void OnTooltipRejectClicked(ClickEvent _)
        {
            if (_selectedOffer == null) return;
            _offerSystemService.RejectOffer(_selectedOffer.RuntimeId);
            if (_tooltipPanel != null) _tooltipPanel.style.display = DisplayStyle.None;
            _selectedOffer = null;
            RefreshBlockingWindowState();
        }

        private void OnOfferStateChanged()
        {
            if (_selectedOffer != null)
            {
                bool stillExists = ContainsRuntimeId(_offerSystemService.AvailableOffers, _selectedOffer.RuntimeId) ||
                                   ContainsRuntimeId(_offerSystemService.ActiveOffers, _selectedOffer.RuntimeId);
                if (!stillExists)
                {
                    _selectedOffer = null;
                    if (_tooltipPanel != null) _tooltipPanel.style.display = DisplayStyle.None;
                    RefreshBlockingWindowState();
                }
            }

            Render();
        }

        private void EnsurePanelReady()
        {
            if (_panel != null)
            {
                return;
            }

            _panel = EnsurePanelCreated(_root);
            if (_panel == null)
            {
                return;
            }

            _closeButton = _panel.Q<Button>("offers-close-btn");
            _newOffersTabButton = _panel.Q<Button>("offers-tab-new");
            _acceptedTabButton = _panel.Q<Button>("offers-tab-accepted");
            _availableSection = _panel.Q<VisualElement>("offers-available-section");
            _activeSection = _panel.Q<VisualElement>("offers-active-section");
            _goldLabel = _panel.Q<Label>("offers-gold-label");
            _availableList = _panel.Q<ScrollView>("offers-available-list");
            _activeList = _panel.Q<ScrollView>("offers-active-list");
            _closeButton?.RegisterCallback<ClickEvent>(OnCloseClicked);
            _newOffersTabButton?.RegisterCallback<ClickEvent>(OnNewOffersTabClicked);
            _acceptedTabButton?.RegisterCallback<ClickEvent>(OnAcceptedTabClicked);
        }

        private VisualElement EnsurePanelCreated(VisualElement root)
        {
            if (root == null)
            {
                return null;
            }

            VisualElement existingPanel = root.Q<VisualElement>("offers-panel");
            if (existingPanel != null)
            {
                return existingPanel;
            }

            if (_panelTemplate == null)
            {
                Debug.LogWarning($"[OfferPanelPresenter] Offers panel template not found at Resources/{PANEL_TEMPLATE_PATH}.uxml");
                return null;
            }

            VisualElement panelTree = _panelTemplate.Instantiate();
            VisualElement instantiatedPanel = panelTree.Q<VisualElement>("offers-panel");
            if (instantiatedPanel == null)
            {
                Debug.LogWarning("[OfferPanelPresenter] offers-panel was not found inside template.");
                return null;
            }

            instantiatedPanel.RemoveFromHierarchy();
            root.Add(instantiatedPanel);
            return instantiatedPanel;
        }

        private void EnsureTooltipReady()
        {
            if (_root == null)
            {
                return;
            }

            if (_root.Q<VisualElement>("offer-tooltip-panel") != null)
            {
                return;
            }

            if (_tooltipTemplate == null)
            {
                Debug.LogWarning($"[OfferPanelPresenter] Offer tooltip template not found at Resources/{TOOLTIP_TEMPLATE_PATH}.uxml");
                return;
            }

            VisualElement tooltipTree = _tooltipTemplate.Instantiate();
            VisualElement tooltipPanel = tooltipTree.Q<VisualElement>("offer-tooltip-panel");
            if (tooltipPanel == null)
            {
                Debug.LogWarning("[OfferPanelPresenter] offer-tooltip-panel was not found inside tooltip template.");
                return;
            }

            tooltipPanel.RemoveFromHierarchy();
            _root.Add(tooltipPanel);
        }

        private void CloseAllUi()
        {
            if (_panel != null)
            {
                _panel.style.display = DisplayStyle.None;
            }

            if (_tooltipPanel != null)
            {
                _tooltipPanel.style.display = DisplayStyle.None;
            }

            _selectedOffer = null;
            RefreshBlockingWindowState();
        }

        private void RefreshBlockingWindowState()
        {
            bool isPanelVisible = _panel != null && _panel.style.display != DisplayStyle.None;
            bool isTooltipVisible = _tooltipPanel != null && _tooltipPanel.style.display != DisplayStyle.None;
            _hudWindowCoordinator?.SetBlockingWindowOpen(WINDOW_ID, isPanelVisible || isTooltipVisible);
        }

        private void UpdateViewStates()
        {
            bool showNewOffers = _activeViewMode == OfferViewMode.NewOffers;
            if (_availableSection != null)
            {
                _availableSection.style.display = showNewOffers ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (_activeSection != null)
            {
                _activeSection.style.display = showNewOffers ? DisplayStyle.None : DisplayStyle.Flex;
            }

            bool hasAcceptedOffers = _offerSystemService != null && _offerSystemService.ActiveOffers.Count > 0;
            SetTabState(_newOffersTabButton, showNewOffers, false, false);
            SetTabState(_acceptedTabButton, !showNewOffers, true, hasAcceptedOffers);
        }

        private static void SetTabState(Button button, bool isActive, bool isAcceptedTab, bool hasAcceptedOffers)
        {
            if (button == null)
            {
                return;
            }

            button.EnableInClassList("offers-tab-btn-active", isActive && (!isAcceptedTab || !hasAcceptedOffers));

            // Accepted tab stays green when there are accepted contracts so the player can spot them immediately.
            button.EnableInClassList("offers-tab-btn-accepted-ready", hasAcceptedOffers && !isActive && isAcceptedTab);
            button.EnableInClassList("offers-tab-btn-accepted-active", hasAcceptedOffers && isActive && isAcceptedTab);
        }
    }
}
