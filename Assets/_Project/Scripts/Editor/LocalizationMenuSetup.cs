using System.Collections.Generic;
using _Project.Scripts.Data.ColonyEvents;
using _Project.Scripts.Data.Construction;
using _Project.Scripts.Data.Offers;
using _Project.Scripts.Data.Shop;
using _Project.Scripts.Presentation.UI;
using _Project.Scripts.Systems.Resources;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace _Project.Scripts.Editor
{
    /// <summary>
    /// Создаёт базовые локали и таблицу текстов HUD-меню Artemis.
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
            AddMenuEntries(collection);
            AddResourceEntries(collection);
            AddHudMenuEntries(collection);
            AddShopProductEntries(collection);
            AddCustomerEntries(collection);
            AddBuildingEntries(collection);
            AddOfferEntries(collection);
            AddColonyEventEntries(collection);

            EditorUtility.SetDirty(settings);
            EditorUtility.SetDirty(collection);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
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

        private static void AddMenuEntries(StringTableCollection collection)
        {
            Dictionary<string, (string Russian, string English)> entries = new Dictionary<string, (string, string)>
            {
                { "menu.offers", ("Предложения", "Offers") },
                { "menu.shop", ("Магазин", "Shop") },
                { "resource.electricity", ("Электричество", "Electricity") },
                { "resource.oxygen", ("Кислород", "Oxygen") },
                { "resource.water", ("Вода", "Water") },
                { "resource.module", ("Модули", "Modules") },
                { "building.ladder", ("Лестница", "Ladder") },
                { "action.destroy", ("Уничтожить", "Destroy") },
                { "action.shovel", ("Лопата", "Shovel") },
                { "action.cancel", ("Отмена", "Cancel") },
                { "action.cable.exit", ("Выйти из кабеля", "Cable Exit") },
                { "action.water.exit", ("Выйти из воды", "Water Exit") },
                { "action.oxygen.exit", ("Выйти из кислорода", "Oxygen Exit") },
                { "offers.title", ("Предложения", "Offers") },
                { "offers.new", ("Новые предложения", "New Offers") },
                { "offers.accepted", ("Принятые задания", "Accepted Tasks") },
                { "offers.gold", ("Золото: {0}", "Gold: {0}") },
                { "offers.task", ("ЗАДАНИЕ", "TASK") },
                { "offers.customer", ("ЗАКАЗЧИК", "CUSTOMER") },
                { "offers.reputation", ("РЕПУТАЦИЯ", "REP") },
                { "offers.resources", ("РЕСУРСЫ", "RESOURCES") },
                { "offers.deadline", ("ДЕДЛАЙН", "DEADLINE") },
                { "offers.cooldown", ("ПЕРЕЗАРЯДКА", "COOLDOWN") },
                { "offers.actions", ("ДЕЙСТВИЯ", "ACTIONS") },
                { "offers.decision", ("РЕШЕНИЕ", "DECISION") },
                { "offers.no.new", ("Нет новых предложений.", "No new offers.") },
                { "offers.no.accepted", ("Нет принятых заданий.", "No accepted tasks.") },
                { "offers.details", ("Подробнее", "Details") },
                { "offers.accept", ("Принять", "Accept") },
                { "offers.reserve", ("Зарезервировать", "Reserve") },
                { "offers.unreserve", ("Снять резерв", "Unreserve") },
                { "offers.close", ("Закрыть", "Close") },
                { "offers.reject", ("Отклонить", "Reject") },
                { "shop.title", ("Заказ товаров", "Order Goods") },
                { "shop.gold", ("Золото: {0}", "Gold: {0}") },
                { "shop.all", ("Все", "All") },
                { "shop.food", ("Еда", "Food") },
                { "shop.equipment", ("Оборудование", "Equipment") },
                { "shop.personnel", ("Персонал", "Personnel") },
                { "shop.orders", ("Заказы", "Orders") },
                { "shop.catalog", ("Каталог", "Catalog") },
                { "shop.product", ("Товар", "Product") },
                { "shop.supplier", ("Поставщик", "Supplier") },
                { "shop.selected", ("Выбрано", "Selected") },
                { "shop.limit", ("Лимит", "Limit") },
                { "shop.unit.price", ("Цена за ед.", "Unit Price") },
                { "shop.total", ("Всего", "Total") },
                { "shop.amount", ("Количество", "Amount") },
                { "shop.price", ("Цена", "Price") },
                { "shop.days.left", ("Осталось дней", "Days Left") },
                { "shop.action", ("Действие", "Action") },
                { "shop.order", ("Заказать", "Order") },
                { "shop.cancel", ("Отмена", "Cancel") },
                { "shop.no.products", ("Нет доступных товаров.", "No products available.") },
                {
                    "shop.no.filtered.products",
                    ("Нет товаров в выбранной категории.", "No products for selected category.")
                },
                { "shop.no.orders", ("Нет активных заказов.", "No active orders.") },
            };

            foreach (KeyValuePair<string, (string Russian, string English)> entry in entries)
            {
                StringTable russianTable = GetOrCreateTable(collection, "ru");
                StringTable englishTable = GetOrCreateTable(collection, "en");
                russianTable.AddEntry(entry.Key, entry.Value.Russian);
                englishTable.AddEntry(entry.Key, entry.Value.English);
            }

            foreach (StringTable table in collection.StringTables)
            {
                EditorUtility.SetDirty(table);
            }
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

        private static void AddResourceEntries(StringTableCollection collection)
        {
            EnsureEntryInAllTables(collection, ResourceLocalizationKeys.InventoryTitle);

            foreach (string resourceId in ResourceLocalizationKeys.GetKnownResourceIds())
            {
                EnsureEntryInAllTables(collection, ResourceLocalizationKeys.Name(resourceId));
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

        /// <summary>
        /// Регистрирует заголовки и описания всех ColonyEventDefinition.
        /// Для событий исходный текст хранится на русском, поэтому английские значения
        /// задаются отдельно и не копируются из ScriptableObject.
        /// </summary>
        private static void AddColonyEventEntries(StringTableCollection collection)
        {
            Dictionary<string, (string Russian, string EnglishTitle, string EnglishDescription)> translations =
                new Dictionary<string, (string, string, string)>
                {
                    {
                        "meteor-shower",
                        ("Метеоритный дождь", "Meteor Shower",
                            "An intense meteor shower. Solar panels operate worse than usual today.")
                    },
                    {
                        "moonquake-week",
                        ("Неделя лунных потрясений", "Moonquake Week",
                            "A moonquake may affect equipment operation and life-support modules.")
                    },
                    {
                        "radiation-week",
                        ("Неделя активного радиационного фона", "High Radiation Week",
                            "Astronomers warned of a week of high radiation. Be careful and spend less time on the lunar surface.")
                    },
                    {
                        "solar-activity",
                        ("Солнечная активность", "Solar Activity",
                            "Astronomers predict increased solar activity. Energy generation is higher today, but radiation levels are rising.")
                    }
                };

            string[] guids = AssetDatabase.FindAssets(
                "t:ColonyEventDefinition",
                new[] { "Assets/_Project/Resources/ColonyEvents" });

            int scannedEvents = 0;

            for (int assetIndex = 0; assetIndex < guids.Length; assetIndex++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[assetIndex]);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                ColonyEventDefinition definition = AssetDatabase.LoadAssetAtPath<ColonyEventDefinition>(assetPath);

                if (definition == null || string.IsNullOrWhiteSpace(definition.EventId))
                {
                    continue;
                }

                scannedEvents++;
                string eventId = definition.EventId;

                string russianTitle = string.IsNullOrWhiteSpace(definition.Title)
                    ? eventId
                    : definition.Title;

                string russianDescription = definition.Description ?? string.Empty;
                string englishTitle = russianTitle;
                string englishDescription = russianDescription;

                if (translations.TryGetValue(eventId,
                        out (string Russian, string EnglishTitle, string EnglishDescription) translation))
                {
                    russianTitle = translation.Russian;
                    englishTitle = translation.EnglishTitle;
                    englishDescription = translation.EnglishDescription;
                }

                AddLocalizationEntry(collection, $"event.{eventId}.title", russianTitle, englishTitle);

                AddLocalizationEntry(collection, $"event.{eventId}.description", russianDescription,
                    englishDescription);
            }

            Debug.Log($"[Localization] ColonyEventDefinition assets scanned: {scannedEvents}/{guids.Length}.");
        }

        /// <summary>
        /// Добавляет значение только при отсутствии ключа, сохраняя уже выполненный перевод.
        /// </summary>
        private static void AddLocalizationEntry(
            StringTableCollection collection,
            string key,
            string russian,
            string english)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            StringTable russianTable = GetOrCreateTable(collection, "ru");
            StringTable englishTable = GetOrCreateTable(collection, "en");

            if (russianTable.GetEntry(key) == null)
            {
                russianTable.AddEntry(key, russian ?? string.Empty);
            }

            if (englishTable.GetEntry(key) == null)
            {
                englishTable.AddEntry(key, english ?? string.Empty);
            }
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