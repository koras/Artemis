using UnityEngine.Localization;

namespace _Project.Scripts.Data.Offers
{
    public static class OfferCustomerLocalizationKeys
    {
        public const string FullNameSuffix = "full_name";
        public const string CompanyNameSuffix = "company_name";
        public const string CompanyDescriptionSuffix = "company_description";

        public static string Scope(string customerId)
        {
            return $"customer.{customerId}";
        }

        public static string Key(string customerId, string suffix)
        {
            return $"{Scope(customerId)}.{suffix}";
        }

        public static LocalizedString Localized(string key)
        {
            return new LocalizedString("UI", key);
        }
    }
}