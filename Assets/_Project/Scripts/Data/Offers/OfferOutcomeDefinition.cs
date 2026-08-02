using System;
using _Project.Scripts.Data.Localization;
using UnityEngine;

namespace _Project.Scripts.Data.Offers
{
    [Serializable]
    public sealed class OfferOutcomeDefinition
    {
        public string OutcomeId;

        [LocalizationKey("Description", "description")]
        [SerializeField]
        private string _descriptionLocalizationKey = "description";

        public int GoldDelta;
        public int ReputationDelta;
        public string UnlockTag;
    }
}