using UnityEngine;

namespace _Project.Scripts.Presentation.Grid
{
    /// <summary>
    /// Resource-backed sprite catalog for life-module overlay rendering.
    /// </summary>
    [CreateAssetMenu(menuName = "Artemis/Grid/Life Module Visual Catalog", fileName = "LifeModuleVisualCatalog")]
    public sealed class LifeModuleVisualCatalog : ScriptableObject
    {
        [SerializeField] private Sprite _previewLeftSprite;
        [SerializeField] private Sprite _previewMiddle1Sprite;
        [SerializeField] private Sprite _previewMiddle2Sprite;
        [SerializeField] private Sprite _previewMiddle3Sprite;
        [SerializeField] private Sprite _previewMiddle4Sprite;
        [SerializeField] private Sprite _previewMiddle5Sprite;
        [SerializeField] private Sprite _previewRightSprite;
        [SerializeField] private Sprite _builtLeftSprite;
        [SerializeField] private Sprite _builtMiddle2Sprite;
        [SerializeField] private Sprite _builtMiddle3Sprite;
        [SerializeField] private Sprite _builtMiddle4Sprite;
        [SerializeField] private Sprite _builtMiddle5Sprite;
        [SerializeField] private Sprite _builtRightSprite;

        public Sprite PreviewLeftSprite => _previewLeftSprite;
        public Sprite PreviewMiddle1Sprite => _previewMiddle1Sprite;
        public Sprite PreviewMiddle2Sprite => _previewMiddle2Sprite;
        public Sprite PreviewMiddle3Sprite => _previewMiddle3Sprite;
        public Sprite PreviewMiddle4Sprite => _previewMiddle4Sprite;
        public Sprite PreviewMiddle5Sprite => _previewMiddle5Sprite;
        public Sprite PreviewRightSprite => _previewRightSprite;
        public Sprite BuiltLeftSprite => _builtLeftSprite;
        public Sprite BuiltMiddle2Sprite => _builtMiddle2Sprite;
        public Sprite BuiltMiddle3Sprite => _builtMiddle3Sprite;
        public Sprite BuiltMiddle4Sprite => _builtMiddle4Sprite;
        public Sprite BuiltMiddle5Sprite => _builtMiddle5Sprite;
        public Sprite BuiltRightSprite => _builtRightSprite;
    }
}
