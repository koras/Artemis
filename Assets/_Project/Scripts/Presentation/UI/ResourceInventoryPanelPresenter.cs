using System;
using System.Collections.Generic;
using _Project.Scripts.Systems.Resources;
using UnityEngine;
using UnityEngine.UIElements;

namespace _Project.Scripts.Presentation.UI
{
    /// <summary>
    /// Отрисовывает правую HUD-панель с ресурсами, которые уже доставили на склад.
    /// </summary>
    public sealed class ResourceInventoryPanelPresenter : IDisposable
    {
        private const string PANEL_TEMPLATE_PATH = "UI/Mode/ResourceInventoryPanel";
        private static readonly string[] DefaultResourceIds =
        {
           ResourceInventoryService.GOLD_RESOURCE_ID, "Cable", "Water Pipe", "Oxygen Pipe", "Ladder", "Iron", "Titan", "aluminium", "Rogalite"
        };
        private const string DroppedPrefabsLabel = "Dropped Prefabs";

        private readonly VisualElement _resourceList;
        private readonly VisualElement _resourceTooltip;
        private readonly Label _resourceTooltipLabel;
        private readonly ResourceInventoryService _resourceInventoryService;
        private readonly SceneResourceObjectService _sceneResourceObjectService;
        private readonly Dictionary<string, Texture2D> _resourceIconCache = new Dictionary<string, Texture2D>();

        public ResourceInventoryPanelPresenter(
            VisualElement root,
            ResourceInventoryService resourceInventoryService,
            SceneResourceObjectService sceneResourceObjectService)
        {
            EnsurePanelCreated(root);
            _resourceList = root?.Q<VisualElement>("resource-inventory-list");
            _resourceTooltip = root?.Q<VisualElement>("resource-tooltip");
            _resourceTooltipLabel = root?.Q<Label>("resource-tooltip-label");
            _resourceInventoryService = resourceInventoryService;
            _sceneResourceObjectService = sceneResourceObjectService;

            if (_resourceInventoryService != null)
            {
                _resourceInventoryService.InventoryChanged += OnInventoryChanged;
            }

            Render();
        }

        public void Dispose()
        {
            if (_resourceInventoryService != null)
            {
                _resourceInventoryService.InventoryChanged -= OnInventoryChanged;
            }
        }

        public void Render()
        {
            if (_resourceList == null) return;

            _resourceList.Clear();

            List<string> resourceIds = BuildResourceIds();
            for (int i = 0; i < resourceIds.Count; i++)
            {
                string resourceId = resourceIds[i];
                int amount = _resourceInventoryService != null
                    ? _resourceInventoryService.GetAmount(resourceId)
                    : 0;
                _resourceList.Add(BuildResourceItem(resourceId, amount));
            }

            int droppedPrefabs = _sceneResourceObjectService != null
                ? _sceneResourceObjectService.GetTotalDroppedPrefabCount()
                : 0;
            _resourceList.Add(BuildResourceItem(DroppedPrefabsLabel, droppedPrefabs));
        }

        private List<string> BuildResourceIds()
        {
            var resourceIds = new List<string>(DefaultResourceIds);

            if (_resourceInventoryService == null) return resourceIds;

            Dictionary<string, int> snapshot = _resourceInventoryService.GetAmountsSnapshot();
            foreach (string resourceId in snapshot.Keys)
            {
                if (resourceIds.Contains(resourceId)) continue;

                resourceIds.Add(resourceId);
            }

            return resourceIds;
        }

        private VisualElement BuildResourceItem(string resourceId, int amount)
        {
            // Иконка в HUD компактная: первая буква ресурса в круге.
            var row = new VisualElement();
            row.AddToClassList("resource-row");

            string iconText = string.IsNullOrWhiteSpace(resourceId)
                ? "?"
                : resourceId.Substring(0, 1).ToUpperInvariant();
            var iconLabel = new Label(iconText);
            iconLabel.AddToClassList("resource-icon");
            iconLabel.tooltip = BuildTooltipText(resourceId, amount);
            TryApplyIconTexture(iconLabel, resourceId);

            var nameLabel = new Label(resourceId);
            nameLabel.AddToClassList("resource-row-name");

            var amountLabel = new Label(amount.ToString());
            amountLabel.AddToClassList("resource-row-amount");

            row.Add(iconLabel);
            row.Add(nameLabel);
            row.Add(amountLabel);
            RegisterTooltipCallbacks(row, resourceId, amount);
            return row;
        }

        private static string BuildTooltipText(string resourceId, int amount)
        {
            return $"{resourceId}\nAmount: {amount}";
        }

        private void TryApplyIconTexture(Label iconLabel, string resourceId)
        {
            if (iconLabel == null || string.IsNullOrWhiteSpace(resourceId))
            {
                return;
            }

            if (!_resourceIconCache.TryGetValue(resourceId, out Texture2D iconTexture))
            {
                iconTexture = Resources.Load<Texture2D>($"UI/ResourceIcons/{resourceId}");
                _resourceIconCache[resourceId] = iconTexture;
            }

            if (iconTexture == null)
            {
                return;
            }

            // Если для ресурса есть PNG-иконка, показываем её поверх текстовой заглушки.
            iconLabel.text = string.Empty;
            iconLabel.style.backgroundImage = new StyleBackground(iconTexture);
            iconLabel.style.backgroundRepeat = new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat);
            iconLabel.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            iconLabel.style.unityBackgroundImageTintColor = Color.white;
        }

        private void OnInventoryChanged()
        {
            Render();
        }

        private void RegisterTooltipCallbacks(VisualElement row, string resourceId, int amount)
        {
            if (_resourceTooltip == null || _resourceTooltipLabel == null)
            {
                return;
            }

            row.RegisterCallback<MouseEnterEvent>(_ => ShowTooltip(row, resourceId, amount));
            row.RegisterCallback<MouseLeaveEvent>(_ => HideTooltip());
        }

        private void ShowTooltip(VisualElement row, string resourceId, int amount)
        {
            // Tooltip позиционируем под иконкой ресурса внутри общей верхней панели.
            _resourceTooltipLabel.text = BuildTooltipText(resourceId, amount);
            _resourceTooltip.style.display = DisplayStyle.Flex;
            _resourceTooltip.style.left = row.layout.xMin;
        }

        private void HideTooltip()
        {
            if (_resourceTooltip == null)
            {
                return;
            }

            _resourceTooltip.style.display = DisplayStyle.None;
        }

        private static void EnsurePanelCreated(VisualElement root)
        {
            if (root == null)
            {
                return;
            }

            if (root.Q<VisualElement>("resource-inventory-panel") != null)
            {
                return;
            }

            VisualTreeAsset panelTemplate = Resources.Load<VisualTreeAsset>(PANEL_TEMPLATE_PATH);
            if (panelTemplate == null)
            {
                Debug.LogWarning($"[ResourceInventoryPanelPresenter] Resource panel template not found at Resources/{PANEL_TEMPLATE_PATH}.uxml");
                return;
            }

            VisualElement panelTree = panelTemplate.Instantiate();
            VisualElement panel = panelTree.Q<VisualElement>("resource-inventory-panel");
            if (panel == null)
            {
                Debug.LogWarning("[ResourceInventoryPanelPresenter] resource-inventory-panel was not found inside template.");
                return;
            }

            panel.RemoveFromHierarchy();
            root.Add(panel);
        }
    }
}
