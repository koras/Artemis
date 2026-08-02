using UnityEngine;
using UnityEngine.Localization;
using _Project.Scripts.Data.Localization;

namespace _Project.Scripts.Data.Offers
{
    /// <summary>
    /// Заказчик офферов: профиль, компания и набор портретов по уровню репутации.
    /// </summary>
    /// <remarks>
    /// Generic custom Inspector: <c>LocalizationConfigEditor</c> in
    /// Assets/_Project/Scripts/Editor/LocalizationConfigEditors.cs.
    /// Localization dropdowns: <c>LocalizationConfigEditorUtility</c> in
    /// Assets/_Project/Scripts/Editor/LocalizationConfigEditorUtility.cs.
    /// </remarks>
    [LocalizationNamespace("customer", nameof(OfferCustomerDefinition.LocalizationId))]
    [CreateAssetMenu(menuName = "Artemis/Offers/Customer Definition", fileName = "OfferCustomerDefinition")]
    public sealed class OfferCustomerDefinition : BaseLocalizedDefinitionConfig
    {
        [Header("Identity")]
        [Tooltip("Stable ID used in customer localization keys. Do not change after translations are added.")]
        [LocalizationId("Customer Id")]
        [HideInInspector] public string CustomerId;

        public string OfferAffinityTag;

        [LocalizationKey("First Name + Last Name", OfferCustomerLocalizationKeys.FullNameSuffix)]
        [SerializeField, HideInInspector]
        private string _fullNameLocalizationKey = OfferCustomerLocalizationKeys.FullNameSuffix;

        [LocalizationKey("Company Name", OfferCustomerLocalizationKeys.CompanyNameSuffix)]
        [SerializeField, HideInInspector]
        private string _companyNameLocalizationKey = OfferCustomerLocalizationKeys.CompanyNameSuffix;

        [LocalizationKey("Company Description", OfferCustomerLocalizationKeys.CompanyDescriptionSuffix)]
        [SerializeField, HideInInspector] private string _companyDescriptionLocalizationKey =
            OfferCustomerLocalizationKeys.CompanyDescriptionSuffix;

        [Header("Portraits By Reputation")]
        public Sprite KindPortrait;

        public Sprite NeutralPortrait;
        public Sprite AngryPortrait;
        public Sprite VeryAngryPortrait;

        /// <summary>Возвращает стабильный идентификатор профиля для ключей таблицы локализации.</summary>
        public string LocalizationId => CustomerId.ToLowerInvariant();

        public string FullNameLocalizationKey => GetLocalizationKey(_fullNameLocalizationKey);

        public string CompanyNameLocalizationKey => GetLocalizationKey(_companyNameLocalizationKey);

        public string CompanyDescriptionLocalizationKey => GetLocalizationKey(_companyDescriptionLocalizationKey);

        /// <summary>Возвращает локализованное полное имя заказчика.</summary>
        public LocalizedString GetLocalizedFullName()
        {
            return OfferCustomerLocalizationKeys.Localized(FullNameLocalizationKey);
        }

        /// <summary>Возвращает локализованное название компании.</summary>
        public LocalizedString GetLocalizedCompanyName()
        {
            return OfferCustomerLocalizationKeys.Localized(CompanyNameLocalizationKey);
        }

        /// <summary>Возвращает локализованное описание компании.</summary>
        public LocalizedString GetLocalizedCompanyDescription()
        {
            return OfferCustomerLocalizationKeys.Localized(CompanyDescriptionLocalizationKey);
        }

        private string GetLocalizationKey(string suffix)
        {
            return OfferCustomerLocalizationKeys.Key(LocalizationId, suffix);
        }
    }
}