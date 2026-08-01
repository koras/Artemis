using System;
using _Project.Scripts.Data.Localization;
using UnityEngine;

namespace _Project.Scripts.Data.Offers
{
    [Serializable]
    public sealed class OfferUnlockCondition
    {
        public string ConditionId;

        [LocalizationKey("Description", "description")]
        [SerializeField]
        private string _descriptionLocalizationKey = "description";

        public OfferObjectiveDefinition Objective = new OfferObjectiveDefinition();
    }
}