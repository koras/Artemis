using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using _Project.Scripts.Data.Construction;
using _Project.Scripts.Data.Offers;
using _Project.Scripts.Presentation.UI;
using UnityEditor;
using UnityEngine;

namespace _Project.Scripts.Editor
{
    /// <summary>
    /// Editor table for HUD buttons and their unlock conditions.
    /// </summary>
    public sealed class HudMenuEditorWindow : EditorWindow
    {
        private static readonly Regex ButtonElementRegex = new Regex(
            @"<ui:Button\b(?<attributes>[^>]*)/>",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex AttributeRegex = new Regex(
            @"(?<name>[A-Za-z0-9_-]+)\s*=\s*""(?<value>[^""]*)""",
            RegexOptions.Compiled);

        private readonly List<HudButtonRow> _buttonRows = new List<HudButtonRow>();
        private Vector2 _scrollPosition;
        private HudMenuIconSet _menuIconSet;
        private SerializedObject _serializedMenuIconSet;

        [MenuItem("Artemis/Menu")]
        public static void Open()
        {
            HudMenuEditorWindow window = GetWindow<HudMenuEditorWindow>("Artemis Menu");
            window.minSize = new Vector2(980f, 420f);
            window.Show();
        }

        private void OnEnable()
        {
            LoadDefaultMenuIconSet();
            RefreshButtonRows();
        }

        private void OnGUI()
        {
            DrawToolbar();

            if (_menuIconSet == null)
            {
                EditorGUILayout.HelpBox(
                    "HudMenuIconSet не найден. Таблица всё равно показывает кнопки из UXML, но условия разблокировки редактировать нельзя.",
                    MessageType.Warning);
            }

            DrawButtonTable();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUILayout.LabelField("Artemis / Menu", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();

                HudMenuIconSet selected = (HudMenuIconSet)EditorGUILayout.ObjectField(
                    _menuIconSet,
                    typeof(HudMenuIconSet),
                    false,
                    GUILayout.Width(220f));
                if (selected != _menuIconSet)
                {
                    SetMenuIconSet(selected);
                }

                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70f)))
                {
                    RefreshButtonRows();
                }
            }
        }

        private void DrawButtonTable()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("HUD buttons", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Название", EditorStyles.boldLabel, GUILayout.Width(180f));
                EditorGUILayout.LabelField("Что отвечает", EditorStyles.boldLabel, GUILayout.Width(260f));
                EditorGUILayout.LabelField("Событие появления", EditorStyles.boldLabel, GUILayout.Width(170f));
                EditorGUILayout.LabelField("Объект события", EditorStyles.boldLabel, GUILayout.Width(180f));
                EditorGUILayout.LabelField("LifeModule", EditorStyles.boldLabel, GUILayout.Width(90f));
            }

            _serializedMenuIconSet?.Update();
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            for (int i = 0; i < _buttonRows.Count; i++)
            {
                DrawButtonRow(_buttonRows[i], i % 2 == 0);
            }

            EditorGUILayout.EndScrollView();
            _serializedMenuIconSet?.ApplyModifiedProperties();
        }

        private void DrawButtonRow(HudButtonRow row, bool evenRow)
        {
            HudMenuButtonDefinition definition = FindDefinition(row.ButtonId);
            string description = definition != null && !string.IsNullOrWhiteSpace(definition.Description)
                ? definition.Description
                : "Описание не задано";

            Color previousColor = GUI.backgroundColor;
            GUI.backgroundColor = evenRow ? new Color(0.22f, 0.24f, 0.28f) : new Color(0.17f, 0.19f, 0.23f);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUI.backgroundColor = previousColor;
                EditorGUILayout.LabelField($"{row.Text}  ({row.ButtonId})", GUILayout.Width(180f));

                if (definition == null)
                {
                    EditorGUILayout.LabelField(description, GUILayout.Width(260f));
                    EditorGUILayout.LabelField("Не настроено", GUILayout.Width(170f));
                    EditorGUILayout.LabelField("—", GUILayout.MinWidth(180f));
                    if (GUILayout.Button("Добавить", GUILayout.Width(75f)))
                    {
                        AddDefinition(row.ButtonId);
                    }
                    return;
                }

                SerializedProperty definitionProperty = FindSerializedDefinition(row.ButtonId);
                SerializedProperty descriptionProperty = definitionProperty.FindPropertyRelative("Description");
                SerializedProperty unlockTypeProperty = definitionProperty.FindPropertyRelative("UnlockType");
                SerializedProperty buildingProperty = definitionProperty.FindPropertyRelative("RequiredBuildingDef");
                SerializedProperty offerProperty = definitionProperty.FindPropertyRelative("RequiredOfferDefinition");
                SerializedProperty lifeModuleProperty = definitionProperty.FindPropertyRelative("RequiresLifeModuleBuilt");

                descriptionProperty.stringValue = EditorGUILayout.TextField(
                    GUIContent.none,
                    descriptionProperty.stringValue,
                    GUILayout.Width(260f));
                EditorGUILayout.PropertyField(unlockTypeProperty, GUIContent.none, GUILayout.Width(170f));

                if (unlockTypeProperty.enumValueIndex == (int)HudMenuButtonUnlockType.BuildingViewCreated)
                {
                    EditorGUILayout.PropertyField(buildingProperty, GUIContent.none, GUILayout.MinWidth(180f));
                    offerProperty.objectReferenceValue = null;
                }
                else if (unlockTypeProperty.enumValueIndex == (int)HudMenuButtonUnlockType.OfferCompleted)
                {
                    EditorGUILayout.PropertyField(offerProperty, GUIContent.none, GUILayout.MinWidth(180f));
                    buildingProperty.objectReferenceValue = null;
                }
                else
                {
                    EditorGUILayout.LabelField("Всегда доступна", GUILayout.MinWidth(180f));
                    buildingProperty.objectReferenceValue = null;
                    offerProperty.objectReferenceValue = null;
                }

                lifeModuleProperty.boolValue = EditorGUILayout.Toggle(
                    GUIContent.none,
                    lifeModuleProperty.boolValue,
                    GUILayout.Width(90f));
            }
        }

        private SerializedProperty FindSerializedDefinition(string buttonId)
        {
            SerializedProperty definitionsProperty = _serializedMenuIconSet.FindProperty("_menuButtonDefinitions");
            for (int i = 0; i < definitionsProperty.arraySize; i++)
            {
                SerializedProperty definitionProperty = definitionsProperty.GetArrayElementAtIndex(i);
                if (string.Equals(
                    definitionProperty.FindPropertyRelative("ButtonId").stringValue,
                    buttonId,
                    StringComparison.Ordinal))
                {
                    return definitionProperty;
                }
            }

            return null;
        }

        private void AddDefinition(string buttonId)
        {
            if (_serializedMenuIconSet == null)
            {
                return;
            }

            _serializedMenuIconSet.Update();
            SerializedProperty definitionsProperty = _serializedMenuIconSet.FindProperty("_menuButtonDefinitions");
            definitionsProperty.InsertArrayElementAtIndex(definitionsProperty.arraySize);
            SerializedProperty definitionProperty = definitionsProperty.GetArrayElementAtIndex(definitionsProperty.arraySize - 1);
            definitionProperty.FindPropertyRelative("ButtonId").stringValue = buttonId;
            definitionProperty.FindPropertyRelative("Description").stringValue = string.Empty;
            definitionProperty.FindPropertyRelative("UnlockType").enumValueIndex = (int)HudMenuButtonUnlockType.AlwaysVisible;
            definitionProperty.FindPropertyRelative("RequiredBuildingDef").objectReferenceValue = null;
            definitionProperty.FindPropertyRelative("RequiredOfferDefinition").objectReferenceValue = null;
            definitionProperty.FindPropertyRelative("RequiresLifeModuleBuilt").boolValue = true;
            _serializedMenuIconSet.ApplyModifiedProperties();
        }

        private void RefreshButtonRows()
        {
            _buttonRows.Clear();
            HashSet<string> knownButtonIds = new HashSet<string>(StringComparer.Ordinal);
            string[] assetGuids = AssetDatabase.FindAssets(
                "t:VisualTreeAsset",
                new[] { "Assets/_Project/UI", "Assets/_Project/Resources/UI" });

            for (int i = 0; i < assetGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(assetGuids[i]);
                if (!assetPath.EndsWith(".uxml", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string contents = File.ReadAllText(assetPath);
                MatchCollection matches = ButtonElementRegex.Matches(contents);
                for (int matchIndex = 0; matchIndex < matches.Count; matchIndex++)
                {
                    Match match = matches[matchIndex];
                    string attributes = match.Groups["attributes"].Value;
                    string buttonId = GetAttribute(attributes, "name");
                    if (string.IsNullOrWhiteSpace(buttonId) || !knownButtonIds.Add(buttonId))
                    {
                        continue;
                    }

                    _buttonRows.Add(new HudButtonRow(
                        buttonId,
                        GetAttribute(attributes, "text"),
                        assetPath));
                }
            }

            _buttonRows.Sort((left, right) => string.Compare(left.ButtonId, right.ButtonId, StringComparison.Ordinal));
            Repaint();
        }

        private void LoadDefaultMenuIconSet()
        {
            string[] assetGuids = AssetDatabase.FindAssets("t:HudMenuIconSet");
            if (assetGuids.Length == 0)
            {
                return;
            }

            SetMenuIconSet(AssetDatabase.LoadAssetAtPath<HudMenuIconSet>(
                AssetDatabase.GUIDToAssetPath(assetGuids[0])));
        }

        private void SetMenuIconSet(HudMenuIconSet menuIconSet)
        {
            _menuIconSet = menuIconSet;
            _serializedMenuIconSet = _menuIconSet != null ? new SerializedObject(_menuIconSet) : null;
            Repaint();
        }

        private HudMenuButtonDefinition FindDefinition(string buttonId)
        {
            HudMenuButtonDefinition[] definitions = _menuIconSet != null
                ? _menuIconSet.MenuButtonDefinitions
                : null;
            if (definitions == null)
            {
                return null;
            }

            for (int i = 0; i < definitions.Length; i++)
            {
                HudMenuButtonDefinition definition = definitions[i];
                if (definition != null && string.Equals(definition.ButtonId, buttonId, StringComparison.Ordinal))
                {
                    return definition;
                }
            }

            return null;
        }

        private static string GetUnlockDescription(HudMenuButtonDefinition definition)
        {
            if (definition != null && definition.UnlockType == HudMenuButtonUnlockType.AlwaysHidden)
            {
                return "Всегда скрыта";
            }

            if (definition == null || definition.UnlockType == HudMenuButtonUnlockType.AlwaysVisible)
            {
                return "Сразу";
            }

            if (definition.UnlockType == HudMenuButtonUnlockType.BuildingViewCreated)
            {
                return definition.RequiredBuildingDef != null
                    ? $"После строительства: {definition.RequiredBuildingDef.name}"
                    : "После BuildingView: НЕ НАСТРОЕНО";
            }

            return definition.RequiredOfferDefinition != null
                ? $"После оффера: {definition.RequiredOfferDefinition.name}"
                : "После OfferDefinition: НЕ НАСТРОЕНО";
        }

        private static string GetAttribute(string attributes, string attributeName)
        {
            MatchCollection matches = AttributeRegex.Matches(attributes);
            for (int i = 0; i < matches.Count; i++)
            {
                Match match = matches[i];
                if (string.Equals(match.Groups["name"].Value, attributeName, StringComparison.OrdinalIgnoreCase))
                {
                    return match.Groups["value"].Value;
                }
            }

            return string.Empty;
        }

        private readonly struct HudButtonRow
        {
            public HudButtonRow(string buttonId, string text, string assetPath)
            {
                ButtonId = buttonId;
                Text = string.IsNullOrWhiteSpace(text) ? buttonId : text;
                AssetPath = assetPath;
            }

            public string ButtonId { get; }
            public string Text { get; }
            public string AssetPath { get; }
        }
    }
}
