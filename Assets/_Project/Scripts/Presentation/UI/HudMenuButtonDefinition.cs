using System;
using _Project.Scripts.Data.Construction;
using _Project.Scripts.Data.Localization;
using _Project.Scripts.Data.Offers;
using UnityEngine;
using UnityEngine.Localization;

namespace _Project.Scripts.Presentation.UI
{
    public enum HudMenuButtonUnlockType
    {
        [InspectorName("Всегда доступно")]
        AlwaysVisible = 0,
        [InspectorName("После создания BuildingView")]
        BuildingViewCreated = 1,
        [InspectorName("После завершения OfferDefinition")]
        OfferCompleted = 2,
        [InspectorName("Всегда скрыто")]
        AlwaysHidden = 3
    }

    /// <summary>
    /// Описывает отображаемую в Menu кнопку и условие её появления.
    /// Пустой каталог означает, что кнопки работают как раньше и видимы сразу.
    /// </summary>
    [Serializable]
    public sealed class HudMenuButtonDefinition
    {
        public string ButtonId;

        [LocalizationKey("Description", "description")]
        [SerializeField]
        private string _descriptionLocalizationKey = "description";

        public HudMenuButtonUnlockType UnlockType = HudMenuButtonUnlockType.AlwaysVisible;
        public BuildingDef RequiredBuildingDef;
        public OfferDefinition RequiredOfferDefinition;
        public bool RequiresLifeModuleBuilt = true;

        public string DescriptionLocalizationKey =>
            $"hud.menu.button.{ButtonId}.{_descriptionLocalizationKey}";

        public LocalizedString GetLocalizedDescription()
        {
            return new LocalizedString("UI", DescriptionLocalizationKey);
        }
    }
}