using System.Collections.Generic;
using _Project.Scripts.Data.Offers;
using UnityEditor;
using UnityEngine;

namespace _Project.Scripts.Editor
{
    public sealed class OfferCatalogDebugWindow : EditorWindow
    {
        private Vector2 _scrollPosition;
        private string _search = string.Empty;
        private readonly List<OfferDefinition> _offers = new List<OfferDefinition>();

        [MenuItem("Artemis/Offers/Offer Catalog Debug")]
        public static void Open()
        {
            OfferCatalogDebugWindow window = GetWindow<OfferCatalogDebugWindow>("Offer Catalog");
            window.minSize = new Vector2(900f, 420f);
            window.RefreshOffers();
            window.Show();
        }

        private void OnFocus()
        {
            RefreshOffers();
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
                string nextSearch = GUILayout.TextField(_search, EditorStyles.toolbarTextField, GUILayout.MinWidth(220f));
                if (!string.Equals(nextSearch, _search))
                {
                    _search = nextSearch;
                }

                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(90f)))
                {
                    RefreshOffers();
                }
            }
        }

        private static void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("OfferId", GUILayout.Width(160f));
                GUILayout.Label("Title", GUILayout.Width(180f));
                GUILayout.Label("Customer", GUILayout.Width(160f));
                GUILayout.Label("Triggers", GUILayout.Width(140f));
                GUILayout.Label("Repeat", GUILayout.Width(80f));
                GUILayout.Label("Cooldown", GUILayout.Width(80f));
                GUILayout.Label("MinTime", GUILayout.Width(80f));
                GUILayout.Label("Chance", GUILayout.Width(60f));
                GUILayout.Label("Asset", GUILayout.MinWidth(120f));
            }
        }

        private void DrawRows()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            for (int i = 0; i < _offers.Count; i++)
            {
                OfferDefinition offer = _offers[i];
                if (offer == null)
                {
                    continue;
                }

                if (!MatchesSearch(offer))
                {
                    continue;
                }

                using (new EditorGUILayout.HorizontalScope(EditorStyles.textArea))
                {
                    GUILayout.Label(offer.OfferId, GUILayout.Width(160f));
                    GUILayout.Label(offer.Title, GUILayout.Width(180f));
                    GUILayout.Label(offer.Customer != null ? offer.Customer.FullName : "<missing>", GUILayout.Width(160f));
                    GUILayout.Label(offer.TriggerTypes.ToString(), GUILayout.Width(140f));
                    GUILayout.Label(offer.IsRepeatable ? "Yes" : "No", GUILayout.Width(80f));
                    GUILayout.Label(offer.CooldownGameMinutes.ToString(), GUILayout.Width(80f));
                    GUILayout.Label(offer.MinGameMinutesToAppear.ToString(), GUILayout.Width(80f));
                    GUILayout.Label(offer.HourlySpawnChance.ToString("0.00"), GUILayout.Width(60f));

                    if (GUILayout.Button("Select", GUILayout.Width(70f)))
                    {
                        Selection.activeObject = offer;
                        EditorGUIUtility.PingObject(offer);
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private bool MatchesSearch(OfferDefinition offer)
        {
            if (string.IsNullOrWhiteSpace(_search))
            {
                return true;
            }

            string needle = _search.Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(offer.OfferId) && offer.OfferId.ToLowerInvariant().Contains(needle))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(offer.Title) && offer.Title.ToLowerInvariant().Contains(needle))
            {
                return true;
            }

            if (offer.Customer != null && !string.IsNullOrWhiteSpace(offer.Customer.FullName) && offer.Customer.FullName.ToLowerInvariant().Contains(needle))
            {
                return true;
            }

            return false;
        }

        private void RefreshOffers()
        {
            _offers.Clear();
            string[] guids = AssetDatabase.FindAssets("t:OfferDefinition");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                OfferDefinition offer = AssetDatabase.LoadAssetAtPath<OfferDefinition>(path);
                if (offer != null)
                {
                    _offers.Add(offer);
                }
            }

            _offers.Sort((a, b) => string.CompareOrdinal(a.OfferId, b.OfferId));
            Repaint();
        }
    }
}
