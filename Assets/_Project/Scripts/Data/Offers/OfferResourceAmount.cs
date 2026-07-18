using System;

namespace _Project.Scripts.Data.Offers
{
    /// <summary>
    /// Пара "идентификатор ресурса + количество" для условий и наград оффера.
    /// </summary>
    [Serializable]
    public struct OfferResourceAmount
    {
        public string ResourceId;
        public int Amount;
    }
}
