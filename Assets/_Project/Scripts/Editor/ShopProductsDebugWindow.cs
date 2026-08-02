using System.Collections.Generic;
using _Project.Scripts.Data.Shop;
using UnityEditor;
using UnityEngine;

namespace _Project.Scripts.Editor
{
    public sealed class ShopProductsDebugWindow : EditorWindow
    {
        private Vector2 _scrollPosition;
        private string _search = string.Empty;
        private readonly List<ShopOfferDefinition> _definitions = new List<ShopOfferDefinition>();

        [MenuItem("Artemis/Shops/Definitions")]
        public static void Open()
        {
            ShopProductsDebugWindow window = GetWindow<ShopProductsDebugWindow>("Shop Definitions");
            window.minSize = new Vector2(1440f, 460f);
            window.RefreshDefinitions();
            window.Show();
        }

        private void OnFocus()
        {
            RefreshDefinitions();
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawHeader();
            DrawRows();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Search", GUILayout.Width(45f));

                string nextSearch =
                    GUILayout.TextField(_search, EditorStyles.toolbarTextField, GUILayout.MinWidth(240f));

                if (!string.Equals(nextSearch, _search))
                {
                    _search = nextSearch;
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(90f)))
                {
                    RefreshDefinitions();
                }
            }
        }

        private static void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("DefinitionId", GUILayout.Width(170f));
                GUILayout.Label("Product", GUILayout.Width(170f));
                GUILayout.Label("Supplier", GUILayout.Width(160f));
                GUILayout.Label("BaseUnitPrice", GUILayout.Width(95f));
                GUILayout.Label("MaxPurchase", GUILayout.Width(95f));
                GUILayout.Label("Priority", GUILayout.Width(70f));
                GUILayout.Label("Period", GUILayout.Width(190f));
                GUILayout.Label("Reputation", GUILayout.Width(150f));
                GUILayout.Label("Resource", GUILayout.Width(170f));
                GUILayout.Label("Asset", GUILayout.MinWidth(90f));
            }
        }

        private void DrawRows()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            for (int i = 0; i < _definitions.Count; i++)
            {
                ShopOfferDefinition definition = _definitions[i];

                if (definition == null || !MatchesSearch(definition))
                {
                    continue;
                }

                string supplierName = definition.Supplier != null ? definition.Supplier.LocalizationId : "<missing>";

                using (new EditorGUILayout.HorizontalScope(EditorStyles.textArea))
                {
                    GUILayout.Label(definition.DefinitionId, GUILayout.Width(170f));
                    var localizedString = definition.Product.GetLocalizedName();
                    var productName = localizedString.GetLocalizedString();

                    GUILayout.Label(definition.Product != null ? productName : "<missing>", GUILayout.Width(170f));
                    GUILayout.Label(supplierName, GUILayout.Width(160f));
                    GUILayout.Label(definition.BaseUnitPrice.ToString(), GUILayout.Width(95f));
                    GUILayout.Label(definition.MaxPurchaseAmount.ToString(), GUILayout.Width(95f));
                    GUILayout.Label(definition.Priority.ToString(), GUILayout.Width(70f));
                    GUILayout.Label(BuildPeriodText(definition), GUILayout.Width(190f));
                    GUILayout.Label(BuildReputationText(definition), GUILayout.Width(150f));
                    GUILayout.Label(BuildResourceText(definition), GUILayout.Width(170f));

                    if (GUILayout.Button("Select", GUILayout.Width(70f)))
                    {
                        Selection.activeObject = definition;
                        EditorGUIUtility.PingObject(definition);
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private static string BuildPeriodText(ShopOfferDefinition definition)
        {
            ShopPeriodicWindowCondition periodic =
                definition.Conditions != null ? definition.Conditions.PeriodicWindow : null;

            if (periodic == null || !periodic.Enabled)
            {
                return "always";
            }

            return $"every {periodic.IntervalDays}d / live {periodic.LifetimeDays}d";
        }

        private static string BuildReputationText(ShopOfferDefinition definition)
        {
            ShopSupplierReputationCondition condition =
                definition.Conditions != null ? definition.Conditions.SupplierReputation : null;

            if (condition == null || !condition.Enabled)
            {
                return "-";
            }

            return $"{condition.Comparison} {condition.Threshold}";
        }

        private static string BuildResourceText(ShopOfferDefinition definition)
        {
            ShopResourceAmountCondition condition =
                definition.Conditions != null ? definition.Conditions.ResourceAmount : null;

            if (condition == null || !condition.Enabled)
            {
                return "-";
            }

            return $"{condition.ResourceId} {condition.Comparison} {condition.Threshold}";
        }

        private bool MatchesSearch(ShopOfferDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(_search))
            {
                return true;
            }

            string needle = _search.Trim().ToLowerInvariant();

            if (!string.IsNullOrWhiteSpace(definition.DefinitionId) &&
                definition.DefinitionId.ToLowerInvariant().Contains(needle))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(definition.OfferId) &&
                definition.OfferId.ToLowerInvariant().Contains(needle))
            {
                return true;
            }

            if (definition.Product != null
                && !string.IsNullOrWhiteSpace(definition.Product.GetLocalizedName().GetLocalizedString())
                && definition.Product.GetLocalizedName().GetLocalizedString().ToLowerInvariant().Contains(needle))
            {
                return true;
            }

            if (definition.Supplier != null
                && !string.IsNullOrWhiteSpace(definition.Supplier.LocalizationId)
                && definition.Supplier.LocalizationId.ToLowerInvariant().Contains(needle))
            {
                return true;
            }

            return false;
        }

        private void RefreshDefinitions()
        {
            _definitions.Clear();
            string[] guids = AssetDatabase.FindAssets("t:ShopOfferDefinition");

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                ShopOfferDefinition definition = AssetDatabase.LoadAssetAtPath<ShopOfferDefinition>(path);

                if (definition != null)
                {
                    _definitions.Add(definition);
                }
            }

            _definitions.Sort((a, b) => string.CompareOrdinal(a.DefinitionId, b.DefinitionId));
            Repaint();
        }
    }
}