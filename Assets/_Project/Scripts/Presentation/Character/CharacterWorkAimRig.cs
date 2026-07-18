using UnityEngine;

namespace _Project.Scripts.Presentation.Character
{
    /// <summary>
    /// Applies work-direction aiming for the character rig without relying on mouse input.
    /// </summary>
    public sealed class CharacterWorkAimRig : MonoBehaviour
    {
        [SerializeField] private Transform _aimPivot;
        [SerializeField] private float _angleOffset = -15f;
        [SerializeField] private bool _flipRootToAim = true;

        // Keep the prefab-authored offset because the work clip animates the root transform.
        private Vector3 _authoredLocalPosition;
        private Vector2 _lastAimDirection = Vector2.right;
        private bool _isWorkAimEnabled;

        private void Awake()
        {
            _authoredLocalPosition = transform.localPosition;
        }

        /// <summary>
        /// Enables work aiming and stores the latest valid world-space direction.
        /// </summary>
        public void SetWorkAim(Vector2 direction)
        {
            if (direction.sqrMagnitude > 0.0001f)
            {
                _lastAimDirection = direction.normalized;
            }

            _isWorkAimEnabled = true;
        }

        /// <summary>
        /// Disables work aiming while preserving the last valid direction for the next activation.
        /// </summary>
        public void DisableWorkAim()
        {
            _isWorkAimEnabled = false;
        }

        private void LateUpdate()
        {
            // Preserve the authored root offset so the animation clip cannot snap the rig to origin.
            transform.localPosition = _authoredLocalPosition;

            if (!_isWorkAimEnabled)
            {
                return;
            }

            bool isFacingLeft = false;

            if (_flipRootToAim)
            {
                Vector3 scale = transform.localScale;
                isFacingLeft = _lastAimDirection.x < 0f;
                scale.x = Mathf.Abs(scale.x) * (isFacingLeft ? -1f : 1f);
                transform.localScale = scale;
            }

            Vector2 localAimDirection = isFacingLeft
                ? new Vector2(-_lastAimDirection.x, _lastAimDirection.y)
                : _lastAimDirection;

            float angle = Mathf.Atan2(localAimDirection.y, localAimDirection.x) * Mathf.Rad2Deg + _angleOffset;
            _aimPivot.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }
}
