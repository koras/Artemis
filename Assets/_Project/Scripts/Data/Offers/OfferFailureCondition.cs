using System;
using _Project.Scripts.Data.Localization;
using UnityEngine;

namespace _Project.Scripts.Data.Offers
{
    [Serializable]
    public sealed class OfferFailureCondition
    {
        public string ConditionId;

        [LocalizationKey("Description", "description")]
        [SerializeField]
        private string _descriptionLocalizationKey = "description";

        public OfferObjectiveDefinition Objective = new OfferObjectiveDefinition();
        public bool FailWhenObjectiveIncomplete = true;
    }
}