using System.Collections.Generic;
using _Project.Scripts.Data.Construction;
using _Project.Scripts.Data.Offers;
using _Project.Scripts.Data.Shop;
using _Project.Scripts.Presentation.UI;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace _Project.Scripts.Editor
{
    /// <summary>
    /// Импортирует CSV-источник и синхронизирует все известные ключи со всеми локалями.
    /// </summary>
    public static class LocalizationMenuSetup
    {
        private const string SettingsPath = "Assets/_Project/Localization/LocalizationSettings.asset";
        private const string LocaleDirectory = "Assets/_Project/Localization/Locales";
        private const string TableDirectory = "Assets/_Project/Localization/Tables";
        private const string TableName = "UI";

        [MenuItem("Artemis/Localization/Setup Menu Localization")]
        public static void Setup()
        {
            EnsureFolder("Assets/_Project/Localization");
            EnsureFolder(LocaleDirectory);
            EnsureFolder(TableDirectory);

            LocalizationSettings settings = GetOrCreateSettings();
            Locale russianLocale = GetOrCreateLocale("ru", "Russian");
            Locale englishLocale = GetOrCreateLocale("en", "English");

            LocalizationEditorSettings.ActiveLocalizationSettings = settings;
            LocalizationSettings.Instance = settings;
            SetRussianAsDefault(russianLocale);

            StringTableCollection collection = LocalizationEditorSettings.GetStringTableCollection(TableName);

            if (collection == null)
            {
                collection = LocalizationEditorSettings.CreateStringTableCollection(
                    TableName,
                    TableDirectory,
                    new List<Locale> { russianLocale, englishLocale });
            }

            // Перечитываем таблицы после внешних правок, иначе Unity может сравнивать
            // устаревшее значение из памяти с актуальным YAML-файлом на диске.
            AssetDatabase.ImportAsset($"{TableDirectory}/UI_ru.asset", ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset($"{TableDirectory}/UI_en.asset", ImportAssetOptions.ForceUpdate);
            AddHudMenuEntries(collection);
            AddShopProductEntries(collection);
            AddCustomerEntries(collection);
            AddBuildingEntries(collection);
            AddOfferEntries(collection);
            LocalizationSourceMenu.ImportSourceInto(collection);
            EnsureAllKnownEntriesInAllTables(collection);

            EditorUtility.SetDirty(settings);
            EditorUtility.SetDirty(collection);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            LocalizationSourceMenu.NormalizeTableEntries(collection);
            Debug.Log("[Localization] Russian and English menu localization configured.");
        }

        private static LocalizationSettings GetOrCreateSettings()
        {
            LocalizationSettings settings = AssetDatabase.LoadAssetAtPath<LocalizationSettings>(SettingsPath);

            if (settings != null)
            {
                return settings;
            }

            settings = ScriptableObject.CreateInstance<LocalizationSettings>();
            settings.name = "LocalizationSettings";
            AssetDatabase.CreateAsset(settings, SettingsPath);
            return settings;
        }

        private static Locale GetOrCreateLocale(string code, string displayName)
        {
            Locale locale = LocalizationEditorSettings.GetLocale(code);

            if (locale == null)
            {
                locale = Locale.CreateLocale(code);
                locale.name = displayName;
                AssetDatabase.CreateAsset(locale, $"{LocaleDirectory}/{displayName}.asset");
                LocalizationEditorSettings.AddLocale(locale);
            }

            return locale;
        }

        private static void SetRussianAsDefault(Locale russianLocale)
        {
            LocalizationSettings.ProjectLocale = russianLocale;

            // Явно выбираем русский при старте, не полагаясь на язык ОС игрока.
            LocalizationSettings.StartupLocaleSelectors.Clear();

            LocalizationSettings.StartupLocaleSelectors.Add(new SpecificLocaleSelector
            {
                LocaleId = russianLocale.Identifier
            });
        }

        private static void AddCustomerEntries(StringTableCollection collection)
        {
            string[] guids = AssetDatabase.FindAssets("t:OfferCustomerDefinition");

            for (int assetIndex = 0; assetIndex < guids.Length; assetIndex++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[assetIndex]);
                OfferCustomerDefinition customer = AssetDatabase.LoadAssetAtPath<OfferCustomerDefinition>(assetPath);

                if (customer == null)
                {
                    continue;
                }

                EnsureEntryInAllTables(collection, customer.FullNameLocalizationKey);
                EnsureEntryInAllTables(collection, customer.CompanyNameLocalizationKey);
                EnsureEntryInAllTables(collection, customer.CompanyDescriptionLocalizationKey);
            }
        }

        private static void AddBuildingEntries(StringTableCollection collection)
        {
            string[] guids = AssetDatabase.FindAssets("t:BuildingDef");

            for (int assetIndex = 0; assetIndex < guids.Length; assetIndex++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[assetIndex]);
                BuildingDef building = AssetDatabase.LoadAssetAtPath<BuildingDef>(assetPath);

                if (building == null)
                {
                    continue;
                }

                EnsureEntryInAllTables(collection, building.NameLocalizationKey);
                EnsureEntryInAllTables(collection, building.DescriptionLocalizationKey);
            }
        }

        private static void AddShopProductEntries(StringTableCollection collection)
        {
            string[] guids = AssetDatabase.FindAssets("t:ShopProductDefinition");

            for (int assetIndex = 0; assetIndex < guids.Length; assetIndex++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[assetIndex]);
                ShopProductDefinition product = AssetDatabase.LoadAssetAtPath<ShopProductDefinition>(assetPath);

                if (product == null || string.IsNullOrWhiteSpace(product.ProductId))
                {
                    continue;
                }

                EnsureEntryInAllTables(collection, product.NameLocalizationKey);
                EnsureEntryInAllTables(collection, product.DescriptionLocalizationKey);
            }
        }

        private static void AddHudMenuEntries(StringTableCollection collection)
        {
            string[] guids = AssetDatabase.FindAssets("t:HudMenuIconSet");

            for (int assetIndex = 0; assetIndex < guids.Length; assetIndex++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[assetIndex]);
                HudMenuIconSet menuIconSet = AssetDatabase.LoadAssetAtPath<HudMenuIconSet>(assetPath);
                HudMenuButtonDefinition[] definitions = menuIconSet.MenuButtonDefinitions;

                for (int definitionIndex = 0; definitionIndex < definitions.Length; definitionIndex++)
                {
                    HudMenuButtonDefinition definition = definitions[definitionIndex];

                    if (string.IsNullOrWhiteSpace(definition.ButtonId))
                    {
                        continue;
                    }

                    EnsureEntryInAllTables(collection, definition.DescriptionLocalizationKey);
                }
            }
        }

        private static void EnsureEntry(StringTable table, string key)
        {
            if (table.GetEntry(key) == null)
            {
                table.AddEntry(key, string.Empty);
                EditorUtility.SetDirty(table);
            }
        }

        /// <summary>
        /// Ключи берутся из SharedTableData, поэтому новая локаль получает весь набор
        /// ключей без ручного списка для каждого UI или типа конфига.
        /// </summary>
        internal static void EnsureAllKnownEntriesInAllTables(StringTableCollection collection)
        {
            HashSet<string> keys = new HashSet<string>();

            foreach (SharedTableData.SharedTableEntry entry in collection.SharedData.Entries)
            {
                if (!string.IsNullOrWhiteSpace(entry.Key))
                {
                    keys.Add(entry.Key);
                }
            }

            foreach (Locale locale in LocalizationEditorSettings.GetLocales())
            {
                StringTable table = GetOrCreateTable(collection, locale.Identifier);

                foreach (string key in keys)
                {
                    EnsureEntry(table, key);
                }
            }

            EditorUtility.SetDirty(collection.SharedData);
        }

        private static void EnsureEntryInAllTables(StringTableCollection collection, string key)
        {
            foreach (Locale locale in LocalizationEditorSettings.GetLocales())
            {
                EnsureEntry(GetOrCreateTable(collection, locale.Identifier), key);
            }
        }

        private static StringTable GetOrCreateTable(StringTableCollection collection, string localeCode)
        {
            return GetOrCreateTable(collection, new LocaleIdentifier(localeCode));
        }

        private static StringTable GetOrCreateTable(
            StringTableCollection collection,
            LocaleIdentifier localeIdentifier)
        {
            StringTable table = collection.GetTable(localeIdentifier) as StringTable;
            return table ?? (StringTable)collection.AddNewTable(localeIdentifier);
        }

        /// <summary>
        /// Регистрирует все текстовые поля OfferDefinition и вложенных конфигураций.
        /// Исходный английский текст добавляется как стартовое значение для обеих локалей,
        /// чтобы его можно было сразу выгрузить и заменить переводчиком.
        /// </summary>
        private static void AddOfferEntries(StringTableCollection collection)
        {
            // Assets могли быть изменены внешним редактором, поэтому сначала принудительно
            // обновляем импорт, иначе Unity может вернуть устаревшую структуру Stages.
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            string[] guids = AssetDatabase.FindAssets(
                "t:OfferDefinition",
                new[] { "Assets/_Project/Configs/Offers" });

            int addedOfferAssets = 0;

            for (int assetIndex = 0; assetIndex < guids.Length; assetIndex++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[assetIndex]);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                OfferDefinition definition = AssetDatabase.LoadAssetAtPath<OfferDefinition>(assetPath);

                if (definition == null || string.IsNullOrWhiteSpace(definition.OfferId))
                {
                    continue;
                }

                addedOfferAssets++;

                AddOfferKey(collection, definition.TitleLocalizationKey);
                AddOfferKey(collection, definition.DescriptionLocalizationKey);
                AddOfferKey(collection, definition.IntroLocalizationKey);
                AddOfferKey(collection, definition.AcceptLocalizationKey);
                AddOfferKey(collection, definition.CompleteLocalizationKey);
                AddOfferKey(collection, definition.FailLocalizationKey);

                if (definition.Stages != null)
                {
                    for (int stageIndex = 0; stageIndex < definition.Stages.Length; stageIndex++)
                    {
                        OfferStageDefinition stage = definition.Stages[stageIndex];

                        if (stage == null)
                        {
                            continue;
                        }

                        AddOfferKey(collection, OfferLocalizationKeys.Stage(definition.OfferId, stageIndex, "title"));
                        AddOfferKey(collection,
                            OfferLocalizationKeys.Stage(definition.OfferId, stageIndex, "description"));

                        if (stage.Objectives == null)
                        {
                            continue;
                        }

                        for (int objectiveIndex = 0; objectiveIndex < stage.Objectives.Length; objectiveIndex++)
                        {
                            OfferObjectiveDefinition objective = stage.Objectives[objectiveIndex];

                            if (objective != null)
                            {
                                AddOfferKey(collection,
                                    OfferLocalizationKeys.Objective(definition.OfferId, stageIndex, objectiveIndex));
                            }
                        }
                    }
                }

                if (definition.ExtraUnlockConditions != null)
                {
                    for (int conditionIndex = 0;
                         conditionIndex < definition.ExtraUnlockConditions.Length;
                         conditionIndex++)
                    {
                        AddOfferKey(
                            collection,
                            OfferLocalizationKeys.UnlockCondition(definition.OfferId, conditionIndex));
                    }
                }

                if (definition.FailureConditions != null)
                {
                    for (int conditionIndex = 0; conditionIndex < definition.FailureConditions.Length; conditionIndex++)
                    {
                        AddOfferKey(
                            collection,
                            OfferLocalizationKeys.FailureCondition(definition.OfferId, conditionIndex));
                    }
                }

                if (definition.Outcomes != null)
                {
                    for (int outcomeIndex = 0; outcomeIndex < definition.Outcomes.Length; outcomeIndex++)
                    {
                        AddOfferKey(
                            collection,
                            OfferLocalizationKeys.Outcome(definition.OfferId, outcomeIndex));
                    }
                }
            }

            Debug.Log($"[Localization] OfferDefinition assets scanned: {addedOfferAssets}/{guids.Length}.");
        }

        private static void AddOfferKey(StringTableCollection collection, string key)
        {
            EnsureEntryInAllTables(collection, key);
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(folderPath)?.Replace("\\", "/");
            string folderName = System.IO.Path.GetFileName(folderPath);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}