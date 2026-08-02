using System;
using _Project.Scripts.Data.Localization;
using UnityEngine;

namespace _Project.Scripts.Data.Offers
{
    [Serializable]
    public sealed class OfferStageDefinition
    {
        public string StageId;

        [LocalizationKey("Title", "title")]
        [SerializeField]
        private string _titleLocalizationKey = "title";

        [LocalizationKey("Description", "description")]
        [SerializeField]
        private string _descriptionLocalizationKey = "description";

        [LocalizationCollection("objective")]
        public OfferObjectiveDefinition[] Objectives;
        public bool ScheduleInspection;
        public int BonusGold;
        public int BonusReputation;
    }
}