using UnityEngine;
using UnityEngine.UIElements;

namespace _Project.Scripts.Presentation.UI
{
    /// <summary>
    /// Скрывает HUD в Edit Mode и автоматически включает его в Play Mode.
    /// </summary>
    [ExecuteAlways]
    public sealed class HudEditModeVisibilityController : MonoBehaviour
    {
        [SerializeField] private UIDocument _uiDocument;

        private void OnEnable()
        {
            ApplyVisibility();
        }

        private void Update()
        {
            ApplyVisibility();
        }

        private void ApplyVisibility()
        {
            if (_uiDocument == null)
            {
                return;
            }

            _uiDocument.enabled = Application.isPlaying;
        }
    }
}
