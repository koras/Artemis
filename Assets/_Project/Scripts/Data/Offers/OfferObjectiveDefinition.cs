using System;
using _Project.Scripts.Data.Construction;

namespace _Project.Scripts.Data.Offers
{
    [Serializable]
    public sealed class OfferObjectiveDefinition
    {
        public string ObjectiveId;
        public OfferObjectiveType ObjectiveType;

        public string ResourceId;
        public int RequiredAmount = 1;
        public BuildObjectType BuildObjectType;
        public int RequiredCount = 1;
        public int RequiredWidth = 1;
        public int RequiredSols = 1;
        public bool RequireOperational = true;
    }
}