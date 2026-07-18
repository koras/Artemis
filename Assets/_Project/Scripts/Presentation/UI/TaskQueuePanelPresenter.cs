using System.Collections.Generic;
using _Project.Scripts.Data.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace _Project.Scripts.Presentation.UI
{
    public sealed class TaskQueuePanelPresenter
    {
        public const string WINDOW_ID = "TaskQueue";

        private const string PANEL_TEMPLATE_PATH = "UI/Mode/TaskQueuePanel";

        private readonly VisualElement _panel;
        private readonly ScrollView _taskQueueList;
        private bool _isVisible = true;

        public TaskQueuePanelPresenter(VisualElement root, HudWindowCoordinator hudWindowCoordinator)
        {
            _panel = EnsurePanelCreated(root);
            _taskQueueList = _panel?.Q<ScrollView>("task-queue-list");
            hudWindowCoordinator?.Register(WINDOW_ID, () => SetVisible(false));
        }

        public bool IsVisible => _isVisible;

        public void SetVisible(bool isVisible)
        {
            _isVisible = isVisible;
            if (_panel == null)
            {
                return;
            }

            _panel.style.display = _isVisible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void Render(IReadOnlyList<TaskQueueItemViewModel> items)
        {
            if (_taskQueueList == null)
            {
                return;
            }

            _taskQueueList.Clear();

            if (items == null || items.Count == 0)
            {
                Label empty = new Label("No tasks in queue");
                empty.AddToClassList("task-row-empty");
                _taskQueueList.Add(empty);
                return;
            }

            for (int i = 0; i < items.Count; i++)
            {
                TaskQueueItemViewModel item = items[i];

                var row = new VisualElement();
                row.AddToClassList("task-row");
                row.pickingMode = PickingMode.Position;
                row.userData = item;
                row.RegisterCallback<ClickEvent>(OnTaskRowClicked);

                var title = new Label(item.TaskTitle);
                title.AddToClassList("task-row-title");

                string reasonPart = string.IsNullOrWhiteSpace(item.WaitReasonText) ? "" : $" | Reason: {item.WaitReasonText}";
                var meta = new Label($"Status: {item.StatusText} | Priority: {item.PriorityText} | Distance: {item.DistanceToNearestUnit}{reasonPart}");
                meta.AddToClassList("task-row-meta");

                row.Add(title);
                row.Add(meta);
                _taskQueueList.Add(row);
            }
        }

        private static void OnTaskRowClicked(ClickEvent clickEvent)
        {
            if (clickEvent.currentTarget is not VisualElement row)
            {
                return;
            }

            if (row.userData is not TaskQueueItemViewModel item)
            {
                return;
            }

            // Click on HUD task row prints full diagnostic snapshot for quick AI triage.
            Debug.Log(
                $"[TaskQueueHud] Clicked task: {item.TaskTitle}\n" +
                $"TaskId={item.TaskId}, TaskType={item.TaskTypeText}, TargetCell={item.TargetCellText}\n" +
                $"Status={item.StatusText}, Priority={item.PriorityText}, DistanceToNearestUnit={item.DistanceToNearestUnit}\n" +
                $"WaitReason={item.WaitReasonText}\n" +
                $"NotTakenReason={item.NotTakenReasonText}\n" +
                $"PendingClearing={item.PendingClearingDetailsText}\n" +
                $"UnitBlockSummary={item.UnitBlockSummaryText}");
        }

        private static VisualElement EnsurePanelCreated(VisualElement root)
        {
            if (root == null)
            {
                return null;
            }

            VisualElement existingPanel = root.Q<VisualElement>("task-queue-panel");
            if (existingPanel != null)
            {
                return existingPanel;
            }

            VisualTreeAsset panelTemplate = Resources.Load<VisualTreeAsset>(PANEL_TEMPLATE_PATH);
            if (panelTemplate == null)
            {
                Debug.LogWarning($"[TaskQueuePanelPresenter] Task queue panel template not found at Resources/{PANEL_TEMPLATE_PATH}.uxml");
                return null;
            }

            VisualElement panelTree = panelTemplate.Instantiate();
            VisualElement instantiatedPanel = panelTree.Q<VisualElement>("task-queue-panel");
            if (instantiatedPanel == null)
            {
                Debug.LogWarning("[TaskQueuePanelPresenter] task-queue-panel was not found inside template.");
                return null;
            }

            instantiatedPanel.RemoveFromHierarchy();
            root.Add(instantiatedPanel);
            return instantiatedPanel;
        }
    }

    public readonly struct TaskQueueItemViewModel
    {
        public readonly string TaskTitle;
        public readonly int TaskId;
        public readonly string TaskTypeText;
        public readonly string TargetCellText;
        public readonly string StatusText;
        public readonly string WaitReasonText;
        public readonly string NotTakenReasonText;
        public readonly string UnitBlockSummaryText;
        public readonly string PendingClearingDetailsText;
        public readonly string PriorityText;
        public readonly int DistanceToNearestUnit;

        public TaskQueueItemViewModel(
            string taskTitle,
            int taskId,
            string taskTypeText,
            string targetCellText,
            string statusText,
            string waitReasonText,
            string notTakenReasonText,
            string unitBlockSummaryText,
            string pendingClearingDetailsText,
            string priorityText,
            int distanceToNearestUnit)
        {
            TaskTitle = taskTitle;
            TaskId = taskId;
            TaskTypeText = taskTypeText;
            TargetCellText = targetCellText;
            StatusText = statusText;
            WaitReasonText = waitReasonText;
            NotTakenReasonText = notTakenReasonText;
            UnitBlockSummaryText = unitBlockSummaryText;
            PendingClearingDetailsText = pendingClearingDetailsText;
            PriorityText = priorityText;
            DistanceToNearestUnit = distanceToNearestUnit;
        }
    }
}
