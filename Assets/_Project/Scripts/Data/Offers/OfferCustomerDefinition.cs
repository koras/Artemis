using UnityEngine;
using UnityEngine.Localization;

namespace _Project.Scripts.Data.Offers
{
    /// <summary>
    /// Заказчик офферов: профиль, компания и набор портретов по уровню репутации.
    /// </summary>
    [CreateAssetMenu(menuName = "Artemis/Offers/Customer Definition", fileName = "OfferCustomerDefinition")]
    public sealed class OfferCustomerDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string FirstName;
        public string LastName;
        public string CompanyName;
        [TextArea] public string CompanyDescription;
        public string OfferAffinityTag;

        [Header("Portraits By Reputation")]
        public Sprite KindPortrait;
        public Sprite NeutralPortrait;
        public Sprite AngryPortrait;
        public Sprite VeryAngryPortrait;

        public string FullName => $"{FirstName} {LastName}".Trim();

        /// <summary>
        /// Возвращает стабильный идентификатор профиля для ключей таблицы локализации.
        /// </summary>
        public string LocalizationId => name.Replace("OfferCustomerDefinition", string.Empty).ToLowerInvariant();

        /// <summary>Возвращает локализованное полное имя заказчика.</summary>
        public LocalizedString GetLocalizedFullName()
        {
            return new LocalizedString("UI", $"customer.{LocalizationId}.full_name");
        }

        /// <summary>Возвращает локализованное название компании.</summary>
        public LocalizedString GetLocalizedCompanyName()
        {
            return new LocalizedString("UI", $"customer.{LocalizationId}.company_name");
        }

        /// <summary>Возвращает локализованное описание компании.</summary>
        public LocalizedString GetLocalizedCompanyDescription()
        {
            return new LocalizedString("UI", $"customer.{LocalizationId}.company_description");
        }
    }
}
