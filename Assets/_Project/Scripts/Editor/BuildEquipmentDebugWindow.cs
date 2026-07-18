using System;
using System.Collections.Generic;
using _Project.Scripts.Data.Construction;
using _Project.Scripts.Presentation.Buildings;
using UnityEditor;
using UnityEngine;

namespace _Project.Scripts.Editor
{
    public sealed class BuildEquipmentDebugWindow : EditorWindow
    {
        private sealed class EquipmentRow
        {
            public BuildingViewBase ViewPrefab;
            public BuildingDef BuildingDef;
        }

        private readonly List<EquipmentRow> _rows = new List<EquipmentRow>();
        private Vector2 _scrollPosition;
        private string _search = string.Empty;
        private bool _filterPowerRelated;
        private bool _filterWaterRelated;
        private bool _filterOxygenRelated;

        [MenuItem("Artemis/Build")]
        public static void Open()
        {
            BuildEquipmentDebugWindow window = GetWindow<BuildEquipmentDebugWindow>("Build Equipment");
            window.minSize = new Vector2(2200f, 540f);
            window.RefreshRows();
            window.Show();
        }

        private void OnFocus()
        {
            RefreshRows();
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
                string nextSearch = GUILayout.TextField(_search, EditorStyles.toolbarTextField, GUILayout.MinWidth(240f));
                if (!string.Equals(nextSearch, _search, StringComparison.Ordinal))
                {
                    _search = nextSearch;
                }

                _filterPowerRelated = GUILayout.Toggle(_filterPowerRelated, "Power", EditorStyles.toolbarButton, GUILayout.Width(70f));
                _filterWaterRelated = GUILayout.Toggle(_filterWaterRelated, "Water", EditorStyles.toolbarButton, GUILayout.Width(70f));
                _filterOxygenRelated = GUILayout.Toggle(_filterOxygenRelated, "Oxygen", EditorStyles.toolbarButton, GUILayout.Width(70f));
                if (GUILayout.Button("Clear Filters", EditorStyles.toolbarButton, GUILayout.Width(95f)))
                {
                    _filterPowerRelated = false;
                    _filterWaterRelated = false;
                    _filterOxygenRelated = false;
                }

                GUILayout.FlexibleSpace();
                GUILayout.Label("W/O2: water/oxygen simulation profiles", EditorStyles.miniLabel, GUILayout.Width(280f));
                GUILayout.Label($"Count: {_rows.Count}", EditorStyles.miniLabel, GUILayout.Width(90f));
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(90f)))
                {
                    RefreshRows();
                }
            }
        }

        private static void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("Open", GUILayout.Width(120f));
                GUILayout.Label("ObjectType", GUILayout.Width(120f));
                GUILayout.Label("UiName", GUILayout.Width(130f));
                GUILayout.Label("Prefab", GUILayout.Width(150f));
                GUILayout.Label("BuildingDef", GUILayout.Width(150f));
                GUILayout.Label("Size", GUILayout.Width(70f));
                GUILayout.Label("Walkable", GUILayout.Width(65f));
                GUILayout.Label("BuildTicks", GUILayout.Width(65f));
                GUILayout.Label("Support", GUILayout.Width(90f));
                GUILayout.Label("Net(P/W/O2)", GUILayout.Width(120f));
                GUILayout.Label("Power (kW)", GUILayout.Width(120f));
                GUILayout.Label("Water Profile", GUILayout.Width(320f));
                GUILayout.Label("Oxygen Profile", GUILayout.Width(430f));
                GUILayout.Label("Open", GUILayout.Width(120f));
            }
        }

        private void DrawRows()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            for (int i = 0; i < _rows.Count; i++)
            {
                EquipmentRow row = _rows[i];
                if (row == null || row.BuildingDef == null)
                {
                    continue;
                }

                if (!MatchesSearch(row))
                {
                    continue;
                }
                if (!MatchesResourceFilters(row))
                {
                    continue;
                }

                DrawRow(row);
            }

            EditorGUILayout.EndScrollView();
        }

        private static void DrawRow(EquipmentRow row)
        {
            BuildingDef buildingDef = row.BuildingDef;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.textArea))
            {
                using (new EditorGUILayout.HorizontalScope(GUILayout.Width(120f)))
                {
                    if (GUILayout.Button("Prefab", GUILayout.Width(55f)))
                    {
                        if (row.ViewPrefab != null)
                        {
                            Selection.activeObject = row.ViewPrefab;
                            EditorGUIUtility.PingObject(row.ViewPrefab);
                        }
                    }

                    if (GUILayout.Button("Def", GUILayout.Width(45f)))
                    {
                        Selection.activeObject = buildingDef;
                        EditorGUIUtility.PingObject(buildingDef);
                    }
                }

                GUILayout.Label(buildingDef.ObjectType.ToString(), GUILayout.Width(120f));
                GUILayout.Label(string.IsNullOrWhiteSpace(buildingDef.UiName) ? "<empty>" : buildingDef.UiName, GUILayout.Width(130f));
                GUILayout.Label(row.ViewPrefab != null ? row.ViewPrefab.name : "<missing>", GUILayout.Width(150f));
                GUILayout.Label(buildingDef.name, GUILayout.Width(150f));
                GUILayout.Label($"{Mathf.Max(1, buildingDef.Width)}x{Mathf.Max(1, buildingDef.Height)}", GUILayout.Width(70f));
                GUILayout.Label(buildingDef.IsWalkableAfterBuild ? "Yes" : "No", GUILayout.Width(65f));
                GUILayout.Label(buildingDef.BuildTicks.ToString(), GUILayout.Width(65f));
                GUILayout.Label(buildingDef.SupportRequirement.ToString(), GUILayout.Width(90f));
                GUILayout.Label(
                    $"{(buildingDef.UsesPowerNetwork ? "P" : "-")}/{(buildingDef.UsesWaterNetwork ? "W" : "-")}/{(buildingDef.UsesOxygenNetwork ? "O2" : "-")}",
                    GUILayout.Width(120f));
                GUILayout.Label(FormatPowerProfile(buildingDef), GUILayout.Width(120f));
                GUILayout.Label(FormatWaterProfile(buildingDef), GUILayout.Width(320f));
                GUILayout.Label(FormatOxygenProfile(buildingDef), GUILayout.Width(430f));

                using (new EditorGUILayout.HorizontalScope(GUILayout.Width(120f)))
                {
                    if (GUILayout.Button("Prefab", GUILayout.Width(55f)))
                    {
                        if (row.ViewPrefab != null)
                        {
                            Selection.activeObject = row.ViewPrefab;
                            EditorGUIUtility.PingObject(row.ViewPrefab);
                        }
                    }

                    // Отдельный переход к BuildingDef как в окне Products.
                    if (GUILayout.Button("Def", GUILayout.Width(45f)))
                    {
                        Selection.activeObject = buildingDef;
                        EditorGUIUtility.PingObject(buildingDef);
                    }
                }
            }
        }

        private bool MatchesSearch(EquipmentRow row)
        {
            if (string.IsNullOrWhiteSpace(_search))
            {
                return true;
            }

            string needle = _search.Trim().ToLowerInvariant();
            BuildingDef buildingDef = row.BuildingDef;
            if (ContainsIgnoreCase(buildingDef.name, needle)
                || ContainsIgnoreCase(buildingDef.ObjectType.ToString(), needle)
                || ContainsIgnoreCase(buildingDef.UiName, needle)
                || ContainsIgnoreCase(buildingDef.UiDescription, needle)
                || ContainsIgnoreCase(row.ViewPrefab != null ? row.ViewPrefab.name : string.Empty, needle)
                )
            {
                return true;
            }

            return false;
        }

        private bool MatchesResourceFilters(EquipmentRow row)
        {
            BuildingDef buildingDef = row?.BuildingDef;
            if (buildingDef == null)
            {
                return false;
            }

            if (_filterPowerRelated && !IsPowerRelated(buildingDef))
            {
                return false;
            }

            if (_filterWaterRelated && !IsWaterRelated(buildingDef))
            {
                return false;
            }

            if (_filterOxygenRelated && !IsOxygenRelated(buildingDef))
            {
                return false;
            }

            return true;
        }

        private static bool ContainsIgnoreCase(string source, string needle)
        {
            return !string.IsNullOrWhiteSpace(source) && source.ToLowerInvariant().Contains(needle);
        }

        private static bool IsPowerRelated(BuildingDef buildingDef)
        {
            if (buildingDef == null)
            {
                return false;
            }

            return buildingDef.UsesPowerNetwork
                   || buildingDef.RequiresPower
                   || buildingDef.PowerConsumptionKw > 0f
                   || buildingDef.PowerGenerationKwDay > 0f
                   || buildingDef.BatteryCapacityKwh > 0f;
        }

        private static bool IsWaterRelated(BuildingDef buildingDef)
        {
            if (buildingDef == null)
            {
                return false;
            }

            return buildingDef.UsesWaterNetwork
                   || buildingDef.WaterRole != WaterRole.None
                   || buildingDef.WaterProductionLitersPerHour > 0f
                   || buildingDef.WaterConsumerCapacityLiters > 0f
                   || buildingDef.WaterStorageCapacityLiters > 0f;
        }

        private static bool IsOxygenRelated(BuildingDef buildingDef)
        {
            if (buildingDef == null)
            {
                return false;
            }

            return buildingDef.UsesOxygenNetwork
                   || buildingDef.OxygenRole != OxygenRole.None
                   || buildingDef.OxygenProductionLitersPerHour > 0f
                   || buildingDef.OxygenConsumerCapacityLiters > 0f
                   || buildingDef.OxygenStorageCapacityLiters > 0f
                   || buildingDef.OxygenWaterConsumptionLitersPerHour > 0f;
        }

        /// <summary>
        /// Returns a compact power profile string for the Build window table.
        /// </summary>
        private static string FormatPowerProfile(BuildingDef buildingDef)
        {
            if (buildingDef == null)
            {
                return "-";
            }

            if (!buildingDef.UsesPowerNetwork)
            {
                return "off-grid";
            }

            if (buildingDef.PowerGenerationKwDay > 0f)
            {
                return $"gen:{buildingDef.PowerGenerationKwDay:0.##}";
            }

            if (buildingDef.PowerConsumptionKw > 0f)
            {
                return $"cons:{buildingDef.PowerConsumptionKw:0.##}";
            }

            return "passive";
        }

        /// <summary>
        /// Returns water simulation details in one line to simplify build balancing.
        /// </summary>
        private static string FormatWaterProfile(BuildingDef buildingDef)
        {
            if (buildingDef == null || !buildingDef.UsesWaterNetwork || buildingDef.WaterRole == WaterRole.None)
            {
                return "-";
            }

            switch (buildingDef.WaterRole)
            {
                case WaterRole.Producer:
                    return $"role:P prod:{buildingDef.WaterProductionLitersPerHour:0.##}L/h cyc:{buildingDef.WaterProductionLitersPerCycle:0.##}/{buildingDef.WaterProductionRogalitePerCycle}";
                case WaterRole.Consumer:
                    return $"role:C fill:{buildingDef.WaterConsumerFillRateLitersPerHour:0.##}L/h cap:{buildingDef.WaterConsumerCapacityLiters:0.##}L prio:{buildingDef.WaterConsumerPriority}";
                case WaterRole.Storage:
                    return $"role:S cap:{buildingDef.WaterStorageCapacityLiters:0.##}L in:{buildingDef.WaterStorageFillRateLitersPerHour:0.##} out:{buildingDef.WaterStorageDischargeRateLitersPerHour:0.##}";
                default:
                    return "-";
            }
        }

        /// <summary>
        /// Returns oxygen simulation details including production and water consumption.
        /// </summary>
        private static string FormatOxygenProfile(BuildingDef buildingDef)
        {
            if (buildingDef == null || !buildingDef.UsesOxygenNetwork || buildingDef.OxygenRole == OxygenRole.None)
            {
                return "-";
            }

            switch (buildingDef.OxygenRole)
            {
                case OxygenRole.Producer:
                    return $"role:P prod:{buildingDef.OxygenProductionLitersPerHour:0.##}L/h cyc:{buildingDef.OxygenProductionLitersPerCycle:0.##}/{buildingDef.OxygenProductionRegolithPerCycle} H2O:{buildingDef.OxygenWaterConsumptionLitersPerHour:0.##}L/h";
                case OxygenRole.Consumer:
                    return $"role:C fill:{buildingDef.OxygenConsumerFillRateLitersPerHour:0.##}L/h cap:{buildingDef.OxygenConsumerCapacityLiters:0.##}L prio:{buildingDef.OxygenConsumerPriority}";
                case OxygenRole.Storage:
                    return $"role:S cap:{buildingDef.OxygenStorageCapacityLiters:0.##}L in:{buildingDef.OxygenStorageFillRateLitersPerHour:0.##} out:{buildingDef.OxygenStorageDischargeRateLitersPerHour:0.##}";
                default:
                    return "-";
            }
        }

        private void RefreshRows()
        {
            _rows.Clear();

            // В список попадает только оборудование, где на prefab есть BuildingViewBase.
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                BuildingViewBase viewPrefab = AssetDatabase.LoadAssetAtPath<BuildingViewBase>(prefabPath);
                if (viewPrefab == null || viewPrefab.BuildingDef == null)
                {
                    continue;
                }

                BuildingDef buildingDef = viewPrefab.BuildingDef;
                EquipmentRow row = new EquipmentRow
                {
                    ViewPrefab = viewPrefab,
                    BuildingDef = buildingDef
                };

                _rows.Add(row);
            }

            _rows.Sort((a, b) => string.CompareOrdinal(a.BuildingDef.name, b.BuildingDef.name));
            Repaint();
        }

    }
}
