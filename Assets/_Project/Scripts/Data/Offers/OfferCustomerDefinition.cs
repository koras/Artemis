using UnityEngine;

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

        [Header("Portraits By Reputation")]
        public Sprite KindPortrait;
        public Sprite NeutralPortrait;
        public Sprite AngryPortrait;
        public Sprite VeryAngryPortrait;

        public string FullName => $"{FirstName} {LastName}".Trim();
    }
}
