using System;
using _Project.Scripts.Data.Construction;
using _Project.Scripts.Data.Localization;
using UnityEngine;

namespace _Project.Scripts.Data.Offers
{
    [Serializable]
    public sealed class OfferObjectiveDefinition
    {
        public string ObjectiveId;
        public OfferObjectiveType ObjectiveType;

        [LocalizationKey("Description", "description")]
        [SerializeField]
        private string _descriptionLocalizationKey = "description";

        public string ResourceId;
        public int RequiredAmount = 1;
        public BuildObjectType BuildObjectType;
        public int RequiredCount = 1;
        public int RequiredWidth = 1;
        public int RequiredSols = 1;
        public bool RequireOperational = true;
    }
}