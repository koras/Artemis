using System;
using System.Collections.Generic;
using System.Linq;
using _Project.Scripts.Data.Construction;
using _Project.Scripts.Data.Offers;
using UnityEngine.UIElements;

namespace _Project.Scripts.Presentation.UI
{
    /// <summary>
    /// Applies Menu button visibility rules and unlocks buttons from gameplay events.
    /// </summary>
    public sealed class HudMenuUnlockService
    {
        private readonly VisualElement _root;
        private readonly Dictionary<string, HudMenuButtonDefinition> _definitionsByButtonId;
        private readonly HashSet<string> _unlockedButtonIds = new HashSet<string>(StringComparer.Ordinal);
        private bool _isLifeModuleBuilt;

        public HudMenuUnlockService(VisualElement root, HudMenuButtonDefinition[] definitions)
        {
            _root = root;
            _definitionsByButtonId = BuildDefinitionMap(definitions);
            ApplyVisibility();
        }

        public event Action Changed;

        public bool IsUnlocked(string buttonId)
        {
            if (!_definitionsByButtonId.TryGetValue(buttonId, out HudMenuButtonDefinition definition))
            {
                return true;
            }

            if (definition.UnlockType == HudMenuButtonUnlockType.AlwaysHidden)
            {
                return false;
            }

            if (definition.RequiresLifeModuleBuilt && !_isLifeModuleBuilt)
            {
                return false;
            }

            return definition.UnlockType == HudMenuButtonUnlockType.AlwaysVisible
                || _unlockedButtonIds.Contains(buttonId);
        }

        public void HandleBuildingViewCreated(BuildingDef buildingDef)
        {
            if (buildingDef == null)
            {
                return;
            }

            UnlockMatchingButtons(HudMenuButtonUnlockType.BuildingViewCreated, definition => definition.RequiredBuildingDef == buildingDef);
        }

        public void HandleOfferCompleted(OfferDefinition offerDefinition)
        {
            if (offerDefinition == null)
            {
                return;
            }

            UnlockMatchingButtons(HudMenuButtonUnlockType.OfferCompleted, definition => definition.RequiredOfferDefinition == offerDefinition);
        }

        public void HandleLifeModuleBuilt()
        {
            if (_isLifeModuleBuilt)
            {
                return;
            }

            _isLifeModuleBuilt = true;
            ApplyVisibility();
            Changed?.Invoke();
        }

        public void ApplyVisibility()
        {
            if (_root == null)
            {
                return;
            }

            foreach (Button button in _root.Query<Button>().ToList())
            {
                button.style.display = IsUnlocked(button.name) ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void UnlockMatchingButtons(HudMenuButtonUnlockType unlockType, Func<HudMenuButtonDefinition, bool> predicate)
        {
            bool changed = false;
            foreach (KeyValuePair<string, HudMenuButtonDefinition> pair in _definitionsByButtonId)
            {
                if (pair.Value.UnlockType != unlockType || !predicate(pair.Value))
                {
                    continue;
                }

                changed |= _unlockedButtonIds.Add(pair.Key);
            }

            if (!changed)
            {
                return;
            }

            ApplyVisibility();
            Changed?.Invoke();
        }

        private static Dictionary<string, HudMenuButtonDefinition> BuildDefinitionMap(HudMenuButtonDefinition[] definitions)
        {
            var result = new Dictionary<string, HudMenuButtonDefinition>(StringComparer.Ordinal);
            if (definitions == null)
            {
                return result;
            }

            for (int i = 0; i < definitions.Length; i++)
            {
                HudMenuButtonDefinition definition = definitions[i];
                if (definition == null || string.IsNullOrWhiteSpace(definition.ButtonId))
                {
                    continue;
                }

                result[definition.ButtonId] = definition;
            }

            return result;
        }
    }
}