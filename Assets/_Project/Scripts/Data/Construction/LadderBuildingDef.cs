using UnityEngine;

namespace _Project.Scripts.Data.Construction
{
    /// <summary>
    /// Definition of a ladder building and its ladder-specific visual settings.
    /// </summary>
    [CreateAssetMenu(menuName = "Artemis/Construction/Ladder Building Def", fileName = "LadderBuildingDef")]
    public sealed class LadderBuildingDef : BuildingDef
    {
        [Header("Ladder Sprites")]
        // Ladder-specific sprites belong only to ladder definitions, not to every building definition.
        public Sprite LadderBottomBuiltSprite;
        public Sprite LadderCenterBuiltSprite;
        public Sprite LadderTopBuiltSprite;
        public Sprite LadderBottomPreviewSprite;
        public Sprite LadderCenterPreviewSprite;
        public Sprite LadderTopPreviewSprite;

        /// <summary>
        /// Resolves a ladder part using the presence of ladder cells above and below.
        /// Falls back to the regular building sprite when a specialized sprite is not assigned.
        /// </summary>
        public Sprite ResolveLadderSprite(bool hasLadderBelow, bool hasLadderAbove, bool isPreview)
        {
            // If only the lower neighbor is missing, use the bottom end; if only the upper neighbor is missing, use the top end.
            // When both neighbors are present or both are absent, use the center part.
            if (!hasLadderBelow && hasLadderAbove)
            {
                return isPreview
                    ? (LadderBottomPreviewSprite != null ? LadderBottomPreviewSprite : PreviewSprite)
                    : (LadderBottomBuiltSprite != null ? LadderBottomBuiltSprite : BuiltSprite);
            }

            if (hasLadderBelow && !hasLadderAbove)
            {
                return isPreview
                    ? (LadderTopPreviewSprite != null ? LadderTopPreviewSprite : PreviewSprite)
                    : (LadderTopBuiltSprite != null ? LadderTopBuiltSprite : BuiltSprite);
            }

            return isPreview
                ? (LadderCenterPreviewSprite != null ? LadderCenterPreviewSprite : PreviewSprite)
                : (LadderCenterBuiltSprite != null ? LadderCenterBuiltSprite : BuiltSprite);
        }
    }
}
