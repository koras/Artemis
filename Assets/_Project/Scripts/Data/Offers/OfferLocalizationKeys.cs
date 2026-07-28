using UnityEngine.Localization;

namespace _Project.Scripts.Data.Offers
{
    /// <summary>
    /// Формирует стабильные ключи локализации для всех текстовых полей OfferDefinition.
    /// </summary>
    public static class OfferLocalizationKeys
    {
        public static string Offer(string offerId, string field)
        {
            return $"offer.{offerId}.{field}";
        }

        public static string Stage(string offerId, int stageIndex, string field)
        {
            return $"offer.{offerId}.stage.{stageIndex}.{field}";
        }

        public static string Objective(string offerId, int stageIndex, int objectiveIndex)
        {
            return $"offer.{offerId}.stage.{stageIndex}.objective.{objectiveIndex}.description";
        }

        public static string UnlockCondition(string offerId, int conditionIndex)
        {
            return $"offer.{offerId}.unlock.{conditionIndex}.description";
        }

        public static string FailureCondition(string offerId, int conditionIndex)
        {
            return $"offer.{offerId}.failure.{conditionIndex}.description";
        }

        public static string Outcome(string offerId, int outcomeIndex)
        {
            return $"offer.{offerId}.outcome.{outcomeIndex}.description";
        }

        /// <summary>Создаёт локализуемую строку из ключа OfferDefinition.</summary>
        public static LocalizedString Localized(string key)
        {
            return new LocalizedString("UI", key);
        }
    }
}
