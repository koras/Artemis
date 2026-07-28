using _Project.Scripts.Data.Shop;
using _Project.Scripts.Presentation.UI;
using _Project.Scripts.Systems.Shop;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UIElements;

namespace _Project.Scripts.Presentation.UI.Shop
{
    /// <summary>
    /// Shop UI presenter: open/close panel, category filter, order toggle, and order actions.
    /// </summary>
    public sealed class ShopPanelPresenter : System.IDisposable
    {
        public const string WINDOW_ID = "Shop";

        private enum ShopViewMode
        {
            Catalog = 0,
            Orders = 1
        }

        private const string PANEL_TEMPLATE_PATH = "UI/Mode/ShopPanel";
        private const string ROW_TEMPLATE_PATH = "UI/Mode/ShopRow";

        private readonly ShopSystemService _shopSystemService;
        private readonly HudWindowCoordinator _hudWindowCoordinator;
        private readonly VisualElement _root;
        private readonly Button _openButton;
        private readonly VisualTreeAsset _panelTemplate;
        private readonly VisualTreeAsset _rowTemplate;

        private Button _closeButton;
        private Button _ordersTabButton;
        private Button _filterAllButton;
        private Button _filterFoodButton;
        private Button _filterEquipmentButton;
        private Button _filterPersonnelButton;
        private Label _goldLabel;
        private readonly LocalizedString _goldLabelString = new LocalizedString("UI", "shop.gold");
        private VisualElement _panel;
        private VisualElement _filterRow;
        private VisualElement _tableHeader;
        private VisualElement _rowsContainer;
        private ShopProductCategory? _activeFilterCategory;
        private ShopViewMode _activeViewMode = ShopViewMode.Catalog;

        public ShopPanelPresenter(
            VisualElement root,
            ShopSystemService shopSystemService,
            HudWindowCoordinator hudWindowCoordinator)
        {
            _root = root;
            _shopSystemService = shopSystemService;
            _hudWindowCoordinator = hudWindowCoordinator;
            _openButton = root?.Q<Button>("shop-open-btn");
            _panelTemplate = Resources.Load<VisualTreeAsset>(PANEL_TEMPLATE_PATH);
            _rowTemplate = Resources.Load<VisualTreeAsset>(ROW_TEMPLATE_PATH);

            _openButton?.RegisterCallback<ClickEvent>(OnOpenClicked);
            EnsurePanelReady();
            _goldLabelString.StringChanged += OnGoldLabelChanged;

            if (_shopSystemService != null)
            {
                _shopSystemService.StateChanged += OnShopStateChanged;
            }

            _hudWindowCoordinator?.Register(WINDOW_ID, ClosePanel);
            Render();
        }

        public void Dispose()
        {
            _hudWindowCoordinator?.SetBlockingWindowOpen(WINDOW_ID, false);
            _openButton?.UnregisterCallback<ClickEvent>(OnOpenClicked);
            _closeButton?.UnregisterCallback<ClickEvent>(OnCloseClicked);
            UnbindOrdersButton();
            UnbindFilterButtons();
            _goldLabelString.StringChanged -= OnGoldLabelChanged;

            if (_shopSystemService != null)
            {
                _shopSystemService.StateChanged -= OnShopStateChanged;
            }
        }

        public void Render()
        {
            if (_rowsContainer == null)
            {
                return;
            }

            _rowsContainer.Clear();
            UpdateViewStates();
            RefreshGoldLabel();

            if (_activeViewMode == ShopViewMode.Orders)
            {
                RenderOrders();
                return;
            }

            RenderCatalog();
        }

        private void RenderCatalog()
        {
            if (_shopSystemService == null || _shopSystemService.AvailableEntries.Count == 0)
            {
                _rowsContainer.Add(CreateLocalizedLabel("shop.no.products", "shop-empty-row"));
                return;
            }

            int addedRows = 0;
            for (int i = 0; i < _shopSystemService.AvailableEntries.Count; i++)
            {
                ShopSystemService.ShopRuntimeEntry entry = _shopSystemService.AvailableEntries[i];
                if (!MatchesFilter(entry.Product.Category))
                {
                    continue;
                }

                if (_shopSystemService.GetRemainingOrderCapacity(entry.EntryKey) <= 0)
                {
                    continue;
                }

                _rowsContainer.Add(BuildProductRow(entry));
                addedRows++;
            }

            if (addedRows == 0)
            {
                _rowsContainer.Add(CreateLocalizedLabel("shop.no.filtered.products", "shop-empty-row"));
            }
        }

        private void RenderOrders()
        {
            if (_shopSystemService == null || _shopSystemService.PendingOrders.Count == 0)
            {
                _rowsContainer.Add(CreateLocalizedLabel("shop.no.orders", "shop-empty-row"));
                return;
            }

            for (int i = 0; i < _shopSystemService.PendingOrders.Count; i++)
            {
                _rowsContainer.Add(BuildOrderRow(_shopSystemService.PendingOrders[i]));
            }
        }

        private bool MatchesFilter(ShopProductCategory category)
        {
            return !_activeFilterCategory.HasValue || _activeFilterCategory.Value == category;
        }

        private VisualElement BuildProductRow(ShopSystemService.ShopRuntimeEntry entry)
        {
            var row = _rowTemplate != null ? _rowTemplate.Instantiate() : new VisualElement();
            row.AddToClassList(GetRowTypeClass(entry.Product.Category));

            string entryKey = entry.EntryKey;
            int selectedAmount = _shopSystemService.GetSelectedAmount(entryKey);
            int remainingOrderCapacity = _shopSystemService.GetRemainingOrderCapacity(entryKey);
            // UI shows the still-free limit after the current selection, while the service keeps validating against pending orders.
            int visibleRemainingCapacity = Mathf.Max(0, remainingOrderCapacity - selectedAmount);
            int totalPrice = selectedAmount * entry.UnitPrice;

            var productName = row.Q<Label>("shop-row-product-name");
            if (productName != null)
            {
                productName.text = entry.Product.ProductName;
                productName.tooltip = entry.Product.Description;
            }

            SetLabelText(row, "shop-row-supplier", entry.Supplier != null ? entry.Supplier.CompanyName : "<missing>");
            SetLabelText(row, "shop-row-selected-value", selectedAmount.ToString());
            SetLabelText(row, "shop-row-max", $"{visibleRemainingCapacity}/{entry.MaxPurchaseAmount}");
            SetLabelText(row, "shop-row-unit-price", entry.UnitPrice.ToString());
            SetLabelText(row, "shop-row-total-price", totalPrice.ToString());

            var minusButton = row.Q<Button>("shop-row-minus");
            if (minusButton != null)
            {
                minusButton.clicked += () =>
                {
                    _shopSystemService.ChangeSelectedAmount(entryKey, -1);
                    Render();
                };
            }

            var plusButton = row.Q<Button>("shop-row-plus");
            if (plusButton != null)
            {
                plusButton.SetEnabled(visibleRemainingCapacity > 0);
                plusButton.clicked += () =>
                {
                    _shopSystemService.ChangeSelectedAmount(entryKey, 1);
                    Render();
                };
            }

            var orderButton = row.Q<Button>("shop-row-order");
            if (orderButton != null)
            {
                orderButton.SetEnabled(selectedAmount > 0 && visibleRemainingCapacity < remainingOrderCapacity);
                orderButton.clicked += () =>
                {
                    _shopSystemService.PlaceOrder(entryKey);
                    Render();
                };
            }

            return row;
        }

        private VisualElement BuildOrderRow(ShopSystemService.PendingShopOrder order)
        {
            var row = new VisualElement();
            row.AddToClassList("shop-table-row");
            row.AddToClassList("shop-order-row");

            var productCell = new VisualElement();
            productCell.AddToClassList("shop-table-cell");
            productCell.AddToClassList("shop-col-product");

            var productName = new Label(order.Product != null ? order.Product.ProductName : "<missing>");
            productName.AddToClassList("shop-product-name");
            if (order.Product != null)
            {
                productName.tooltip = order.Product.Description;
            }

            productCell.Add(productName);
            row.Add(productCell);
            row.Add(BuildOrderCell(order.Amount.ToString(), "shop-col-order-amount"));
            row.Add(BuildOrderCell(order.TotalPrice.ToString(), "shop-col-order-price"));
            row.Add(BuildOrderCell(_shopSystemService.GetRemainingDeliveryDays(order).ToString(), "shop-col-order-days"));

            var actionsCell = new VisualElement();
            actionsCell.AddToClassList("shop-col-actions");
            actionsCell.AddToClassList("shop-order-actions");

            // Cancel action refunds the exact amount originally paid for this order.
            var cancelButton = new Button(() =>
            {
                _shopSystemService.CancelOrder(order.OrderId);
                Render();
            });
            cancelButton.SetBinding("text", new LocalizedString("UI", "shop.cancel"));
            cancelButton.AddToClassList("shop-order-cancel-btn");
            actionsCell.Add(cancelButton);
            row.Add(actionsCell);

            return row;
        }

        private static Label BuildOrderCell(string text, string className)
        {
            var label = new Label(text);
            label.AddToClassList("shop-table-cell");
            label.AddToClassList(className);
            return label;
        }

        private static void SetLabelText(VisualElement row, string name, string value)
        {
            var label = row.Q<Label>(name);
            if (label != null)
            {
                label.text = value;
            }
        }

        private static string GetRowTypeClass(ShopProductCategory category)
        {
            switch (category)
            {
                case ShopProductCategory.Food:
                    return "shop-category-food";
                case ShopProductCategory.Equipment:
                    return "shop-category-equipment";
                case ShopProductCategory.Personnel:
                    return "shop-category-personnel";
                default:
                    return "shop-category-food";
            }
        }

        private void OnOpenClicked(ClickEvent _)
        {
            EnsurePanelReady();
            if (_panel == null)
            {
                return;
            }

            _activeViewMode = ShopViewMode.Catalog;
            _activeFilterCategory = null;
            _hudWindowCoordinator?.CloseAll(WINDOW_ID);
            _panel.style.display = DisplayStyle.Flex;
            _hudWindowCoordinator?.SetBlockingWindowOpen(WINDOW_ID, true);
            Render();
        }

        private void OnCloseClicked(ClickEvent _)
        {
            ClosePanel();
        }

        private void OnOrdersTabClicked(ClickEvent _)
        {
            _activeViewMode = _activeViewMode == ShopViewMode.Catalog
                ? ShopViewMode.Orders
                : ShopViewMode.Catalog;
            Render();
        }

        private void OnFilterAllClicked(ClickEvent _)
        {
            _activeViewMode = ShopViewMode.Catalog;
            _activeFilterCategory = null;
            Render();
        }

        private void OnFilterFoodClicked(ClickEvent _)
        {
            _activeViewMode = ShopViewMode.Catalog;
            _activeFilterCategory = ShopProductCategory.Food;
            Render();
        }

        private void OnFilterEquipmentClicked(ClickEvent _)
        {
            _activeViewMode = ShopViewMode.Catalog;
            _activeFilterCategory = ShopProductCategory.Equipment;
            Render();
        }

        private void OnFilterPersonnelClicked(ClickEvent _)
        {
            _activeViewMode = ShopViewMode.Catalog;
            _activeFilterCategory = ShopProductCategory.Personnel;
            Render();
        }

        private void OnShopStateChanged()
        {
            EnsurePanelReady();
            Render();
        }

        private void EnsurePanelReady()
        {
            if (_panel != null)
            {
                return;
            }

            _panel = EnsurePanelCreated(_root);
            BindPanelElements();
            BindOrdersButton();
            BindFilterButtons();
            if (_panel != null)
            {
                _panel.style.display = DisplayStyle.None;
            }

            _closeButton?.RegisterCallback<ClickEvent>(OnCloseClicked);
        }

        private void BindPanelElements()
        {
            if (_panel == null)
            {
                return;
            }

            _closeButton = _panel.Q<Button>("shop-close-btn");
            _goldLabel = _panel.Q<Label>("shop-gold-label");
            _ordersTabButton = _panel.Q<Button>("shop-tab-orders");
            _filterRow = _panel.Q<VisualElement>("shop-filter-row");
            _tableHeader = _panel.Q<VisualElement>("shop-table-header");
            _rowsContainer = _panel.Q<VisualElement>("shop-rows");
            _filterAllButton = _panel.Q<Button>("shop-filter-all");
            _filterFoodButton = _panel.Q<Button>("shop-filter-food");
            _filterEquipmentButton = _panel.Q<Button>("shop-filter-equipment");
            _filterPersonnelButton = _panel.Q<Button>("shop-filter-personnel");
        }

        private void BindOrdersButton()
        {
            _ordersTabButton?.RegisterCallback<ClickEvent>(OnOrdersTabClicked);
        }

        private void BindFilterButtons()
        {
            _filterAllButton?.RegisterCallback<ClickEvent>(OnFilterAllClicked);
            _filterFoodButton?.RegisterCallback<ClickEvent>(OnFilterFoodClicked);
            _filterEquipmentButton?.RegisterCallback<ClickEvent>(OnFilterEquipmentClicked);
            _filterPersonnelButton?.RegisterCallback<ClickEvent>(OnFilterPersonnelClicked);
        }

        private void UnbindOrdersButton()
        {
            _ordersTabButton?.UnregisterCallback<ClickEvent>(OnOrdersTabClicked);
        }

        private void UnbindFilterButtons()
        {
            _filterAllButton?.UnregisterCallback<ClickEvent>(OnFilterAllClicked);
            _filterFoodButton?.UnregisterCallback<ClickEvent>(OnFilterFoodClicked);
            _filterEquipmentButton?.UnregisterCallback<ClickEvent>(OnFilterEquipmentClicked);
            _filterPersonnelButton?.UnregisterCallback<ClickEvent>(OnFilterPersonnelClicked);
        }

        private void RefreshGoldLabel()
        {
            if (_goldLabel == null)
            {
                return;
            }

            // Header gold mirrors runtime balance so price changes are visible while shopping.
            _goldLabelString.Arguments = new object[] { _shopSystemService != null ? _shopSystemService.Gold : 0 };
            _goldLabelString.RefreshString();
        }

        private void UpdateViewStates()
        {
            bool isCatalog = _activeViewMode == ShopViewMode.Catalog;
            if (_filterRow != null)
            {
                _filterRow.style.display = DisplayStyle.Flex;
            }

            if (_tableHeader != null)
            {
                _tableHeader.Clear();
                PopulateTableHeader(_tableHeader, isCatalog);
            }

            SetTabButtonState(_ordersTabButton, !isCatalog, true);
            SetFilterButtonState(_filterAllButton, !_activeFilterCategory.HasValue);
            SetFilterButtonState(_filterFoodButton, _activeFilterCategory == ShopProductCategory.Food);
            SetFilterButtonState(_filterEquipmentButton, _activeFilterCategory == ShopProductCategory.Equipment);
            SetFilterButtonState(_filterPersonnelButton, _activeFilterCategory == ShopProductCategory.Personnel);
            if (_ordersTabButton != null)
            {
                _ordersTabButton.SetBinding(
                    "text",
                    new LocalizedString("UI", isCatalog ? "shop.orders" : "shop.catalog"));
            }
        }

        private void ClosePanel()
        {
            if (_panel != null)
            {
                _panel.style.display = DisplayStyle.None;
            }

            _hudWindowCoordinator?.SetBlockingWindowOpen(WINDOW_ID, false);
        }

        private static void PopulateTableHeader(VisualElement header, bool isCatalog)
        {
            if (isCatalog)
            {
                header.Add(BuildHeaderLabel("shop.product", "shop-col-product"));
                header.Add(BuildHeaderLabel("shop.supplier", "shop-col-supplier"));
                header.Add(BuildHeaderLabel("shop.selected", "shop-col-selected"));
                header.Add(BuildHeaderLabel("shop.limit", "shop-col-max"));
                header.Add(BuildHeaderLabel("shop.unit.price", "shop-col-unit-price"));
                header.Add(BuildHeaderLabel("shop.total", "shop-col-total-price"));
                header.Add(BuildHeaderLabel("shop.action", "shop-col-actions-title"));
                return;
            }

            header.Add(BuildHeaderLabel("shop.product", "shop-col-product"));
            header.Add(BuildHeaderLabel("shop.amount", "shop-col-order-amount"));
            header.Add(BuildHeaderLabel("shop.price", "shop-col-order-price"));
            header.Add(BuildHeaderLabel("shop.days.left", "shop-col-order-days"));
            header.Add(BuildHeaderLabel("shop.action", "shop-col-actions-title"));
        }

        private static Label BuildHeaderLabel(string key, string className)
        {
            var label = new Label();
            label.SetBinding("text", new LocalizedString("UI", key));
            label.AddToClassList("shop-table-cell");
            label.AddToClassList(className);
            return label;
        }

        private static Label CreateLocalizedLabel(string key, string name)
        {
            var label = new Label { name = name };
            label.SetBinding("text", new LocalizedString("UI", key));
            return label;
        }

        private void OnGoldLabelChanged(string text)
        {
            if (_goldLabel != null)
            {
                _goldLabel.text = text;
            }
        }

        private static void SetFilterButtonState(Button button, bool isActive)
        {
            if (button == null)
            {
                return;
            }

            const string activeClass = "shop-filter-btn-active";
            button.EnableInClassList(activeClass, isActive);
        }

        private static void SetTabButtonState(Button button, bool isActive, bool isOrdersTab)
        {
            if (button == null)
            {
                return;
            }

            const string activeCatalogClass = "shop-tab-btn-active";
            const string activeOrdersClass = "shop-tab-btn-orders-active";
            button.EnableInClassList(activeCatalogClass, isActive && !isOrdersTab);
            button.EnableInClassList(activeOrdersClass, isActive && isOrdersTab);
        }

        private VisualElement EnsurePanelCreated(VisualElement root)
        {
            if (root == null)
            {
                return null;
            }

            VisualElement existingPanel = root.Q<VisualElement>("shop-panel");
            if (existingPanel != null)
            {
                return existingPanel;
            }

            if (_panelTemplate == null)
            {
                return null;
            }

            VisualElement panelTree = _panelTemplate.Instantiate();
            VisualElement instantiatedPanel = panelTree.Q<VisualElement>("shop-panel");
            if (instantiatedPanel == null)
            {
                return null;
            }

            instantiatedPanel.RemoveFromHierarchy();
            root.Add(instantiatedPanel);
            return instantiatedPanel;
        }
    }
}
